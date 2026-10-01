////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Ported from upstream net/sf/saxon/expr/IntersectionIterator.java (replaces the hollow stub).
// Implements the XPath 2.0 operator "intersect": a merge join over two document-ordered node iterators.

using System.Collections.Generic;
using OutSmart.DAXon.Model;

namespace OutSmart.DAXon.Expressions
{
    internal sealed class IntersectionIterator : ISequenceIterator
    {
        private readonly ISequenceIterator e1;
        private readonly ISequenceIterator e2;
        private NodeInfo nextNode1;
        private NodeInfo nextNode2;
        private readonly IComparer<NodeInfo> comparer;

        /// <summary>Form the intersection of two node-sets (each delivered in document order).</summary>
        public IntersectionIterator(ISequenceIterator p1, ISequenceIterator p2, IComparer<NodeInfo> comparer)
        {
            e1 = p1;
            e2 = p2;
            this.comparer = comparer;
            nextNode1 = NextNode(e1);
            nextNode2 = NextNode(e2);
        }

        private NodeInfo NextNode(ISequenceIterator iter)
        {
            return (NodeInfo)iter.Next();
        }

        public IItem Next()
        {
            if (nextNode1 == null)
            {
                e2.Dispose();
                return null;
            }

            if (nextNode2 == null)
            {
                e1.Dispose();
                return null;
            }

            while (nextNode1 != null && nextNode2 != null)
            {
                int c = comparer.Compare(nextNode1, nextNode2);
                if (c < 0)
                {
                    nextNode1 = NextNode(e1);
                }
                else if (c > 0)
                {
                    nextNode2 = NextNode(e2);
                }
                else
                {
                    // keys equal: the node is in both
                    NodeInfo current = nextNode2;
                    nextNode2 = NextNode(e2);
                    nextNode1 = NextNode(e1);
                    return current;
                }
            }

            return null;
        }

        public void Close()
        {
            e1.Dispose();
            e2.Dispose();
        }

        public void Dispose() => Close();
    }
}
