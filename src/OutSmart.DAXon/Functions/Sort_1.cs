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
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
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

        protected virtual List<ItemToBeSorted> GetItemsToBeSorted(ISequence input)
        {
            List<ItemToBeSorted> inputList = new List<ItemToBeSorted>();
            int i = 0;
            ISequenceIterator iterator = input.Iterate();
            IItem item;
            while ((item = iterator.Next()) != null)
            {
                ItemToBeSorted member = new ItemToBeSorted();
                member.value = item;
                member.originalPosition = i++;
                member.sortKey = item.Atomize();
                inputList.Add(member);
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
                int flatComparisons = 0;
                try
                {
                    inputList.Sort((a, b) =>
                    {
                        if ((++flatComparisons & 1023) == 0)
                        {
                            Core.Controller.CheckActiveTimeoutNow();
                        }

                        double x = a.dkey, y = b.dkey;
                        int result = double.IsNaN(x)
                            ? (double.IsNaN(y) ? 0 : -1)
                            : double.IsNaN(y) ? +1 : x == y ? 0 : x < y ? -1 : +1;
                        return result == 0 ? a.originalPosition - b.originalPosition : result;
                    });
                }
                catch (InvalidOperationException e) when (e.InnerException is XPathException limit && limit.IsRunLimit())
                {
                    throw limit;
                }

                List<IItem> flatOutput = new List<IItem>(inputList.Count);
                foreach (ItemToBeSorted member in inputList)
                {
                    flatOutput.Add(member.value);
                }

                return new SequenceExtent.Of<IItem>(flatOutput);
            }

            int comparisons = 0;
            try
            {
                inputList.Sort((a, b) =>
                {
                    // Nothing else stops a List.Sort: a million string keys make some twenty million comparisons.
                    if ((++comparisons & 1023) == 0)
                    {
                        Core.Controller.CheckActiveTimeoutNow();
                    }

                    int result = ArraySort.CompareSortKeys(a.sortKey, b.sortKey, atomicComparer);
                    if (result == 0)
                    {

                        // TODO: unnecessary, we are now using a stable sort routine
                        return a.originalPosition - b.originalPosition;
                    }
                    else
                    {
                        return result;
                    }
                }); //GenericSorter.quickSort(0, inputList.size(), sortable);
            }
            catch (InvalidOperationException e) when (e.InnerException != null)
            {
                // List<T>.Sort wraps any comparer exception in InvalidOperationException, so the plain
                // catch below never saw it. A real XPathException (e.g. from key evaluation) must propagate
                // as-is; a type-comparison failure (InvalidCastException, upstream ClassCastException) is the
                // XPTY0004 "non-comparable types" type error.
                if (e.InnerException is XPathException xe)
                {
                    throw xe;
                }
                if (e.InnerException is InvalidCastException)
                {
                    throw new XPathException("Non-comparable types found while sorting: " + e.InnerException.Message, "XPTY0004").AsTypeError();
                }
                throw;
            }
            catch (InvalidCastException e)
            {
                throw new XPathException("Non-comparable types found while sorting: " + e.Message, "XPTY0004").AsTypeError();
            }

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
