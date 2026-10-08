////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace OutSmart.DAXon.Model
{
    /// <summary>
    /// An implementation of INodeName for the common case of a name in no namespace
    /// </summary>
    internal sealed class NoNamespaceName : INodeName
    {
        private readonly string localName;
        private int nameCode = -1;

        public string DisplayName => localName;

        public int Fingerprint => nameCode & NamePool.FP_MASK;
        public NoNamespaceName(string localName)
        {
            this.localName = localName;
        }

        public NoNamespaceName(string localName, int nameCode)
        {
            this.localName = localName;
            this.nameCode = nameCode;
        }

        public string GetPrefix()
        {
            return "";
        }

        public NamespaceUri GetNamespaceUri()
        {
            return NamespaceUri.NULL;
        }

        public string GetLocalPart()
        {
            return localName;
        }

        public StructuredQName GetStructuredQName()
        {
            return new StructuredQName("", NamespaceUri.NULL, GetLocalPart());
        }

        public bool HasURI(NamespaceUri ns)
        {
            return ns.IsEmpty();
        }

        public NamespaceBinding GetNamespaceBinding()
        {
            return NamespaceBinding.DEFAULT_UNDECLARATION;
        }

        public bool HasFingerprint()
        {
            return nameCode != -1;
        }

        public int ObtainFingerprint(NamePool namePool)
        {
            if (nameCode == -1)
            {
                return nameCode = namePool.AllocateFingerprint(NamespaceUri.NULL, localName);
            }
            else
            {
                return nameCode;
            }
        }

        /// <summary>
        /// Returns a hash code value for the object.
        /// </summary>
        public override int GetHashCode()
        {
            return StructuredQName.ComputeHashCode(NamespaceUri.NULL, localName);
        }

        public override bool Equals(object obj)
        {
            return obj is INodeName && ((INodeName)obj).GetLocalPart().Equals(localName) && ((INodeName)obj).HasURI(NamespaceUri.NULL);
        }

        public override string ToString()
        {
            return localName;
        }

        public bool IsIdentical(IIdentityComparable other)
        {
            return other is INodeName && this.Equals(other) && (((INodeName)other).GetPrefix().Length == 0);
        }

        public int IdentityHashCode()
        {
            return GetHashCode() ^ GetPrefix().GetHashCode();
        }

        // === Auto-generated stubs (StubGenerator Phase 3.1f) ===
        public string GetURI() => ""; // no-namespace name
    }
}
