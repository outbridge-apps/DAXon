////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Trees.Iterators;

namespace OutSmart.DAXon.Patterns
{
    /// <summary>
    /// An iterator over nodes, that concatenates the nodes returned by two supplied iterators.
    /// </summary>
    // Was a hollow stub (empty class, (object,object) no-op ctor implementing nothing): Pattern.SelectNodes
    // builds an element-or-attribute stream by concatenating self + attribute axes and casts the result to
    // IAxisIterator, so the stub InvalidCast'd — breaking the built-in idref key index (fn:idref) and any
    // pattern over the attribute axis. Faithful port of net.sf.saxon.tree.iter.ConcatenatingAxisIterator.
    internal sealed class ConcatenatingAxisIterator : IAxisIterator
    {
        private readonly IAxisIterator first;
        private readonly IAxisIterator second;
        private IAxisIterator active;

        public ConcatenatingAxisIterator(IAxisIterator first, IAxisIterator second)
        {
            this.first = first;
            this.second = second;
            this.active = first;
        }

        public NodeInfo Next()
        {
            NodeInfo n = active.Next();
            if (n == null && active == first)
            {
                active = second;
                n = second.Next();
            }

            return n;
        }

        // net472 has no covariant interface implementation: NodeInfo Next() satisfies IAxisIterator.Next()
        // but not ISequenceIterator.Next() (IItem), so bridge it explicitly.
        IItem ISequenceIterator.Next() => Next();

        public void Dispose()
        {
            first.Dispose();
            second.Dispose();
        }
    }
}
