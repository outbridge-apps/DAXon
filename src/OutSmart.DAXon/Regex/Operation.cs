////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Regex.CharClass;
using OutSmart.DAXon.Collections;
using System;
namespace OutSmart.DAXon.Regex
{
    internal abstract class Operation
    {
        protected const int MATCHES_ZLS_AT_START = 1;
        protected const int MATCHES_ZLS_AT_END = 2;
        public const int MATCHES_ZLS_ANYWHERE = 7;
        protected const int MATCHES_ZLS_NEVER = 1024;
        public virtual int MatchLength => -1;

        public virtual int MinimumMatchLength
        {
            get
            {
                int @fixed = MatchLength;
                return Math.Max(@fixed, 0);
            }
        }
        public virtual int MaxLoopingDepth => 0;
        public abstract IIntIterator IterateMatches(REMatcher matcher, int position);

        public abstract int MatchesEmptyString();
        public virtual bool ContainsCapturingExpressions()
        {
            return false;
        }

        public virtual ICharacterClass GetInitialCharacterClass(bool caseBlind)
        {
            return EmptyCharacterClass.Complement;
        }

        public virtual Operation Optimize(REProgram program, REFlags flags)
        {
            return this;
        }

        public abstract string Display();

        protected sealed class ForceProgressIterator : AbstractIntIterator
        {
            private readonly IIntIterator @base;
            private readonly REMatcher matcher;
            int countZeroLength = 0;
            int currentPos = -1;
            readonly int loopingDepth = 1;
            int maxTries = 10;
            public ForceProgressIterator(IIntIterator @base, int loopingDepth, REMatcher matcher)
            {
                this.@base = @base;
                this.loopingDepth = Math.Max(loopingDepth, 1);
                this.matcher = matcher;
            }

            public override bool HasNext()
            {
                // Every pull on an ambiguous repeat is one backtracking step (round BE):
                // nested quantifiers never pass through OpSequence, so the shared budget
                // and the deadline are enforced here, at every nesting level. Deterministic
                // repeats (OpUnambiguousRepeat, the fixed/fast shapes) don't wrap and pay nothing.
                matcher.CountBacktrackStep();
                return countZeroLength <= maxTries && @base.MoveNext();
            }

            public override int Next()
            {
                int p = @base.Current;
                if (p == currentPos)
                {
                    countZeroLength++;
                }
                else
                {
                    countZeroLength = 0;
                    currentPos = p;

                    // See bug #6426. We're computing an upper bound on the number of different ways
                    // that a position p in the input can be reached, essentially ((p+2) ^ n)/2 where n is
                    // the maximum depth of looping.
                    double limit = Math.Min(int.MaxValue, Math.Pow(currentPos + 2, loopingDepth) / 2);
                    maxTries = (int)limit;
                }

                return p;
            }
        }
    }
}