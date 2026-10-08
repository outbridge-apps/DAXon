////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;

namespace OutSmart.DAXon.Trees.Iterators
{
    // Faithful port of upstream AncestorIterator: walk GetParent() applying the NodeTest. IAxisIterator
    // redeclares `new NodeInfo Next()`, so the explicit ISequenceIterator.Next() bridges to it
    // (covariant-redirect idiom, mirrors DocumentOrderIterator).
    internal sealed class AncestorIterator : IAxisIterator
    {
        private readonly NodeInfo startNode;
        private NodeInfo current;
        private readonly NodeTest test;
        public AncestorIterator(NodeInfo node, NodeTest nodeTest)
        {
            test = nodeTest;
            startNode = node;
            current = startNode;
        }
        public NodeInfo Next()
        {
            if (current == null)
            {
                return null;
            }
            NodeInfo node = current.GetParent();
            while (node != null && !test.Test(node))
            {
                node = node.GetParent();
            }
            return current = node;
        }
        IItem ISequenceIterator.Next() => Next();
        void ISequenceIterator.Dispose() { }
        void IDisposable.Dispose() { }
    }
}
