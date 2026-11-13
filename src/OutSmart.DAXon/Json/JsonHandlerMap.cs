////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;

using OutSmart.DAXon.Values.Arrays;
using OutSmart.DAXon.Values.Maps;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
namespace OutSmart.DAXon.Json
{
    internal sealed class JsonHandlerMap : JsonHandler
    {
        // ArrayItem | MapBuilder frames under construction; the finished value at the bottom
        readonly Stack<object> stack;
        // An array of similar objects repeats each schema key once per object; interning collapses
        // those copies to one shared instance (large drop in retained heap + GC). Capped so an
        // all-distinct-keys document cannot grow the pool without bound.
        private const int KEY_INTERN_CAP = 1024;
        private RangePool<string> keyPool;
        // The canonical key Key() last returned: SetKey takes it as pooled without a second lookup.
        private string rangeKey;
        // Tabular JSON (an array of records) also repeats low-cardinality column VALUES -- currency
        // codes, category ids, empty strings -- once per row. Sharing the immutable StringValue for
        // those collapses the allocations; high-cardinality columns simply overflow the cap and fall
        // back to a fresh instance, so the pool stays bounded.
        private const int VALUE_INTERN_CAP = 1024;
        private const int VALUE_INTERN_LENGTH = 64;
        private RangePool<StringValue> valuePool;
        // One-slot layout cache: consecutive records with the same key sequence share one
        // TabularShape, so the key-to-slot index is built once per layout, not once per map.
        private TabularShape lastShape;
        private readonly Stack<MapBuilder> builderPool = new Stack<MapBuilder>();
        private readonly Stack<ArrayBuilder> arrayPool = new Stack<ArrayBuilder>();
        // Linear key scans are bounded; wider objects switch to a per-builder index dictionary
        private const int LINEAR_LIMIT = 32;

        // An array's members as they are read. The head buffer is reused across arrays and never grows past a chunk; a
        // longer array moves on in fixed chunks, and the array keeps one exact copy: no doubling garbage, no spare capacity.
        private sealed class ArrayBuilder
        {
            private const int Chunk = 8192;   // 64 KB of references: below the large-object heap
            private readonly List<IGroundedValue> head = new List<IGroundedValue>();
            private List<IGroundedValue[]> chunks;
            private bool held;

            internal void Add(IGroundedValue member)
            {
                if (head.Count == Chunk)
                {
                    chunks = chunks ?? new List<IGroundedValue[]>();
                    chunks.Add(head.ToArray());
                    head.Clear();
                    if (!held)
                    {
                        held = true;
                        OutSmart.DAXon.Core.MemoryLedger.Hold(this, Sizer);   // summed as it grows
                    }
                }

                head.Add(member);
            }

            // The array, the builder emptied for the next one.
            internal ArrayItem Finish()
            {
                if (chunks == null)
                {
                    ArrayItem small = head.Count == 0 ? SimpleArrayItem.EMPTY_ARRAY : new SimpleArrayItem(head.ToArray());
                    head.Clear();
                    return small;
                }

                var all = new IGroundedValue[chunks.Count * Chunk + head.Count];
                for (int c = 0; c < chunks.Count; c++)
                {
                    Array.Copy(chunks[c], 0, all, c * Chunk, Chunk);
                }

                head.CopyTo(all, chunks.Count * Chunk);
                head.Clear();
                chunks = null;
                return new SimpleArrayItem(all);
            }

            private static readonly Func<object, long> Sizer = o =>
            {
                var b = (ArrayBuilder)o;
                List<IGroundedValue[]> full = b.chunks;
                long n = (full == null ? 0 : (long)full.Count * Chunk) + b.head.Count;
                return full == null ? OutSmart.DAXon.Core.MemoryLedger.ListBytes(b.head)
                    : 24 + 8 * n + OutSmart.DAXon.Core.MemoryLedger.MembersBytes(full[0], Chunk) / Chunk * n;
            };
        }

        private sealed class MapBuilder
        {
            public readonly List<string> keys = new List<string>();
            public readonly List<IGroundedValue> values = new List<IGroundedValue>();
            public Dictionary<string, int> bigIndex;   // engaged once keys.Count exceeds LINEAR_LIMIT
            public string pendingKey;
            public int pendingDup = -1;                // >= 0: pendingKey duplicates keys[pendingDup]
            public bool allPooled = true;              // every key so far is a canonical pool instance
            public bool held;                          // in the memory ledger (kept across Reset: the sizer reads the lists)

            // A large object, as ArrayBuilder: its keys, values and index summed as they grow.
            public static readonly Func<object, long> Sizer = o =>
            {
                var m = (MapBuilder)o;
                return OutSmart.DAXon.Core.MemoryLedger.ListBytes(m.keys) + OutSmart.DAXon.Core.MemoryLedger.ListBytes(m.values)
                    + (m.bigIndex == null ? 0 : 40L * m.bigIndex.Count);
            };

