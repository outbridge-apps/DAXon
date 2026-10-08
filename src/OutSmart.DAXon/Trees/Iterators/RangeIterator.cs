////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;

using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
namespace OutSmart.DAXon.Trees.Iterators
{
    internal abstract class RangeIterator : IGroundedIterator
    {
        public abstract IntegerValue First { get; }
        public abstract IGroundedValue GetResidue();
        public abstract IntegerValue GetLast();
        public abstract IntegerValue GetMin();
        public abstract IntegerValue GetMax();
        public abstract IntegerValue GetStep();
        public virtual bool ContainsEq(NumericValue val)
        {
            IntegerValue intVal;

            // See bug #5625 - I thought this code was unreachable, but qt4 test ByExpr441c hits it. MHK 2022-08-03
            if (val is IntegerValue)
            {
                intVal = (IntegerValue)val;
            }
            else
            {
                if (!val.IsWholeNumber())
                {
                    return false;
                }

                try
                {
                    intVal = (IntegerValue)Converter.NumericToInteger.INSTANCE.Convert(val).AsAtomic();
                }
                catch (ValidationException)
                {
                    return false;
                }
            }

            try
            {
                return intVal.CompareTo(GetMin()) >= 0 && intVal.CompareTo(GetMax()) <= 0 && intVal.Minus(First).Mod(GetStep()).Equals(Int64Value.ZERO);
            }
            catch (XPathException e)
            {
                throw new InvalidOperationException(e.Message, e);
            }
        }
        public virtual bool IsActuallyGrounded() => true; // a range has GetResidue(): always grounded
        // All three subclasses re-bridge ISequenceIterator.Next to their covariant IntegerValue
        // Next(); this slot only fires if a future subclass forgets that bridge.
        public virtual IItem Next() => throw new InvalidOperationException("RangeIterator subclass must bridge ISequenceIterator.Next to its covariant Next()");
        public virtual void Dispose() { }

        // upstream: RangeIterator has no materialize override — the GroundedIterator interface
        // default applies: SequenceExtent.from(this).reduce() (remaining items from current position).
        public virtual IGroundedValue Materialize() => OutSmart.DAXon.Values.SequenceExtent.From(this).Reduce();
    }
}
