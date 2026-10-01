////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Model
{
    public sealed class NamePool
    {
        private readonly object syncLock = new object();
        public const int FP_MASK = 0xfffff;
        // Since fingerprints in the range 0-1023 belong to predefined names, user-defined names
        // will always have a fingerprint above this range, which can be tested by a mask.
        public const int USER_DEFINED_MASK = 0xffc00;
        // Limit: maximum number of fingerprints
        private static readonly int MAX_FINGERPRINT = FP_MASK;
        // Writes happen under the AllocateFingerprint lock, but its hit path, GetFingerprint and
        // GetUnprefixedQName read lock-free from transform threads, so both maps must be
        // concurrent (upstream: ConcurrentHashMap): a plain-Dictionary read that straddles
        // the writer's resize walks a mixed-modulus chain and misses a present key.
        // A map from names to fingerprints
        private readonly ConcurrentDictionary<NameKey, int> qNameToInteger = new ConcurrentDictionary<NameKey, int>(1, 1000);
        // A map from fingerprints to QNames
        private readonly ConcurrentDictionary<int, StructuredQName> integerToQName = new ConcurrentDictionary<int, StructuredQName>(1, 1000);
        // Next fingerprint available to be allocated. Starts at 1024 as low-end fingerprints are statically allocated to system-defined
        // names
        private readonly AtomicCounter unique = new AtomicCounter(1024);
        // A map containing suggested prefixes for particular URIs. Concurrent because
        // SuggestPrefix is public and takes no lock at all.
        private readonly ConcurrentDictionary<NamespaceUri, string> suggestedPrefixes = new ConcurrentDictionary<NamespaceUri, string>();
        /// <summary>
        /// Create a NamePool
        /// </summary>
        public NamePool()
        {
        }

        public void SuggestPrefix(string prefix, NamespaceUri uri)
        {
            suggestedPrefixes[uri] = prefix;
        }

        public StructuredQName GetUnprefixedQName(int nameCode)
        {
            int fp = nameCode & FP_MASK;
            if ((fp & USER_DEFINED_MASK) == 0)
            {
                return StandardNames.GetUnprefixedQName(fp);
            }

            return integerToQName.GetOrDefault(fp);
        }

        public StructuredQName GetStructuredQName(int fingerprint)
        {
            return GetUnprefixedQName(fingerprint);
        }

        public static bool IsPrefixed(int nameCode)
        {
            return (nameCode & 0x3ff00000) != 0;
        }

        public string SuggestPrefixForURI(NamespaceUri uri)
        {
            if (uri.Equals(NamespaceUri.XML))
            {
                return "xml";
            }

            return suggestedPrefixes.GetOrDefault(uri);
        }

        public int AllocateFingerprint(NamespaceUri uri, string local)
        {
            if (NamespaceUri.IsReserved(uri) || NamespaceUri.SAXON.Equals(uri))
            {
                int fp = StandardNames.GetFingerprint(uri, local);
                if (fp != -1)
                {
                    return fp;
                }
            }

            // Every document re-resolves the same names: a hit needs neither the lock nor an allocation
            NameKey key = new NameKey(uri, local);
            if (qNameToInteger.TryGetValue(key, out int existing))
            {
                return existing;
            }

            lock (syncLock)
            {
                if (qNameToInteger.TryGetValue(key, out existing))
                {
                    return existing;
                }

                long nextUnique = unique.AndIncrement;
                if (nextUnique > MAX_FINGERPRINT)
                {
                    // Terminal, not transient: nothing can be evicted (fingerprints are baked into
                    // compiled patterns), so every later new name on this Processor fails the same
                    // way. The message has to say that, or the host reads it as a one-off.
                    throw new NamePoolLimitException(
                        "NamePool exhausted: " + qNameToInteger.Count + " distinct names allocated, ceiling is "
                        + MAX_FINGERPRINT + ". Fingerprints are permanent - they are baked into compiled patterns"
                        + " and into each mode's rule-chain index, so none can be evicted and every further new"
                        + " name on this Processor will fail the same way. Replace the Processor, together with"
                        + " any cached XsltExecutable built from it. Workloads that mint names per message"
                        + " (data-derived element names, a GUID in a namespace) walk into this ceiling; watch"
                        + " NamePool.UserDefinedNameCount and see docs/HOSTING.md.");
                }

                int next = (int)nextUnique;
                // Reverse map first: a lock-free reader that finds the fingerprint must be able to resolve it
                integerToQName[next] = new StructuredQName("", uri, local);
                qNameToInteger[key] = next;
                return next;
            }
        }

        /// <summary>
        /// Distinct user-defined names this pool has allocated. Fingerprints are permanent -
        /// nothing evicts them, because they are baked into compiled patterns - so this only
        /// grows, up to the MAX_FINGERPRINT ceiling. See docs/HOSTING.md.
        /// </summary>
        public int UserDefinedNameCount => qNameToInteger.Count;

        public NamespaceUri GetURI(int nameCode)
        {
            int fp = nameCode & FP_MASK;
            if ((fp & USER_DEFINED_MASK) == 0)
            {
                return StandardNames.GetURI(fp);
            }

            return GetUnprefixedQName(fp).GetNamespaceUri();
        }

        public string GetLocalName(int nameCode)
        {
            return GetUnprefixedQName(nameCode).GetLocalPart();
        }

        public string GetDisplayName(int nameCode)
        {
            return GetStructuredQName(nameCode).DisplayName;
        }

        public string GetClarkName(int nameCode)
        {
            return GetUnprefixedQName(nameCode).ClarkName;
        }

        public string GetEQName(int nameCode)
        {
            return GetUnprefixedQName(nameCode).EQName;
        }

        public int AllocateClarkName(string expandedName)
        {
            NamespaceUri @namespace;
            string localName;
            if (expandedName[0] == '{')
            {
                int closeBrace = expandedName.IndexOf('}');
                if (closeBrace < 0)
                {
                    throw new ArgumentException("No closing '}' in Clark name");
                }

                @namespace = NamespaceUri.Of(expandedName.Substring(1, closeBrace - 1) /*Java substring(begin,END) -> C# (start,LENGTH)*/);
                if (closeBrace == expandedName.Length)
                {
                    throw new ArgumentException("Missing local part in Clark name");
                }

                localName = expandedName.Substring(closeBrace + 1);
            }
            else
            {
                @namespace = NamespaceUri.NULL;
                localName = expandedName;
            }

            return AllocateFingerprint(@namespace, localName);
        }

        public int GetFingerprint(NamespaceUri uri, string localName)
        {

            // A read-only version of allocate()
            if (NamespaceUri.IsReserved(uri) || uri.Equals(NamespaceUri.SAXON))
            {
                int fp = StandardNames.GetFingerprint(uri, localName);
                if (fp != -1)
                {
                    return fp; // otherwise, look for the name in this namepool
                }
            }

            return qNameToInteger.TryGetValue(new NameKey(uri, localName), out int found) ? found : -1;
        }

        // StructuredQName equality (identity of the interned URI, ordinal local part) without the allocation
        private readonly struct NameKey : IEquatable<NameKey>
        {
            private readonly NamespaceUri uri;
            private readonly string local;

            public NameKey(NamespaceUri uri, string local)
            {
                this.uri = uri;
                this.local = local;
            }

            public bool Equals(NameKey other) => uri == other.uri && string.Equals(local, other.local);
            public override bool Equals(object obj) => obj is NameKey other && Equals(other);
            public override int GetHashCode() => StructuredQName.ComputeHashCode(uri, local);
        }

        /// <summary>
        /// Unchecked Exception raised when some limit in the design of the name pool is exceeded
        /// </summary>
        internal class NamePoolLimitException : Exception
        {
            public NamePoolLimitException(string message) : base(message)
            {
            }
        }
    }
}