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
namespace OutSmart.DAXon.Regex
{
    /// <summary>
    /// A choice of several branches within a regular expression
    /// </summary>
    internal sealed class OpChoice : Operation
    {
        readonly IList<Operation> branches;

        public override int MatchLength
        {
            get
            {
                int @fixed = branches[0].MatchLength;
                for (int i = 1; i < branches.Count; i++)
                {
                    if (branches[i].MatchLength != @fixed)
                    {
                        return -1;
                    }
                }

                return @fixed;
            }
        }

        public override int MinimumMatchLength
        {
            get
            {
                int min = branches[0].MinimumMatchLength;
                for (int i = 1; i < branches.Count; i++)
                {
                    int m = branches[i].MinimumMatchLength;
                    if (m < min)
                    {
                        min = m;
                    }
                }

                return min;
            }
        }

        public override int MaxLoopingDepth
        {
            get
            {
                int max = 0;
                foreach (Operation o in branches)
                {
                    max = Math.Max(max, o.MaxLoopingDepth);
                }

                return max;
            }
        }
        public OpChoice(IList<Operation> branches)
        {
            this.branches = branches;
        }

        public override int MatchesEmptyString()
        {
            int m = 0;
            foreach (Operation branch in branches)
            {
                int b = branch.MatchesEmptyString();
                if (b != MATCHES_ZLS_NEVER)
                {
                    m |= b;
                }
            }

            return m;
        }

        public override bool ContainsCapturingExpressions()
        {
            foreach (Operation o in branches)
            {
                if (o is OpCapture || o.ContainsCapturingExpressions())
                {
                    return true;
                }
            }

            return false;
        }

        public override ICharacterClass GetInitialCharacterClass(bool caseBlind)
        {
            // combined n-ary: pairwise MakeUnion nested a class per branch (round BD-F4)
            IList<ICharacterClass> parts = new List<ICharacterClass>(branches.Count);
            foreach (Operation o in branches)
            {
                parts.Add(o.GetInitialCharacterClass(caseBlind));
            }

            return RECompiler.MakeUnion(parts);
        }

        public override Operation Optimize(REProgram program, REFlags flags)
        {
            for (int i = 0; i < branches.Count; i++)
            {
                Operation o1 = branches[i];
                Operation o2 = o1.Optimize(program, flags);
                if (o1 != o2)
                {
                    branches[i] = o2;
                }
            }

            return this;
        }

        public override IIntIterator IterateMatches(REMatcher matcher, int position)
        {
            return new AnonymousIntIterator(this, matcher, position);
        }

        public override string Display()
        {
            StringBuilder fsb = new StringBuilder(64);
            fsb.Append("(?:");
            bool first = true;
            foreach (Operation branch in branches)
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    fsb.Append('|');
                }

                fsb.Append(branch.Display());
            }

            fsb.Append(')');
            return fsb.ToString();
        }

        private sealed class AnonymousIntIterator : AbstractIntIterator
        {

            private readonly REMatcher matcher;
            private readonly int position;
            readonly IEnumerator<Operation> branchIter;
            IIntIterator currentIter;
            Operation currentOp;
            public AnonymousIntIterator(OpChoice parent, REMatcher matcher, int position)
            {
                this.matcher = matcher;
                this.position = position;
                this.branchIter = parent.branches.GetEnumerator();
            }
            public override bool HasNext()
            {
                while (true)
                {
                    if (currentIter == null)
                    {
                        if (branchIter.MoveNext())
                        {
                            matcher.ClearCapturedGroupsBeyond(position);
                            currentOp = branchIter.Current;
                            currentIter = currentOp.IterateMatches(matcher, position);
                        }
                        else
                        {
                            return false;
                        }
                    }

                    if (currentIter.MoveNext())
                    {
                        return true;
                    }
                    else
                    {
                        currentIter = null; //continue;
                    }
                }
            }

            public override int Next()
            {
                return currentIter.Current;
            }
        }
    }
}