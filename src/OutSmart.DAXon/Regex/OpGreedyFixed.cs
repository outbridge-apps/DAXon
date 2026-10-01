////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Regex
{
    internal sealed class OpGreedyFixed : OpRepeat
    {
        private readonly int len;

        public override int MatchLength => min == max ? min * len : -1;
        public OpGreedyFixed(Operation op, int min, int max, int len) : base(op, min, max, true)
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
            if (max == 0)
            {
                return new OpNothing();
            }

            if (op.MatchLength == 0)
            {
                return op;
            }

            op = op.Optimize(program, flags);
            return this;
        }

        public override IIntIterator IterateMatches(REMatcher matcher, int position)
        {
            int guard = matcher.search.Length32();
            if (max < int.MaxValue)
            {
                guard = Math.Min(guard, position + len * max);
            }

            if (position >= guard && min > 0)
            {
                return EmptyIntIterator.GetInstance();
            }

            int p = position;
            int matches = 0;
            while (p <= guard)
            {
                IIntIterator it = op.IterateMatches(matcher, p);
                bool matched = false;
                if (it.MoveNext())
                {
                    matched = true;
                }

                if (matched)
                {
                    matches++;
                    p += len;
                    if (matches == max)
                    {
                        break;
                    }
                }
                else
                {
                    break;
                }
            }

            if (matches < min)
            {
                return EmptyIntIterator.GetInstance();
            }

            return new IntStepIterator(p, -len, position + len * min);
        }
    }
}