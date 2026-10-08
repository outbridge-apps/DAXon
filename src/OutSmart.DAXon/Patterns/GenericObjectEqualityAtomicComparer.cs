////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.Collections.Generic;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Values;

namespace OutSmart.DAXon.Patterns
{

    // Stub IAtomicComparer that wraps generic equality (compile-only).
    internal sealed class GenericObjectEqualityAtomicComparer : IAtomicComparer
    {
        public static readonly GenericObjectEqualityAtomicComparer Instance = new GenericObjectEqualityAtomicComparer();
        public IStringCollator Collator => null; // object-equality comparer uses no collation (sibling comparers expose their collator or null)
        public IAtomicComparer ProvideContext(IXPathContext context) => this;
        public int CompareAtomicValues(AtomicValue a, AtomicValue b) => EqualityComparer<object>.Default.Equals(a, b) ? 0 : 1;
        public bool ComparesEqual(AtomicValue a, AtomicValue b) => EqualityComparer<object>.Default.Equals(a, b);
        public string Save() => "STUB";
    }
}
