////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Collections.Generic;
namespace OutSmart.DAXon.Model
{
    internal sealed class SmallAttributeMap : IAttributeMap
    {
        private readonly List<AttributeInfo> attributes;
        public SmallAttributeMap(IList<AttributeInfo> attributes)
        {

            // TODO: check uniqueness of names?
            this.attributes = new List<AttributeInfo>(attributes);
        }

        public int Size()
        {
            return attributes.Count;
        }

        public AttributeInfo Get(INodeName name)
        {
            foreach (AttributeInfo info in attributes)
            {
                if (info.GetNodeName().Equals(name))
                {
                    return info;
                }
            }

            return null;
        }

        public AttributeInfo Get(NamespaceUri uri, string local)
        {
            foreach (AttributeInfo info in attributes)
            {
                INodeName name = info.GetNodeName();
                if (name.GetLocalPart().Equals(local) && name.HasURI(uri))
                {
                    return info;
                }
            }

            return null;
        }

        public AttributeInfo GetByFingerprint(int fingerprint, NamePool namePool)
        {
            foreach (AttributeInfo info in attributes)
            {
                INodeName name = info.GetNodeName();
                if (name.ObtainFingerprint(namePool) == fingerprint)
                {
                    return info;
                }
            }

            return null;
        }

        public List<AttributeInfo> AsList()
        {
            return attributes;
        }

        public AttributeInfo ItemAt(int index)
        {
            return attributes[index];
        }
        public IEnumerator<AttributeInfo> GetEnumerator() => attributes.GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => attributes.GetEnumerator();

        // === Auto-generated stubs (StubGenerator Phase 3.1f) ===
        public string GetValue(NamespaceUri uri, string local) { AttributeInfo att = Get(uri, local); return att == null ? null : att.Value; }
        public string GetValue(string local) { AttributeInfo att = Get(NamespaceUri.NULL, local); return att == null ? null : att.Value; }
        public IAttributeMap Put(AttributeInfo att) { List<AttributeInfo> list = new List<AttributeInfo>(Size() + 1); foreach (AttributeInfo a in attributes) { if (!a.GetNodeName().Equals(att.GetNodeName())) { list.Add(a); } } list.Add(att); return SequenceTool.AttributeMapFromList(list); }
        public IAttributeMap Remove(INodeName name) { List<AttributeInfo> list = new List<AttributeInfo>(Size()); foreach (AttributeInfo a in attributes) { if (!a.GetNodeName().Equals(name)) { list.Add(a); } } return SequenceTool.AttributeMapFromList(list); }
        public void Verify() { }
        public IAttributeMap Apply(Func<AttributeInfo, AttributeInfo> mapper) { List<AttributeInfo> list = new List<AttributeInfo>(Size()); foreach (AttributeInfo a in attributes) { list.Add(mapper(a)); } return SequenceTool.AttributeMapFromList(list); }
    }
}