            public void Reset()
            {
                keys.Clear();
                values.Clear();
                bigIndex = null;
                pendingKey = null;
                pendingDup = -1;
                allPooled = true;
            }
        }

        public JsonHandlerMap(IXPathContext context, int flags)
        {
            Context = context;
            stack = new Stack<object>();
            escape = (flags & JsonParser.ESCAPE) != 0;
            charChecker = context.GetConfiguration().ValidCharacterChecker;
        }

        private string InternKey(string key, out bool pooled)
        {
            if (ReferenceEquals(key, rangeKey))
            {
                pooled = true;
                return key;
            }

            return InternKey(key, 0, key.Length, key, out pooled);
        }

        // whole: the range as a string when the caller already has one, else null (cut on a miss)
        private string InternKey(string input, int start, int length, string whole, out bool pooled)
        {
            if (keyPool == null)
            {
                keyPool = new RangePool<string>(KEY_INTERN_CAP);
            }

            int hash = RangePool<string>.Hash(input, start, length);
            int slot = keyPool.Find(input, start, length, hash);
            if (slot >= 0)
            {
                pooled = true;
                return keyPool.Value(slot);
            }

            string key = whole ?? input.Substring(start, length);
            pooled = keyPool.Add(key, key, hash);
            return key;
        }

        private StringValue InternValue(string val)
        {
            return InternValue(val, 0, val.Length, val);
        }

        private StringValue InternValue(string input, int start, int length, string whole)
        {
            if (length > VALUE_INTERN_LENGTH)
            {
                // the repeated values are short; two hashes of a long one cost what its parse did
                return new StringValue(whole ?? input.Substring(start, length));
            }

            if (valuePool == null)
            {
                valuePool = new RangePool<StringValue>(VALUE_INTERN_CAP);
            }

            int hash = RangePool<StringValue>.Hash(input, start, length);
            int slot = valuePool.Find(input, start, length, hash);
            if (slot >= 0)
            {
                return valuePool.Value(slot);
            }

            string val = whole ?? input.Substring(start, length);
            StringValue sv = new StringValue(val);
            valuePool.Add(val, sv, hash);
            return sv;
        }

        // A range needs no unescaping (the tokenizer saw no backslash or control character); it needs no
        // re-escaping either when every character lies below the surrogates, as ReEscape's fast path asks.
        private bool IsClean(string input, int start, int length)
        {
            if (escape)
            {
                return false;
            }

            for (int i = start, end = start + length; i < end; i++)
            {
                if (input[i] >= 0xD800)
                {
                    return false;
                }
            }

            return true;
        }

        public override string Key(string input, int start, int length)
        {
            if (!IsClean(input, start, length))
            {
                return null;
            }

            string key = InternKey(input, start, length, null, out bool pooled);
            rangeKey = pooled ? key : null;
            return key;
        }

        public override bool WriteString(string input, int start, int length)
        {
            if (!IsClean(input, start, length))
            {
                return false;
            }

            WriteItem(InternValue(input, start, length, null));
            return true;
        }

        public override ISequence GetResult()
        {
            return (ISequence)stack.Peek();
        }

        public override bool SetKey(string unEscaped, string reEscaped)
        {
            reEscaped = InternKey(reEscaped, out bool pooled);
            MapBuilder b = (MapBuilder)stack.Peek();
            if (!pooled)
            {
                b.allPooled = false;
            }

            int dup = FindKey(b, reEscaped, pooled);
            b.pendingKey = reEscaped;
            b.pendingDup = dup;
            return dup >= 0;
        }

