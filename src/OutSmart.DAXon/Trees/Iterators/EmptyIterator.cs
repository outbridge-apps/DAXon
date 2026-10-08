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
    internal class EmptyIterator : ISequenceIterator, IReversibleIterator, ILastPositionFinder, IGroundedIterator, ILookaheadIterator, IAtomizedValueIterator
    {
        private static readonly EmptyIterator theInstance = new EmptyIterator();

        public virtual bool HasNext => false;

        protected EmptyIterator()
        {
        }
        public static EmptyIterator GetInstance()
        {
            return theInstance;
        }

        public virtual IAtomicSequence NextAtomizedValue()
        {
            return null;
        }

        public virtual IItem Next()
        {
            return null;
        }

        public virtual bool SupportsGetLength()
        {
            return true;
        }

        public virtual int GetLength()
        {
            return 0;
        }

        public virtual EmptyIterator GetReverseIterator()
        {
            return this;
        }

        public virtual IGroundedValue Materialize()
        {
            return EmptySequence.GetInstance();
        }

        public virtual IGroundedValue GetResidue()
        {
            return EmptySequence.GetInstance();
        }

        public virtual bool SupportsHasNext()
        {
            return true;
        }

        public virtual bool IsActuallyGrounded()
        {
            return true;
        }

        public static IAxisIterator OfNodes()
        {
            return OfNodesIter.THE_INSTANCE;
        }

        public static IAtomicIterator OfAtomic()
        {
            return OfAtomicIter.THE_INSTANCE;
        }
        ISequenceIterator IReversibleIterator.GetReverseIterator() => GetReverseIterator();
        public virtual void Dispose() { }

        /// <summary>
        /// An empty iterator for use where a sequence of nodes is required
        /// </summary>
        private sealed class OfNodesIter : EmptyIterator, IAxisIterator
        {
            public static readonly OfNodesIter THE_INSTANCE = new OfNodesIter();
            public override IItem Next()
            {
                return null;
            }
            NodeInfo IAxisIterator.Next() => null;
        }

        /// <summary>
        /// An empty iterator for use where a sequence of atomic values is required
        /// </summary>
        private sealed class OfAtomicIter : EmptyIterator, IAtomicIterator
        {
            public static readonly OfAtomicIter THE_INSTANCE = new OfAtomicIter();
            public override IItem Next()
            {
                return null;
            }
            AtomicValue IAtomicIterator.Next() => null;
        }
    }
}


