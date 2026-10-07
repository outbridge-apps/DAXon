////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Expressions.Flwor
{
    internal sealed class OrderByClausePull : TuplePull
    {
        private readonly TuplePull @base;
        private readonly OrderByClause orderByClause;
        private readonly TupleExpression tupleExpr;
        private int currentPosition = -1;
        private readonly IAtomicComparer[] comparers;
        private readonly List<ObjectToBeSorted> tupleArray = new List<ObjectToBeSorted>(100);

        public OrderByClausePull(TuplePull @base, TupleExpression tupleExpr, OrderByClause orderBy, IXPathContext context)
        {
            this.@base = @base;
            this.tupleExpr = tupleExpr;
            this.orderByClause = orderBy;
            IAtomicComparer[] suppliedComparers = orderBy.AtomicComparers;
            comparers = new IAtomicComparer[suppliedComparers.Length];
            for (int n = 0; n < comparers.Length; n++)
            {
                this.comparers[n] = suppliedComparers[n].ProvideContext(context);
            }
        }

        public override bool NextTuple(IXPathContext context)
        {
            if (currentPosition < 0)
            {
                currentPosition = 0;
                int position = 0;
                while (@base.NextTuple(context))
                {
                    Tuple tuple = (Tuple)tupleExpr.EvaluateItem(context);
                    SortKeyDefinitionList sortKeyDefinitions = orderByClause.SortKeyDefinitions;
                    ObjectToBeSorted itbs = new ObjectToBeSorted(sortKeyDefinitions.Size());
                    itbs.value = tuple;
                    for (int i = 0; i < sortKeyDefinitions.Size(); i++)
                    {
                        itbs.sortKeyValues[i] = orderByClause.EvaluateSortKey(i, context);
                    }
                    itbs.originalPosition = ++position;
                    tupleArray.Add(itbs);
                }
                SortTupleArray(tupleArray, comparers);
            }
            if (currentPosition < tupleArray.Count)
            {
                tupleExpr.SetCurrentTuple(context, (Tuple)tupleArray[currentPosition++].value);
                return true;
            }
            return false;
        }

        // Shared by the pull and push forms. List sorting wraps comparer exceptions in
        // InvalidOperationException (same gotcha as fn:sort, R5): unwrap an inner XPathException as-is and
        // turn an inner InvalidCastException (non-comparable sort keys) into XPTY0004, as upstream does for
        // ClassCastException. Stability is guaranteed by the originalPosition tie-break in the comparator.
        internal static void SortTupleArray(List<ObjectToBeSorted> tupleArray, IAtomicComparer[] comparers)
        {
            int comparisons = 0;
            try
            {
                tupleArray.Sort((a, b) =>
                {
                    if ((++comparisons & 1023) == 0)
                    {
                        Core.Controller.CheckActiveTimeoutNow();   // as in SortedIterator: nothing else stops a List.Sort
                    }

                    for (int i = 0; i < comparers.Length; i++)
                    {
                        int comp = comparers[i].CompareAtomicValues(a.sortKeyValues[i], b.sortKeyValues[i]);
                        if (comp != 0)
                        {
                            return comp;
                        }
                    }
                    return a.originalPosition - b.originalPosition;
                });
            }
            catch (InvalidCastException e)
            {
                throw new XPathException("Non-comparable types found while sorting: " + e.Message, "XPTY0004").AsTypeError();
            }
            catch (InvalidOperationException e) when (e.InnerException != null)
            {
                if (e.InnerException is XPathException xe)
                {
                    throw xe;
                }
                if (e.InnerException is InvalidCastException ice)
                {
                    throw new XPathException("Non-comparable types found while sorting: " + ice.Message, "XPTY0004").AsTypeError();
                }
                throw;
            }
        }

        public override void Dispose()
        {
            @base.Dispose();
        }
    }
}
