////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;

using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Values;
using System.Collections.Generic;
using OutSmart.DAXon.Internal;
using System.Numerics;
namespace OutSmart.DAXon.Expressions
{
    internal sealed class BigRangeIterator : RangeIterator, IAtomicIterator, ILastPositionFinder, ILookaheadIterator
    {
        readonly BigInteger start;
        readonly BigInteger step;
        BigInteger currentValue;
        readonly BigInteger limit;
        readonly bool descending;

        public override IntegerValue First => IntegerValue.MakeIntegerValue(start);

        public bool HasNext => Test(currentValue + step);
        public BigRangeIterator(BigInteger start, BigInteger step, BigInteger end)
        {
            if (((end - start) / step).CompareTo(new BigInteger(int.MaxValue)) > 0)
            {
                throw new XPathException("Saxon limit on sequence length exceeded (2^31)", "XPDY0130");
            }

            this.start = start;
            this.step = step;
            currentValue = start - step;
            limit = end; // TODO normalise
            descending = step.Sign < 0;
        }

        // The items not read yet. Saxon 12.9 returned SequenceExtent.MakeResidue(this), which asks this
        // grounded iterator for its residue again: a stack overflow (no caller reaches it today).
        public override IGroundedValue GetResidue()
        {
            return Ground(currentValue + step);
        }

        public override IntegerValue GetLast()
        {
            return IntegerValue.MakeIntegerValue(limit);
        }

        public override IntegerValue GetMin()
        {
            return descending ? GetLast() : First;
        }

        public override IntegerValue GetMax()
        {
            return descending ? First : GetLast();
        }

        public override IntegerValue GetStep()
        {
            return IntegerValue.MakeIntegerValue(step);
        }

        private bool Test(BigInteger value)
        {
            return descending ? value.CompareTo(limit) >= 0 : value.CompareTo(limit) <= 0;
        }

        public bool SupportsHasNext()
        {
            return true;
        }

        public new IntegerValue Next()
        {
            OutSmart.DAXon.Core.Controller.CheckActiveTimeout();
            currentValue = currentValue + step;
            if (!Test(currentValue))
            {
                return null;
            }

            return IntegerValue.MakeIntegerValue(currentValue);
        }

        public bool SupportsGetLength()
        {
            return true;
        }

        public int GetLength()
        {

            // ((end - start) / step) + 1;
            BigInteger len = (limit - start) / step;
            if (len.CompareTo(new BigInteger(int.MaxValue)) > 0)
            {
                throw new UncheckedXPathException(new XPathException("Sequence exceeds Saxon limit (32-bit integer)"));
            }

            return len.IntValue() + 1;
        }

        // Was `virtual` (a NEW method) not `override`, and — unlike AscendingRangeIterator — this class does
        // not re-list IGroundedIterator, so the interface slot stayed bound to the base
        // RangeIterator.IsActuallyGrounded() (=> throw NIE). A positional predicate on a BigInteger range
        // (e.g. (10^21 to 10^21+3)[2], RangeExpr-409b/411b) reaches SubscriptExpression.GetItemAt ->
        // IGroundedIterator.IsActuallyGrounded() and crashed with NotImplementedException. `override` binds it.
        public override bool IsActuallyGrounded()
        {
            return true;
        }

        // ToGroundedValue calls Materialize() on a grounded iterator. The base RangeIterator.Materialize() is
        // => throw NIE and (unlike AscendingRangeIterator) this class had no override, so grounding a BigInteger
        // range crashed. IntegerRange is long-only, so build a SequenceExtent (GetLength already caps at 2^31).
        public override IGroundedValue Materialize()
        {
            return Ground(start);
        }

        private IGroundedValue Ground(BigInteger from)
        {
            List<IItem> list = new List<IItem>();
            BigInteger v = from;
            while (Test(v))
            {
                // Same per-item check as Next(): grounding is the one path that walks the whole
                // range in a single instruction step, and it can box up to 2^31 values.
                OutSmart.DAXon.Core.Controller.CheckActiveTimeout();
                list.Add(IntegerValue.MakeIntegerValue(v));
                v = v + step;
            }

            return new SequenceExtent.Of<IItem>(list);
        }
        AtomicValue IAtomicIterator.Next() => Next();
        IItem ISequenceIterator.Next() => Next();
    }
}

