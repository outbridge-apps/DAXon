////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;

using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Collections;
using System;
namespace OutSmart.DAXon.Trees.Tiny
{
    sealed class SiblingIterator : IAxisIterator, ILookaheadIterator, IAtomizedValueIterator
    {
        private readonly TinyTree tree;
        private int nextNodeNr;
        private readonly NodeTest test;
        private readonly TinyNodeImpl parentNode;
        private bool needToAdvance = false;
        private readonly IIntPredicateProxy matcher;

        public bool HasNext
        {
            get
            {
                int n = nextNodeNr;
                if (needToAdvance)
                {
                    int[] tNext = tree.next;
                    if (test == null)
                    {
                        do
                        {
                            n = tNext[n];
                        }
                        while (tree.nodeKind[n] == Types.Type.PARENT_POINTER);
                    }
                    else
                    {
                        do
                        {
                            n = tNext[n];
                        }
                        while (n >= nextNodeNr && !matcher.Test(n));
                    }

                    if (n < nextNodeNr)
                    {

                        // indicates we've got to the last sibling
                        return false;
                    }
                }

                return n != -1;
            }
        }
        public SiblingIterator(TinyTree tree, TinyNodeImpl node, NodeTest nodeTest, bool getChildren)
        {
            this.tree = tree;
            test = nodeTest;
            if (nodeTest == null)
            {
                matcher = IntSetPredicate.ALWAYS_TRUE;
            }
            else
            {
                matcher = nodeTest.GetMatcher(tree);
            }

            if (getChildren)
            {

                // child.axis
                parentNode = node;

                // move to first child
                // ASSERT: we don't invoke this code unless the node has children
                nextNodeNr = node.nodeNr + 1;
            } // following-sibling.axis
            else
            {

                // following-sibling.axis
                parentNode = node.GetParent();
                if (parentNode == null)
                {
                    nextNodeNr = -1;
                }
                else
                {

                    // move to next sibling
                    nextNodeNr = tree.next[node.nodeNr];
                    while (tree.nodeKind[nextNodeNr] == Types.Type.PARENT_POINTER)
                    {

                        // skip dummy nodes
                        nextNodeNr = tree.next[nextNodeNr];
                    }

                    if (nextNodeNr < node.nodeNr)
                    {

                        // if "next" pointer goes backwards, it's really an owner pointer from the last sibling
                        nextNodeNr = -1;
                    }
                }
            }


            // check if this matches the conditions
            if (nextNodeNr >= 0 && nodeTest != null)
            {
                if (!matcher.Test(nextNodeNr))
                {
                    needToAdvance = true;
                }
            }
        }

        public NodeInfo Next()
        {
            if (needToAdvance)
            {
                int thisNode = nextNodeNr;
                int[] tNext = tree.next;
                if (test == null)
                {
                    do
                    {
                        nextNodeNr = tNext[nextNodeNr];
                    }
                    while (tree.nodeKind[nextNodeNr] == Types.Type.PARENT_POINTER);
                }
                else
                {
                    do
                    {
                        nextNodeNr = tNext[nextNodeNr];
                    }
                    while (nextNodeNr >= thisNode && !matcher.Test(nextNodeNr));
                }

                if (nextNodeNr < thisNode)
                {

                    // indicates we've got to the last sibling
                    nextNodeNr = -1;
                    needToAdvance = false;
                    return null;
                }
            }

            if (nextNodeNr == -1)
            {
                return null;
            }

            needToAdvance = true;
            TinyNodeImpl nextNode = tree.GetNode(nextNodeNr);
            nextNode.SetParentNode(parentNode);
            return nextNode;
        }

        public IAtomicSequence NextAtomizedValue()
        {
            if (needToAdvance)
            {
                int thisNode = nextNodeNr;
                int[] tNext = tree.next;
                if (test == null)
                {
                    do
                    {
                        nextNodeNr = tNext[nextNodeNr];
                    }
                    while (tree.nodeKind[nextNodeNr] == Types.Type.PARENT_POINTER);
                }
                else
                {
                    do
                    {
                        nextNodeNr = tNext[nextNodeNr];
                    }
                    while (nextNodeNr >= thisNode && !matcher.Test(nextNodeNr));
                }

                if (nextNodeNr < thisNode)
                {

                    // indicates we've got to the last sibling
                    nextNodeNr = -1;
                    needToAdvance = false;
                    return null;
                }
            }

            if (nextNodeNr == -1)
            {
                return null;
            }

            needToAdvance = true;
            int kind = tree.nodeKind[nextNodeNr];
            switch (kind)
            {
                case Types.Type.TEXT:
                    {
                        return StringValue.MakeUntypedAtomic(TinyTextImpl.GetStringValue(tree, nextNodeNr));
                    }

                case Types.Type.WHITESPACE_TEXT:
                    {
                        return StringValue.MakeUntypedAtomic(WhitespaceTextImpl.GetStringValue(tree, nextNodeNr));
                    }

                case Types.Type.ELEMENT:
                case Types.Type.TEXTUAL_ELEMENT:
                    {
                        return tree.GetTypedValueOfElement(nextNodeNr);
                    }

                case Types.Type.COMMENT:
                case Types.Type.PROCESSING_INSTRUCTION:
                    return tree.GetAtomizedValueOfUntypedNode(nextNodeNr);
                default:
                    throw new InvalidOperationException("Unknown node kind on child axis");
            }
        }

        public bool SupportsHasNext()
        {
            return true;
        }
        IItem ISequenceIterator.Next() => Next();
        public void Dispose() { }
    }
}
