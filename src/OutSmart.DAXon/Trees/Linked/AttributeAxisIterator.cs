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
    /// <summary>
    /// The attribute axis of an element whose attributes are an ordinary attribute map; each
    /// attribute node is created on demand, keyed by its position in the map.
    /// </summary>
    internal sealed class AttributeAxisIterator : IAxisIterator, ILookaheadIterator
    {
        private readonly ElementImpl element;
        private readonly INodePredicate nodeTest;
        private readonly int length;
        private NodeInfo next;
        private int index;

        public AttributeAxisIterator(ElementImpl node, INodePredicate nodeTest)
        {
            element = node;
            this.nodeTest = nodeTest;
            length = node.Attributes().Size();
            Advance();
        }

        public bool HasNext => next != null;

        public bool SupportsHasNext()
        {
            return true;
        }

        private void Advance()
        {
            while (index < length)
            {
                NodeInfo candidate = new AttributeImpl(element, index++);
                if (nodeTest == null || nodeTest.Test(candidate))
                {
                    next = candidate;
                    return;
                }
            }

            next = null;
        }

        public NodeInfo Next()
        {
            NodeInfo current = next;
            if (current != null)
            {
                Advance();
            }

            return current;
        }

        IItem ISequenceIterator.Next() => Next();
        public void Dispose() { }
    }
}
