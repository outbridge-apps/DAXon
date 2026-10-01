////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2020 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
//
// Ported from upstream net/sf/saxon/functions/hof/SystemFunctionWithBoundContextItem.java (part of the
// fn:function-lookup cascade).

using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Types;

namespace OutSmart.DAXon.Functions.HigherOrder
{
    /// <summary>
    /// A function item that wraps a system function together with a saved context item, so that when the
    /// function is later called (e.g. after being returned by function-lookup) it sees the bound context item.
    /// </summary>
    internal sealed class SystemFunctionWithBoundContextItem : AbstractFunction
    {
        private readonly SystemFunction target;
        private readonly IItem contextItem;

        public override IFunctionItemType FunctionItemType => target.FunctionItemType;

        public override string Description => target.Description;

        public SystemFunctionWithBoundContextItem(SystemFunction target, IXPathContext context)
        {
            this.target = target;
            IItem ci = context.GetContextItem();
            if (ci is NodeInfo && ci.IsStreamed())
            {
                ci = null; // causing an XPDY0002 when the function is actually called
            }

            this.contextItem = ci;
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            IXPathContext c2 = context.NewMinorContext();
            c2.SetCurrentIterator(new ManualIterator(contextItem));
            return target.Call(c2, arguments);
        }

        public override int GetArity()
        {
            return target.GetArity();
        }

        public override StructuredQName GetFunctionName()
        {
            return target.GetFunctionName();
        }
    }
}
