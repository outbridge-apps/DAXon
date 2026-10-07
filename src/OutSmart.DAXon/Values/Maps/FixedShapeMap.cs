////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Values.Maps
{
    /// <summary>
    /// A flat, read-optimized map produced by `map { "k1": v1, ... }` with distinct xs:string-literal
    /// keys (see FixedKeyMapConstructor). Every instance of one such constructor shares an immutable
    /// <see cref="Shape"/> (the interned key layout + precomputed HAMT iteration order); the instance
    /// itself holds only a value array in source order. That is ~2 retained objects per map instead of
    /// the ~10 a HashTrieMap retains (map + trie node + subnode array + per-entry KeyValuePair/EntryNode),
    /// which is the dominant retained footprint when millions of tiny maps live in an array{} awaiting
    /// serialization (see docs/PERF-TRACK3-PLAN.md §V3). Any mutation (AddEntry/Remove) drops back to a
    /// HashTrieMap, so this type only needs to be correct for read + serialize; keys are always xs:string.
    /// Iteration order is byte-identical to the equivalent HashTrieMap because Shape derives it from one.
    /// </summary>
    internal sealed class FixedShapeMap : MapItem
    {
        private readonly Shape shape;
        private readonly IGroundedValue[] values;   // source order, index-aligned with shape.keys

        public FixedShapeMap(Shape shape, IGroundedValue[] values)
        {
            this.shape = shape;
            this.values = values;
            MemoryLedger.SampleSmall(this, MemoryLedger.RecordBytes(values));   // its keys are its shape's
        }

        // All keys are xs:string literals, so the key UType is constant.
        public override UType KeyUType => UType.STRING;

        public override IGroundedValue Get(AtomicValue key)
        {
            IAtomicMatchKey k = key.AsMapKey();
            IAtomicMatchKey[] mk = shape.matchKeys;
            for (int i = 0; i < mk.Length; i++)
            {
                if (mk[i].Equals(k))
                {
                    return values[i];
                }
            }

            return null;
        }

        public override int Size()
        {
            return shape.keys.Length;
        }

        public override bool IsEmpty()
        {
            return false;   // FixedKeyMapConstructor only emits this for >= 2 keys
        }

        public override IAtomicIterator Keys()
        {
            return new KeyIterator(this);
        }

        public override IEnumerable<KeyValuePair> KeyValuePairs()
        {
            int[] order = shape.iterOrder;
            List<KeyValuePair> list = new List<KeyValuePair>(order.Length);
            for (int p = 0; p < order.Length; p++)
            {
                int i = order[p];
                list.Add(new KeyValuePair(shape.keys[i], values[i]));
            }

            return list;
        }

        public override MapItem AddEntry(AtomicValue key, IGroundedValue value)
        {
            return ToHashTrieMap().AddEntry(key, value);
        }

        public override MapItem Remove(AtomicValue key)
        {
            if (Get(key) == null)
            {
                return this;
            }

            return ToHashTrieMap().Remove(key);
        }

        public override bool Conforms(IPlainType requiredKeyType, SequenceType requiredValueType, TypeHierarchy th)
        {
            StringValue[] keys = shape.keys;
            for (int i = 0; i < keys.Length; i++)
            {
                if (!requiredKeyType.Matches(keys[i], th))
                {
                    return false;
                }

                if (!requiredValueType.Matches(values[i], th))
                {
                    return false;
                }
            }

            return true;
        }

        public override ItemType GetItemType(TypeHierarchy th)
        {
            UType valueType = UType.VOID;
            int valueCard = 0;
            for (int i = 0; i < values.Length; i++)
            {
                IGroundedValue val = values[i];
                valueType = valueType.Union(SequenceTool.GetUType(val));
                valueCard = Cardinality.Union(valueCard, SequenceTool.GetCardinality(val));
            }

            return new MapType(BuiltInAtomicType.STRING, SequenceType.MakeSequenceType(valueType.ToItemType(), valueCard));
        }

        private HashTrieMap ToHashTrieMap()
        {
            HashTrieMap target = new HashTrieMap();
            StringValue[] keys = shape.keys;
            for (int i = 0; i < keys.Length; i++)
            {
                target.InitialPut(keys[i], values[i]);
            }

            return target;
        }

        /// <summary>
        /// Immutable, thread-safe layout shared by every instance built from one FixedKeyMapConstructor:
        /// the interned key values, their precomputed match keys, and the HAMT iteration order.
        /// </summary>
        internal sealed class Shape
        {
            internal readonly StringValue[] keys;             // source order
            internal readonly IAtomicMatchKey[] matchKeys;    // keys[i].AsMapKey(), source order
            internal readonly int[] iterOrder;                // iterOrder[p] = source index of p-th HAMT entry

            public Shape(StringValue[] keys)
            {
                this.keys = keys;
                int n = keys.Length;
                matchKeys = new IAtomicMatchKey[n];
                for (int i = 0; i < n; i++)
                {
                    matchKeys[i] = keys[i].AsMapKey();
                }

                // HAMT iteration order is a pure function of the keys' hashes (insertion-order
                // independent), so build one throwaway HashTrieMap with these exact key objects and
                // read back the order — the stored KeyValuePair.key IS the passed object, matched by
                // reference. This guarantees serialize() stays byte-identical to the HashTrieMap path.
                HashTrieMap probe = new HashTrieMap();
                for (int i = 0; i < n; i++)
                {
                    probe.InitialPut(keys[i], keys[i]);
                }

                int[] order = new int[n];
                int p = 0;
                foreach (KeyValuePair kv in probe.KeyValuePairs())
                {
                    for (int j = 0; j < n; j++)
                    {
                        if (ReferenceEquals(kv.key, keys[j]))
                        {
                            order[p++] = j;
                            break;
                        }
                    }
                }

                iterOrder = order;
            }
        }

        private sealed class KeyIterator : IAtomicIterator
        {
            private readonly FixedShapeMap map;
            private int pos;

            public KeyIterator(FixedShapeMap map)
            {
                this.map = map;
            }

            public AtomicValue Next()
            {
                int[] order = map.shape.iterOrder;
                if (pos < order.Length)
                {
                    return map.shape.keys[order[pos++]];
                }

                return null;
            }

            IItem ISequenceIterator.Next() => Next();
            public void Dispose() { }
        }
    }
}
