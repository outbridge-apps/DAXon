////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Values;
using System.Collections.Generic;

namespace OutSmart.DAXon.Expressions.Sorting
{
    // Faithful port of net.sf.saxon.expr.sort.MergeIterator (Saxon 12.9). Was a hollow stub that didn't
    // even implement ISequenceIterator — every multi-source xsl:merge cast it and crashed (InvalidCast).
    // The sorted merge of two merge inputs, retaining all duplicates; no grouping of adjacent items.
    internal sealed class MergeIterator : ISequenceIterator, ILookaheadIterator
    {
        private readonly ISequenceIterator e1;
        private readonly ISequenceIterator e2;
        private ObjectValue<ItemWithMergeKeys> nextItem1;
        private ObjectValue<ItemWithMergeKeys> nextItem2;
        private readonly IComparer<ObjectValue<ItemWithMergeKeys>> comparer;

        public bool HasNext => nextItem1 != null || nextItem2 != null;

        /// <summary>
        /// Create the iterator. The two input iterators must return nodes in merge key order.
        /// </summary>
        public MergeIterator(ISequenceIterator p1, ISequenceIterator p2, IComparer<ObjectValue<ItemWithMergeKeys>> comparer)
        {
            this.e1 = p1;
            this.e2 = p2;
            this.comparer = comparer;
            nextItem1 = (ObjectValue<ItemWithMergeKeys>)e1.Next();
            nextItem2 = (ObjectValue<ItemWithMergeKeys>)e2.Next();
        }

        public bool SupportsHasNext() => true;

        public IItem Next()
        {
            // main merge loop: take an item from whichever set has the lower value; ties go to the first.
            if (nextItem1 != null && nextItem2 != null)
            {
                int c;
                try
                {
                    c = comparer.Compare(nextItem1, nextItem2);
                }
                catch (System.InvalidCastException)
                {
                    ItemWithMergeKeys i1 = nextItem1.GetObject();
                    ItemWithMergeKeys i2 = nextItem2.GetObject();
                    AtomicValue a1 = i1.sortKeyValues[0];
                    AtomicValue a2 = i2.sortKeyValues[0];
                    XPathException err = new XPathException("Merge key values are of non-comparable types ("
                        + OutSmart.DAXon.Types.Type.DisplayTypeName(a1)
                        + " and " + OutSmart.DAXon.Types.Type.DisplayTypeName(a2) + ")", "XTTE2230");
                    err.SetIsTypeError(true);
                    throw new UncheckedXPathException(err);
                }

                if (c <= 0)
                {
                    ObjectValue<ItemWithMergeKeys> current = nextItem1;
                    nextItem1 = (ObjectValue<ItemWithMergeKeys>)e1.Next();
                    return current;
                }
                else
                {
                    ObjectValue<ItemWithMergeKeys> current = nextItem2;
                    nextItem2 = (ObjectValue<ItemWithMergeKeys>)e2.Next();
                    return current;
                }
            }

            // collect the remaining items from whichever set has a residue
            if (nextItem1 != null)
            {
                ObjectValue<ItemWithMergeKeys> current = nextItem1;
                nextItem1 = (ObjectValue<ItemWithMergeKeys>)e1.Next();
                return current;
            }

            if (nextItem2 != null)
            {
                ObjectValue<ItemWithMergeKeys> current = nextItem2;
                nextItem2 = (ObjectValue<ItemWithMergeKeys>)e2.Next();
                return current;
            }

            return null;
        }

        public void Dispose()
        {
            e1.Dispose();
            e2.Dispose();
        }
    }
}
