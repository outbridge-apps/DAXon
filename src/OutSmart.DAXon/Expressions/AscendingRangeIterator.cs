////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees;
using OutSmart.DAXon.Core;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Expressions
{
    internal sealed class AscendingRangeIterator : RangeIterator, IAtomicIterator, IReversibleIterator, ILastPositionFinder, ILookaheadIterator, IGroundedIterator
    {
        long start;
        long step;
        long currentValue;
        long limit;

        public override IntegerValue First => new Int64Value(start);

        public bool HasNext => currentValue + step <= limit;

        public AscendingRangeIterator(long start, long step, long end)
        {
            this.start = start;
            this.step = step;
            currentValue = start - step;
            limit = end;
            if (step != 1)
            {
                limit = start + ((end - start) / step) * step;
            }
        }
        public static IAtomicIterator MakeRangeIterator(IntegerValue start, IntegerValue step, IntegerValue end)
        {
            if (start == null || step == null || end == null)
            {
                return EmptyIterator.OfAtomic();
            }
            else
            {
                int direction = step.CompareTo(Int64Value.ZERO);
                if (direction == 0 || start.CompareTo(end) > 0)
                {
                    return EmptyIterator.OfAtomic();
                }

                if (start is BigIntegerValue || step is BigIntegerValue || end is BigIntegerValue)
                {
                    if (direction < 0)
                    {
                        return new BigRangeIterator(end.AsBigInteger(), step.AsBigInteger(), start.AsBigInteger());
                    }
                    else
                    {
                        return new BigRangeIterator(start.AsBigInteger(), step.AsBigInteger(), end.AsBigInteger());
                    }
                }
                else
                {
                    long startVal = start.LongValue();
                    long stepVal = step.LongValue();
                    long endVal = end.LongValue();
                    if (IntegerRange.CountExceedsLimit(startVal, stepVal, endVal))
                    {
                        throw new XPathException("Saxon limit on sequence length exceeded (2^31)", "XPDY0130");
                    }

                    if (stepVal > 0)
                    {
                        return new AscendingRangeIterator(startVal, stepVal, endVal);
                    }
                    else
                    {
                        return new DescendingRangeIterator(endVal, -stepVal, startVal);
                    }
                }
            }
        }

        public override IntegerValue GetLast()
        {
            return new Int64Value(limit);
        }

        public override IntegerValue GetMin()
        {
            return new Int64Value(start);
        }

        public override IntegerValue GetMax()
        {
            return new Int64Value(limit);
        }

        public override IntegerValue GetStep()
        {
            return new Int64Value(step);
        }

        public bool SupportsHasNext()
        {
            return true;
        }

        public new IntegerValue Next()
        {
            OutSmart.DAXon.Core.Controller.CheckActiveTimeout();
            currentValue += step;
            if (currentValue > limit)
            {
                return null;
            }

            return Int64Value.MakeIntegerValue(currentValue);
        }

        // Closed-form total of the whole range for fn:sum. False once iteration has begun
        // or on overflow — the caller then iterates (IntegerRange.TrySum has the formula).
        internal bool TryComputeTotal(out long total)
        {
            total = 0;
            return currentValue == start - step && Values.IntegerRange.TrySum(start, step, limit, out total);
        }

        public override bool IsActuallyGrounded()
        {
            return true;
        }

        public bool SupportsGetLength()
        {
            return true;
        }

        public int GetLength()
        {
            return (int)((limit - start) / step + 1);
        }

        public IAtomicIterator GetReverseIterator()
        {
            return new DescendingRangeIterator(limit, step, start);
        }

        public override IGroundedValue Materialize()
        {
            return new IntegerRange(start, step, limit);
        }

        // The items not read yet: currentValue is the last one delivered (start - step before the first)
        public override IGroundedValue GetResidue()
        {
            return HasNext ? new IntegerRange(currentValue + step, step, limit) : EmptySequence.GetInstance();
        }
        AtomicValue IAtomicIterator.Next() => Next();
        ISequenceIterator IReversibleIterator.GetReverseIterator() => GetReverseIterator();
        IItem ISequenceIterator.Next() => Next(); // redirect StubGen hollow to the real covariant Next(); default = silent empty iteration
    }
}

