////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;

using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Collections;
namespace OutSmart.DAXon.Trees.Tiny
{
    sealed class PrecedingSiblingIterator : IAxisIterator
    {
        private readonly TinyTree document;
        private int nextNodeNr;
        private readonly TinyNodeImpl parentNode;
        private readonly IIntPredicateProxy matcher;
        internal PrecedingSiblingIterator(TinyTree doc, TinyNodeImpl node, NodeTest nodeTest)
        {
            document = doc;
            document.EnsurePriorIndex();
            nextNodeNr = node.nodeNr;
            parentNode = node.parent; // doesn't matter if this is null (unknown)
            this.matcher = nodeTest.GetMatcher(doc);
        }

        public NodeInfo Next()
        {
            if (nextNodeNr < 0)
            {

                // This check is needed because an errant caller can call next() again after hitting the end of sequence
                return null;
            }

            while (true)
            {
                nextNodeNr = document.prior[nextNodeNr];
                if (nextNodeNr < 0)
                {
                    return null;
                }

                if (matcher.Test(nextNodeNr))
                {
                    TinyNodeImpl next = document.GetNode(nextNodeNr);
                    next.SetParentNode(parentNode);
                    return next;
                }
            }
        }
        IItem ISequenceIterator.Next() => Next(); // redirect StubGen hollow to the real covariant Next(); default = silent empty iteration
        public void Dispose() { }
    }
}

