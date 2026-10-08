////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2020 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Functions.HigherOrder
{
    /// <summary>
    /// This class implements the function fn:filter(), which is a standard function in XQuery 3.0
    /// </summary>
    internal sealed class FilterFn : SystemFunction
    {

        public override string StreamerName => "FilterFn";
        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            return SequenceTool.ToLazySequence(EvalFilter((IFunctionItem)arguments[1].Head(), arguments[0].Iterate(), context));
        }

        private ISequenceIterator EvalFilter(IFunctionItem function, ISequenceIterator basis, IXPathContext context)
        {
            // Predicate results are consumed to a boolean before the next item, so the coerced
            // lambda chain can run through a reused clean context + argument array.
            FusedArity1Caller fused = FusedArity1Caller.TryMake(function, context);
            if (fused != null)
            {
                return ItemMappingIterator.Filter(basis, (item) =>
                {
                    Core.Controller.CheckActiveTimeoutPerStep();
                    IItem r = fused.CallOne(item).Head();
                    if (!(r is BooleanValue b))
                    {
                        throw new XPathException("fn:filter: the filtering function must return a single xs:boolean", "XPTY0004");
                    }
                    return b.GetBooleanValue();
                });
            }

            return ItemMappingIterator.Filter(basis, (item) =>
            {
                Core.Controller.CheckActiveTimeoutPerStep();
                IItem r = DynamicCall(function, context, new ISequence[] { item }).Head();
                if (!(r is BooleanValue b))
                {
                    throw new XPathException("fn:filter: the filtering function must return a single xs:boolean", "XPTY0004");
                }
                return b.GetBooleanValue();
            });
        }
    }
}
