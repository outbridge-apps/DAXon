////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Collections;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Regex
{
    internal sealed class ARegexIterator : IRegexIterator, ILastPositionFinder
    {
        private readonly UnicodeString theString; // the input string being matched
        private readonly UnicodeString _regex;
        private readonly REMatcher _matcher; // the Matcher object that does the matching, and holds the state
        private UnicodeString current; // the string most recently returned by the iterator
        private UnicodeString nextSubstring; // if the last string was a matching string, null; otherwise the next substring
        private int prevEnd; // the position in the input string of the end of the last match or non-match
        private IntToIntHashMap nestingTable;
        private bool skip; // indicates the last match was zero length

        public int NumberOfGroups => _matcher.ParenCount;
        public ARegexIterator(UnicodeString str, UnicodeString regex, REMatcher matcher)
        {
            if (str == null)
                throw new NullReferenceException();
            if (regex == null)
                throw new NullReferenceException();
            if (matcher == null)
                throw new NullReferenceException();
            theString = str;
            this._regex = regex;
            this._matcher = matcher;
            nextSubstring = null;
        }

        public bool SupportsGetLength()
        {
            return true;
        }

        public int GetLength()
        {
            ARegexIterator another = new ARegexIterator(theString, _regex, new REMatcher(_matcher.Program));
            int n = 0;
            while (another.Next() != null)
            {
                n++;
            }

            return n;
        }

        public StringValue Next()
        {
            try
            {
                if (nextSubstring == null && prevEnd >= 0)
                {

                    // we've returned a match (or we're at the start), so find the next match
                    int searchStart = prevEnd;
                    if (skip)
                    {

                        // previous match was zero-length
                        searchStart++;
                        if (searchStart >= theString.Length())
                        {
                            if (prevEnd < theString.Length())
                            {
                                current = theString.Substring(prevEnd);
                                nextSubstring = null;
                            }
                            else
                            {
                                current = null;
                                prevEnd = -1;
                                return null;
                            }
                        }
                    }

                    if (_matcher.Match(theString, searchStart))
                    {
                        int start = _matcher.GetParenStart(0);
                        int end = _matcher.GetParenEnd(0);
                        skip = start == end;
                        if (prevEnd == start)
                        {

                            // there's no intervening non-matching string to return
                            nextSubstring = null;
                            current = theString.Substring(start, end);
                            prevEnd = end;
                        }
                        else
                        {

                            // return the non-matching substring first
                            current = theString.Substring(prevEnd, start);
                            nextSubstring = theString.Substring(start, end);
                        }
                    }
                    else
                    {

                        // there are no more regex matches, we must return the final non-matching text if any
                        if (prevEnd < theString.Length())
                        {
                            current = theString.Substring(prevEnd);
                            nextSubstring = null;
                        }
                        else
                        {

                            // this really is the end...
                            current = null;
                            prevEnd = -1;
                            return null;
                        }

                        prevEnd = -1;
                    }
                }
                else
                {

                    // we've returned a non-match, so now return the match that follows it, if there is one
                    if (prevEnd >= 0)
                    {
                        current = nextSubstring;
                        nextSubstring = null;
                        prevEnd = _matcher.GetParenEnd(0);
                    }
                    else
                    {
                        current = null;
                        return null;
                    }
                }

                return CurrentStringValue();
            }
            catch (RecursionDepthError e) when (!e.Described)
            {
                // Not wrapped into an XPathException: this Next() is pulled once per level of a
                // recursion whose body sits inside xsl:analyze-string, so an XPathException from
                // here unwinds through the whole engine stack above (round BC).
                throw ARegularExpression.DescribeOverflow(e);
            }
        }

        private StringValue CurrentStringValue()
        {
            return new StringValue(current);
        }

        public bool IsMatching()
        {
            return nextSubstring == null && prevEnd >= 0;
        }

        public UnicodeString GetRegexGroup(int number)
        {
            if (!IsMatching())
            {
                return null;
            }

            if (number >= _matcher.ParenCount || number < 0)
            {
                return EmptyUnicodeString.GetInstance();
            }

            UnicodeString us = _matcher.GetParen(number);
            return (us == null ? EmptyUnicodeString.GetInstance() : us);
        }

        public void ProcessMatchingSubstring(IRegexMatchHandler action)
        {
            int c = _matcher.ParenCount - 1;
            if (c == 0)
            {
                action.Characters(current);
            }
            else
            {

                // Create a map from positions in the string to lists of actions.
                // The "actions" in each list are: +N: start group N; -N: end group N.
                IntHashMap<IList<int>> actions = new IntHashMap<IList<int>>(c);
                for (int i = 1; i <= c; i++)
                {
                    int start = _matcher.GetParenStart(i) - _matcher.GetParenStart(0);
                    if (start != -1)
                    {
                        int end = _matcher.GetParenEnd(i) - _matcher.GetParenStart(0);
                        if (start < end)
                        {

                            // Add the start action after all other actions on the list for the same position
                            IList<int> s = actions[start];
                            if (s == null)
                            {
                                s = new List<int>(4);
                                actions.Put(start, s);
                            }

                            s.Add(i);

                            // Add the end action before all other actions on the list for the same position
                            IList<int> e = actions[end];
                            if (e == null)
                            {
                                e = new List<int>(4);
                                actions.Put(end, e);
                            }

                            e.Insert(0,-i);
                        }
                        else
                        {

                            // zero-length group (start==end). The problem here is that the information available
                            // from Java isn't sufficient to determine the nesting of groups: match("a", "(a(b?))")
                            // and match("a", "(a)(b?)") will both give the same result for group 2 (start=1, end=1).
                            // So we need to go back to the original regex to determine the group nesting
                            if (nestingTable == null)
                            {
                                nestingTable = ComputeNestingTable(_regex);
                            }

                            int parentGroup = nestingTable[i];

                            // insert the start and end events immediately before the end event for the parent group,
                            // if present; otherwise after all existing events for this position
                            IList<int> s = actions[start];
                            if (s == null)
                            {
                                s = new List<int>(4);
                                actions.Put(start, s);
                                s.Add(i);
                                s.Add(-i);
                            }
                            else
                            {
                                int pos = s.Count;
                                for (int e = 0; e < s.Count; e++)
                                {
                                    if (s[e] == -parentGroup)
                                    {
                                        pos = e;
                                        break;
                                    }
                                }

                                s.Insert(pos,-i);
                                s.Insert(pos,i);
                            }
                        }
                    }
                }

                UnicodeBuilder buff = new UnicodeBuilder();
                for (int i = 0; i < current.Length() + 1; i++)
                {
                    IList<int> events = actions[i];
                    if (events != null)
                    {
                        if (!buff.IsEmpty())
                        {
                            action.Characters(buff.ToUnicodeString());
                            buff.Clear();
                        }

                        foreach (int group in events)
                        {
                            if (group > 0)
                            {
                                action.OnGroupStart(group);
                            }
                            else
                            {
                                action.OnGroupEnd(-group);
                            }
                        }
                    }

                    if (i < current.Length())
                    {
                        buff.Append(current.CodePointAt(i));
                    }
                }

                if (!buff.IsEmpty())
                {
                    action.Characters(buff.ToUnicodeString());
                }
            }
        }

        public static IntToIntHashMap ComputeNestingTable(UnicodeString regex)
        {

            // See bug 3211
            IntToIntHashMap nestingTable = new IntToIntHashMap(16);
            int[] stack = new int[regex.Length32()];
            int tos = 0;
            bool[] captureStack = new bool[regex.Length32()];
            int captureTos = 0;
            int group = 1;
            int inBrackets = 0;
            stack[tos++] = 0;
            for (int i = 0; i < regex.Length(); i++)
            {
                int ch = regex.CodePointAt(i);
                if (ch == '\\')
                {
                    i++;
                }
                else if (ch == '[')
                {
                    inBrackets++;
                }
                else if (ch == ']')
                {
                    inBrackets--;
                }
                else if (ch == '(' && inBrackets == 0)
                {
                    bool capture = regex.CodePointAt(i + 1) != '?';
                    captureStack[captureTos++] = capture;
                    if (capture)
                    {
                        nestingTable.Put(group, stack[tos - 1]);
                        stack[tos++] = group++;
                    }
                }
                else if (ch == ')' && inBrackets == 0)
                {
                    bool capture = captureStack[--captureTos];
                    if (capture)
                    {
                        tos--;
                    }
                }
            }

            return nestingTable;
        }
        IItem ISequenceIterator.Next() => Next();
        public void Dispose() { }
    }
}