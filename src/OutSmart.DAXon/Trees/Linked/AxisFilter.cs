////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Trees.Iterators;
using System;

namespace OutSmart.DAXon.Trees.Linked
{
    internal sealed class AxisFilter : IAxisIterator
    {
        public AxisFilter(object iter, object filter) { }
        public NodeInfo Next() => null;
        IItem ISequenceIterator.Next() => null;
        void ISequenceIterator.Dispose() { }
        void IDisposable.Dispose() { }
    }
}
