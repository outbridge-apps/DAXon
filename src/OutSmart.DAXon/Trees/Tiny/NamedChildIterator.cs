////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;

using OutSmart.DAXon.Trees.Iterators;
namespace OutSmart.DAXon.Trees.Tiny
{
    sealed class NamedChildIterator : IAxisIterator, ILookaheadIterator, IAtomizedValueIterator
    {
        private readonly TinyTree tree;
        private int nextNodeNr;
        private readonly int fingerprint;
        private TinyNodeImpl startNode;
        private bool needToAdvance = false;

        public bool HasNext
        {
            get
            {
                int n = nextNodeNr;
                if (needToAdvance)
                {
                    int thisNode = n;
                    do
                    {
                        n = tree.next[n];
                        if (n < thisNode)
                        {
                            return false;
                        }
                    }
                    while ((tree.nodeKind[n] & 0xf) != Types.Type.ELEMENT || (tree.nameCode[n] & 0xfffff) != fingerprint);
                    return true;
                }
                else
                {
                    return n != -1;
                }
            }
        }
        public NamedChildIterator(TinyTree tree, TinyNodeImpl node, int fingerprint)
        {
            this.tree = tree;
            this.fingerprint = fingerprint;
            this.startNode = node;

            //startNode = node;
            // move to first child
            // ASSERT: we don't invoke this code unless the node has children
            nextNodeNr = node.nodeNr + 1;

            // check if this matches the conditions
            //if (nextNr >= 0) {
            if (((tree.nodeKind[nextNodeNr] & 0xf) != Types.Type.ELEMENT) || (tree.nameCode[nextNodeNr] & 0xfffff) != fingerprint)
            {
                needToAdvance = true;
            } //}
        }

        //}
        public NodeInfo Next()
        {
            if (needToAdvance)
            {
                int thisNode = nextNodeNr;
                do
                {
                    nextNodeNr = tree.next[nextNodeNr];
                    if (nextNodeNr < thisNode)
                    {

                        // indicates we've got to the last sibling
                        nextNodeNr = -1;
                        needToAdvance = false;
                        return null;
                    }
                }
                while (((tree.nameCode[nextNodeNr] & 0xfffff) != fingerprint) || ((tree.nodeKind[nextNodeNr] & 0xf) != Types.Type.ELEMENT));
            }
            else if (nextNodeNr == -1)
            {
                return null;
            }

            needToAdvance = true;
            TinyNodeImpl nextNode = tree.GetNode(nextNodeNr);
            nextNode.SetParentNode(startNode);
            return nextNode;
        }

        //}
        public IAtomicSequence NextAtomizedValue()
        {
            if (needToAdvance)
            {
                int thisNode = nextNodeNr;
                do
                {
                    nextNodeNr = tree.next[nextNodeNr];
                    if (nextNodeNr < thisNode)
                    {

                        // indicates we've got to the last sibling
                        nextNodeNr = -1;
                        needToAdvance = false;
                        return null;
                    }
                }
                while (((tree.nameCode[nextNodeNr] & 0xfffff) != fingerprint) || (tree.nodeKind[nextNodeNr] & 0xf) != Types.Type.ELEMENT);
            }
            else if (nextNodeNr == -1)
            {
                return null;
            }

            needToAdvance = true;
            return tree.GetTypedValueOfElement(nextNodeNr);
        }

        //}
        public bool SupportsHasNext()
        {
            return true;
        }
        IItem ISequenceIterator.Next() => Next();
        public void Dispose() { }
    }
}
