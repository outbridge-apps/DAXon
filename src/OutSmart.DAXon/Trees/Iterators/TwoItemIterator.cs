////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Ported from upstream net/sf/saxon/tree/iter/TwoItemIterator.java (replaces the Phase 4.8c throwing stub).

using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Values;

namespace OutSmart.DAXon.Trees.Iterators
{
    /// <summary>An iterator over a pair of items.</summary>
    internal sealed class TwoItemIterator : ISequenceIterator, ILookaheadIterator, IGroundedIterator, ILastPositionFinder
    {
        private readonly IItem one;
        private readonly IItem two;
        private int pos;

        public bool HasNext => pos < 2;

        public TwoItemIterator(IItem one, IItem two)
        {
            this.one = one;
            this.two = two;
        }

        public bool SupportsHasNext() => true;

        public IItem Next()
        {
            switch (pos++)
            {
                case 0: return one;
                case 1: return two;
                default: return null;
            }
        }

        public bool SupportsGetLength() => true;

        public int GetLength() => 2;

        public bool IsActuallyGrounded() => true;

        public IGroundedValue Materialize() => new SequenceExtent.Of<IItem>(new IItem[] { one, two });

        public IGroundedValue GetResidue()
        {
            switch (pos)
            {
                case 0: return new SequenceExtent.Of<IItem>(new IItem[] { one, two });
                case 1: return two;
                default: return EmptySequence.GetInstance();
            }
        }
        public void Dispose() { }
    }
}