        private static int FindKey(MapBuilder b, string key, bool pooled)
        {
            if (b.bigIndex != null)
            {
                return b.bigIndex.TryGetValue(key, out int ix) ? ix : -1;
            }

            List<string> ks = b.keys;
            int n = ks.Count;
            if (pooled && b.allPooled)
            {
                // canonical instances on both sides: reference scan, no hashing
                for (int i = 0; i < n; i++)
                {
                    if (ReferenceEquals(ks[i], key))
                    {
                        return i;
                    }
                }

                return -1;
            }

            for (int i = 0; i < n; i++)
            {
                if (string.Equals(ks[i], key, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        public override void StartArray()
        {
            stack.Push(arrayPool.Count > 0 ? arrayPool.Pop() : new ArrayBuilder());
        }

        public override void EndArray()
        {
            var builder = (ArrayBuilder)stack.Pop();
            ArrayItem array = builder.Finish();
            arrayPool.Push(builder);
            if (stack.Count == 0)
            {
                stack.Push(array); // the end
            }
            else
            {
                WriteItem(array);
            }
        }

        public override void StartMap()
        {
            stack.Push(builderPool.Count > 0 ? builderPool.Pop() : new MapBuilder());
        }

        public override void EndMap()
        {
            MapBuilder b = (MapBuilder)stack.Pop();
            MapItem map = Materialize(b);
            b.Reset();
            builderPool.Push(b);
            if (stack.Count == 0)
            {
                stack.Push(map); // the end
            }
            else
            {
                WriteItem(map);
            }
        }

        private MapItem Materialize(MapBuilder b)
        {
            int n = b.keys.Count;
            TabularShape shape = lastShape;
            if (shape != null && shape.keys.Length == n)
            {
                for (int i = 0; i < n; i++)
                {
                    if (!ReferenceEquals(shape.keys[i], b.keys[i]))
                    {
                        shape = null;
                        break;
                    }
                }
            }
            else
            {
                shape = null;
            }

            if (shape == null)
            {
                shape = new TabularShape(b.keys.ToArray());
                lastShape = shape;
            }

            return new TabularMap(shape, b.values.ToArray());
        }

        private void WriteItem(IGroundedValue val)
        {
            if (stack.Count == 0)
            {
                stack.Push(val);
                return;
            }

            object top = stack.Peek();
            if (top is ArrayBuilder builder)
            {
                builder.Add(val.Materialize());
                return;
            }

            MapBuilder b = (MapBuilder)top;
            if (b.pendingDup >= 0)
            {
                // duplicate key under use-last/retain: overwrite in place, order preserved
                b.values[b.pendingDup] = val;
                return;
            }

            b.keys.Add(b.pendingKey);
            b.values.Add(val);
            if (!b.held && b.keys.Count == OutSmart.DAXon.Core.MemoryLedger.BigCount)
            {
                b.held = true;
                OutSmart.DAXon.Core.MemoryLedger.Hold(b, MapBuilder.Sizer);
            }

            if (b.bigIndex != null)
            {
                b.bigIndex[b.pendingKey] = b.keys.Count - 1;
            }
            else if (b.keys.Count > LINEAR_LIMIT)
            {
                var d = new Dictionary<string, int>(b.keys.Count * 2);
                for (int i = 0; i < b.keys.Count; i++)
                {
                    d[b.keys[i]] = i;
                }

                b.bigIndex = d;
            }
        }

        public override void WriteNumeric(string asString, AtomicValue parsedValue)
        {
            WriteItem(parsedValue);
        }

        public override bool NeedsNumberText => false;

        public override void WriteString(string val)
        {
            WriteItem(InternValue(ReEscape(val)));
        }

        public override void WriteBoolean(bool value)
        {
            WriteItem(BooleanValue.Get(value));
        }

        public override void WriteNull()
        {
            WriteItem(EmptySequence.GetInstance());
        }
    }

    // Open-addressing table of at most `cap` strings, each with a value, looked up by a range of another string so
    // a hit allocates nothing. Starts small for the many tiny documents; past the cap Add declines.
    internal sealed class RangePool<T> where T : class
    {
        private struct Entry
        {
            public string Key;
            public T Value;
            public int Hash;
        }

        private readonly int cap;
        private Entry[] entries = new Entry[8];
        private int count;

        public RangePool(int cap)
        {
            this.cap = cap;
        }

        public static int Hash(string s, int start, int length)
        {
            uint h = 2166136261;
            for (int i = start, end = start + length; i < end; i++)
            {
                h = (h ^ s[i]) * 16777619;
            }

            return (int)h;
        }

        // The slot holding s[start, start + length), or -1.
        public int Find(string s, int start, int length, int hash)
        {
            int mask = entries.Length - 1;
            for (int slot = hash & mask; ; slot = (slot + 1) & mask)
            {
                string key = entries[slot].Key;
                if (key == null)
                {
                    return -1;
                }

                if (entries[slot].Hash == hash && key.Length == length && string.CompareOrdinal(key, 0, s, start, length) == 0)
                {
                    return slot;
                }
            }
        }

        public T Value(int slot) => entries[slot].Value;

        // The key must not be in the table yet: callers add after a missed Find.
        public bool Add(string key, T value, int hash)
        {
            if (count >= cap)
            {
                return false;
            }

            if (2 * (count + 1) > entries.Length)
            {
                Entry[] old = entries;
                entries = new Entry[old.Length * 2];
                foreach (Entry e in old)
                {
                    if (e.Key != null)
                    {
                        Put(e.Key, e.Value, e.Hash);
                    }
                }
            }

            Put(key, value, hash);
            count++;
            return true;
        }

        private void Put(string key, T value, int hash)
        {
            int mask = entries.Length - 1;
            int slot = hash & mask;
            while (entries[slot].Key != null)
            {
                slot = (slot + 1) & mask;
            }

            entries[slot].Key = key;
            entries[slot].Value = value;
            entries[slot].Hash = hash;
        }
    }
}
