////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;

using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Collections;
namespace OutSmart.DAXon.Trees.Tiny
{
    sealed class PrecedingIterator : IAxisIterator
    {
        private readonly TinyTree tree;
        private NodeInfo current;
        private int nextAncestorDepth;
        private readonly bool includeAncestors;
        private readonly IIntPredicateProxy matcher;
        private NodeInfo pending = null;
        private readonly NodeTest nodeTest;
        private readonly bool matchesTextNodes;
        public PrecedingIterator(TinyTree doc, TinyNodeImpl node, NodeTest nodeTest, bool includeAncestors)
        {
            this.includeAncestors = includeAncestors;
            tree = doc;
            current = node;
            nextAncestorDepth = doc.depth[node.nodeNr] - 1;
            this.nodeTest = nodeTest;
            this.matcher = nodeTest.GetMatcher(doc);
            matchesTextNodes = nodeTest.GetUType().Overlaps(UType.TEXT);
        }

        public NodeInfo Next()
        {
            if (pending != null)
            {
                current = pending;
                pending = null;
                return current;
            }

            if (current == null)
            {
                return null;
            }

            if (current is TinyTextualElement.TinyTextualElementText)
            {
                current = current.GetParent();
            }

            int nextNodeNr = ((TinyNodeImpl)current).nodeNr;
            while (true)
            {
                if (!includeAncestors)
                {
                    nextNodeNr--;

                    // skip over ancestor elements
                    while (nextAncestorDepth >= 0 && tree.depth[nextNodeNr] == nextAncestorDepth)
                    {
                        if (nextAncestorDepth-- <= 0)
                        {

                            // bug 1121528
                            current = null;
                            return null;
                        }

                        nextNodeNr--;
                    }
                }
                else
                {
                    if (tree.depth[nextNodeNr] == 0)
                    {
                        current = null;
                        return null;
                    }
                    else
                    {
                        nextNodeNr--;
                    }
                }

                if (matchesTextNodes && tree.nodeKind[nextNodeNr] == Types.Type.TEXTUAL_ELEMENT)
                {
                    TinyTextualElement element = (TinyTextualElement)tree.GetNode(nextNodeNr);
                    TinyTextualElement.TinyTextualElementText text = element.TextNode;
                    if (nodeTest.Test(text))
                    {
                        if (nodeTest.Test(element))
                        {
                            pending = element;
                        }

                        return current = text;
                    }
                    else if (nodeTest.Test(element))
                    {
                        return current = element;
                    }
                }
                else
                {
                    if (matcher.Test(nextNodeNr))
                    {
                        current = tree.GetNode(nextNodeNr);
                        return current;
                    }

                    if (tree.depth[nextNodeNr] == 0)
                    {
                        current = null;
                        return null;
                    }
                }
            }
        }
        IItem ISequenceIterator.Next() => Next(); // redirect StubGen hollow to the real covariant Next(); default = silent empty iteration
        public void Dispose() { }
    }
}

