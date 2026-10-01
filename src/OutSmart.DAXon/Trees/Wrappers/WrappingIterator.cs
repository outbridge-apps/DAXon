////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Trees.Iterators;

namespace OutSmart.DAXon.Trees.Wrappers
{
    /// <summary>
    /// A WrappingIterator delivers wrappers for the nodes delivered by its underlying iterator.
    /// It is used when no whitespace stripping is actually needed, e.g. for the attribute axis,
    /// so that further iteration remains in the virtual layer rather than switching to real nodes.
    /// </summary>
    internal sealed class WrappingIterator : IAxisIterator
    {
        internal IAxisIterator @base;
        internal IVirtualNode parent;
        internal NodeInfo _current;
        internal bool atomizing = false;
        internal IWrappingFunction wrappingFunction;

        public WrappingIterator(IAxisIterator @base, IWrappingFunction function, IVirtualNode parent)
        {
            this.@base = @base;
            this.wrappingFunction = function;
            this.parent = parent;
        }

        public NodeInfo Next()
        {
            NodeInfo n = @base.Next();
            if (n == null)
            {
                return _current = null;
            }

            if (atomizing)
            {
                _current = n;
            }
            else
            {
                _current = wrappingFunction.MakeWrapper(n, parent);
            }

            return _current;
        }

        IItem ISequenceIterator.Next() => Next();

        public void Dispose()
        {
            @base.Dispose();
        }
    }
}
