////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Regex.CharClass;
using OutSmart.DAXon.Collections;
using System;
using System.Collections.Generic;
using System.Text;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Regex
{
    /// <summary>
    /// A sequence of multiple pieces in a regular expression
    /// </summary>
    internal sealed class OpSequence : Operation
    {
        protected readonly IList<Operation> operations;

        public IList<Operation> Operations => operations;

        public override int MatchLength
        {
            get
            {
                int len = 0;
                foreach (Operation o in operations)
                {
                    int i = o.MatchLength;
                    if (i == -1)
                    {
                        return -1;
                    }

                    len += i;
                }

                return len;
            }
        }

        public override int MinimumMatchLength
        {
            get
            {
                int len = 0;
                foreach (Operation o in operations)
                {
                    len += o.MinimumMatchLength;
                }

                return len;
            }
        }

        public override int MaxLoopingDepth
        {
            get
            {
                int max = 0;
                foreach (Operation o in operations)
                {
                    max = Math.Max(max, o.MaxLoopingDepth);
                }

                return max;
            }
        }
        public OpSequence(IList<Operation> operations)
        {
            this.operations = operations;
        }

        public override int MatchesEmptyString()
        {

            // The operation matches empty anywhere if every suboperation matches empty anywhere
            bool matchesEmptyAnywhere = true;
            foreach (Operation o in operations)
            {
                int m = o.MatchesEmptyString();
                if (m == MATCHES_ZLS_NEVER)
                {
                    return MATCHES_ZLS_NEVER;
                }

                if (m != MATCHES_ZLS_ANYWHERE)
                {
                    matchesEmptyAnywhere = false;
                    break;
                }
            }

            if (matchesEmptyAnywhere)
            {
                return MATCHES_ZLS_ANYWHERE;
            }


            // The operation matches BOL if every suboperation matches BOL (which includes
            // the case of matching empty anywhere)
            bool matchesBOL = true;
            foreach (Operation o in operations)
            {
                if ((o.MatchesEmptyString() & MATCHES_ZLS_AT_START) == 0)
                {
                    matchesBOL = false;
                    break;
                }
            }

            if (matchesBOL)
            {
                return MATCHES_ZLS_AT_START;
            }


            // The operation matches EOL if every suboperation matches EOL (which includes
            // the case of matching empty anywhere)
            bool matchesEOL = true;
            foreach (Operation o in operations)
            {
                if ((o.MatchesEmptyString() & MATCHES_ZLS_AT_END) == 0)
                {
                    matchesEOL = false;
                    break;
                }
            }

            if (matchesEOL)
            {
                return MATCHES_ZLS_AT_END;
            }

            return 0;
        }

        // Memoized: IterateMatches asks per position, but the tree is immutable once matching
        // starts (Optimize runs at compile). Benign race on a shared program: idempotent int write.
        private int containsCaptures = -1;

        public override bool ContainsCapturingExpressions()
        {
            if (containsCaptures < 0)
            {
                bool found = false;
                foreach (Operation o in operations)
                {
                    if (o is OpCapture || o.ContainsCapturingExpressions())
                    {
                        found = true;
                        break;
                    }
                }

                containsCaptures = found ? 1 : 0;
            }

            return containsCaptures == 1;
        }

        public override ICharacterClass GetInitialCharacterClass(bool caseBlind)
        {
            // combined n-ary: pairwise MakeUnion nested a class per operation (round BD-F4)
            IList<ICharacterClass> parts = new List<ICharacterClass>();
            foreach (Operation o in operations)
            {
                parts.Add(o.GetInitialCharacterClass(caseBlind));
                if (o.MatchesEmptyString() == MATCHES_ZLS_NEVER)
                {
                    break;
                }
            }

            return RECompiler.MakeUnion(parts);
        }

        public override string Display()
        {
            StringBuilder fsb = new StringBuilder(64);
            foreach (Operation op in operations)
            {
                fsb.Append(op.Display());
            }

            return fsb.ToString();
        }

        public override Operation Optimize(REProgram program, REFlags flags)
        {
            if (operations.Count == 0)
            {
                return new OpNothing();
            }
            else if (operations.Count == 1)
            {
                return operations[0];
            }
            else
            {
                for (int i = 0; i < operations.Count - 1; i++)
                {
                    Operation o1 = operations[i];
                    Operation o2 = o1.Optimize(program, flags);
                    if (o1 != o2)
                    {
                        operations[i] = o2;
                    }

                    if (o2 is OpRepeat)
                    {
                        Operation o1r = ((OpRepeat)o1).RepeatedOperation;
                        if (o1r is OpAtom || o1r is OpCharClass)
                        {
                            Operation o2r = operations[i + 1];
                            if (((OpRepeat)o1).min == ((OpRepeat)o1).max || RECompiler.NoAmbiguity(o1r, o2r, flags.IsCaseIndependent(), !((OpRepeat)o1).greedy))
                            {
                                operations[i] = new OpUnambiguousRepeat(o1r, ((OpRepeat)o1).min, ((OpRepeat)o1).max);
                            }
                        }
                    }
                }

                return this;
            }
        }

        public override IIntIterator IterateMatches(REMatcher matcher, int position)
        {
            // Same pattern-nesting stack guard as OpCapture (covers non-capturing groups too).
            StackGuard.Probe();

            // A stack of iterators, one for each piece in the sequence
            Stack<IIntIterator> iterators = new Stack<IIntIterator>();
            int[] savedState = ContainsCapturingExpressions() ? matcher.CaptureState() : null;
            return new AnonymousIntIterator(this, iterators, matcher, position, savedState);
        }

        private sealed class AnonymousIntIterator : AbstractIntIterator
        {

            private readonly OpSequence parent;
            private readonly Stack<IIntIterator> iterators;
            private readonly REMatcher matcher;
            private readonly int position;
            private readonly int[] savedState;
            private bool primed = false;
            private int nextPos;
            // Phase5: closure-captured locals from IterateMatches
            public AnonymousIntIterator(OpSequence parent, Stack<IIntIterator> iterators, REMatcher matcher, int position, int[] savedState)
            {
                this.parent = parent;
                this.iterators = iterators;
                this.matcher = matcher;
                this.position = position;
                this.savedState = savedState;
            }
            private int Advance()
            {
                while (iterators.Count > 0)
                {
                    IIntIterator top = iterators.Peek();
                    while (top.MoveNext())
                    {
                        int p = top.Current;
                        matcher.ClearCapturedGroupsBeyond(p);
                        int i = iterators.Count;
                        if (i >= parent.operations.Count)
                        {
                            return p;
                        }

                        top = parent.operations[i].IterateMatches(matcher, p);
                        iterators.Push(top);
                    }

                    iterators.Pop();
                    // Shared per-attempt budget + deadline (round BE): one accounting authority
                    // for all backtracking, whether it happens here or inside a nested repeat.
                    matcher.CountBacktrackStep();
                }

                if (savedState != null)
                {
                    matcher.ResetState(savedState);
                }


                return -1;
            }

            public override bool HasNext()
            {
                if (!primed)
                {
                    iterators.Push(parent.operations[0].IterateMatches(matcher, position));
                    primed = true;
                }

                nextPos = Advance();
                return nextPos >= 0;
            }

            public override int Next()
            {
                return nextPos;
            }
        }
    }
}