////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Regex.CharClass;
using OutSmart.DAXon.Collections;
using System;
using System.Collections.Generic;
namespace OutSmart.DAXon.Regex
{
    internal class OpRepeat : Operation
    {
        public Operation op;
        public int min;
        public int max;
        public bool greedy;
        int loopingDepth = -1;

        public virtual Operation RepeatedOperation => op;

        public override int MatchLength => min == max && op.MatchLength >= 0 ? min * op.MatchLength : -1;

        public override int MinimumMatchLength => min * op.MinimumMatchLength;

        public override int MaxLoopingDepth
        {
            get
            {
                if (loopingDepth < 0)
                {
                    loopingDepth = op.MaxLoopingDepth + 1;
                }

                return loopingDepth;
            }
        }
        public OpRepeat(Operation op, int min, int max, bool greedy)
        {
            this.op = op;
            this.min = min;
            this.max = max;
            this.greedy = greedy;
        }

        public override int MatchesEmptyString()
        {
            if (min == 0)
            {
                return MATCHES_ZLS_ANYWHERE;
            }

            return op.MatchesEmptyString();
        }

        public override bool ContainsCapturingExpressions()
        {
            return op is OpCapture || op.ContainsCapturingExpressions();
        }

        public override ICharacterClass GetInitialCharacterClass(bool caseBlind)
        {
            return op.GetInitialCharacterClass(caseBlind);
        }

        public override Operation Optimize(REProgram program, REFlags flags)
        {
            op = op.Optimize(program, flags);
            if (min == 0 && op.MatchesEmptyString() == MATCHES_ZLS_ANYWHERE)
            {

                // turns (a?)* into (a?)+
                min = 1;
            }

            return this;
        }

        public override IIntIterator IterateMatches(REMatcher matcher, int position)
        {
            Stack<IIntIterator> iterators = new Stack<IIntIterator>();
            Stack<int> positions = new Stack<int>();
            int bound = Math.Min(max, matcher.search.Length32() - position + 1);
            int p = position;
            if (greedy)
            {

                // Prime the arrays first with iterators up to the maximum length, stopping if there is no match
                if (min == 0 && !matcher.history.IsDuplicateZeroLengthMatch(this, position))
                {

                    // add a match at the current position if zero occurrences are allowed
                    iterators.Push(new IntSingletonIterator(position));
                    positions.Push(p);
                }

                for (int i = 0; i < bound; i++)
                {
                    IIntIterator it = op.IterateMatches(matcher, p);
                    if (it.MoveNext())
                    {
                        p = it.Current;
                        iterators.Push(it);
                        positions.Push(p);
                    }
                    else if (iterators.Count == 0)
                    {
                        return EmptyIntIterator.GetInstance();
                    }
                    else
                    {
                        break;
                    }
                }


                // Now return an iterator which returns all the matching positions in order
                IIntIterator @base = new AnonymousIntIterator(this, iterators, positions, bound, op, matcher);
                return new ForceProgressIterator(@base, MaxLoopingDepth, matcher);
            }
            else
            {

                // reluctant (non-greedy) repeat.
                // rewritten for bug 3902
                IIntIterator iter = new AnonymousIntIterator1(this, position, op, matcher);
                return new ForceProgressIterator(iter, MaxLoopingDepth, matcher);
            }
        }

        public override string Display()
        {
            string quantifier;
            if (min == 0 && max == int.MaxValue)
            {
                quantifier = "*";
            }
            else if (min == 1 && max == int.MaxValue)
            {
                quantifier = "+";
            }
            else if (min == 0 && max == 1)
            {
                quantifier = "?";
            }
            else
            {
                quantifier = "{" + min + "," + max + "}";
            }

            if (!greedy)
            {
                quantifier += "?";
            }

            return op.Display() + quantifier;
        }

        private sealed class AnonymousIntIterator : AbstractIntIterator
        {

            private readonly OpRepeat parent;
            private readonly Stack<IIntIterator> iterators;
            private readonly Stack<int> positions;
            private readonly int bound;
            private readonly Operation op;
            private readonly REMatcher matcher;
            bool primed = true;
            public AnonymousIntIterator(OpRepeat parent, Stack<IIntIterator> iterators, Stack<int> positions, int bound, Operation op, REMatcher matcher)
            {
                this.parent = parent;
                this.iterators = iterators;
                this.positions = positions;
                this.bound = bound;
                this.op = op;
                this.matcher = matcher;
            }
            private void Advance()
            {
                IIntIterator top = iterators.Peek();
                if (top.MoveNext())
                {
                    int p = top.Current;
                    positions.Pop();
                    positions.Push(p);
                    while (iterators.Count < bound)
                    {

                        // bug 3787
                        IIntIterator it = op.IterateMatches(matcher, p);
                        if (it.MoveNext())
                        {
                            p = it.Current;
                            iterators.Push(it);
                            positions.Push(p);
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                else
                {
                    iterators.Pop();
                    positions.Pop();
                }
            }

            public override bool HasNext()
            {
                if (primed && iterators.Count >= parent.min)
                {
                    return iterators.Count != 0;
                }
                else if (iterators.Count == 0)
                {
                    return false;
                }
                else
                {
                    do
                    {
                        Advance();
                    }
                    while (iterators.Count < parent.min && (iterators.Count != 0));
                    return iterators.Count != 0;
                }
            }

            public override int Next()
            {
                primed = false;
                return positions.Peek();
            }
        }

        private sealed class AnonymousIntIterator1 : AbstractIntIterator
        {

            private readonly OpRepeat parent;
            private readonly Operation op;
            private readonly REMatcher matcher;
            private int pos;
            private int counter = 0;
            public AnonymousIntIterator1(OpRepeat parent, int position, Operation op, REMatcher matcher)
            {
                this.parent = parent;
                this.op = op;
                this.matcher = matcher;
                this.pos = position;
            }
            private void Advance()
            {
                IIntIterator it = op.IterateMatches(matcher, pos);
                if (it.MoveNext())
                {
                    pos = it.Current;
                    if (++counter > parent.max)
                    {
                        pos = -1;
                    }
                }
                else if (parent.min == 0 && counter == 0)
                {
                    counter++;
                }
                else
                {
                    pos = -1;
                }
            }

            public override bool HasNext()
            {
                do
                {
                    Advance();
                }
                while (counter < parent.min && pos >= 0);
                return pos >= 0;
            }

            public override int Next()
            {
                return pos;
            }
        }
    }
}