////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2020 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Core;
using System.Collections.Generic;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Values.Maps
{
    internal sealed class DictionaryMap : MapItem
    {
        private readonly Dictionary<string, IGroundedValue> hashMap;

        public override UType KeyUType => hashMap.Count == 0 ? UType.VOID : UType.STRING;
        public DictionaryMap()
        {
            hashMap = new Dictionary<string, IGroundedValue>();
            MemoryLedger.SampleSmall(this, 160);
        }

        public DictionaryMap(int size)
        {
            hashMap = new Dictionary<string, IGroundedValue>(size);
            MemoryLedger.SampleSmall(this, 160);
        }

        public void InitialPut(string key, IGroundedValue value)
        {
            hashMap[key] = value;
            MemoryLedger.SampleSmall(this, 64 + 2L * key.Length + MemoryLedger.MemberBytes(value));   // the entry, which the map holds
        }

        public override IGroundedValue Get(AtomicValue key)
        {
            if (key is StringValue)
            {
                return hashMap.GetOrDefault(key.GetStringValue());
            }
            else
            {
                return null;
            }
        }

        public override int Size()
        {
            return hashMap.Count;
        }

        public override bool IsEmpty()
        {
            return hashMap.Count == 0;
        }

        public override IAtomicIterator Keys()
        {
            return new KeyIterator(hashMap);
        }

        public override IEnumerable<KeyValuePair> KeyValuePairs()
        {
            IList<KeyValuePair> pairs = new List<KeyValuePair>();
            foreach (KeyValuePair<string, IGroundedValue> entry in hashMap)
            {
                pairs.Add(new KeyValuePair(new StringValue(entry.Key), entry.Value));
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

            foreach (IGroundedValue val in hashMap.Values)
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

            // we need to test the entries individually
            foreach (KeyValuePair<string, IGroundedValue> entry in hashMap)
            {
                IGroundedValue val = entry.Value;
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

                // empty map
                return MapType.EMPTY_MAP_TYPE;
            }
            else
            {
                return new MapType(BuiltInAtomicType.STRING, SequenceType.MakeSequenceType(valueType, valueCard));
            }
        }

        /// <summary>
        /// Convert to a HashTrieMap
        /// </summary>
        private HashTrieMap ToHashTrieMap()
        {

            HashTrieMap target = new HashTrieMap();
            foreach (KeyValuePair<string, IGroundedValue> entry in hashMap)
            {
                target.InitialPut(new StringValue(entry.Key), entry.Value);
            }

            return target;
        }

        /// <summary>
        /// Convert to a HashTrieMap
        /// </summary>
        private sealed class KeyIterator : IAtomicIterator
        {
            readonly IEnumerator<string> keyIter;
            public KeyIterator(Dictionary<string, IGroundedValue> hashMap)
            {
                this.keyIter = hashMap.Keys.GetEnumerator();
            }

            public AtomicValue Next()
            {
                return this.keyIter.MoveNext() ? new StringValue(this.keyIter.Current) : null;
            }
            IItem ISequenceIterator.Next() => Next();
            public void Dispose() { }
        }
    }
}
