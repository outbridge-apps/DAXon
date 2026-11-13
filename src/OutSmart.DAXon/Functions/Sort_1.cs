////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Values.Arrays;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// This class implements the function fn:sort#1, which is a standard function in XPath 3.1
    /// </summary>
    internal class Sort_1 : SystemFunction
    {

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            List<ItemToBeSorted> inputList = GetItemsToBeSorted(arguments[0]);
            IStringCollator collation = context.GetConfiguration().GetCollation(GetRetainedStaticContext().DefaultCollationName);
            return DoSort(inputList, collation, context);
        }

        // The items being sorted, with their keys: a record each, the key, and the item as the middle one tells.
        internal static readonly Func<object, long> SortListSizer = o =>
        {
            var items = (List<ItemToBeSorted>)o;
            int n = items.Count;
            return n == 0 ? 0 : 24 + 8L * items.Capacity + (88 + Core.MemoryLedger.MemberBytes(items[n / 2].value)) * n;
        };

        protected virtual List<ItemToBeSorted> GetItemsToBeSorted(ISequence input)
        {
            List<ItemToBeSorted> inputList = new List<ItemToBeSorted>();
            int i = 0;
            long weight = 0;   // of medium strings, which few can make heavy (MemoryLedger.Weighs)
            bool held = false;
            ISequenceIterator iterator = input.Iterate();
            IItem item;
            while ((item = iterator.Next()) != null)
            {
                ItemToBeSorted member = new ItemToBeSorted();
                member.value = item;
                member.originalPosition = i++;
                member.sortKey = item.Atomize();
                inputList.Add(member);
                if (!held && (inputList.Count == Core.MemoryLedger.BigCount || Core.MemoryLedger.Weighs(item, ref weight)))
                {
                    held = true;
                    Core.MemoryLedger.Hold(inputList, SortListSizer);   // summed as it grows
                }
            }

            return inputList;
        }

        protected virtual ISequence DoSort(List<ItemToBeSorted> inputList, IStringCollator collation, IXPathContext context)
        {
            IAtomicComparer atomicComparer = AtomicSortComparer.MakeSortComparer(collation, StandardNames.XS_ANY_ATOMIC_TYPE, context);

            // Flat path for numeric key lambdas (sort($seq, (), fn($x){number(..)})): every key a
            // single DoubleValue -> compare raw doubles with the exact semantics of the comparer's
            // double/double branch (NaN first, +-0 equal, position tie-break), skipping the
            // per-comparison type tests, virtual comparer call and double unboxing (n log n of them).
            // Throws only the run's limits, so the non-comparable-types handling below does not apply.
            bool allDoubles = true;
            foreach (ItemToBeSorted m in inputList)
            {
                if (m.sortKey is DoubleValue dv)
                {
                    m.dkey = dv.GetDoubleValue();
                }
                else
                {
                    allDoubles = false;
                    break;
                }
            }

            if (allDoubles)
            {
                DeadlineSort.Sort(inputList, (a, b) =>
                {
                    double x = a.dkey, y = b.dkey;
                    int result = double.IsNaN(x)
                        ? (double.IsNaN(y) ? 0 : -1)
                        : double.IsNaN(y) ? +1 : x == y ? 0 : x < y ? -1 : +1;
                    return result == 0 ? a.originalPosition - b.originalPosition : result;
                });

                List<IItem> flatOutput = new List<IItem>(inputList.Count);
                foreach (ItemToBeSorted member in inputList)
                {
                    flatOutput.Add(member.value);
                }

                return new SequenceExtent.Of<IItem>(flatOutput);
            }

            // A collation that compares .NET strings (a language's, case-order): each key a string once, as SortedIterator
            if (collation is SimpleCollation || collation is CaseFirstCollator)
            {
                foreach (ItemToBeSorted m in inputList)
                {
                    if (m.sortKey is StringValue sv)
                    {
                        m.sortKey = sv.StringBacked();
                    }
                }
            }

            DeadlineSort.Sort(inputList, (a, b) =>
            {
                // Equal keys keep the input order: List.Sort is not stable, this tie-break makes it so.
                int result = ArraySort.CompareSortKeys(a.sortKey, b.sortKey, atomicComparer);
                return result == 0 ? a.originalPosition - b.originalPosition : result;
            });

            List<IItem> outputList = new List<IItem>(inputList.Count);
            foreach (ItemToBeSorted member in inputList)
            {
                outputList.Add(member.value);
            }

            return new SequenceExtent.Of<IItem>(outputList);
        }
        internal sealed class ItemToBeSorted
        {
            public IItem value;
            public IGroundedValue sortKey;
            public int originalPosition;
            public double dkey;   // flat key when every sortKey is a single DoubleValue
        }
    }
}
