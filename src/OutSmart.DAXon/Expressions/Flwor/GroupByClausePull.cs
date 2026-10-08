////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System.Collections.Generic;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Expressions.Flwor
{
    internal sealed class GroupByClausePull : TuplePull
    {
        private readonly TuplePull @base;
        private readonly GroupByClause groupByClause;
        private readonly GenericAtomicComparer[] comparers;
        private IEnumerator<List<GroupByClause.ObjectToBeGrouped>> groupIterator;

        public GroupByClausePull(TuplePull @base, GroupByClause groupBy, IXPathContext context)
        {
            this.@base = @base;
            this.groupByClause = groupBy;
            comparers = new GenericAtomicComparer[groupBy.comparers.Length];
            for (int i = 0; i < comparers.Length; i++)
            {
                comparers[i] = groupBy.comparers[i].ProvideContext(context);
            }
        }

        public override bool NextTuple(IXPathContext context)
        {
            if (groupIterator == null)
            {
                TupleExpression groupingTupleExpr = groupByClause.GroupingTupleExpression;
                TupleExpression retainedTupleExpr = groupByClause.RetainedTupleExpression;
                var map = new Dictionary<object, List<GroupByClause.ObjectToBeGrouped>>();
                while (@base.NextTuple(context))
                {
                    var otbg = new GroupByClause.ObjectToBeGrouped();
                    ISequence[] groupingValues = ((Tuple)groupingTupleExpr.EvaluateItem(context)).GetMembers();
                    GroupByClausePush.CheckGroupingValues(groupingValues);
                    otbg.groupingValues = new Tuple(groupingValues);
                    otbg.retainedValues = (Tuple)retainedTupleExpr.EvaluateItem(context);
                    object key = groupByClause.GetComparisonKey(otbg.groupingValues, comparers);
                    map.TryGetValue(key, out var group);
                    GroupByClausePush.AddToGroup(key, otbg, group, map);
                }
                groupIterator = map.Values.GetEnumerator();
            }
            if (groupIterator.MoveNext())
            {
                groupByClause.ProcessGroup(groupIterator.Current, context);
                return true;
            }
            return false;
        }

        public override void Dispose()
        {
            @base.Dispose();
            groupIterator = null;
        }
    }
}
