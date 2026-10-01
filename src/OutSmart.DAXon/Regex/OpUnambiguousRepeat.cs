////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Regex
{
    internal sealed class OpUnambiguousRepeat : OpRepeat
    {

        public override int MatchLength
        {
            get
            {
                if (op.MatchLength != -1 && min == max)
                {
                    return op.MatchLength * min;
                }
                else
                {
                    return -1;
                }
            }
        }

        public override int MaxLoopingDepth => op.MaxLoopingDepth + 1;
        public OpUnambiguousRepeat(Operation op, int min, int max) : base(op, min, max, true)
        {
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
            int guard = matcher.search.Length32();
            int p = position;
            int matches = 0;
            while (matches < max && p <= guard)
            {
                IIntIterator it = op.IterateMatches(matcher, p);
                if (it.MoveNext())
                {
                    matches++;
                    p = it.Current;
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
            else
            {
                return new IntSingletonIterator(p);
            }
        }
    }
}