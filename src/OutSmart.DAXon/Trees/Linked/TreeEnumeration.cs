////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;

using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Trees.Iterators;
namespace OutSmart.DAXon.Trees.Linked
{
    abstract class TreeEnumeration : IAxisIterator, ILookaheadIterator
    {
        protected NodeImpl start;
        protected NodeImpl nextNode;
        protected INodePredicate nodeTest;
        protected NodeImpl current;
        protected int position;
        public virtual bool HasNext => nextNode != null;
        public TreeEnumeration(NodeImpl origin, INodePredicate nodeTest)
        {
            nextNode = origin;
            start = origin;
            this.nodeTest = nodeTest;
        }

        protected virtual bool Conforms(NodeImpl node)
        {
            return node == null || nodeTest == null || nodeTest.Test(node);
        }

        protected void Advance()
        {
            do
            {
                Step();
            }
            while (!Conforms(nextNode));
        }

        protected abstract void Step();

        /// <summary>
        /// Return the next node in the sequence
        /// </summary>
        public NodeInfo Next()
        {
            if (nextNode == null)
            {
                current = null;
                position = -1;
                return null;
            }
            else
            {
                current = nextNode;
                position++;
                Advance();
                return current;
            }
        }

        /// <summary>
        /// Return the next node in the sequence
        /// </summary>
        public virtual bool SupportsHasNext()
        {
            return true;
        }
        IItem ISequenceIterator.Next() => Next();
        public virtual void Dispose() { }
    }
}

