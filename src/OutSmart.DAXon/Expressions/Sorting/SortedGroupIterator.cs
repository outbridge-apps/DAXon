////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Api;
using System;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Expressions.Sorting
{
    internal sealed class SortedGroupIterator : SortedIterator, IGroupIterator
    {
        public SortedGroupIterator(IXPathContext context, IGroupIterator @base, ISortKeyEvaluator sortKeyEvaluator, IAtomicComparer[] comparators) : base(context, @base, sortKeyEvaluator, comparators, true)
        {
            SetHostLanguage(HostLanguage.XSLT);
        }

        protected override void BuildArray()
        {
            // No GetLength: a grouping iterator answers it by grouping the population a second time.
            ISequenceIterator @base = BaseIterator;
            int allocated = 100;
            values = new ObjectToBeSorted[allocated];
            count = 0;
            XPathContextMajor c2 = context.NewContext();
            c2.SetCurrentIterator((IFocusIterator)@base);
            IGroupIterator groupIter = (IGroupIterator)((FocusTrackingIterator)@base).UnderlyingIterator;
            c2.SetCurrentGroupIterator(groupIter);

            // initialise the array with data
            IItem item;
            while ((item = @base.Next()) != null)
            {
                Core.Controller.CheckActiveTimeout();
                if (count == allocated)
                {
                    allocated *= 2;
                    Array.Resize(ref values, allocated);
                }

                GroupToBeSorted gtbs = new GroupToBeSorted(comparators.Length);
                values[count] = gtbs;
                gtbs.value = item;
                for (int n = 0; n < comparators.Length; n++)
                {
                    gtbs.sortKeyValues[n] = sortKeyEvaluator.EvaluateSortKey(n, c2);
                }

                gtbs.originalPosition = count++;
                if (count == Core.MemoryLedger.BigCount)
                {
                    Core.MemoryLedger.Hold(this, Sizer);   // summed as it grows, as the plain sort
                }

                gtbs.currentGroupingKey = groupIter.GetCurrentGroupingKey();
                gtbs.currentGroup = groupIter.CurrentGroup();
            }
        }

        public IAtomicSequence GetCurrentGroupingKey()
        {
            return ((GroupToBeSorted)values[position - 1]).currentGroupingKey;
        }

        public IGroundedValue CurrentGroup()
        {
            return ((GroupToBeSorted)values[position - 1]).currentGroup;
        }
    }
}