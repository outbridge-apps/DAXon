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
    public sealed class NamespaceBinding : INamespaceBindingSet
    {
        public static readonly NamespaceBinding XML = new NamespaceBinding("xml", NamespaceUri.XML);
        public static readonly NamespaceBinding DEFAULT_UNDECLARATION = new NamespaceBinding("", NamespaceUri.NULL);
        public static readonly NamespaceBinding[] EMPTY_ARRAY = new NamespaceBinding[0];
        private readonly string prefix;
        private readonly NamespaceUri uri;
        public NamespaceBinding(string prefix, NamespaceUri uri)
        {
            this.prefix = prefix;
            this.uri = uri;
            if (prefix == null || uri == null)
            {
                throw new NullReferenceException();
            }
        }

        public NamespaceUri GetNamespaceUri(string prefix)
        {
            return prefix.Equals(this.prefix) ? uri : null;
        }

        public string GetPrefix()
        {
            return prefix;
        }

        public NamespaceUri GetNamespaceUri()
        {
            return uri;
        }

        public bool IsXmlNamespace()
        {
            return prefix.Equals("xml");
        }

        public bool IsDefaultUndeclaration()
        {
            return (prefix.Length == 0) && uri == NamespaceUri.NULL;
        }

        public IEnumerator<NamespaceBinding> IIterator()
        {
            yield return this;
        }

        public override bool Equals(object obj)
        {
            return obj is NamespaceBinding && prefix.Equals(((NamespaceBinding)obj).GetPrefix()) && uri.Equals(((NamespaceBinding)obj).GetNamespaceUri());
        }

        public override int GetHashCode()
        {
            return prefix.GetHashCode() ^ uri.GetHashCode();
        }

        public override string ToString()
        {
            return prefix + "=" + uri;
        }
        // A NamespaceBinding is its own singleton INamespaceBindingSet (cf. IIterator above);
        // enumerating it yields just this one binding. Was a NIE stub -> XMLIndenter crashed serializing.
        public IEnumerator<NamespaceBinding> GetEnumerator() { yield return this; }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
