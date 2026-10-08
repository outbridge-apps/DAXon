////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Trees.Iterators;
namespace OutSmart.DAXon.Expressions
{
    internal sealed class ValueTailIterator : ISequenceIterator, IGroundedIterator, ILookaheadIterator
    {
        private readonly IGroundedValue baseValue;
        private readonly int start; // zero-based
        private int pos = 0;

        public bool HasNext => baseValue.ItemAt(start + pos) != null;
        public ValueTailIterator(IGroundedValue @base, int start)
        {
            baseValue = @base;
            this.start = start;
            pos = 0;
        }

        public IItem Next()
        {
            return baseValue.ItemAt(start + pos++);
        }

        public bool SupportsHasNext()
        {
            return true;
        }

        public bool IsActuallyGrounded()
        {
            return true;
        }

        public IGroundedValue Materialize()
        {
            if (start == 0)
            {
                return baseValue;
            }
            else
            {
                return baseValue.Subsequence(start, int.MaxValue);
            }
        }

        public IGroundedValue GetResidue()
        {
            if (start == 0 && pos == 0)
            {
                return baseValue;
            }
            else
            {
                return baseValue.Subsequence(start + pos, int.MaxValue);
            }
        }
        public void Dispose() { }
    }
}
