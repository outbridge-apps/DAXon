////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Expressions
{
    internal sealed class DescendingRangeIterator : RangeIterator, IAtomicIterator, IReversibleIterator, ILastPositionFinder, ILookaheadIterator
    {
        long start;
        long step;
        long currentValue;
        long limit;

        public override IntegerValue First => new Int64Value(start);

        public bool HasNext => currentValue - step >= limit;
        public DescendingRangeIterator(long start, long step, long end)
        {
            // Backstop matching the ascending sibling's guard in MakeRangeIterator: every caller
            // already checks the length, but the guard used to be commented out, and start - end
            // overflows a long for spans past 2^63. CountExceedsLimit is overflow-safe.
            if (IntegerRange.CountExceedsLimit(start, step, end))
            {
                throw new XPathException("Saxon limit on sequence length exceeded (2^31)", "XPDY0130");
            }

            this.start = start;
            this.step = step;
            currentValue = start + step;
            limit = end;
            if (step != 1)
            {
                limit = start + ((end - start) / step) * step;
            }
        }

        public override bool IsActuallyGrounded()
        {
            return true;
        }

        // Closed-form total of the whole descending range for fn:sum (a negative-step series).
        // False once iteration has begun or on overflow — the caller then iterates.
        internal bool TryComputeTotal(out long total)
        {
            total = 0;
            return currentValue == start + step && Values.IntegerRange.TrySum(start, -step, limit, out total);
        }

        public override IGroundedValue Materialize()
        {
            return new IntegerRange(start, -step, limit);
        }

        // The items not read yet: currentValue is the last one delivered (start + step before the first)
        public override IGroundedValue GetResidue()
        {
            return HasNext ? new IntegerRange(currentValue - step, -step, limit) : EmptySequence.GetInstance();
        }

        public override IntegerValue GetLast()
        {
            return new Int64Value(limit);
        }

        public override IntegerValue GetMin()
        {
            return new Int64Value(limit);
        }

        public override IntegerValue GetMax()
        {
            return new Int64Value(start);
        }

        public override IntegerValue GetStep()
        {
            return new Int64Value(-step);
        }

        public bool SupportsHasNext()
        {
            return true;
        }

        public new IntegerValue Next()
        {
            OutSmart.DAXon.Core.Controller.CheckActiveTimeout();
            currentValue -= step;
            if (currentValue < limit)
            {
                return null;
            }

            return Int64Value.MakeIntegerValue(currentValue);
        }

        public bool SupportsGetLength()
        {
            return true;
        }

        public int GetLength()
        {
            return (int)((start - limit) / step + 1);
        }

        // Saxon 12.9 passed (start, step, limit): an ascending range from the top, so empty, of length 2 - n
        public IAtomicIterator GetReverseIterator()
        {
            return new AscendingRangeIterator(limit, step, start);
        }
        AtomicValue IAtomicIterator.Next() => Next();
        ISequenceIterator IReversibleIterator.GetReverseIterator() => GetReverseIterator();
        IItem ISequenceIterator.Next() => Next(); // redirect StubGen hollow to the real covariant Next(); default = silent empty iteration
    }
}

