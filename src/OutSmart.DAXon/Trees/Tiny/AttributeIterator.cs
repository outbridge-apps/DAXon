////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Trees.Iterators;
using System;

namespace OutSmart.DAXon.Trees.Tiny
{
    // The attribute axis of a TinyTree element, as Saxon's: the attributes whose attParent is the element, filtered
    // by the node test.
    internal sealed class AttributeIterator : IAxisIterator
    {
        private readonly TinyTree _tree;
        private readonly int _element;
        private readonly NodeTest _test;
        private int _index;
        public AttributeIterator(object tree, int node, object test)
        {
            _tree = tree as TinyTree;
            _element = node;
            _test = test as NodeTest;
            if (_tree != null)
            {
                _index = _tree.alpha[node];
            }
        }
        public NodeInfo Next()
        {
            while (_tree != null && _index >= 0 && _index < _tree.numberOfAttributes && _tree.attParent[_index] == _element)
            {
                int cur = _index++;
                var att = new TinyAttributeImpl(_tree, cur);
                if (_test == null || _test.Test(att))
                {
                    return att;
                }
            }
            return null;
        }
        IItem ISequenceIterator.Next() => Next();
        void ISequenceIterator.Dispose() { }
        void IDisposable.Dispose() { }
    }
}
