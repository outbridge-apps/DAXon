////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Collections;

namespace OutSmart.DAXon.Model
{
    // EmptyAttributeMap implements IAttributeMap (20 callers assign to IAttributeMap).
    internal sealed class EmptyAttributeMap : IAttributeMap
    {
        private static readonly EmptyAttributeMap _instance = new EmptyAttributeMap();
        public static EmptyAttributeMap GetInstance() => _instance;
        public int Size() => 0;
        public AttributeInfo Get(INodeName name) => null;
        public AttributeInfo Get(NamespaceUri uri, string local) => null;
        public AttributeInfo GetByFingerprint(int fingerprint, NamePool namePool) => null; // no attributes: absent, like the sibling maps' miss result
        public string GetValue(NamespaceUri uri, string local) => null;
        public string GetValue(string local) => null;
        // 2026-06-17: was `=> this` (dropped the attribute!) -> any synthesized attribute map built from
        // EmptyAttributeMap.Put(...) came back empty (e.g. MetaTagAdjuster's <meta http-equiv...> lost its
        // attributes -> bare <meta>). Faithful to the real EmptyAttributeMap.Put: return a SingletonAttributeMap.
        public IAttributeMap Put(AttributeInfo att) => SingletonAttributeMap.Of(att);
        public IAttributeMap Remove(INodeName name) => this;
        public void Verify() { }
        public IAttributeMap Apply(Func<AttributeInfo, AttributeInfo> mapper) => this;
        public List<AttributeInfo> AsList() => new List<AttributeInfo>();
        public AttributeInfo ItemAt(int index) => throw new IndexOutOfRangeException(index + " of 0");
        // IEnumerable<AttributeInfo>
        public IEnumerator<AttributeInfo> GetEnumerator() => EmptyEnumerator.SHARED;
        IEnumerator IEnumerable.GetEnumerator() => EmptyEnumerator.SHARED;

        // Stateless (MoveNext is always false), so one shared instance serves every foreach -
        // net472 allocates a fresh enumerator even for Enumerable.Empty<T>()
        private sealed class EmptyEnumerator : IEnumerator<AttributeInfo>
        {
            internal static readonly EmptyEnumerator SHARED = new EmptyEnumerator();
            public AttributeInfo Current => null;
            object IEnumerator.Current => null;
            public bool MoveNext() => false;
            public void Reset() { }
            public void Dispose() { }
        }
    }
}
