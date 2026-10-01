////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Expressions.Sorting
{
    internal sealed class LFUCache<K, V>
    {
        private int targetSize;
        private int retentionThreshold = 1;
        private Dictionary<K, LFUCacheEntryWithCounter<V>> map;
        // Indexer for `cache[key]` syntax (Java's cache.get(key))
        public V this[K key]
        {
            get
            {
                if (map == null)
                    return default(V);
                LFUCacheEntryWithCounter<V> entry;
                if (map.TryGetValue(key, out entry) && entry != null)
                {
                    return entry.value;
                }
                return default(V);
            }
            set
            {
                if (map == null)
                    return;
                map[key] = new LFUCacheEntryWithCounter<V>(value);
            }
        }

        // Single-threaded LFU memo cache. Upstream also had an LFUCache(int, bool concurrent) overload
        // that backed the map with a ConcurrentHashMap; that overload was dropped here because the
        // port silently backed BOTH branches with a plain Dictionary, so "concurrent:true" was never
        // actually thread-safe -- a trap. The two callers that needed concurrency (ConversionRules,
        // XPathCompiler) now use Internal.Caching.ClockCache; the sole remaining caller (EvaluateInstr)
        // holds its cache per-Controller and is single-threaded.
        public LFUCache(int cacheSize)
        {
            targetSize = cacheSize;
            map = new Dictionary<K, LFUCacheEntryWithCounter<V>>(cacheSize);
        }

        public void Put(K key, V value)
        {
            map[key] = new LFUCacheEntryWithCounter<V>(value);

            // Consider purging rarely-used entries
            if (map.Count > 3 * targetSize)
            {
                Rebuild();
            }
        }

        private void Rebuild()
        {
            Dictionary<K, LFUCacheEntryWithCounter<V>> m2 = new Dictionary<K, LFUCacheEntryWithCounter<V>>(targetSize);
            int retained = 0;
            foreach (KeyValuePair<K, LFUCacheEntryWithCounter<V>> entry in map)
            {
                if (entry.Value.counter > retentionThreshold)
                {
                    m2[entry.Key] = new LFUCacheEntryWithCounter<V>(entry.Value.value);
                    retained++;
                }
            }


            // Consider adjusting the threshold for next time
            if (retained > 1.5 * targetSize)
            {

                // We retained too many entries, try to do better next time
                retentionThreshold++;
            }
            else if (retentionThreshold > 0 && retained < targetSize)
            {

                // We discarded too many entries, try to do better next time
                retentionThreshold--;
            }


            // Replace the map. Note this update isn't thread-safe; it doesn't matter if we lose it, or if some
            // other thread is doing the same thing concurrently.
            map = m2;
        }
    }
}
