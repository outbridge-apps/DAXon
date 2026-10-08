////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Trees.Iterators
{
    /// <summary>
    /// SingletonIterator: an iterator over a sequence of exactly one atomic value
    /// </summary>
    public class SingleAtomicIterator : SingletonIterator, IAtomicIterator, IReversibleIterator, ILastPositionFinder, IGroundedIterator, ILookaheadIterator
    {
        protected SingleAtomicIterator(AtomicValue value) : base(value)
        {
        }

        public static IAtomicIterator MakeIterator(AtomicValue item)
        {
            if (item == null)
            {
                return EmptyIterator.OfAtomic();
            }
            else
            {
                return new SingleAtomicIterator(item);
            }
        }

        public override SingletonIterator GetReverseIterator()
        {
            return new SingleAtomicIterator((AtomicValue)Value);
        }

        public new AtomicValue Next()
        {
            return (AtomicValue)base.Next();
        }
        ISequenceIterator IReversibleIterator.GetReverseIterator() => GetReverseIterator();
    }
}

