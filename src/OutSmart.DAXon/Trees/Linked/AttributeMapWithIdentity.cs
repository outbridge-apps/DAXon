////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Internal.Collections;
using OutSmart.DAXon.Internal.Streams;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Trees.Linked
{
    internal sealed class AttributeMapWithIdentity : IAttributeMap
    {
        private readonly IList<AttributeInfo> attributes;
        public AttributeMapWithIdentity(IList<AttributeInfo> attributes)
        {
            this.attributes = attributes;
        }

        public int Size()
        {
            int count = 0;
            foreach (AttributeInfo att in attributes)
            {
                if (!(att is AttributeInfo.Deleted))
                {
                    count++;
                }
            }

            return count;
        }

        public IAxisIterator IterateAttributes(ElementImpl owner)
        {
            IList<NodeInfo> list = new List<NodeInfo>(attributes.Count);
            for (int i = 0; i < attributes.Count; i++)
            {
                AttributeInfo att = attributes[i];
                if (!(att is AttributeInfo.Deleted))
                {
                    list.Add(new AttributeImpl(owner, i));
                }
            }

            return new NodeListIterator(list);
        }

        public AttributeInfo Get(INodeName name)
        {
            foreach (AttributeInfo info in attributes)
            {
                if (info.GetNodeName().Equals(name) && !(info is AttributeInfo.Deleted))
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
                if (name.GetLocalPart().Equals(local) && name.HasURI(uri) && !(info is AttributeInfo.Deleted))
                {
                    return info;
                }
            }

            return null;
        }

        public AttributeMapWithIdentity Set(int index, AttributeInfo info)
        {
            IList<AttributeInfo> newList = new List<AttributeInfo>(attributes);
            if (index >= 0 && index < attributes.Count)
            {
                newList[index] = info;
            }
            else if (index == attributes.Count)
            {
                newList.Add(info);
            }

            return new AttributeMapWithIdentity(newList);
        }

        public AttributeMapWithIdentity Add(AttributeInfo info)
        {
            IList<AttributeInfo> newList = new List<AttributeInfo>(attributes);
            newList.Add(info);
            return new AttributeMapWithIdentity(newList);
        }

        public AttributeMapWithIdentity Remove(int index)
        {
            IList<AttributeInfo> newList = new List<AttributeInfo>(attributes);
            if (index >= 0 && index < attributes.Count)
            {
                AttributeInfo.Deleted del = new AttributeInfo.Deleted(attributes[index]);
                newList[index] = del;
            }

            return new AttributeMapWithIdentity(newList);
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
            IList<AttributeInfo> list = attributes.Where((info) => !(info is AttributeInfo.Deleted)).ToList();
            return list is List<object> ? (List<AttributeInfo>)list : new List<AttributeInfo>(list);
        }

        public AttributeInfo ItemAt(int index)
        {
            return attributes[index];
        }
        // Non-deleted live entries; the class already tracks deletions via AttributeInfo.Deleted.
        private IEnumerable<AttributeInfo> Live() => attributes.Where(info => !(info is AttributeInfo.Deleted));
        public IEnumerator<AttributeInfo> GetEnumerator() => Live().GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        // Formerly NIE stubs; every one is expressible via the existing Get/Add/Set helpers.
        public string GetValue(NamespaceUri uri, string local) { AttributeInfo a = Get(uri, local); return a == null ? null : a.Value; }
        public string GetValue(string local) => GetValue(NamespaceUri.NULL, local);
        public IAttributeMap Put(AttributeInfo att)
        {
            for (int i = 0; i < attributes.Count; i++)
            {
                if (!(attributes[i] is AttributeInfo.Deleted) && attributes[i].GetNodeName().Equals(att.GetNodeName()))
                {
                    return Set(i, att);
                }
            }

            return Add(att);
        }
        public IAttributeMap Remove(INodeName name)
        {
            for (int i = 0; i < attributes.Count; i++)
            {
                if (!(attributes[i] is AttributeInfo.Deleted) && attributes[i].GetNodeName().Equals(name))
                {
                    return Remove(i);
                }
            }

            return this;
        }
        public void Verify() { } // no invariant to check on a plain list-backed map
        public IAttributeMap Apply(Func<AttributeInfo, AttributeInfo> mapper)
        {
            IList<AttributeInfo> mapped = new List<AttributeInfo>(attributes.Count);
            foreach (AttributeInfo info in attributes)
            {
                mapped.Add(info is AttributeInfo.Deleted ? info : mapper(info));
            }

            return new AttributeMapWithIdentity(mapped);
        }
    }
}