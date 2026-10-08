////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Collections.Concurrent;

namespace OutSmart.DAXon.Internal.Caching
{
    /// <summary>
    /// Bounded, thread-safe memoization cache with CLOCK (second-chance) eviction.
    /// Hits are lock-free dictionary reads; the only write a hit can make is the one-time
    /// 0-to-1 flip of the entry's reference bit, so steady-state lookups touch no shared
    /// mutable cache line. (Replaced a lock-per-hit LFU design whose lock serialized
    /// multi-threaded hosts — 29% of CPU in an 8-thread format-dateTime run, round AP.)
    /// Intended to be held in a STATIC field so the cache is per-process and survives
    /// re-instantiation of Processor/Configuration. Cache only pure functions whose key
    /// captures every result-affecting input (see REFACTORING-PLAN Appendix B,
    /// "Correctness rules").
    /// </summary>
    internal sealed class ClockCache<TKey, TValue>
    {
        private readonly ConcurrentDictionary<TKey, Entry> _map;
        private readonly object _writeLock = new object();
        private readonly int _capacity;

        // The clock is a REAL ring with a persistent hand (round BK). The first version swept
        // the ConcurrentDictionary from the start on every eviction: enumeration order is not
        // insertion order, so entries in late buckets were effectively immortal - a cache full
        // of evicted-in-name-only 100 KB compiled regexes measured ~200 MB resident after the
        // workload had long moved on. The hand makes eviction age-ordered (oldest unreferenced
        // under the hand goes first), and a newcomer sits just BEHIND the hand, so it gets one
        // full revolution of grace before it can be considered.
        private readonly Entry[] _ring;
        private int _hand;
        private int _filled;

        public int Count => _map.Count;

        public ClockCache(int capacity)
        {
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "capacity must be >= 1");
            }

            _capacity = capacity;
            _ring = new Entry[capacity];
            _map = new ConcurrentDictionary<TKey, Entry>(Environment.ProcessorCount, capacity);
        }

        /// <summary>
        /// Returns the cached value for <paramref name="key"/>, computing it via
        /// <paramref name="factory"/> on a miss. The factory runs outside the lock, so a
        /// concurrent miss on the same key may compute twice — harmless for a pure factory
        /// (last writer wins). A throwing factory caches nothing and re-throws.
        /// </summary>
        public TValue GetOrAdd(TKey key, Func<TKey, TValue> factory)
        {
            if (_map.TryGetValue(key, out Entry hit))
            {
                if (hit.Referenced == 0)
                {
                    hit.Referenced = 1;   // benign race; written at most once per clock sweep
                }

                return hit.Value;
            }

            TValue value = factory(key);
            lock (_writeLock)
            {
                Insert(key, value);
            }

            return value;
        }

        /// <summary>
        /// Stores <paramref name="value"/> under <paramref name="key"/>, evicting if full — the
        /// insert half of GetOrAdd, for callers whose value is computed from more inputs than the
        /// key (e.g. a rule search needing the node and dynamic context). Last writer wins.
        /// </summary>
        public void Set(TKey key, TValue value)
        {
            lock (_writeLock)
            {
                Insert(key, value);
            }
        }

        /// <summary>Looks up <paramref name="key"/>; a hit counts as a use (sets the reference bit).</summary>
        public bool TryGet(TKey key, out TValue value)
        {
            if (_map.TryGetValue(key, out Entry hit))
            {
                if (hit.Referenced == 0)
                {
                    hit.Referenced = 1;
                }

                value = hit.Value;
                return true;
            }

            value = default;
            return false;
        }

        /// <summary>Empties the cache. Test hook only — not part of the steady-state contract.</summary>
        public void Clear()
        {
            lock (_writeLock)
            {
                _map.Clear();
                Array.Clear(_ring, 0, _ring.Length);
                _hand = 0;
                _filled = 0;
            }
        }

        // Caller holds _writeLock. An existing key is replaced in place (same slot, count
        // unchanged). A new key takes a free slot while warming up; at capacity the hand
        // sweeps: a referenced entry gets its bit cleared and one more revolution, the first
        // unreferenced one under the hand is evicted and its slot reused. The sweep is bounded
        // to two revolutions: a hit sets the reference bit lock-free (GetOrAdd/TryGet), so a
        // reader can re-set a bit the instant the hand clears it, and "one revolution clears
        // every bit" does NOT hold under contention — without the cap the sweep could livelock.
        // After each entry has had its second chance we force-evict the one under the hand.
        private void Insert(TKey key, TValue value)
        {
            if (_map.TryGetValue(key, out Entry old))
            {
                Entry replacement = new Entry(key, value, old.Slot);
                _ring[old.Slot] = replacement;
                _map[key] = replacement;
                return;
            }

            int slot;
            if (_filled < _capacity)
            {
                slot = _filled++;
            }
            else
            {
                int steps = 0;
                int maxSteps = 2 * _capacity;
                while (true)
                {
                    Entry cand = _ring[_hand];
                    if (cand.Referenced != 0 && steps < maxSteps)
                    {
                        cand.Referenced = 0;
                        _hand = (_hand + 1) % _capacity;
                        steps++;
                        continue;
                    }

                    _map.TryRemove(cand.Key, out _);
                    slot = _hand;
                    _hand = (_hand + 1) % _capacity;
                    break;
                }
            }

            Entry entry = new Entry(key, value, slot);
            _ring[slot] = entry;
            _map[key] = entry;
        }

        // Value is readonly: initialized before the entry is published through the dictionary,
        // so lock-free readers can never observe a half-written value.
        private sealed class Entry
        {
            internal readonly TKey Key;
            internal readonly TValue Value;
            internal readonly int Slot;
            internal int Referenced;

            internal Entry(TKey key, TValue value, int slot)
            {
                Key = key;
                Value = value;
                Slot = slot;
            }
        }
    }
}
