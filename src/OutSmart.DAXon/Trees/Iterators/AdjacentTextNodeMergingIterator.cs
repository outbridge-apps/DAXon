////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Utilities;

namespace OutSmart.DAXon.Trees.Iterators
{
    // Faithful port of net/sf/saxon/tree/iter/AdjacentTextNodeMergingIterator.java (Saxon 12.9).
    // Eliminates zero-length text nodes and merges adjacent text nodes from the underlying iterator.
    internal sealed class AdjacentTextNodeMergingIterator : ILookaheadIterator
    {
        private readonly ISequenceIterator @base;
        private IItem _next;

        public bool HasNext => _next != null;

        public AdjacentTextNodeMergingIterator(ISequenceIterator @base)
        {
            try
            {
                this.@base = @base;
                _next = @base.Next();
            }
            catch (UncheckedXPathException e)
            {
                throw e.GetXPathException();
            }
        }

        public IItem Next()
        {
            // A loop, not a call per node: a run of empty text nodes once cost a stack frame each.
            while (true)
            {
                IItem current = _next;
                if (current == null)
                {
                    return null;
                }
                _next = @base.Next();

                if (!AdjacentTextNodeMerger.IsTextNode(current))
                {
                    return current;
                }

                UnicodeBuilder ub = new UnicodeBuilder();
                ub.Accept(current.UnicodeStringValue);
                while (AdjacentTextNodeMerger.IsTextNode(_next))
                {
                    ub.Accept(_next.UnicodeStringValue);
                    _next = @base.Next();
                }

                if (!ub.IsEmpty())
                {
                    Orphan o = new Orphan(((NodeInfo)current).GetConfiguration());
                    o.SetNodeKind(Types.Type.TEXT);
                    o.SetStringValue(ub.ToUnicodeString());
                    return o;
                }
            }
        }

        public bool SupportsHasNext()
        {
            return true;
        }

        public void Dispose()
        {
            @base.Dispose();
        }
    }
}
