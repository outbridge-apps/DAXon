////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Transformation;
using System.Collections.Generic;

namespace OutSmart.DAXon.Trees.Iterators
{
    /// <summary>
    /// An iterator that returns the same items as its base iterator, checking to see that they are either
    /// all nodes, or all non-nodes; if they are all nodes, it delivers them in document order.
    /// </summary>
    internal sealed class HomogeneityCheckerIterator : ISequenceIterator
    {
        private ISequenceIterator @base = null;
        private ILocation loc;
        private int state;
        // state = 0: initial state, will accept either nodes or atomic values
        // state = +1: have seen a node, all further items must be nodes
        // state = -1: have seen an atomic value or function item, all further items must be the same

        public HomogeneityCheckerIterator(ISequenceIterator @base, ILocation loc)
        {
            this.@base = @base;
            this.loc = loc;
            state = 0;
        }

        public void Dispose()
        {
            @base.Dispose();
        }

        private UncheckedXPathException ReportMixedItems()
        {
            return new UncheckedXPathException(
                new XPathException("Cannot mix nodes and atomic values in the result of a path expression")
                    .WithErrorCode("XPTY0018")
                    .WithLocation(loc));
        }

        public IItem Next()
        {
            IItem item = @base.Next();
            if (item == null)
            {
                return null;
            }

            //first item in iterator
            if (state == 0)
            {
                if (item is NodeInfo)
                {
                    List<IItem> nodes = new List<IItem>(50);
                    nodes.Add(item);
                    while ((item = @base.Next()) != null)
                    {
                        if (!(item is NodeInfo))
                        {
                            throw ReportMixedItems();
                        }
                        else
                        {
                            nodes.Add(item);
                        }
                    }

                    @base = new DocumentOrderIterator(new ListIterator.Of<IItem>(nodes), GlobalOrderComparer.GetInstance());
                    state = 1; // first item is a node
                    return @base.Next();
                }
                else
                {
                    state = -1; // first item is an atomic value or function item
                }
            }
            else if (state == -1 && item is NodeInfo)
            {
                throw ReportMixedItems();
            }

            return item;
        }
    }
}
