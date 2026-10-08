////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Api;

using OutSmart.DAXon.Types;
using System;
using System.Collections.Generic;
namespace OutSmart.DAXon.Model
{
    /// <summary>
    /// An implementation of IAttributeMap for use when there is exactly one attribute
    /// </summary>
    internal sealed class SingletonAttributeMap : AttributeInfo, IAttributeMap
    {
        internal SingletonAttributeMap(INodeName nodeName, ISimpleType type, string value, ILocation location, int properties) : base(nodeName, type, value, location, properties)
        {
        }

        public static SingletonAttributeMap Of(AttributeInfo att)
        {
            if (att is SingletonAttributeMap)
            {
                return (SingletonAttributeMap)att;
            }
            else
            {
                return new SingletonAttributeMap(att.GetNodeName(), att.GetType(), att.Value, att.GetLocation(), att.GetProperties());
            }
        }

        public int Size()
        {
            return 1;
        }

        public AttributeInfo Get(INodeName name)
        {
            return name.Equals(GetNodeName()) ? this : null;
        }

        public AttributeInfo Get(NamespaceUri uri, string local)
        {
            return GetNodeName().GetLocalPart().Equals(local) && GetNodeName().HasURI(uri) ? this : null;
        }

        public AttributeInfo GetByFingerprint(int fingerprint, NamePool namePool)
        {
            return GetNodeName().ObtainFingerprint(namePool) == fingerprint ? this : null;
        }

        public IAttributeMap Put(AttributeInfo att)
        {
            if (GetNodeName().Equals(att.GetNodeName()))
            {
                return SingletonAttributeMap.Of(att);
            }
            else
            {
                IList<AttributeInfo> list = new List<AttributeInfo>(2);
                list.Add(this);
                list.Add(att);
                return new SmallAttributeMap(list);
            }
        }

        public IAttributeMap Remove(INodeName name)
        {
            if (name.Equals(GetNodeName()))
            {
                return EmptyAttributeMap.GetInstance();
            }
            else
            {
                return this;
            }
        }

        public IAttributeMap Apply(Func<AttributeInfo, AttributeInfo> mapper)
        {
            return SingletonAttributeMap.Of(mapper(this));
        }

        public List<AttributeInfo> AsList()
        {
            List<AttributeInfo> list = new List<AttributeInfo>(1);
            list.Add(this);
            return list;
        }

        public AttributeInfo ItemAt(int index)
        {
            if (index == 0)
            {
                return this;
            }
            else
            {
                throw new IndexOutOfRangeException(index + " of 1");
            }
        }
        // One allocation instead of the three behind AsList().GetEnumerator() (List + backing array + enumerator box)
        public IEnumerator<AttributeInfo> GetEnumerator() => new SingleEnumerator(this);
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => new SingleEnumerator(this);

        private sealed class SingleEnumerator : IEnumerator<AttributeInfo>
        {
            private readonly SingletonAttributeMap att;
            private int pos = -1;
            internal SingleEnumerator(SingletonAttributeMap att)
            {
                this.att = att;
            }

            public AttributeInfo Current => pos == 0 ? att : null;
            object System.Collections.IEnumerator.Current => Current;
            public bool MoveNext() => ++pos == 0;
            public void Reset() => pos = -1;
            public void Dispose() { }
        }

        // === Auto-generated stubs (StubGenerator Phase 3.1f) ===
        public string GetValue(NamespaceUri uri, string local) { AttributeInfo att = Get(uri, local); return att == null ? null : att.Value; }
        public string GetValue(string local) { AttributeInfo att = Get(NamespaceUri.NULL, local); return att == null ? null : att.Value; }
        public void Verify() { }
    }
}
