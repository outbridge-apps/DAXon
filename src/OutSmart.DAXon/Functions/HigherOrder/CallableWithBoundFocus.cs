////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2020 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
//
// Ported from upstream net/sf/saxon/functions/hof/CallableWithBoundFocus.java (part of the fn:function-lookup
// cascade).

using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Trees.Iterators;

namespace OutSmart.DAXon.Functions.HigherOrder
{
    /// <summary>
    /// A Callable that wraps another Callable and a dynamic context, in effect acting as a closure that
    /// executes the original callable with a saved context.
    /// </summary>
    internal sealed class CallableWithBoundFocus : ICallable
    {
        private readonly ICallable target;
        private readonly IXPathContext boundContext;

        public CallableWithBoundFocus(ICallable target, IXPathContext context)
        {
            this.target = target;
            boundContext = context.NewContext();
            if (context.GetCurrentIterator() == null)
            {
                boundContext.SetCurrentIterator(null);
            }
            else
            {
                ManualIterator iter = new ManualIterator(context.GetContextItem(), context.GetCurrentIterator().Position());
                iter.SetLengthFinder(() => context.GetLast());
                boundContext.SetCurrentIterator(iter);
            }
        }

        public ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            return target.Call(boundContext, arguments);
        }
    }
}
