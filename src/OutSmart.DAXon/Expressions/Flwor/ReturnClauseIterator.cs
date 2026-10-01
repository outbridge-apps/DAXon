////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Model;

namespace OutSmart.DAXon.Expressions.Flwor
{
    /// <summary>
    /// Applies the return expression of a FLWOR expression to each of the tuples in a supplied tuple
    /// stream, returning the result as an iterator.
    /// </summary>
    internal sealed class ReturnClauseIterator : ISequenceIterator
    {
        private readonly TuplePull @base;
        private readonly IPullEvaluator action;
        private readonly IXPathContext context;
        private ISequenceIterator results = null;

        public ReturnClauseIterator(TuplePull @base, IPullEvaluator returnAction, IXPathContext context)
        {
            this.@base = @base;
            this.action = returnAction;
            this.context = context;
        }

        public IItem Next()
        {
            IItem nextItem;
            while (true)
            {
                if (results != null)
                {
                    nextItem = results.Next();
                    if (nextItem != null)
                    {
                        break;
                    }
                    else
                    {
                        results = null;
                    }
                }

                if (@base.NextTuple(context))
                {
                    // Call the supplied return expression
                    results = action.Iterate(context);
                    nextItem = results.Next();
                    if (nextItem == null)
                    {
                        results = null;
                    }
                    else
                    {
                        break;
                    }

                    // now go round the loop to get the next item from the base sequence
                }
                else
                {
                    results = null;
                    return null;
                }
            }

            return nextItem;
        }

        public void Dispose()
        {
            results?.Dispose();
            @base.Dispose();
        }
    }
}
