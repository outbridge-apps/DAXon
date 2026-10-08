////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2020 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Trees.Iterators;
using System.Collections.Generic;
using OutSmart.DAXon.Types;

namespace OutSmart.DAXon.Values.Maps
{
    /// <summary>
    /// The key layout of a TabularMap: the ordered key strings plus one key-to-slot index,
    /// shared by every map with the same layout (an array of similar JSON records repeats one
    /// layout per row, so the index is built once instead of once per map).
    /// </summary>
    internal sealed class TabularShape
    {
        public readonly string[] keys;
        public readonly Dictionary<string, int> index;

        public TabularShape(string[] keys)
        {
            this.keys = keys;
            index = new Dictionary<string, int>(keys.Length);
            for (int i = 0; i < keys.Length; i++)
            {
                index[keys[i]] = i;
            }
        }
    }

    /// <summary>
    /// A string-keyed immutable-on-construction map (same contract as DictionaryMap) whose keys
    /// live in a shared TabularShape and whose values are a plain slot array. Modification
    /// (AddEntry/Remove) converts to a HashTrieMap, exactly like DictionaryMap.
    /// </summary>
    internal sealed class TabularMap : MapItem
    {
        private readonly TabularShape shape;
        private readonly IGroundedValue[] values;

        public override UType KeyUType => values.Length == 0 ? UType.VOID : UType.STRING;

        public TabularMap(TabularShape shape, IGroundedValue[] values)
        {
            this.shape = shape;
            this.values = values;
            OutSmart.DAXon.Core.MemoryLedger.SampleSmall(this, OutSmart.DAXon.Core.MemoryLedger.RecordBytes(values));   // its keys are its siblings'
        }

        public override IGroundedValue Get(AtomicValue key)
        {
            if (key is StringValue && shape.index.TryGetValue(key.GetStringValue(), out int i))
            {
                return values[i];
            }

            return null;
        }

        public override int Size()
        {
            return values.Length;
        }

        public override bool IsEmpty()
        {
            return values.Length == 0;
        }

        public override IAtomicIterator Keys()
        {
            return new KeyIterator(shape.keys);
        }

        public override IEnumerable<KeyValuePair> KeyValuePairs()
        {
            IList<KeyValuePair> pairs = new List<KeyValuePair>(values.Length);
            for (int i = 0; i < values.Length; i++)
            {
                pairs.Add(new KeyValuePair(new StringValue(shape.keys[i]), values[i]));
            }

            return pairs;
        }

        public override MapItem AddEntry(AtomicValue key, IGroundedValue value)
        {
            return ToHashTrieMap().AddEntry(key, value);
        }

        public override MapItem Remove(AtomicValue key)
        {
            return ToHashTrieMap().Remove(key);
        }

        public override bool Conforms(IPlainType keyType, SequenceType valueType, TypeHierarchy th)
        {
            if (IsEmpty())
            {
                return true;
            }

            if (!(keyType == BuiltInAtomicType.STRING || keyType == BuiltInAtomicType.ANY_ATOMIC))
            {
                return false;
            }

            if (valueType.Equals(SequenceType.ANY_SEQUENCE))
            {
                return true;
            }

            foreach (IGroundedValue val in values)
            {
                if (!valueType.Matches(val, th))
                {
                    return false;
                }
            }

            return true;
        }

        public override ItemType GetItemType(TypeHierarchy th)
        {
            ItemType valueType = null;
            int valueCard = 0;
            foreach (IGroundedValue val in values)
            {
                if (valueType == null)
                {
                    valueType = SequenceTool.GetItemType(val, th);
                    valueCard = SequenceTool.GetCardinality(val);
                }
                else
                {
                    valueType = Types.Type.GetCommonSuperType(valueType, SequenceTool.GetItemType(val, th), th);
                    valueCard = Cardinality.Union(valueCard, SequenceTool.GetCardinality(val));
                }
            }

            if (valueType == null)
            {
                return MapType.EMPTY_MAP_TYPE;
            }

            return new MapType(BuiltInAtomicType.STRING, SequenceType.MakeSequenceType(valueType, valueCard));
        }

        private HashTrieMap ToHashTrieMap()
        {
            HashTrieMap target = new HashTrieMap();
            for (int i = 0; i < values.Length; i++)
            {
                target.InitialPut(new StringValue(shape.keys[i]), values[i]);
            }

            return target;
        }

        private sealed class KeyIterator : IAtomicIterator
        {
            private readonly string[] keys;
            private int position;

            public KeyIterator(string[] keys)
            {
                this.keys = keys;
            }

            public AtomicValue Next()
            {
                return position < keys.Length ? new StringValue(keys[position++]) : null;
            }

            IItem ISequenceIterator.Next() => Next(); // redirect StubGen hollow to the real covariant Next(); default = silent empty iteration
            public void Dispose() { }
        }
    }
}
