////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Values.Arrays;
using OutSmart.DAXon.Values.Maps;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Json
{
    internal sealed class JsonHandlerMap : JsonHandler
    {
        // ArrayItem | MapBuilder frames under construction; the finished value at the bottom
        Stack<object> stack;
        // An array of similar objects repeats each schema key once per object; interning collapses
        // those copies to one shared instance (large drop in retained heap + GC). Capped so an
        // all-distinct-keys document cannot grow the pool without bound.
        private const int KEY_INTERN_CAP = 1024;
        private Dictionary<string, string> keyPool;
        // Tabular JSON (an array of records) also repeats low-cardinality column VALUES -- currency
        // codes, category ids, empty strings -- once per row. Sharing the immutable StringValue for
        // those collapses the allocations; high-cardinality columns simply overflow the cap and fall
        // back to a fresh instance, so the pool stays bounded.
        private const int VALUE_INTERN_CAP = 1024;
        private Dictionary<string, StringValue> valuePool;
        // One-slot layout cache: consecutive records with the same key sequence share one
        // TabularShape, so the key-to-slot index is built once per layout, not once per map.
        private TabularShape lastShape;
        private readonly Stack<MapBuilder> builderPool = new Stack<MapBuilder>();
        // Linear key scans are bounded; wider objects switch to a per-builder index dictionary
        private const int LINEAR_LIMIT = 32;

        private sealed class MapBuilder
        {
            public readonly List<string> keys = new List<string>();
            public readonly List<IGroundedValue> values = new List<IGroundedValue>();
            public Dictionary<string, int> bigIndex;   // engaged once keys.Count exceeds LINEAR_LIMIT
            public string pendingKey;
            public int pendingDup = -1;                // >= 0: pendingKey duplicates keys[pendingDup]
            public bool allPooled = true;              // every key so far is a canonical pool instance

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
            if (keyPool == null)
            {
                keyPool = new Dictionary<string, string>();
            }

            string existing = keyPool.GetOrDefault(key);
            if (existing != null)
            {
                pooled = true;
                return existing;
            }

            if (keyPool.Count < KEY_INTERN_CAP)
            {
                keyPool[key] = key;
                pooled = true;
            }
            else
            {
                pooled = false;
            }

            return key;
        }

        private StringValue InternValue(string val)
        {
            if (valuePool == null)
            {
                valuePool = new Dictionary<string, StringValue>();
            }

            StringValue existing = valuePool.GetOrDefault(val);
            if (existing != null)
            {
                return existing;
            }

            StringValue sv = new StringValue(val);
            if (valuePool.Count < VALUE_INTERN_CAP)
            {
                valuePool[val] = sv;
            }

            return sv;
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
            stack.Push(new SimpleArrayItem(new List<IGroundedValue>()));
        }

        public override void EndArray()
        {
            ArrayItem array = (ArrayItem)stack.Pop();
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
            if (top is ArrayItem)
            {
                ((SimpleArrayItem)top).GetMembers().Add(val.Materialize());
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
}
