////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Collections;
namespace OutSmart.DAXon.Regex
{
    internal sealed class OpReluctantFixed : OpRepeat
    {
        private readonly int len;

        public override int MatchLength => min == max ? min * len : -1;
        public OpReluctantFixed(Operation op, int min, int max, int len) : base(op, min, max, false)
        {
            this.len = len;
        }

        public override int MatchesEmptyString()
        {
            if (min == 0)
            {
                return MATCHES_ZLS_ANYWHERE;
            }

            return op.MatchesEmptyString();
        }

        public override Operation Optimize(REProgram program, REFlags flags)
        {
            op = op.Optimize(program, flags);
            return this;
        }

        public override IIntIterator IterateMatches(REMatcher matcher, int position)
        {
            return new AnonymousIntIterator(this, matcher, position);
        }

        private sealed class AnonymousIntIterator : AbstractIntIterator
        {

            private readonly OpReluctantFixed parent;
            private readonly REMatcher matcher;
            private int pos;
            private int count;
            private bool started;
            private int min => parent.min; private int max => parent.max; private Operation op => parent.op;
            public AnonymousIntIterator(OpReluctantFixed parent, REMatcher matcher, int position)
            {
                this.parent = parent; this.matcher = matcher; this.pos = position;
            }
            public override bool HasNext()
            {
                if (!started)
                {
                    started = true;
                    while (count < min)
                    {
                        IIntIterator child = op.IterateMatches(matcher, pos);
                        if (child.MoveNext())
                        {
                            pos = child.Current;
                            // zero-length operand: one match stands for any count (see OpGreedyFixed)
                            count = parent.len == 0 ? min : count + 1;
                        }
                        else
                        {
                            return false;
                        }
                    }

                    return true;
                }

                if (count < max && (parent.len > 0 || count == 0))
                {
                    matcher.ClearCapturedGroupsBeyond(pos);
                    IIntIterator child = op.IterateMatches(matcher, pos);
                    if (child.MoveNext())
                    {
                        pos = child.Current;
                        count++;
                        return true;
                    }
                }

                return false;
            }

            public override int Next()
            {
                return pos;
            }
        }
    }
}