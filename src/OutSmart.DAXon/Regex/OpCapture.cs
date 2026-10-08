////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Collections;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Regex
{
    /// <summary>
    /// Open paren (captured group) within a regular expression
    /// </summary>
    internal sealed class OpCapture : Operation
    {
        internal int groupNr;
        public Operation childOp;

        public override int MatchLength => childOp.MatchLength;

        public override int MinimumMatchLength => childOp.MinimumMatchLength;

        public override int MaxLoopingDepth => childOp.MaxLoopingDepth;
        public OpCapture(Operation childOp, int group)
        {
            this.childOp = childOp;
            this.groupNr = group;
        }

        public override int MatchesEmptyString()
        {
            return childOp.MatchesEmptyString();
        }

        public override Operation Optimize(REProgram program, REFlags flags)
        {
            childOp = childOp.Optimize(program, flags);
            return this;
        }

        public override IIntIterator IterateMatches(REMatcher matcher, int position)
        {
            // Match-time recursion tracks pattern nesting (each nested group re-enters here /
            // OpSequence). Bounded so a deeply-nested pattern compiled on a big-stack thread
            // (and cached) cannot overflow a smaller worker thread during matching.
            StackGuard.Probe();
            if ((matcher.program.optimizationFlags & REProgram.OPT_HASBACKREFS) != 0)
            {
                matcher.startBackref[groupNr] = position;
            }

            IIntIterator basis = childOp.IterateMatches(matcher, position);
            return new AnonymousIntIterator(this, basis, matcher, position);
        }

        //}
        public override string Display()
        {
            return "(" + childOp.Display() + ")";
        }

        private sealed class AnonymousIntIterator : AbstractIntIterator
        {

            private readonly OpCapture parent;
            private readonly IIntIterator basis;
            private readonly REMatcher matcher;
            private readonly int position;
            private int groupNr => parent.groupNr;
            public AnonymousIntIterator(OpCapture parent, IIntIterator basis, REMatcher matcher, int position)
            {
                this.parent = parent; this.basis = basis; this.matcher = matcher; this.position = position;
            }
            public override bool HasNext()
            {
                return basis.MoveNext();
            }

            public override int Next()
            {
                int next = basis.Current;

                // Increase valid paren count
                if (groupNr >= matcher._captureState.parenCount)
                {
                    matcher._captureState.parenCount = groupNr + 1;
                }


                // Don't set paren if already set later on
                matcher.SetParenStart(groupNr, position);
                matcher.SetParenEnd(groupNr, next);

                //}
                if ((matcher.program.optimizationFlags & REProgram.OPT_HASBACKREFS) != 0)
                {
                    matcher.startBackref[groupNr] = position;
                    matcher.endBackref[groupNr] = next;
                }

                return next;
            }
        }
    }
}