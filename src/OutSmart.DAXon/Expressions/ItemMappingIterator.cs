////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
namespace OutSmart.DAXon.Expressions
{
    internal sealed class ItemMappingIterator : ISequenceIterator, ILookaheadIterator, ILastPositionFinder
    {
        private readonly ISequenceIterator @base;
        private readonly IItemMappingFunction action;
        private readonly OutSmart.DAXon.Core.Controller controller;   // null: no deadline check
        private readonly bool oneToOne;

        public bool HasNext => ((ILookaheadIterator)@base).HasNext;
        public ItemMappingIterator(ISequenceIterator @base, IItemMappingFunction action)
        {
            this.@base = @base;
            this.action = action;
        }

        public ItemMappingIterator(ISequenceIterator @base, IItemMappingFunction action, bool oneToOne)
        {
            this.@base = @base;
            this.action = action;
            this.oneToOne = oneToOne;
        }

        // Overload used by producers of potentially unbounded mapped sequences (e.g. the pull form
        // of a 'for' expression) so the pull loop honours the transformation's cooperative deadline.
        public ItemMappingIterator(ISequenceIterator @base, IItemMappingFunction action, bool oneToOne, OutSmart.DAXon.Core.Controller controller)
        {
            this.@base = @base;
            this.action = action;
            this.oneToOne = oneToOne;
            this.controller = controller;
        }

        public static ItemMappingIterator IMap(ISequenceIterator @base, ItemMapper.ILambda mappingExpression)
        {
            return new ItemMappingIterator(@base, ItemMapper.Of(mappingExpression));
        }

        public static ItemMappingIterator Filter(ISequenceIterator @base, ItemFilter.ILambda filterExpression)
        {
            return new ItemMappingIterator(@base, ItemFilter.Of(filterExpression));
        }

        public bool SupportsHasNext()
        {
            return oneToOne && @base is ILookaheadIterator && ((ILookaheadIterator)@base).SupportsHasNext();
        }

        public IItem Next()
        {
            try
            {
                while (true)
                {
                    controller?.CheckTimeout();
                    IItem nextSource = @base.Next();
                    if (nextSource == null)
                    {
                        return null;
                    }


                    // Call the supplied mapping function
                    IItem current = action.MapItem(nextSource);
                    if (current != null)
                    {
                        return current;
                    } // otherwise go round the loop to get the next item from the base sequence
                }
            }
            catch (XPathException e)
            {
                throw new UncheckedXPathException(e);
            }
        }

        public void Dispose()
        {
            @base.Dispose();
        }

        public bool SupportsGetLength()
        {
            return oneToOne && SequenceTool.SupportsGetLength(@base);
        }

        public int GetLength()
        {
            return SequenceTool.GetLength(@base);
        }
    }
}