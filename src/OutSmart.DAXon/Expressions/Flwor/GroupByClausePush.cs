////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Expressions.Flwor
{
    internal sealed class GroupByClausePush : TuplePush
    {
        private readonly TuplePush destination;
        private readonly GroupByClause groupByClause;
        private readonly Dictionary<object, List<GroupByClause.ObjectToBeGrouped>> map = new Dictionary<object, List<GroupByClause.ObjectToBeGrouped>>();
        private readonly IXPathContext context;
        private readonly GenericAtomicComparer[] comparers;

        public GroupByClausePush(Outputter outputter, TuplePush destination, GroupByClause groupBy, IXPathContext context) : base(outputter)
        {
            this.destination = destination;
            this.groupByClause = groupBy;
            this.context = context;
            comparers = new GenericAtomicComparer[groupBy.comparers.Length];
            for (int i = 0; i < comparers.Length; i++)
            {
                comparers[i] = groupBy.comparers[i].ProvideContext(context);
            }
        }

        public override void ProcessTuple(IXPathContext context)
        {
            TupleExpression groupingTupleExpr = groupByClause.GroupingTupleExpression;
            TupleExpression retainedTupleExpr = groupByClause.RetainedTupleExpression;
            var otbg = new GroupByClause.ObjectToBeGrouped();
            ISequence[] groupingValues = ((Tuple)groupingTupleExpr.EvaluateItem(context)).GetMembers();
            CheckGroupingValues(groupingValues);
            otbg.groupingValues = new Tuple(groupingValues);
            otbg.retainedValues = (Tuple)retainedTupleExpr.EvaluateItem(context);
            object key = groupByClause.GetComparisonKey(otbg.groupingValues, comparers);
            map.TryGetValue(key, out var group);
            AddToGroup(key, otbg, group, map);
        }

        internal static void AddToGroup(object key, GroupByClause.ObjectToBeGrouped objectToBeGrouped, List<GroupByClause.ObjectToBeGrouped> group, Dictionary<object, List<GroupByClause.ObjectToBeGrouped>> map)
        {
            if (group != null)
            {
                group.Add(objectToBeGrouped);
                map[key] = group;
                if (group.Count == Core.MemoryLedger.BigCount)
                {
                    Core.MemoryLedger.Hold(group, GroupSizer);   // a large group, summed as it grows
                }
            }
            else
            {
                var list = new List<GroupByClause.ObjectToBeGrouped>();
                list.Add(objectToBeGrouped);
                map[key] = list;
                if (map.Count == Core.MemoryLedger.BigCount)
                {
                    Core.MemoryLedger.Hold(map, GroupsSizer);   // many groups, summed as they grow
                }
            }
        }

        // A group's tuples, and a map's groups with a tuple each: the records, their keys and their bindings.
        private static readonly Func<object, long> GroupSizer = o =>
        {
            var group = (List<GroupByClause.ObjectToBeGrouped>)o;
            return 24 + 8L * group.Capacity + 160L * group.Count;
        };

        private static readonly Func<object, long> GroupsSizer = o => 48 + 280L * ((Dictionary<object, List<GroupByClause.ObjectToBeGrouped>>)o).Count;

        internal static void CheckGroupingValues(ISequence[] groupingValues)
        {
            try
            {
                for (int i = 0; i < groupingValues.Length; i++)
                {
                    ISequence v = groupingValues[i];
                    if (!(v is EmptySequence || v is AtomicValue))
                    {
                        IGroundedValue g = SequenceTool.ToGroundedValue(Atomizer.GetAtomizingIterator(v.Iterate(), false));
                        if (g.GetLength() > 1)
                        {
                            throw new XPathException("Grouping key value cannot be a sequence of more than one item", "XPTY0004");
                        }
                        groupingValues[i] = g;
                    }
                }
            }
            catch (UncheckedXPathException e)
            {
                throw e.GetXPathException();
            }
        }

        public override void Dispose()
        {
            foreach (var group in map.Values)
            {
                groupByClause.ProcessGroup(group, context);
                destination.ProcessTuple(context);
            }
            destination.Dispose();
        }
    }
}
