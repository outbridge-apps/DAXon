////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2020 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using System.Collections.Generic;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Functions.HigherOrder
{
    internal sealed class Sort_3 : Sort_2
    {
        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            ISequence input = arguments[0];
            List<ItemToBeSorted> inputList = new List<ItemToBeSorted>();
            int i = 0;
            IFunctionItem key = (IFunctionItem)arguments[2].Head();

            // Key results are atomized (grounded) before the next item, so the coerced lambda
            // chain can run through a reused clean context + argument array.
            FusedArity1Caller fused = FusedArity1Caller.TryMake(key, context);
            NumericChildSortKey numericKey = NumericChildSortKey.TryMake(key);
            ISequenceIterator iterator = input.Iterate();
            IItem item;
            while ((item = iterator.Next()) != null)
            {
                Core.Controller.CheckActiveTimeoutPerStep();   // one call of the key, as in array:sort
                ItemToBeSorted member = new ItemToBeSorted();
                member.value = item;
                member.originalPosition = i++;
                DoubleValue direct = numericKey?.TryExtract(item);
                if (direct != null)
                {
                    member.sortKey = direct;
                    inputList.Add(member);
                    continue;
                }

                // The sort key is the atomized result of the key function (fn:sort compares atomic values);
                // a key that yields nodes (e.g. function($e){$e/name/last, $e/name/first}) must be atomized
                // first, else CompareSortKeys' (AtomicValue) cast fails -> spurious XPTY0004. (Sort_1 atomizes
                // the item directly; the keyed form must atomize the function output.)
                ISequence keyed = fused != null ? fused.CallOne(item) : DynamicCall(key, context, new ISequence[] { item });
                member.sortKey = OutSmart.DAXon.Expressions.Atomizer.Atomize(keyed);
                inputList.Add(member);
            }

            return DoSort(inputList, GetCollation(context, arguments[1]), context);
        }
    }
}