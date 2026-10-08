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
    // Faithful port of net.sf.saxon.expr.sort.MergeGroupingIterator (Saxon 12.9). Was a hollow stub in the
    // WRONG namespace (OutSmart.DAXon.Expressions) that didn't implement ISequenceIterator — every xsl:merge crashed
    // with an InvalidCast the moment grouping started.
    // Groups the result of merging several xsl:merge input streams, identifying groups of adjacent items
    // having the same merge key value.
    internal sealed class MergeGroupingIterator : IGroupIterator, ILookaheadIterator, ILastPositionFinder
    {
        private readonly ISequenceIterator baseItr;
        private ObjectValue<ItemWithMergeKeys> currenti = null;
        private ObjectValue<ItemWithMergeKeys> nextItem;
        private List<IItem> currentMembers;
        private Dictionary<string, List<IItem>> currentSourceMembers;
        private readonly IComparer<ObjectValue<ItemWithMergeKeys>> comparer;
        internal IList<AtomicValue> compositeMergeKey;
        private readonly ILastPositionFinder lastPositionFinder;

        public bool HasNext => nextItem != null;

        public MergeGroupingIterator(ISequenceIterator p1, IComparer<ObjectValue<ItemWithMergeKeys>> comp, ILastPositionFinder lpf)
        {
            this.baseItr = p1;
            nextItem = (ObjectValue<ItemWithMergeKeys>)p1.Next();
            if (nextItem != null)
            {
                compositeMergeKey = nextItem.GetObject().sortKeyValues;
            }

            this.comparer = comp;
            this.lastPositionFinder = lpf;
        }

        // Read ahead a group of items having common merge key values into currentMembers; leave nextItem
        // at the next item after this group, or null if there are no more items.
        private void Advance()
        {
            currentMembers = new List<IItem>(20);
            currentSourceMembers = new Dictionary<string, List<IItem>>(20);
            IItem currentItem = currenti.GetObject().baseItem;
            string source = currenti.GetObject().sourceName;
            currentMembers.Add(currentItem);
            if (source != null)
            {
                List<IItem> list = new List<IItem>();
                list.Add(currentItem);
                currentSourceMembers[source] = list;
            }

            while (true)
            {
                ObjectValue<ItemWithMergeKeys> nextCandidate = (ObjectValue<ItemWithMergeKeys>)baseItr.Next();
                if (nextCandidate == null)
                {
                    nextItem = null;
                    return;
                }

                try
                {
                    int c = comparer.Compare(currenti, nextCandidate);
                    if (c == 0)
                    {
                        currentItem = nextCandidate.GetObject().baseItem;
                        source = nextCandidate.GetObject().sourceName;
                        currentMembers.Add(currentItem);
                        if (source != null)
                        {
                            List<IItem> list;
                            if (!currentSourceMembers.TryGetValue(source, out list))
                            {
                                list = new List<IItem>();
                                currentSourceMembers[source] = list;
                            }

                            list.Add(currentItem);
                        }
                    }
                    else if (c > 0)
                    {
                        IList<AtomicValue> keys = nextCandidate.GetObject().sortKeyValues;
                        throw new XPathException(
                            "Merge input for source " + source + " is not ordered according to merge key, detected at key value: " +
                            string.Join(", ", keys), "XTDE2220");
                    }
                    else
                    {
                        nextItem = nextCandidate;
                        return;
                    }
                }
                catch (System.InvalidCastException)
                {
                    XPathException err = new XPathException("Merge key values are of non-comparable types ("
                        + OutSmart.DAXon.Types.Type.DisplayTypeName(currentItem) + " and "
                        + OutSmart.DAXon.Types.Type.DisplayTypeName(nextCandidate.GetObject().baseItem) + ')', "XTTE2230");
                    err.SetIsTypeError(true);
                    throw err;
                }
            }
        }

        public bool SupportsHasNext() => true;

        public IItem Next()
        {
            try
            {
                if (nextItem == null)
                {
                    currenti = null;
                    return null;
                }

                currenti = nextItem;
                compositeMergeKey = nextItem.GetObject().sortKeyValues;
                Advance();
                return currenti.GetObject().baseItem;
            }
            catch (XPathException e)
            {
                throw new UncheckedXPathException(e);
            }
        }

        public void Dispose()
        {
            baseItr.Dispose();
        }

        public bool SupportsGetLength() => true;

        public int GetLength() => lastPositionFinder.GetLength();

        public IAtomicSequence GetCurrentGroupingKey()
        {
            return new AtomicArray(compositeMergeKey);
        }

        public IGroundedValue CurrentGroup()
        {
            return SequenceExtent.MakeSequenceExtent(currentMembers);
        }

        public ISequenceIterator IterateCurrentGroup(string source)
        {
            List<IItem> sourceMembers;
            if (!currentSourceMembers.TryGetValue(source, out sourceMembers) || sourceMembers == null)
            {
                return EmptyIterator.GetInstance();
            }
            else
            {
                return new ListIterator.Of<IItem>(sourceMembers);
            }
        }
    }
}
