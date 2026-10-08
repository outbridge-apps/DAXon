////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Model
{
    public class NamespaceMap : INamespaceBindingSet, INamespaceResolver
    {
        private static readonly string[] emptyArray = new string[]
        {
        };
        private static readonly NamespaceUri[] emptyUriArray = new NamespaceUri[]
        {
        };
        private static readonly NamespaceMap EMPTY_MAP = new NamespaceMap();
        protected string[] prefixes; // always sorted (ordinal, as Java String.compareTo), for binary search
        protected NamespaceUri[] uris;
        private volatile PutAllMemo putAllMemo1; // LRU-2 cache of PutAll results (construction re-merges
        private volatile PutAllMemo putAllMemo2; // the same constant delta per element)

        private sealed class PutAllMemo
        {
            internal readonly NamespaceMap arg;
            internal readonly NamespaceMap result;
            internal PutAllMemo(NamespaceMap arg, NamespaceMap result)
            {
                this.arg = arg;
                this.result = result;
            }
        }

        // Ordinal binary search: the default string comparer is culture-sensitive (native NLS call
        // per probe, and unlike Java's compareTo it treats e.g. "ab" == "a­b")
        private static int Search(string[] sorted, string key)
        {
            int lo = 0, hi = sorted.Length - 1;
            while (lo <= hi)
            {
                int mid = (lo + hi) >> 1;
                int c = string.CompareOrdinal(sorted[mid], key);
                if (c < 0)
                {
                    lo = mid + 1;
                }
                else if (c > 0)
                {
                    hi = mid - 1;
                }
                else
                {
                    return mid;
                }
            }

            return ~lo;
        }

        public virtual NamespaceUri DefaultNamespace
        {
            get
            {

                // If the prefix "" is present, it will be the first in alphabetical order
                if (prefixes.Length > 0 && (prefixes[0].Length == 0))
                {
                    return uris[0];
                }
                else
                {
                    return NamespaceUri.NULL;
                }
            }
        }

        public virtual NamespaceBinding[] NamespaceBindings
        {
            get
            {
                NamespaceBinding[] result = new NamespaceBinding[prefixes.Length];
                for (int i = 0; i < prefixes.Length; i++)
                {
                    result[i] = new NamespaceBinding(prefixes[i], uris[i]);
                }

                return result;
            }
        }

        public virtual String[] PrefixArray => prefixes;

        public virtual NamespaceUri[] URIsAsArray => uris;

        protected NamespaceMap()
        {
            prefixes = emptyArray;
            uris = emptyUriArray;
        }

        public NamespaceMap(IList<NamespaceBinding> bindings)
        {
            NamespaceBinding[] bindingArray = bindings.ToArray();
            SortByPrefix(bindingArray);
            bool bindsXmlNamespace = false;
            prefixes = new string[bindingArray.Length];
            uris = new NamespaceUri[bindingArray.Length];
            for (int i = 0; i < bindingArray.Length; i++)
            {
                prefixes[i] = bindingArray[i].GetPrefix();
                uris[i] = bindingArray[i].GetNamespaceUri();
                if (prefixes[i].Equals("xml"))
                {
                    bindsXmlNamespace = true;
                    if (!uris[i].Equals(NamespaceUri.XML))
                    {
                        throw new ArgumentException("Binds xml prefix to the wrong namespace");
                    }
                }
                else if (uris[i].Equals(NamespaceUri.XML))
                {
                    throw new ArgumentException("Binds xml namespace to the wrong prefix");
                }
            }

            if (bindsXmlNamespace)
            {
                Remove("xml");
            }
        }
        public static NamespaceMap EmptyMap()
        {
            return EMPTY_MAP;
        }

        public static NamespaceMap Of(string prefix, NamespaceUri uri)
        {
            NamespaceMap map = new NamespaceMap();
            if (map.IsPointlessMapping(prefix, uri))
            {
                return EMPTY_MAP;
            }

            map.prefixes = new string[]
            {
                prefix
            };
            map.uris = new NamespaceUri[]
            {
                uri
            };
            return map;
        }

        protected virtual NamespaceMap MakeNamespaceMap()
        {
            return new NamespaceMap();
        }

        private void SortByPrefix(NamespaceBinding[] bindingArray)
        {
            Array.Sort(bindingArray, Comparer<NamespaceBinding>.Create((a, b) => string.CompareOrdinal(a.GetPrefix(), b.GetPrefix())));
        }

        public static NamespaceMap FromNamespaceResolver(INamespaceResolver resolver)
        {
            if (resolver is NamespaceMap)
            {
                return (NamespaceMap)resolver;
            }

            IEnumerator<string> iter = resolver.IteratePrefixes();
            IList<NamespaceBinding> bindings = new List<NamespaceBinding>();
            while (iter.MoveNext())
            {
                string prefix = iter.Current;
                NamespaceUri uri = resolver.GetURIForPrefix(prefix, true);
                bindings.Add(new NamespaceBinding(prefix, uri));
            }

            return new NamespaceMap(bindings);
        }

        public virtual bool AllowsNamespaceUndeclarations()
        {
            return false;
        }

        public virtual int Size()
        {
            return prefixes.Length;
        }

        public virtual bool IsEmpty()
        {
            return prefixes.Length == 0;
        }

        public virtual NamespaceUri GetNamespaceUri(string prefix)
        {
            if (prefix.Equals("xml"))
            {
                return NamespaceUri.XML;
            }

            int position = Search(prefixes, prefix);
            return position >= 0 ? uris[position] : null;
        }

        public virtual NamespaceMap Put(string prefix, NamespaceUri uri)
        {
            if (uri == null)
            {
                uri = NamespaceUri.NULL;
            }

            if (IsPointlessMapping(prefix, uri))
            {
                return this;
            }

            int position = Search(prefixes, prefix);
            if (position >= 0)
            {

                // An entry for this prefix already exists
                if (uris[position].Equals(uri))
                {

                    // No change
                    return this;
                }
                else if (uri == NamespaceUri.NULL)
                {

                    // Delete the entry for the prefix
                    NamespaceMap n2 = MakeNamespaceMap();
                    if (prefixes.Length > 1)
                    {
                        n2.prefixes = new string[prefixes.Length - 1];
                        Array.Copy(prefixes, 0, n2.prefixes, 0, position);
                        Array.Copy(prefixes, position + 1, n2.prefixes, position, prefixes.Length - position - 1);
                        n2.uris = new NamespaceUri[uris.Length - 1];
                        Array.Copy(uris, 0, n2.uris, 0, position);
                        Array.Copy(uris, position + 1, n2.uris, position, uris.Length - position - 1);
                    }

                    return n2;
                }
                else
                {

                    // Replace the entry for the prefix
                    NamespaceMap n2 = MakeNamespaceMap();
                    n2.prefixes = ArrayTools.CopyOf(prefixes, prefixes.Length);
                    n2.uris = ArrayTools.CopyOf(uris, uris.Length);
                    n2.uris[position] = uri;
                    return n2;
                }
            }
            else
            {
                return PutNoExistingEntry(position, prefix, uri);
            }
        }

        private NamespaceMap PutNoExistingEntry(int position, string prefix, NamespaceUri uri)
        {
            if (prefixes.Length == 0)
            {
                NamespaceMap n2 = MakeNamespaceMap();
                n2.prefixes = new string[]
                {
                    prefix
                };
                n2.uris = new NamespaceUri[]
                {
                    uri
                };
                return n2;
            }
            else
            {

                // No existing entry for the prefix exists
                int insertionPoint = -position - 1;
                string[] p2 = new string[prefixes.Length + 1];
                NamespaceUri[] u2 = new NamespaceUri[uris.Length + 1];
                Array.Copy(prefixes, 0, p2, 0, insertionPoint);
                Array.Copy(uris, 0, u2, 0, insertionPoint);
                p2[insertionPoint] = prefix;
                u2[insertionPoint] = uri;
                Array.Copy(prefixes, insertionPoint, p2, insertionPoint + 1, prefixes.Length - insertionPoint);
                Array.Copy(uris, insertionPoint, u2, insertionPoint + 1, prefixes.Length - insertionPoint);
                NamespaceMap n2 = MakeNamespaceMap();
                n2.prefixes = p2;
                n2.uris = u2;
                return n2;
            }
        }

        private bool IsPointlessMapping(string prefix, NamespaceUri uri)
        {
            if (prefix.Equals("xml"))
            {
                if (!uri.Equals(NamespaceUri.XML))
                {
                    throw new ArgumentException("Invalid URI for xml prefix");
                }

                return true;
            }
            else if (uri.Equals(NamespaceUri.XML))
            {
                throw new ArgumentException("Invalid prefix for XML namespace");
            }

            return false;
        }

        public virtual NamespaceMap Bind(string prefix, NamespaceUri uri)
        {
            if (uri == NamespaceUri.NULL)
            {
                return Remove(prefix);
            }
            else
            {
                return Put(prefix, uri);
            }
        }

        public virtual NamespaceMap Remove(string prefix)
        {
            int position = Search(prefixes, prefix);
            if (position >= 0)
            {
                string[] p2 = new string[prefixes.Length - 1];
                NamespaceUri[] u2 = new NamespaceUri[uris.Length - 1];
                Array.Copy(prefixes, 0, p2, 0, position);
                Array.Copy(uris, 0, u2, 0, position);
                Array.Copy(prefixes, position + 1, p2, position, prefixes.Length - position - 1);
                Array.Copy(uris, position + 1, u2, position, uris.Length - position - 1);
                NamespaceMap n2 = MakeNamespaceMap();
                n2.prefixes = p2;
                n2.uris = u2;
                return n2;
            }
            else
            {
                return this;
            }
        }

        public virtual NamespaceMap PutAll(NamespaceMap delta)
        {
            if (this == delta)
            {
                return this;
            }
            else if (IsEmpty())
            {
                return delta;
            }
            else if (delta.IsEmpty())
            {
                return this;
            }

            // LRU-2 memo: during construction the same one or two constant deltas (the
            // instructions' namespace maps) are merged into this map once per element.
            // Maps are immutable, so (delta ref) fully keys the result; benign race.
            PutAllMemo memo = putAllMemo1;
            if (memo != null && memo.arg == delta)
            {
                return memo.result;
            }

            memo = putAllMemo2;
            if (memo != null && memo.arg == delta)
            {
                return memo.result;
            }

            // Common construction case: a child element re-declares bindings its parent already
            // has (same prefix->uri). Returning this unchanged avoids the merge allocation and
            // preserves instance identity for downstream namespace-map sharing.
            NamespaceMap result = ContainsAll(delta) ? this : MergePutAll(delta);
            putAllMemo2 = putAllMemo1;
            putAllMemo1 = new PutAllMemo(delta, result);
            return result;
        }

        private bool ContainsAll(NamespaceMap delta)
        {
            string[] dp = delta.prefixes;
            NamespaceUri[] du = delta.uris;
            for (int i = 0; i < dp.Length; i++)
            {
                NamespaceUri mine = GetNamespaceUri(dp[i]);
                if (mine == null || !mine.Equals(du[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private NamespaceMap MergePutAll(NamespaceMap delta)
        {

            // Merge of two sorted arrays to produce a sorted array
            string[] p1 = prefixes;
            NamespaceUri[] u1 = uris;
            string[] p2 = delta.prefixes;
            NamespaceUri[] u2 = delta.uris;
            int lengthSum = p1.Length + p2.Length;
            string[] p3 = new string[lengthSum];
            NamespaceUri[] u3 = new NamespaceUri[lengthSum];
            int i1 = 0;
            int i2 = 0;
            int writePos = 0;
            while (true)
            {
                int c = string.CompareOrdinal(p1[i1], p2[i2]);
                if (c < 0)
                {
                    p3[writePos] = p1[i1];
                    u3[writePos++] = u1[i1];
                    if (++i1 >= p1.Length)
                    {
                        break;
                    }
                }
                else if (c > 0)
                {
                    p3[writePos] = p2[i2];
                    u3[writePos++] = u2[i2];
                    if (++i2 >= p2.Length)
                    {
                        break;
                    }
                }
                else
                {

                    // c == 0
                    p3[writePos] = p2[i2];
                    u3[writePos++] = u2[i2];
                    i1++;
                    i2++;
                    if (i1 >= p1.Length || i2 >= p2.Length)
                    {
                        break;
                    }
                }
            }

            while (i1 < p1.Length)
            {
                p3[writePos] = p1[i1];
                u3[writePos++] = u1[i1];
                i1++;
            }

            while (i2 < p2.Length)
            {
                p3[writePos] = p2[i2];
                u3[writePos++] = u2[i2];
                i2++;
            }

            return CreateNewNamespaceMap(p3, u3, lengthSum, writePos);
        }

        // c == 0
        private NamespaceMap CreateNewNamespaceMap(string[] p3, NamespaceUri[] u3, int lengthSum, int writePos)
        {
            NamespaceMap n2 = new NamespaceMap();
            n2.prefixes = writePos == lengthSum ? p3 : ArrayTools.CopyOf(p3, writePos);
            n2.uris = writePos == lengthSum ? u3 : ArrayTools.CopyOf(u3, writePos);
            return n2;
        }

        public virtual NamespaceMap AddAll(INamespaceBindingSet namespaces)
        {
            if (namespaces is NamespaceMap)
            {
                return PutAll((NamespaceMap)namespaces);
            }
            else
            {
                NamespaceMap map = this;
                foreach (NamespaceBinding nb in namespaces)
                {
                    map = map.Put(nb.GetPrefix(), nb.GetNamespaceUri());
                }

                return map;
            }
        }

        public virtual NamespaceMap ApplyDifferences(NamespaceDeltaMap delta)
        {
            if (delta.IsEmpty())
            {
                return this;
            }
            else
            {

                // If every entry in delta is already in this map, return this map unchanged
                bool foundDifferences = false;
                foreach (string prefix in delta.prefixes)
                {
                    NamespaceUri newUri = delta.GetNamespaceUri(prefix);
                    NamespaceUri existingUri = GetNamespaceUri(prefix);
                    if (newUri != existingUri)
                    {
                        foundDifferences = true;
                        break;
                    }
                }

                if (!foundDifferences)
                {
                    return this;
                }


                // Merge of two sorted arrays to produce a sorted array
                string[] p1 = prefixes;
                NamespaceUri[] u1 = uris;
                string[] p2 = delta.prefixes;
                NamespaceUri[] u2 = delta.uris;
                IList<string> prefixList = new List<string>(p1.Length + p2.Length);
                IList<NamespaceUri> uriList = new List<NamespaceUri>(p1.Length + p2.Length);
                int i1 = 0;
                int i2 = 0;
                while (i1 < p1.Length && i2 < p2.Length)
                {
                    int c = string.CompareOrdinal(p1[i1], p2[i2]);
                    if (c < 0)
                    {
                        prefixList.Add(p1[i1]);
                        uriList.Add(u1[i1]);
                        i1++;
                    }
                    else if (c > 0)
                    {
                        if (u2[i2] != NamespaceUri.NULL)
                        {
                            prefixList.Add(p2[i2]);
                            uriList.Add(u2[i2]);
                        }

                        i2++;
                    } // c == 0
                    else
                    {

                        // c == 0
                        // Same prefix. Retain the second URI, unless the new URI is empty,
                        // in which case drop both the old and the new bindings. Bug 6866
                        if (u2[i2] != NamespaceUri.NULL)
                        {
                            prefixList.Add(p2[i2]);
                            uriList.Add(u2[i2]);
                        }

                        i1++;
                        i2++;
                    }
                }

                while (i1 < p1.Length)
                {
                    prefixList.Add(p1[i1]);
                    uriList.Add(u1[i1]);
                    i1++;
                }

                while (i2 < p2.Length)
                {
                    if (u2[i2] != NamespaceUri.NULL)
                    {
                        prefixList.Add(p2[i2]);
                        uriList.Add(u2[i2]);
                    }

                    i2++;
                }

                NamespaceMap n2 = new NamespaceMap();
                n2.prefixes = prefixList.ToArray();
                n2.uris = uriList.ToArray();
                return n2;
            }
        }

        // c == 0
        // c == 0
        public virtual IEnumerator<NamespaceBinding> IIterator()
        {
            return new AnonymousIEnumerator(this);
        }

        public virtual NamespaceBinding[] GetDifferences(NamespaceMap other, bool addUndeclarations)
        {
            IList<NamespaceBinding> result = new List<NamespaceBinding>();
            int i = 0, j = 0;
            while (true)
            {

                // Merge and combine the two sorted lists of prefix/uri pairs
                if (i < prefixes.Length && j < other.prefixes.Length)
                {
                    int c = string.CompareOrdinal(prefixes[i], other.prefixes[j]);
                    if (c < 0)
                    {

                        // prefix in this namespace map, absent from other
                        result.Add(new NamespaceBinding(prefixes[i], uris[i]));
                        i++;
                    }
                    else if (c == 0)
                    {

                        // prefix present in both maps
                        if (uris[i].Equals(other.uris[j]))
                        {
                        }
                        else
                        {

                            // URI is different; use the URI appearing in this map in preference
                            result.Add(new NamespaceBinding(prefixes[i], uris[i]));
                        }

                        i++;
                        j++;
                    }
                    else
                    {

                        // prefix present in other map, absent from this: maybe add an undeclaration
                        if (addUndeclarations || (other.prefixes[j].Length == 0))
                        {
                            result.Add(new NamespaceBinding(other.prefixes[j], NamespaceUri.NULL));
                        }

                        j++;
                    }
                }
                else if (i < prefixes.Length)
                {

                    // prefix in this namespace map, absent from other
                    result.Add(new NamespaceBinding(prefixes[i], uris[i]));
                    i++;
                }
                else if (j < other.prefixes.Length)
                {

                    // prefix present in other map, absent from this: add an undeclaration
                    if (addUndeclarations)
                    {
                        result.Add(new NamespaceBinding(other.prefixes[j], NamespaceUri.NULL));
                    }

                    j++;
                }
                else
                {
                    return result.ToArray();
                }
            }
        }

        // c == 0
        // c == 0
        // URI is the same; this declaration is redundant, so omit it from the result
        public virtual NamespaceUri GetURIForPrefix(string prefix, bool useDefault)
        {
            if (prefix.Equals("xml"))
            {
                return NamespaceUri.XML;
            }

            if (prefix.Equals(""))
            {
                if (useDefault)
                {
                    return DefaultNamespace;
                }
                else
                {
                    return NamespaceUri.NULL;
                }
            }

            return GetNamespaceUri(prefix);
        }

        public virtual IEnumerator<string> IteratePrefixes()
        {
            IList<string> prefixList = new List<string>(prefixes.ToList());
            prefixList.Add("xml");
            return prefixList.GetEnumerator();
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            foreach (NamespaceBinding nb in this)
            {
                sb.Append(nb.GetPrefix()).Append('=').Append(nb.GetNamespaceUri()).Append(' ');
            }

            return sb.ToString();
        }

        public override int GetHashCode()
        {
            return ArrayTools.GetHashCode(prefixes) ^ ArrayTools.GetHashCode(uris);
        }

        public override bool Equals(object obj)
        {
            return this == obj || (obj is NamespaceMap && ArrayTools.Equals(prefixes, ((NamespaceMap)obj).prefixes) && ArrayTools.Equals(uris, ((NamespaceMap)obj).uris));
        }
        public IEnumerator<NamespaceBinding> GetEnumerator() { for (int __i = 0; __i < prefixes.Length; __i++) { yield return new NamespaceBinding(prefixes[__i], uris[__i]); } }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

        private sealed class AnonymousIEnumerator : IEnumerator<NamespaceBinding>
        {

            private readonly NamespaceMap parent;
            int i;



            // .NET IEnumerator shim methods (Phase 3.6.5)

            private object _current;

            public NamespaceBinding Current => (NamespaceBinding)_current; object System.Collections.IEnumerator.Current => _current;
            public AnonymousIEnumerator(NamespaceMap parent)
            {
                this.parent = parent;
            }
            public bool HasNext()
            {
                return i < parent.prefixes.Length;
            }

            public NamespaceBinding Next()

            {

                NamespaceBinding nb = new NamespaceBinding(parent.prefixes[i], parent.uris[i]);

                i++;

                return nb;

            }
            public void Dispose() { }

            public bool MoveNext()

            {

                if (HasNext())
                {
                    _current = Next();
                    return true;
                }

                return false;

            }

            public void Reset() { i = 0; _current = null; }

        }
    }
}