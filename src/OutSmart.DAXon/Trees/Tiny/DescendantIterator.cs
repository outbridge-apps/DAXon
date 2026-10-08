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
    sealed class DescendantIterator : IAxisIterator, IFastCountable
    {
        private readonly TinyTree tree;
        private int nextNodeNr;
        private readonly int startDepth;
        private readonly IIntPredicateProxy matcher;
        private NodeInfo pending;
        internal DescendantIterator(TinyTree doc, TinyNodeImpl node, NodeTest nodeTest)
        {
            tree = doc;
            nextNodeNr = node.nodeNr;
            startDepth = doc.depth[nextNodeNr];
            matcher = nodeTest.GetMatcher(doc);
        }

        public NodeInfo Next()
        {
            // try/catch(IndexOutOfRangeException) → explicit `>= numberOfNodes` bound: the stopper node
            // terminates via the depth test first, so the bound only guards a malformed stopper-less tree
            // (same recovery: return end-of-sequence), without the per-iteration exception-region overhead.
            // Java relies on the stopper and has no try/catch. Byte-identical (depth test + pending +
            // TEXTUAL_ELEMENT handling all unchanged).
            short[] d = tree.depth;
            byte[] nk = tree.nodeKind;
            int nn = tree.numberOfNodes;
            do
            {
                if (pending != null)
                {
                    NodeInfo p = pending;
                    pending = null;
                    return p;
                }

                nextNodeNr++;
                if (nextNodeNr >= nn || d[nextNodeNr] <= startDepth)
                {
                    nextNodeNr = -1;
                    return null;
                }

                if (nk[nextNodeNr] == Types.Type.TEXTUAL_ELEMENT)
                {
                    pending = ((TinyTextualElement)tree.GetNode(nextNodeNr)).TextNode;
                }
            }
            while (!matcher.Test(nextNodeNr));
            return tree.GetNode(nextNodeNr);
        }
        // Same walk as Next() minus GetNode/virtual-text materialization. A TEXTUAL_ELEMENT always
        // queues its inline text as `pending`, and pending is returned without a matcher test, so it
        // contributes one item unconditionally — on top of the element's own matcher verdict.
        public bool TryFastCount(out int count)
        {
            short[] d = tree.depth;
            byte[] nk = tree.nodeKind;
            int nn = tree.numberOfNodes;
            int c = pending != null ? 1 : 0;
            pending = null;
            for (int n = nextNodeNr + 1; n < nn && d[n] > startDepth; n++)
            {
                if (nk[n] == Types.Type.TEXTUAL_ELEMENT)
                {
                    c++;
                }

                if (matcher.Test(n))
                {
                    c++;
                }
            }

            nextNodeNr = -1;
            count = c;
            return true;
        }
        IItem ISequenceIterator.Next() => Next(); // redirect StubGen hollow to the real covariant Next(); default = silent empty iteration
        public void Dispose() { }
    }
}

