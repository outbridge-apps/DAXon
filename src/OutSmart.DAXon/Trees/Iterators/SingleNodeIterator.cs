////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Trees.Iterators
{
    /// <summary>
    /// SingleNodeIterator: an iterator over a sequence of zero or one nodes
    /// </summary>
    internal sealed class SingleNodeIterator : IAxisIterator, IReversibleIterator, ILastPositionFinder, IGroundedIterator, ILookaheadIterator
    {
        private readonly NodeInfo item;
        private int position;

        public bool HasNext => position == 0;

        public NodeInfo Value => item;
        private SingleNodeIterator(NodeInfo value)
        {
            this.item = value;
        }

        public static IAxisIterator MakeIterator(NodeInfo item)
        {
            if (item == null)
            {
                return EmptyIterator.OfNodes();
            }
            else
            {
                return new SingleNodeIterator(item);
            }
        }

        public bool SupportsHasNext()
        {
            return true;
        }

        public NodeInfo Next()
        {
            if (position == 0)
            {
                position = 1;
                return item;
            }
            else if (position == 1)
            {
                position = -1;
                return null;
            }
            else
            {
                return null;
            }
        }

        public bool SupportsGetLength()
        {
            return true;
        }

        public int GetLength()
        {
            return 1;
        }

        public ISequenceIterator GetReverseIterator()
        {
            return new SingleNodeIterator(item);
        }

        public bool IsActuallyGrounded()
        {
            return true;
        }

        public IGroundedValue Materialize()
        {
            return SequenceTool.ItemOrEmpty(item);
        }

        public IGroundedValue GetResidue()
        {
            return SequenceTool.ItemOrEmpty(item);
        }
        IItem ISequenceIterator.Next() => Next(); // runtime: StubGen wrote => default (null) which re-broke the single-child CHILD axis; delegate to the real NodeInfo Next()
        public void Dispose() { }
    }
}

