////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
namespace OutSmart.DAXon.Expressions
{
    internal sealed class ContextMappingIterator : ISequenceIterator
    {
        private readonly IFocusIterator @base;
        private readonly IContextMappingFunction action;
        private readonly IXPathContext context;
        private readonly OutSmart.DAXon.Core.Controller controller;
        private ISequenceIterator stepIterator;
        public ContextMappingIterator(IContextMappingFunction action, IXPathContext context)
        {
            @base = context.GetCurrentIterator();
            this.action = action;
            this.context = context;
            this.controller = context.GetController();
        }

        public IItem Next()
        {
            IItem nextItem;
            while (true)
            {
                controller.CheckTimeout();
                if (stepIterator != null)
                {
                    nextItem = stepIterator.Next();
                    if (nextItem != null)
                    {
                        break;
                    }
                    else
                    {
                        stepIterator = null;
                    }
                }

                if (@base.Next() != null)
                {

                    // Call the supplied mapping function. `!` over a recursive function nests this iterator once per
                    // level: the error is wrapped after the try, since a throw from the catch itself re-entered
                    // dispatch on the stack below (~20 KB per level).
                    XPathException failure = null;
                    try
                    {
                        stepIterator = action.IMap(context);
                    }
                    catch (XPathException e)
                    {
                        failure = e;
                    }

                    if (failure != null)
                    {
                        throw new UncheckedXPathException(failure);
                    }

                    nextItem = stepIterator.Next();
                    if (nextItem == null)
                    {
                        stepIterator = null;
                    }
                    else
                    {
                        break;
                    }
                }
                else
                {
                    stepIterator = null;
                    return null;
                }
            }

            return nextItem;
        }

        public void Dispose()
        {
            @base.Dispose();
            if (stepIterator != null)
            {
                stepIterator.Dispose();
            }
        }
    }

    /// <summary>
    /// ContextMappingIterator for a mapping expression that is statically at most one item:
    /// the action runs as an item evaluator against the tracked focus, with no per-item
    /// sub-iterator (the generic path allocates a SingletonIterator per focus item just to
    /// drain it once). A null result is an empty step, exactly like an empty sub-iterator.
    /// </summary>
    internal sealed class SingletonContextMappingIterator : ISequenceIterator
    {
        private readonly IFocusIterator @base;
        private readonly Elaboration.IItemEvaluator action;
        private readonly IXPathContext context;
        private readonly OutSmart.DAXon.Core.Controller controller;

        public SingletonContextMappingIterator(Elaboration.IItemEvaluator action, IXPathContext context)
        {
            @base = context.GetCurrentIterator();
            this.action = action;
            this.context = context;
            this.controller = context.GetController();
        }

        public IItem Next()
        {
            while (true)
            {
                controller.CheckTimeout();
                if (@base.Next() == null)
                {
                    return null;
                }

                IItem item = null;
                XPathException failure = null;
                try
                {
                    item = action(context);
                }
                catch (XPathException e)
                {
                    failure = e;   // wrapped after the try, as above
                }

                if (failure != null)
                {
                    throw new UncheckedXPathException(failure);
                }

                if (item != null)
                {
                    return item;
                }
            }
        }

        public void Dispose()
        {
            @base.Dispose();
        }
    }
}