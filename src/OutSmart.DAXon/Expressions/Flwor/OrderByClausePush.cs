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
    internal sealed class OrderByClausePush : TuplePush
    {
        private readonly TuplePush destination;
        private readonly OrderByClause orderByClause;
        private readonly TupleExpression tupleExpr;
        private readonly IAtomicComparer[] comparers;
        private readonly IXPathContext context;
        private int position = 0;
        private readonly List<ObjectToBeSorted> tupleArray = new List<ObjectToBeSorted>(100);

        public OrderByClausePush(Outputter outputter, TuplePush destination, TupleExpression tupleExpr, OrderByClause orderBy, IXPathContext context) : base(outputter)
        {
            this.destination = destination;
            this.tupleExpr = tupleExpr;
            this.orderByClause = orderBy;
            this.context = context;
            IAtomicComparer[] suppliedComparers = orderBy.AtomicComparers;
            comparers = new IAtomicComparer[suppliedComparers.Length];
            for (int n = 0; n < comparers.Length; n++)
            {
                this.comparers[n] = suppliedComparers[n].ProvideContext(context);
            }
        }

        public override void ProcessTuple(IXPathContext context)
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
            if (tupleArray.Count == Core.MemoryLedger.BigCount)
            {
                Core.MemoryLedger.Hold(tupleArray, OrderByClausePull.TupleListSizer);   // summed as it grows
            }
        }

        public override void Dispose()
        {
            OrderByClausePull.SortTupleArray(tupleArray, comparers);
            foreach (ObjectToBeSorted itbs in tupleArray)
            {
                tupleExpr.SetCurrentTuple(context, (Tuple)itbs.value);
                destination.ProcessTuple(context);
            }
            destination.Dispose();
        }
    }
}
