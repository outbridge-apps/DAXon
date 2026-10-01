////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using System;

namespace OutSmart.DAXon.Trees.Iterators
{
    // Iterates the node array directly as a real IAxisIterator (ParentNodeImpl.IterateChildren casts to it);
    // equivalent to upstream ArrayIterator.OfNodes without the ArrayIterator/Of/Reverse cascade.
    internal sealed class OfNodes<N> : IAxisIterator
    {
        private readonly N[] _items;
        private int _pos;
        public OfNodes(N[] items) { _items = items ?? new N[0]; }
        public NodeInfo Next() => _pos < _items.Length ? (NodeInfo)(object)_items[_pos++] : null;
        IItem ISequenceIterator.Next() => Next();
        void ISequenceIterator.Dispose() { }
        void IDisposable.Dispose() { }
    }
}
