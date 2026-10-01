////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/*
 * Licensed to the Apache Software Foundation (ASF) under one or more
 * contributor license agreements.  See the NOTICE file distributed with
 * this work for additional information regarding copyright ownership.
 * The ASF licenses this file to You under the Apache License, Version 2.0
 * (the "License"); you may not use this file except in compliance with
 * the License.  You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
/*
 * Originally part of Apache's Jakarta project (downloaded January 2012),
 * this file has been extensively modified for integration into Saxon by
 * Michael Kay, Saxonica.
 */
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Collections;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Regex
{
    internal sealed class REMatcher
    {
        // Limits
        static readonly int MAX_PAREN = 16; // Number of paren pairs
        // State of current program
        public REProgram program; // Compiled regular expression 'program'
        public UnicodeString search; // The string being matched against
        public History history = new History();
        int maxParen = MAX_PAREN;
        // Parenthesized subexpressions
        internal State _captureState = new State();
        // Backreferences
        public int[] startBackref; // Lazily-allocated array of backref starts
        public int[] endBackref; // Lazily-allocated array of backref ends
        public Operation operation;
        public bool anchoredMatch;
        // Shared per-attempt backtracking budget (round BE). The limit used to be counted only
        // in OpSequence's advance loop, but nested quantifiers ((a)*)* compile to OpRepeat and
        // OpCapture alone - no OpSequence anywhere - so catastrophic backtracking churned with
        // no limit and no deadline. Every ambiguous-repeat pull and every sequence backtrack
        // now funnels through CountBacktrackStep.
        int backtrackSteps;
        int backtrackLimit = -1; // cached per attempt: Program.BacktrackingLimit is virtual

        public REProgram Program
        {
            get => program; set
            {
                this.program = value;
                if (value != null && value.maxParens != -1)
                {
                    this.operation = value.operation;
                    this.maxParen = value.maxParens;
                }
                else
                {
                    this.maxParen = MAX_PAREN;
                }
            }
        }

        public int ParenCount => _captureState.parenCount;
        public REMatcher(REProgram program)
        {
            Program = program;
        }

        public UnicodeString GetParen(int which)
        {
            int start;
            if (which < _captureState.parenCount && (start = GetParenStart(which)) >= 0)
            {
                return search.Substring(start, GetParenEnd(which));
            }

            return null;
        }

        public int GetParenStart(int which)
        {
            if (which < _captureState.startn.Length)
            {
                return _captureState.startn[which];
            }

            return -1;
        }

        public int GetParenEnd(int which)
        {
            if (which < _captureState.endn.Length)
            {
                return _captureState.endn[which];
            }

            return -1;
        }

        protected internal void SetParenStart(int which, int i)
        {
            while (which > _captureState.startn.Length - 1)
            {
                int[] s2 = new int[_captureState.startn.Length * 2];
                Array.Copy(_captureState.startn, 0, s2, 0, _captureState.startn.Length);
                ArrayTools.Fill(s2, _captureState.startn.Length, s2.Length, -1);
                _captureState.startn = s2;
            }

            _captureState.startn[which] = i;
        }

        public void SetParenEnd(int which, int i)
        {
            while (which > _captureState.endn.Length - 1)
            {
                int[] e2 = new int[_captureState.endn.Length * 2];
                Array.Copy(_captureState.endn, 0, e2, 0, _captureState.endn.Length);
                ArrayTools.Fill(e2, _captureState.endn.Length, e2.Length, -1);
                _captureState.endn = e2;
            }

            _captureState.endn[which] = i;
        }

        public void ClearCapturedGroupsBeyond(int pos)
        {
            for (int i = 0; i < _captureState.startn.Length; i++)
            {
                if (_captureState.startn[i] >= pos)
                {
                    _captureState.endn[i] = _captureState.startn[i];
                }
            }

            if (startBackref != null)
            {
                for (int i = 0; i < startBackref.Length; i++)
                {
                    if (startBackref[i] >= pos)
                    {
                        endBackref[i] = startBackref[i];
                    }
                }
            }
        }

        // One backtracking step (round BE): called per pull on an ambiguous repeat and per
        // sequence backtrack, at every nesting level. The step counter now advances even with
        // the limit disabled, so the deadline stays braked at a fixed 64-step stride either
        // way (a step is a few ns; the active token throttles actual clock reads further).
        internal void CountBacktrackStep()
        {
            int n = ++backtrackSteps;
            if (backtrackLimit >= 0 && n > backtrackLimit)
            {
                throw new OutSmart.DAXon.Transformation.UncheckedXPathException(new OutSmart.DAXon.Transformation.XPathException("Regex backtracking limit exceeded processing " + operation.Display() + ". Simplify the regular expression, " + "or set Feature<int>.REGEX_BACKTRACKING_LIMIT to -1 to remove this limit."));
            }

            if ((n & 63) == 0)
            {
                OutSmart.DAXon.Core.Controller.CheckActiveTimeout();
            }
        }

        protected bool MatchAt(int i, bool anchored)
        {
            // Cooperative deadline: every regex driver (matches/replace/tokenize/analyze-string,
            // via Match's position scans) funnels each candidate attempt through here, so a
            // multi-megabyte subject honours SXTO0001 instead of outrunning the transform limit.
            // Called per attempt - the active token itself throttles clock sampling (same
            // pattern as the range iterators); a local stride here would multiply with that
            // throttle and defer the first sample by stride^2 iterations.
            OutSmart.DAXon.Core.Controller.CheckActiveTimeout();

            // Fresh step budget per candidate attempt (round BE)
            backtrackSteps = 0;
            backtrackLimit = program.BacktrackingLimit;

            // Initialize start pointer, paren cache and paren count
            _captureState.parenCount = 1;
            anchoredMatch = anchored;
            SetParenStart(0, i);

            // Allocate backref arrays (unless optimizations indicate otherwise)
            if ((program.optimizationFlags & REProgram.OPT_HASBACKREFS) != 0)
            {
                startBackref = new int[maxParen];
                endBackref = new int[maxParen];
            }


            // Match against string
            int idx;
            IIntIterator iter = operation.IterateMatches(this, i);
            if (iter.MoveNext())
            {
                idx = iter.Current;
                SetParenEnd(0, idx);
                return true;
            }


            // Didn't match
            _captureState.parenCount = 0;
            return false;
        }

        public bool IsAnchoredMatch(UnicodeString search)
        {
            this.search = search;
            return MatchAt(0, true);
        }

        // Flat matcher for REProgram.GetFastKind() shapes (fk=1 single class, fk=2 greedy class repeat).
        // Finds the first position whose codepoint satisfies the predicate, extends the run, caps by max,
        // requires min. Byte-identical to the NFA for these shapes: as the whole pattern the greedy longest
        // run is the match, and a run shorter than min cannot start a match at any position inside it (every
        // later start is a strictly shorter suffix, and the run is bounded by a non-matching codepoint).
        // Direct char/byte indexing for the common surrogate-free reps; anything else uses virtual CodePointAt
        // (still no NFA). _captureState was freshly created in Match, so parenCount is already 0 on no-match.
        private bool FastClassMatch(int i, REProgram.FastShape fast)
        {
            anchoredMatch = false;
            UnicodeString s = search;
            int len = s.Length32();
            IIntPredicateProxy pred = fast.Pred;
            int min = fast.Min;
            int max = fast.Max;

            string cs = null;
            byte[] cb = null;
            int off = 0;
            if (s is BMPString)
            {
                cs = s.ToString();
            }
            else if (s is BMPSlice sl)
            {
                cs = sl.Backing;
                off = sl.Start;
            }
            else if (s is Slice8 s8)
            {
                cb = s8.ByteArray;
                off = s8.Start;
            }
            else if (s is Twine8 t8)
            {
                cb = t8.ByteArray;
            }

            while (i < len)
            {
                int c = cs != null ? cs[off + i] : cb != null ? (cb[off + i] & 0xff) : s.CodePointAt(i);
                if (!pred.Test(c))
                {
                    i++;
                    continue;
                }

                int k = i + 1;
                while (k < len)
                {
                    c = cs != null ? cs[off + k] : cb != null ? (cb[off + k] & 0xff) : s.CodePointAt(k);
                    if (!pred.Test(c))
                    {
                        break;
                    }

                    k++;
                }

                int run = k - i;
                if (run < min)
                {
                    // No start in [i, k) can reach min (all shorter suffixes), and k is non-matching.
                    i = k + 1;
                    continue;
                }

                int end = i + (run < max ? run : max);
                _captureState.parenCount = 1;
                SetParenStart(0, i);
                SetParenEnd(0, end);
                return true;
            }

            return false;
        }

        // Flat matcher for the anchored two-segment shape ^C1 C2{m,n}$ (REProgram.FastShape.Anchored2):
        // C1 consumes exactly the first codepoint and the greedy repeat must consume every remaining one
        // (any shorter repeat leaves characters before $), so the whole match is one deterministic scan —
        // no NFA, no per-attempt State churn. Byte/char indexing for the surrogate-free reps as in
        // FastClassMatch.
        private bool FastAnchored2Match(REProgram.FastShape fast)
        {
            anchoredMatch = false;
            UnicodeString s = search;
            int len = s.Length32();
            int tail = len - 1;
            if (tail < fast.Min2 || tail > fast.Max2)
            {
                return false;
            }

            string cs = null;
            byte[] cb = null;
            int off = 0;
            if (s is BMPString)
            {
                cs = s.ToString();
            }
            else if (s is BMPSlice sl)
            {
                cs = sl.Backing;
                off = sl.Start;
            }
            else if (s is Slice8 s8)
            {
                cb = s8.ByteArray;
                off = s8.Start;
            }
            else if (s is Twine8 t8)
            {
                cb = t8.ByteArray;
            }

            int c0 = cs != null ? cs[off] : cb != null ? (cb[off] & 0xff) : s.CodePointAt(0);
            if (!fast.Pred1.Test(c0))
            {
                return false;
            }

            for (int k = 1; k < len; k++)
            {
                int c = cs != null ? cs[off + k] : cb != null ? (cb[off + k] & 0xff) : s.CodePointAt(k);
                if (!fast.Pred2.Test(c))
                {
                    return false;
                }
            }

            _captureState.parenCount = 1;
            SetParenStart(0, 0);
            SetParenEnd(0, len);
            return true;
        }

        // Flat matcher for the unanchored two-group shape (C1+)(C2+) with disjoint classes
        // (REProgram.FastShape.Captures2): a C1 run contains no C2 character, so backtracking the
        // greedy C1 repeat can never enable C2 — the first C1 run followed by at least one C2
        // character IS the match: group 1 = the C1 run, group 2 = the maximal C2 run. When no C2
        // follows a run, no start inside it can match either (every suffix ends at the same
        // non-C2 boundary), so the scan resumes at the run end. Byte/char indexing for the
        // surrogate-free reps as in FastClassMatch.
        private bool FastCaptures2Match(int i, REProgram.FastShape fast)
        {
            anchoredMatch = false;
            UnicodeString s = search;
            int len = s.Length32();
            IIntPredicateProxy p1 = fast.Pred1;
            IIntPredicateProxy p2 = fast.Pred2;

            string cs = null;
            byte[] cb = null;
            int off = 0;
            if (s is BMPString)
            {
                cs = s.ToString();
            }
            else if (s is BMPSlice sl)
            {
                cs = sl.Backing;
                off = sl.Start;
            }
            else if (s is Slice8 s8)
            {
                cb = s8.ByteArray;
                off = s8.Start;
            }
            else if (s is Twine8 t8)
            {
                cb = t8.ByteArray;
            }

            while (i < len)
            {
                int c = cs != null ? cs[off + i] : cb != null ? (cb[off + i] & 0xff) : s.CodePointAt(i);
                if (!p1.Test(c))
                {
                    i++;
                    continue;
                }

                int j = i + 1;
                while (j < len)
                {
                    c = cs != null ? cs[off + j] : cb != null ? (cb[off + j] & 0xff) : s.CodePointAt(j);
                    if (!p1.Test(c))
                    {
                        break;
                    }

                    j++;
                }

                if (j >= len || !p2.Test(cs != null ? cs[off + j] : cb != null ? (cb[off + j] & 0xff) : s.CodePointAt(j)))
                {
                    i = j;
                    continue;
                }

                int k = j + 1;
                while (k < len)
                {
                    c = cs != null ? cs[off + k] : cb != null ? (cb[off + k] & 0xff) : s.CodePointAt(k);
                    if (!p2.Test(c))
                    {
                        break;
                    }

                    k++;
                }

                _captureState.parenCount = 3;
                SetParenStart(0, i);
                SetParenEnd(0, k);
                SetParenStart(1, i);
                SetParenEnd(1, j);
                SetParenStart(2, j);
                SetParenEnd(2, k);
                return true;
            }

            return false;
        }

        // Flat matcher for the unanchored two-segment shape C1 C2{m,n} (REProgram.FastShape.Seq2):
        // C1 consumes exactly one codepoint, so an attempt at i is a single deterministic probe —
        // C1 at i, then the greedy C2 run capped by max; a run under min fails the attempt and the
        // scan advances to i+1 (C1 and C2 may overlap, so no run-skipping). With Seq2Captures the
        // segments publish as groups 1 and 2. Byte/char indexing as in FastClassMatch.
        private bool FastSeq2Match(int i, REProgram.FastShape fast)
        {
            anchoredMatch = false;
            UnicodeString s = search;
            int len = s.Length32();
            IIntPredicateProxy p1 = fast.Pred1;
            IIntPredicateProxy p2 = fast.Pred2;
            int min = fast.Min2;
            int max = fast.Max2;

            string cs = null;
            byte[] cb = null;
            int off = 0;
            if (s is BMPString)
            {
                cs = s.ToString();
            }
            else if (s is BMPSlice sl)
            {
                cs = sl.Backing;
                off = sl.Start;
            }
            else if (s is Slice8 s8)
            {
                cb = s8.ByteArray;
                off = s8.Start;
            }
            else if (s is Twine8 t8)
            {
                cb = t8.ByteArray;
            }

            for (; i < len - 1; i++)
            {
                int c = cs != null ? cs[off + i] : cb != null ? (cb[off + i] & 0xff) : s.CodePointAt(i);
                if (!p1.Test(c))
                {
                    continue;
                }

                int k = i + 1;
                while (k < len && k - i - 1 < max)
                {
                    c = cs != null ? cs[off + k] : cb != null ? (cb[off + k] & 0xff) : s.CodePointAt(k);
                    if (!p2.Test(c))
                    {
                        break;
                    }

                    k++;
                }

                if (k - i - 1 < min)
                {
                    continue;
                }

                _captureState.parenCount = fast.Seq2Captures ? 3 : 1;
                SetParenStart(0, i);
                SetParenEnd(0, k);
                if (fast.Seq2Captures)
                {
                    SetParenStart(1, i);
                    SetParenEnd(1, i + 1);
                    SetParenStart(2, i + 1);
                    SetParenEnd(2, k);
                }

                return true;
            }

            return false;
        }

        public bool Match(UnicodeString search, int i)
        {

            if (search == null)
                throw new NullReferenceException();

            // Save string to search
            this.search = search.Tidy();

            // Clear the captured group state
            _captureState = new State();

            // Flat executor for a bare character class (optionally greedily repeated): the whole pattern is
            // one class, so the next match is the first satisfying codepoint and its maximal run -- no NFA,
            // no per-attempt State/iterator allocation. Falls back (Enabled==false) for anything else.
            REProgram.FastShape fast = program.GetFastShape();
            if (fast.Enabled)
            {
                return FastClassMatch(i, fast);
            }

            if (fast.Anchored2)
            {
                // Non-multi-line ^...$: like the OPT_HASBOL branch below, only i == 0 can match.
                return i == 0 && FastAnchored2Match(fast);
            }

            if (fast.Captures2)
            {
                return FastCaptures2Match(i, fast);
            }

            if (fast.Seq2)
            {
                return FastSeq2Match(i, fast);
            }

            // Can we optimize the search by looking for new lines?
            if ((program.optimizationFlags & REProgram.OPT_HASBOL) == REProgram.OPT_HASBOL)
            {

                // Non multi-line matching with BOL: Must match at '0' index
                if (!program.flags.IsMultiLine())
                {
                    return i == 0 && CheckPreconditions(i) && MatchAt(i, false);
                }


                // Multi-line matching with BOL: Seek to next line
                int nl = i;
                if (MatchAt(nl, false))
                {
                    return true;
                }

                while (true)
                {
                    nl = (int)search.IndexOf('\n', nl) + 1;
                    if (nl >= search.Length() || nl <= 0)
                    {
                        return false; // "^" does not match a NL at the end of the string
                    }
                    else
                    {
                        if (MatchAt(nl, false))
                        {
                            return true;
                        }
                    }
                }
            }


            // Is the string long enough to match?
            int actualLength = search.Length32() - i;
            if (actualLength < program.minimumLength)
            {
                return false;
            }


            // Can we optimize the search by looking for a prefix string?
            if (program.prefix == null)
            {
                if (program.initialCharClass != null)
                {

                    // no prefix known; but the first character must match a predicate
                    IIntPredicateProxy pred = program.initialCharClass;
                    for (; !(i >= search.Length32()); i++)
                    {
                        if (pred.Test(search.CodePointAt(i)))
                        {
                            if (MatchAt(i, false))
                            {
                                return true;
                            }
                        }
                    }

                    return false;
                }


                // Check the preconditions
                if (!CheckPreconditions(i))
                {
                    return false;
                }


                // Unprefixed matching must try for a match at each character
                for (; !(i - 1 >= search.Length32()); i++)
                {

                    // Try a match at index i
                    if (MatchAt(i, false))
                    {
                        return true;
                    }
                }

                return false;
            }
            else
            {

                // Prefix-anchored matching is possible
                UnicodeString prefix = program.prefix;
                int prefixLength = prefix.Length32();
                bool ignoreCase = program.flags.IsCaseIndependent();

                // Byte/char lanes: an 8-bit subject scans its backing byte[] (IndexOf on the first
                // prefix byte), a string-backed subject scans with string.IndexOf - both replace
                // the per-position virtual CodePointAt loop below. Latin1 codepoints equal their
                // byte values, so the comparisons are exact; case-blind stays on the generic loop.
                if (!ignoreCase && prefixLength > 0)
                {
                    bool prefixIsLatin1 = true;
                    for (int k = 0; k < prefixLength; k++)
                    {
                        if (prefix.CodePointAt(k) > 0xFF)
                        {
                            prefixIsLatin1 = false;
                            break;
                        }
                    }

                    byte[] sBytes = null;
                    int sOff = 0;
                    int sLen = 0;
                    if (prefixIsLatin1 && search is Text.Slice8 sl8)
                    {
                        sBytes = sl8.ByteArray;
                        sOff = sl8.Start;
                        sLen = sl8.End - sl8.Start;
                    }
                    else if (prefixIsLatin1 && search is Text.Twine8 tw8)
                    {
                        sBytes = tw8.ByteArray;
                        sLen = sBytes.Length;
                    }

                    if (sBytes != null)
                    {
                        byte first8 = (byte)prefix.CodePointAt(0);
                        for (; i + prefixLength <= sLen; i++)
                        {
                            int at = Array.IndexOf(sBytes, first8, sOff + i, sLen - i - (prefixLength - 1));
                            if (at < 0)
                            {
                                return false;
                            }

                            i = at - sOff;
                            bool restOK = true;
                            for (int k = 1; k < prefixLength; k++)
                            {
                                if (sBytes[at + k] != (byte)prefix.CodePointAt(k))
                                {
                                    restOK = false;
                                    break;
                                }
                            }

                            if (restOK && MatchAt(i, false))
                            {
                                return true;
                            }
                        }

                        return false;
                    }

                    string sChars = null;
                    int cOff = 0;
                    if (search is Text.BMPString bstr)
                    {
                        sChars = bstr.ToString();
                    }
                    else if (search is Text.BMPSlice bslice)
                    {
                        sChars = bslice.Backing;
                        cOff = bslice.Start;
                    }

                    bool prefixIsBmp = true;
                    for (int k = 0; k < prefixLength; k++)
                    {
                        if (prefix.CodePointAt(k) > 0xFFFF)
                        {
                            prefixIsBmp = false;
                            break;
                        }
                    }

                    if (sChars != null && prefixIsBmp)
                    {
                        char firstC = (char)prefix.CodePointAt(0);
                        int cLen = search.Length32();
                        for (; i + prefixLength <= cLen; i++)
                        {
                            int at = sChars.IndexOf(firstC, cOff + i, cLen - i - (prefixLength - 1));
                            if (at < 0)
                            {
                                return false;
                            }

                            i = at - cOff;
                            bool restOK = true;
                            for (int k = 1; k < prefixLength; k++)
                            {
                                if (sChars[at + k] != (char)prefix.CodePointAt(k))
                                {
                                    restOK = false;
                                    break;
                                }
                            }

                            if (restOK && MatchAt(i, false))
                            {
                                return true;
                            }
                        }

                        return false;
                    }
                }

                for (; !(i + prefixLength - 1 >= search.Length()); i++)
                {
                    bool prefixOK = true;
                    if (ignoreCase)
                    {
                        for (int j = i, k = 0; k < prefixLength; j++, k++)
                        {
                            if (!EqualCaseBlind(search.CodePointAt(j), prefix.CodePointAt(k)))
                            {
                                prefixOK = false;
                                break;
                            }
                        }
                    }
                    else
                    {
                        for (int j = i, k = 0; k < prefixLength; j++, k++)
                        {
                            if (search.CodePointAt(j) != prefix.CodePointAt(k))
                            {
                                prefixOK = false;
                                break;
                            }
                        }
                    }


                    // See if the whole prefix string matched
                    if (prefixOK)
                    {

                        // We matched the full prefix at firstChar, so try it
                        if (MatchAt(i, false))
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
        }

        private bool CheckPreconditions(int start)
        {
            foreach (RegexPrecondition condition in program.preconditions)
            {
                if (condition.fixedPosition != -1)
                {
                    bool match = condition.operation.IterateMatches(this, condition.fixedPosition).MoveNext();
                    if (!match)
                    {
                        return false;
                    }
                }
                else
                {
                    int i = start;
                    if (i < condition.minPosition)
                    {
                        i = condition.minPosition;
                    }

                    bool found = false;
                    for (; !(i >= search.Length()); i++)
                    {
                        if ((condition.fixedPosition == -1 || condition.fixedPosition == i) && condition.operation.IterateMatches(this, i).MoveNext())
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public UnicodeString Replace(UnicodeString @in, UnicodeString replacement)
        {

            // Accumulate into one builder rather than a chain of Concat allocations (each Concat
            // built a new growing UnicodeString -- millions of intermediate strings over a large
            // replace). Lazily created on the first match so the no-match case still returns @in.
            UnicodeBuilder result = null;

            // Start at position 0 and search the whole string
            int pos = 0;
            int len = @in.Length32();
            bool firstMatch = true;
            bool simpleReplacement = false;

            // Try a match at each position
            while (pos < len && Match(@in, pos))
            {
                if (result == null)
                {
                    result = new UnicodeBuilder();
                }

                // Append chars from input string before match
                result.Append(@in.Substring(pos, GetParenStart(0)));
                if (firstMatch)
                {
                    simpleReplacement = program.flags.IsLiteral();
                    firstMatch = false;
                }

                if (!simpleReplacement)
                {

                    // Process references to captured substrings
                    int maxCapture = program.maxParens - 1;
                    simpleReplacement = true;
                    for (int i = 0; i < replacement.Length(); i++)
                    {
                        int ch = replacement.CodePointAt(i);
                        if (ch == '\\')
                        {
                            simpleReplacement = false;
                            int index = ++i;
                            ch = replacement.CodePointAt(index);
                            if (ch == '\\' || ch == '$')
                            {
                                result.Append(ch);
                            }
                            else
                            {
                                throw new RESyntaxException("Invalid escape '" + ch + "' in replacement string");
                            }
                        }
                        else if (ch == '$')
                        {
                            simpleReplacement = false;
                            int index = ++i;
                            ch = replacement.CodePointAt(index);
                            if (!(ch >= '0' && ch <= '9'))
                            {
                                throw new RESyntaxException("$ in replacement string must be followed by a digit");
                            }

                            int n = ch - '0';
                            if (maxCapture <= 9)
                            {
                                if (maxCapture >= n)
                                {
                                    UnicodeString captured = GetParen(n);
                                    if (captured != null)
                                    {
                                        result.Append(captured);
                                    }
                                }
                            }
                            else
                            {
                                while (true)
                                {
                                    if (++i >= replacement.Length())
                                    {
                                        break;
                                    }

                                    ch = replacement.CodePointAt(i);
                                    if (ch >= '0' && ch <= '9')
                                    {
                                        int m = n * 10 + (ch - '0');
                                        if (m > maxCapture)
                                        {
                                            i--;
                                            break;
                                        }
                                        else
                                        {
                                            n = m;
                                        }
                                    }
                                    else
                                    {
                                        i--;
                                        break;
                                    }
                                }

                                UnicodeString captured = GetParen(n);
                                if (captured != null)
                                {
                                    result.Append(captured);
                                }
                            }
                        }
                        else
                        {
                            result.Append(ch);
                        }
                    }
                }
                else
                {

                    // Append substitution without processing backreferences
                    result.Append(replacement);
                }


                // Move forward, skipping past match
                int newpos = GetParenEnd(0);

                // We always want to make progress!
                if (newpos == pos)
                {
                    newpos++;
                }


                // Try new position
                pos = newpos;
            }


            // If no matches were found, return the input unchanged
            if (firstMatch)
            {
                return @in;
            }


            // If there's remaining input, append it
            result.Append(@in.Substring(pos, len));

            // Return string buffer
            return result.ToUnicodeString();
        }

        public UnicodeString ReplaceWith(UnicodeString @in, Func<UnicodeString, UnicodeString[], UnicodeString> replacer)
        {

            // String to return
            UnicodeBuilder sb = new UnicodeBuilder();

            // Start at position 0 and search the whole string
            int pos = 0;
            int len = @in.Length32();

            // Try a match at each position
            while (pos < len && Match(@in, pos))
            {

                // Append chars from input string before match
                for (long i = pos; i < GetParenStart(0); i++)
                {
                    sb.Append(@in.CodePointAt(i));
                }

                UnicodeString matchingSubstring = @in.Substring(GetParenStart(0), GetParenEnd(0));
                int nrOfGroups = program.maxParens - 1;
                UnicodeString[] groups = new UnicodeString[nrOfGroups];
                for (int i = 0; i < nrOfGroups; i++)
                {
                    groups[i] = GetParen(i + 1);
                    if (groups[i] == null)
                    {
                        groups[i] = EmptyUnicodeString.GetInstance();
                    }
                }

                UnicodeString replacement = replacer(matchingSubstring,groups);
                IIntIterator iter = replacement.CodePoints();
                while (iter.MoveNext())
                {
                    sb.Append(iter.Current);
                }


                // Move forward, skipping past match
                int newpos = GetParenEnd(0);

                // We always want to make progress!
                if (newpos == pos)
                {
                    newpos++;
                }


                // Try new position
                pos = newpos;
            }


            // If there's remaining input, append it
            for (int i = pos; i < len; i++)
            {
                sb.Append(@in.CodePointAt(i));
            }


            // Return string buffer
            return sb.ToUnicodeString();
        }

        public bool IsNewline(int i)
        {
            return search.CodePointAt(i) == '\n';
        }

        public bool EqualCaseBlind(int c1, int c2)
        {
            if (c1 == c2)
            {
                return true;
            }

            foreach (int v in CaseVariants.GetCaseVariants(c2))
            {
                if (c1 == v)
                {
                    return true;
                }
            }

            return false;
        }

        // A snapshot is one array (parenCount, both lengths, starts, ends): a save is one allocation and
        // a restore none, where State copies were ~15% of matching a pattern with capturing groups.
        public int[] CaptureState()
        {
            State c = _captureState;
            int starts = c.startn.Length;
            int ends = c.endn.Length;
            int[] snapshot = new int[3 + starts + ends];
            snapshot[0] = c.parenCount;
            snapshot[1] = starts;
            snapshot[2] = ends;
            Array.Copy(c.startn, 0, snapshot, 3, starts);
            Array.Copy(c.endn, 0, snapshot, 3 + starts, ends);
            return snapshot;
        }

        // In place: during a match the arrays only grow, and a slot past the snapshot's length reads
        // as "not captured" (-1) exactly as it did when the arrays were that short.
        public void ResetState(int[] snapshot)
        {
            State c = _captureState;
            c.parenCount = snapshot[0];
            c.startn = Restore(c.startn, snapshot, 3, snapshot[1]);
            c.endn = Restore(c.endn, snapshot, 3 + snapshot[1], snapshot[2]);
        }

        private static int[] Restore(int[] target, int[] snapshot, int from, int length)
        {
            if (target.Length < length)
            {
                target = new int[length];
            }

            Array.Copy(snapshot, from, target, 0, length);
            for (int i = length; i < target.Length; i++)
            {
                target[i] = -1;
            }

            return target;
        }

        internal sealed class State
        {
            public int parenCount; // Number of subexpressions matched (num open parens + 1)
            public int[] startn; // Lazily-allocated array of sub-expression starts
            public int[] endn; // Lazily-allocated array of sub-expression ends
            public State()
            {
                parenCount = 0;
                startn = new int[3];
                startn[0] = startn[1] = startn[2] = -1;
                endn = new int[3];
                endn[0] = endn[1] = endn[2] = -1;
            }
        }
    }
}