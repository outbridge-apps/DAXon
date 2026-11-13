////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System.Collections.Generic;
using System;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Transformation;

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
            StackGuard.Probe();
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
                    if (!held && (tupleArray.Count == Core.MemoryLedger.BigCount || Core.MemoryLedger.Weighs(tuple, ref weight)))
                    {
                        held = true;
                        Core.MemoryLedger.Hold(tupleArray, TupleListSizer);   // summed as it grows
                    }
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

        // The tuples an order by holds until sorted: a record each with its keys and the tuple's bindings.
        internal static readonly Func<object, long> TupleListSizer = o =>
        {
            var tuples = (List<ObjectToBeSorted>)o;
            int n = tuples.Count;
            return n == 0 ? 0 : 24 + 8L * tuples.Capacity + (200 + Core.MemoryLedger.MemberBytes(tuples[n / 2].value)) * n;
        };

        private long weight;   // of medium strings in the tuples (MemoryLedger.Weighs)
        private bool held;

        // Shared by the pull and push forms; the originalPosition tie-break makes the sort stable.
        internal static void SortTupleArray(List<ObjectToBeSorted> tupleArray, IAtomicComparer[] comparers)
        {
            DeadlineSort.Sort(tupleArray, (a, b) =>
            {
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

        public override void Dispose()
        {
            StackGuard.Probe();
            @base.Dispose();
        }
    }
}
