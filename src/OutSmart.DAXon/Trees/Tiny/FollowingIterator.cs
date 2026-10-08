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
using System;
namespace OutSmart.DAXon.Trees.Tiny
{
    sealed class FollowingIterator : IAxisIterator
    {
        private readonly TinyTree tree;
        private readonly TinyNodeImpl startNode;
        private NodeInfo current;
        private readonly NodeTest test;
        private readonly bool includeDescendants;
        int position = 0;
        private readonly IIntPredicateProxy matcher;
        private NodeInfo pending;
        public FollowingIterator(TinyTree doc, TinyNodeImpl node, NodeTest nodeTest, bool includeDescendants)
        {
            tree = doc;
            test = nodeTest;
            startNode = node;
            this.includeDescendants = includeDescendants;
            this.matcher = nodeTest.GetMatcher(doc);
        }
        IItem ISequenceIterator.Next() => Next();
        void ISequenceIterator.Dispose() { }
        void IDisposable.Dispose() { }

        public NodeInfo Next()
        {
            if (pending != null)
            {
                NodeInfo p = pending;
                pending = null;
                return p;
            }

            int nodeNr;
            if (position <= 0)
            {
                if (position < 0)
                {

                    // already at end
                    return null;
                }


                // first time call
                nodeNr = startNode.nodeNr;

                // skip the descendant nodes if any
                if (includeDescendants)
                {
                    nodeNr++;
                }
                else
                {
                    while (true)
                    {
                        int nextSib = tree.next[nodeNr];
                        if (nextSib > nodeNr)
                        {
                            nodeNr = nextSib;
                            break;
                        }
                        else if (tree.depth[nextSib] == 0)
                        {
                            current = null;
                            position = -1;
                            return null;
                        }
                        else
                        {
                            nodeNr = nextSib;
                        }
                    }
                }
            }
            else
            {
                TinyNodeImpl here;
                if (current is TinyTextualElement.TinyTextualElementText)
                {
                    here = (TinyNodeImpl)current.GetParent();
                }
                else
                {
                    here = (TinyNodeImpl)current;
                }

                nodeNr = here.nodeNr + 1;
            }

            while (true)
            {
                if (tree.depth[nodeNr] == 0)
                {
                    current = null;
                    position = -1;
                    return null;
                }

                if (tree.nodeKind[nodeNr] == Types.Type.TEXTUAL_ELEMENT)
                {
                    TinyTextualElement e = (TinyTextualElement)tree.GetNode(nodeNr);
                    NodeInfo t = e.TextNode;
                    if (matcher.Test(nodeNr))
                    {
                        if (test.Test(t))
                        {
                            pending = t;
                        }

                        position++;
                        return current = tree.GetNode(nodeNr);
                    }
                    else if (test.Test(t))
                    {
                        position++;
                        return current = t;
                    }
                }
                else if (matcher.Test(nodeNr))
                {
                    position++;
                    current = tree.GetNode(nodeNr);
                    return current;
                }

                nodeNr++;
            }
        }
    }
}