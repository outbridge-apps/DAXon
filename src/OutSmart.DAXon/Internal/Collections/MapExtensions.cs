////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System.Collections.Generic;

namespace OutSmart.DAXon.Internal.Collections
{
    /// <summary>
    /// java.util.Map-surface extension methods over System.Collections.Generic.IDictionary.
    ///
    /// CollectionsModernizer rewrites OutSmart.DAXon.Internal.Collections.HashMap/LinkedHashMap type references in the
    /// transpiled tree to Dictionary&lt;K,V&gt;; the Java map members keep working through
    /// extensions. Most of the surface predates this file in OutSmart.DAXon.Internal.JavaApiExtensions
    /// (do NOT redeclare there-existing signatures - that creates CS0121 at every call site):
    ///   Get(k)            -> default(TValue) on miss, the java.util.Map.get(Object) contract
    ///                        (Java's get NEVER throws on a missing key - Dictionary's
    ///                        indexer getter does, which is why CollectionsModernizer also
    ///                        converts every indexer READ on a map receiver to .Get(k)),
    ///   Put(k,v)          -> returns the PREVIOUS value or default (java.util.Map.put),
    ///   GetOrDefault, PutIfAbsent, ComputeIfAbsent, ComputeIfPresent, Merge,
    ///   KeySet, Values, EntrySet, ContainsKey, Size, IsEmpty.
    ///
    /// This class adds only what was still missing from that surface.
    /// </summary>
    internal static class MapExtensions
    {
        /// <summary>
        /// java.util.Map.remove(Object) - removes the mapping and RETURNS THE OLD VALUE
        /// (or null/default when the key was absent). Named RemoveAndGet because
        /// Dictionary&lt;K,V&gt;.Remove(K) is a bool-returning INSTANCE method (instance
        /// always beats extension). CollectionsModernizer rewrites result-consuming
        /// Remove call sites on map receivers to this name; statement-position calls keep
        /// the BCL Remove (identical removal effect).
        /// </summary>
        public static TValue RemoveAndGet<TKey, TValue>(this IDictionary<TKey, TValue> d, TKey key)
        {
            d.TryGetValue(key, out var old);
            d.Remove(key);
            return old;
        }

        /// <summary>
        /// java.util.Map.putAll(Map) - copies every mapping from the source, overwriting
        /// existing keys (Java's last-write-wins semantics; iteration of the source is
        /// unaffected because the copy targets only the destination).
        /// </summary>
        public static void PutAll<TKey, TValue>(this IDictionary<TKey, TValue> d, IDictionary<TKey, TValue> src)
        {
            if (src == null)
                return;
            foreach (var kv in src)
                d[kv.Key] = kv.Value;
        }

        // java.util.Map.get: null/default when absent (the BCL indexer throws). Mirrors the
        // netcore GetValueOrDefault, which net472 lacks.
        public static TValue GetOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> d, TKey key)
        {
            d.TryGetValue(key, out var v);
            return v;
        }

        public static TValue GetOrDefault<TKey, TValue>(this IDictionary<TKey, TValue> d, TKey key, TValue defaultValue)
            => d.TryGetValue(key, out var v) ? v : defaultValue;

        // java.util.Map.put returns the PREVIOUS value; statement-position call sites use the
        // plain indexer instead — this is only for sites that consume the result.
        public static TValue PutAndGetPrevious<TKey, TValue>(this IDictionary<TKey, TValue> d, TKey key, TValue value)
        {
            d.TryGetValue(key, out var prev);
            d[key] = value;
            return prev;
        }

        public static TValue ComputeIfAbsent<TKey, TValue>(this IDictionary<TKey, TValue> d, TKey key, System.Func<TKey, TValue> factory)
        {
            if (!d.TryGetValue(key, out var v))
            {
                v = factory(key);
                d[key] = v;
            }
            return v;
        }

        public static TValue PutIfAbsent<TKey, TValue>(this IDictionary<TKey, TValue> d, TKey key, TValue value)
        {
            if (d.TryGetValue(key, out var existing))
                return existing;
            d[key] = value;
            return default;
        }
    }
}
