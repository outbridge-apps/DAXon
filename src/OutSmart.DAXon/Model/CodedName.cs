////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace OutSmart.DAXon.Model
{
    internal sealed class CodedName : INodeName
    {
        private readonly int fingerprint;
        private readonly string prefix;
        private readonly NamePool pool;
        // One name is asked several times per output event (HasURI twice, GetLocalPart for the
        // tag); resolve the pool lookup once. Benign race: idempotent write of an immutable QName.
        private StructuredQName resolved;

        public string DisplayName => (prefix.Length == 0) ? GetLocalPart() : prefix + ":" + GetLocalPart();

        public int Fingerprint => fingerprint;
        public CodedName(int fingerprint, string prefix, NamePool pool)
        {

            this.fingerprint = fingerprint;
            this.prefix = prefix;
            this.pool = pool;
        }

        private StructuredQName Resolve()
        {
            return resolved ?? (resolved = pool.GetUnprefixedQName(fingerprint));
        }

        public string GetPrefix()
        {
            return prefix;
        }

        public NamespaceUri GetNamespaceUri()
        {
            return Resolve().GetNamespaceUri();
        }

        public string GetLocalPart()
        {
            return Resolve().GetLocalPart();
        }

        public StructuredQName GetStructuredQName()
        {
            StructuredQName qn = Resolve();
            if ((prefix.Length == 0))
            {
                return qn;
            }
            else
            {
                return new StructuredQName(prefix, qn.GetNamespaceUri(), qn.GetLocalPart());
            }
        }

        public bool HasURI(NamespaceUri ns)
        {
            return Resolve().HasURI(ns);
        }

        public NamespaceBinding GetNamespaceBinding()
        {
            return new NamespaceBinding(prefix, pool.GetURI(fingerprint));
        }

        public bool HasFingerprint()
        {
            return true;
        }

        public int ObtainFingerprint(NamePool namePool)
        {
            return fingerprint;
        }

        /// <summary>
        /// Returns a hash code value for the object.
        /// </summary>
        public override int GetHashCode()
        {
            return StructuredQName.ComputeHashCode(GetNamespaceUri(), GetLocalPart());
        }

        public override bool Equals(object obj)
        {
            if (obj is INodeName)
            {
                INodeName n = (INodeName)obj;
                if (n.HasFingerprint())
                {
                    return Fingerprint == n.Fingerprint;
                }
                else
                {
                    return n.GetLocalPart().Equals(GetLocalPart()) && n.HasURI(GetNamespaceUri());
                }
            }
            else
            {
                return false;
            }
        }

        public bool IsIdentical(IIdentityComparable other)
        {
            return other is INodeName && this.Equals(other) && this.GetPrefix().Equals(((INodeName)other).GetPrefix());
        }

        public int IdentityHashCode()
        {
            return GetHashCode() ^ GetPrefix().GetHashCode();
        }

        public override string ToString()
        {
            return DisplayName;
        }

        public string GetURI() => GetNamespaceUri().ToString(); // NodeImpl/Orphan.GetURI() route through this
    }
}
