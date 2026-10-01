////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Ported from upstream net/sf/saxon/expr/DifferenceIterator.java (replaces the hollow stub).
// Implements the XPath 2.0 operator "except": nodes in p1 that are not in p2 (both in document order).

using System.Collections.Generic;
using OutSmart.DAXon.Model;

namespace OutSmart.DAXon.Expressions
{
    internal sealed class DifferenceIterator : ISequenceIterator
    {
        private readonly ISequenceIterator p1;
        private readonly ISequenceIterator p2;
        private NodeInfo nextNode1;
        private NodeInfo nextNode2;
        private readonly IComparer<NodeInfo> comparer;

        /// <summary>Form the difference p1 - p2 (each operand delivered in document order).</summary>
        public DifferenceIterator(ISequenceIterator p1, ISequenceIterator p2, IComparer<NodeInfo> comparer)
        {
            this.p1 = p1;
            this.p2 = p2;
            this.comparer = comparer;
            nextNode1 = NextNode(p1);
            nextNode2 = NextNode(p2);
        }

        private NodeInfo NextNode(ISequenceIterator iter)
        {
            return (NodeInfo)iter.Next();
        }

        public IItem Next()
        {
            while (true)
            {
                if (nextNode1 == null)
                {
                    p2.Dispose();
                    return null;
                }

                if (nextNode2 == null)
                {
                    // second node-set exhausted; deliver the next from the first
                    return Deliver();
                }

                int c = comparer.Compare(nextNode1, nextNode2);
                if (c < 0)
                {
                    // p1 is lower: it is not in p2
                    return Deliver();
                }
                else if (c > 0)
                {
                    nextNode2 = NextNode(p2);
                    if (nextNode2 == null)
                    {
                        return Deliver();
                    }
                }
                else
                {
                    // keys equal: node is in both, so skip it
                    nextNode2 = NextNode(p2);
                    nextNode1 = NextNode(p1);
                }
            }
        }

        private NodeInfo Deliver()
        {
            NodeInfo current = nextNode1;
            nextNode1 = NextNode(p1);
            return current;
        }

        public void Close()
        {
            p1.Dispose();
            p2.Dispose();
        }

        public void Dispose() => Close();
    }
}
