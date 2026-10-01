////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Trees.Iterators;
using System;

namespace OutSmart.DAXon.Trees.Linked
{
    // Implement IAxisIterator interface so assignment to IAxisIterator works.
    internal sealed class AttributeAxisIterator : IAxisIterator
    {
        public AttributeAxisIterator(object node, object test) { }
        public NodeInfo Next() => null;
        IItem ISequenceIterator.Next() => null;
        void ISequenceIterator.Dispose() { }
        void IDisposable.Dispose() { }
    }
}
