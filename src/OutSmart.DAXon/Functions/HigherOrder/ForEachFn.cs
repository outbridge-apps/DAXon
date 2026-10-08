////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2020 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Functions.HigherOrder
{
    /// <summary>
    /// This class implements the function fn:for-each() (formerly fn:map), which is a standard function in XQuery 3.0
    /// </summary>
    internal sealed class ForEachFn : SystemFunction
    {
        public override ItemType GetResultItemType(Expression[] args)
        {

            // IItem type of the result is the same as the result item type of the function
            ItemType fnType = args[1].GetItemType();
            if (fnType is SpecificFunctionType)
            {
                return ((SpecificFunctionType)fnType).ResultType.PrimaryType;
            }
            else
            {
                return AnyItemType.GetInstance();
            }
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            return SequenceTool.ToLazySequence(EvalMap((IFunctionItem)arguments[1].Head(), arguments[0].Iterate(), context));
        }

        private ISequenceIterator EvalMap(IFunctionItem function, ISequenceIterator @base, IXPathContext context)
        {
            return MappingIterator.IMap(@base, (item) =>
            {
                Core.Controller.CheckActiveTimeoutPerStep();
                return DynamicCall(function, context, new ISequence[] { item }).Iterate();
            });
        }
    }
}
