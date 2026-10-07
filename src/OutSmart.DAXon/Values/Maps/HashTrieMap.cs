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
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal;
using System.IO;
namespace OutSmart.DAXon.Values.Maps
{
    /// <summary>
    /// An immutable map. This implementation, which uses a hash trie, was introduced in Saxon 9.6
    /// </summary>
    internal sealed class HashTrieMap : MapItem
    {
        private object root;   // MapTrie node, null = empty
        // type.
        private UType keyUType = UType.VOID;
        // type.
        private UType valueUType = UType.VOID;
        // type.
        private IAtomicType keyAtomicType = ErrorType.GetInstance();
        // type.
        private ItemType valueItemType = ErrorType.GetInstance();
        // type.
        private int valueCardinality = 0;
        // type.
        private int entries = -1;

        // type.
        public override UType KeyUType => keyUType;
        // type.
        public HashTrieMap()
        {
            this.root = null;
            this.entries = 0;
            MemoryLedger.SampleSmall(this, 72);   // its entries are sampled on their own
        }

        // type.
        private HashTrieMap(object root)
        {
            this.root = root;
            entries = -1;
            MemoryLedger.SampleSmall(this, 72);
        }

        // type.
        public static HashTrieMap Copy(MapItem map)
        {
            if (map is HashTrieMap)
            {
                return (HashTrieMap)map;
            }

            HashTrieMap m2 = new HashTrieMap();
            foreach (KeyValuePair pair in map.KeyValuePairs())
            {
                m2 = (HashTrieMap)m2.AddEntry(pair.key, pair.value);
            }

            return m2;
        }

        // type.
        private void UpdateTypeInformation(AtomicValue key, ISequence val, bool wasEmpty)
        {

            if (wasEmpty)
            {
                keyUType = key.GetUType();
                valueUType = SequenceTool.GetUType(val);
                keyAtomicType = key.GetItemType();
                valueItemType = MapItem.GetItemTypeOfSequence(val);
                valueCardinality = SequenceTool.GetCardinality(val);
            }
            else
            {
                keyUType = keyUType.Union(key.GetUType());
                valueUType = valueUType.Union(SequenceTool.GetUType(val));
                valueCardinality = Cardinality.Union(valueCardinality, SequenceTool.GetCardinality(val));
                if (key.GetItemType() != keyAtomicType)
                {
                    keyAtomicType = null;
                }

                if (!MapItem.IsKnownToConform(val, valueItemType))
                {
                    valueItemType = null;
                }
            }
        }

        // type.
        public override int Size()
        {
            if (entries >= 0)
            {
                return entries;
            }

            int count = 0;

            foreach (KeyValuePair entry in KeyValuePairs())
            {
                count++;
            }

            return entries = count;
        }

        // type.
        public override bool IsEmpty()
        {
            return entries == 0 || root == null;
        }

        // type.
        public override bool Conforms(IPlainType requiredKeyType, SequenceType requiredValueType, TypeHierarchy th)
        {
            if (IsEmpty())
            {
                return true;
            }

            if (keyAtomicType == requiredKeyType && valueItemType == requiredValueType.PrimaryType && Cardinality.Subsumes(requiredValueType.GetCardinality(), valueCardinality))
            {
                return true;
            }

            bool needFullCheck = false;
            if (requiredKeyType != BuiltInAtomicType.ANY_ATOMIC)
            {
                ItemType upperBoundKeyType = keyUType.ToItemType();
                Affinity rel = th.Relationship(requiredKeyType, upperBoundKeyType);
                if (rel == Affinity.SAME_TYPE || rel == Affinity.SUBSUMES)
                {
                }
                else if (rel == Affinity.DISJOINT)
                {
                    return false;
                }
                else
                {
                    needFullCheck = true;
                }
            }

            ItemType requiredValueItemType = requiredValueType.PrimaryType;
            if (requiredValueItemType != BuiltInAtomicType.ANY_ATOMIC)
            {
                ItemType upperBoundValueType = valueUType.ToItemType();
                Affinity rel = th.Relationship(requiredValueItemType, upperBoundValueType);
                if (rel == Affinity.SAME_TYPE || rel == Affinity.SUBSUMES)
                {
                }
                else if (rel == Affinity.DISJOINT)
                {
                    return false;
                }
                else
                {
                    needFullCheck = true;
                }
            }

            int requiredValueCard = requiredValueType.GetCardinality();
            if (!Cardinality.Subsumes(requiredValueCard, valueCardinality))
            {
                needFullCheck = true;
            }

            if (needFullCheck)
            {

                // we need to test the entries individually
                IAtomicIterator keyIter = Keys();
                AtomicValue key;
                while ((key = keyIter.Next()) != null)
                {
                    if (!requiredKeyType.Matches(key, th))
                    {
                        return false;
                    }

                    if (!requiredValueType.Matches(Get(key), th))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        // type.
        public override ItemType GetItemType(TypeHierarchy th)
        {
            UType keyType = UType.VOID;
            UType valueType = UType.VOID;
            int valueCard = 0;

            // we need to test the entries individually
            IAtomicIterator keyIter = Keys();
            AtomicValue key;
            while ((key = keyIter.Next()) != null)
            {
                IGroundedValue val = Get(key);
                keyType = keyType.Union(key.GetUType());
                valueType = valueType.Union(SequenceTool.GetUType(val));
                valueCard = Cardinality.Union(valueCard, SequenceTool.GetCardinality(val));
            }

            ItemType keyItemType = keyType.ToItemType();
            ItemType valueItemType = valueType.ToItemType();
            if (keyType == null)
            {

                // implies the map is empty
                return MapType.ANY_MAP_TYPE;
            }
            else
            {
                this.keyUType = keyType;
                this.valueUType = valueType;
                this.valueCardinality = valueCard;
                return new MapType((IAtomicType)keyItemType, SequenceType.MakeSequenceType(valueItemType, valueCard));
            }
        }

        // type.
        public override MapItem AddEntry(AtomicValue key, IGroundedValue value)
        {
            IAtomicMatchKey amk = MakeKey(key);
            bool isNew = MapTrie.Get(root, amk) == null;
            bool empty = IsEmpty();
            KeyValuePair pair = new KeyValuePair(key, value, amk);
            object root2 = MapTrie.Put(root, amk, pair);
            MemoryLedger.SampleEntry(pair, key, value);
            HashTrieMap t2 = new HashTrieMap(root2);
            t2.valueCardinality = this.valueCardinality;
            t2.keyUType = keyUType;
            t2.valueUType = valueUType;
            t2.keyAtomicType = keyAtomicType;
            t2.valueItemType = valueItemType;
            t2.UpdateTypeInformation(key, value, empty);
            if (entries >= 0)
            {
                t2.entries = isNew ? entries + 1 : entries;
            }

            return t2;
        }

        /// <summary>
        /// Accumulator for map:merge over a stream of small maps. Performs the same put sequence
        /// as a chain of AddEntry calls starting from an empty map — identical resulting trie
        /// structure and type-information — but the trie is privately owned until ToMap(), so
        /// interior nodes update child slots in place (<see cref="MapTrie.PutOwned"/>)
        /// instead of path-copying per entry. No put may follow ToMap().
        /// </summary>
        internal sealed class MergeBuilder
        {
            private readonly HashTrieMap m = new HashTrieMap();
            private object trie;   // MapTrie node, null = empty
            private int count;
            private IAtomicType lastKeyType;   // shape memo: last pair fed to UpdateTypeInformation
            private IAtomicType lastValueType; // (single-atomic values only)

            internal ISequence GetExisting(AtomicValue key, out IAtomicMatchKey amk)
            {
                amk = key.AsMapKey();
                KeyValuePair kvp = MapTrie.Get(trie, amk);
                return kvp == null ? null : kvp.value;
            }

            internal void Put(IAtomicMatchKey amk, AtomicValue key, IGroundedValue value, bool isNew)
            {
                bool wasEmpty = count == 0;
                KeyValuePair pair = new KeyValuePair(key, value, amk);
                trie = MapTrie.PutOwned(trie, amk, pair);
                MemoryLedger.SampleEntry(pair, key, value);
                if (isNew)
                {
                    count++;
                }

                UpdateTypeInfoMemo(key, value, wasEmpty);
            }

            /// <summary>
            /// use-first put: ONE trie descent decides existence and inserts; a duplicate key
            /// leaves the map and its type-information untouched, exactly like the probe-then-skip
            /// two-step it replaces.
            /// </summary>
            internal void PutFirst(AtomicValue key, IGroundedValue value)
            {
                bool wasEmpty = count == 0;
                KeyValuePair kvp = new KeyValuePair(key, value);
                trie = MapTrie.PutIfAbsentOwned(trie, kvp.MatchKey, kvp, out bool inserted);
                if (inserted)
                {
                    MemoryLedger.SampleEntry(kvp, key, value);
                    count++;
                    UpdateTypeInfoMemo(key, value, wasEmpty);
                }
            }

            // The dominant merge stream is shape-homogeneous (same key type, same single-atomic
            // value type pair after pair); for a repeated shape every union/conform check in
            // UpdateTypeInformation is idempotent, so it can be skipped wholesale.
            private void UpdateTypeInfoMemo(AtomicValue key, IGroundedValue value, bool wasEmpty)
            {
                if (!wasEmpty && ReferenceEquals(key.GetItemType(), lastKeyType)
                    && value is AtomicValue av && ReferenceEquals(av.GetItemType(), lastValueType))
                {
                    return;
                }

                m.UpdateTypeInformation(key, value, wasEmpty);
                lastKeyType = key.GetItemType();
                lastValueType = value is AtomicValue av2 ? av2.GetItemType() : null;
            }

            internal HashTrieMap ToMap()
            {
                m.root = trie;
                m.entries = count;
                return m;
            }
        }

        // type.
        public bool InitialPut(AtomicValue key, IGroundedValue value)
        {

            bool empty = IsEmpty();
            IAtomicMatchKey amk = MakeKey(key);
            bool exists = MapTrie.Get(root, amk) != null;
            KeyValuePair pair = new KeyValuePair(key, value, amk);
            root = MapTrie.Put(root, amk, pair);
            MemoryLedger.SampleEntry(pair, key, value);
            UpdateTypeInformation(key, value, empty);
            entries = -1;
            return exists;
        }

        // type.
        private IAtomicMatchKey MakeKey(AtomicValue key)
        {
            return key.AsMapKey();
        }

        // type.
        public override MapItem Remove(AtomicValue key)
        {

            // This code used to assume that if the key wasn't in the
            // map, imap.remove() would return the original object
            // unchanged. But that was only true if the hash bucket
            // that would have contained the value was empty. That
            // won't be the case if other values happen to have been
            // assigned that bucket. So now we do an explicit check.
            // This is probably slower, but remove() is an uncommon
            // operation. And it gives the correct result!
            IAtomicMatchKey amk = MakeKey(key);
            if (MapTrie.Get(root, amk) == null)
            {

                // The key is not present; the map is unchanged
                return this;
            }

            HashTrieMap result = new HashTrieMap(MapTrie.Remove(root, amk));
            result.keyUType = keyUType;
            result.valueUType = valueUType;
            result.valueCardinality = valueCardinality;
            result.entries = entries - 1;
            return result;
        }

        // type.
        public override IGroundedValue Get(AtomicValue key)
        {
            KeyValuePair o = MapTrie.Get(root, MakeKey(key));
            return o == null ? null : o.value;
        }

        // type.
        public override IAtomicIterator Keys()
        {
            return new AnonymousAtomicIterator(this);
        }

        // type.
        public override IEnumerable<KeyValuePair> KeyValuePairs()
        {

            // For C# - don't use a lambda expression here
            return new AnonymousIterable(this);
        }

        // type.
        //    public String toShortString() {
        //                Tuple2<IAtomicMatchKey, KeyValuePair> entry = iter.next();
        //                IAtomicMatchKey k1 = entry._1;
        //                AtomicValue k2 = entry._2.key;
        //                ISequence v = entry._2.value;
        //                buff.append(k2.toShortString());
        //                buff.append(':');
        //                buff.append(Err.depictSequence(v).toString().trim());
        //                buff.append(", ");
        //            if (size == 1) {
        //                buff.append("}");
        public override string ToString()
        {
            return MapItem.MapToString(this);
        }

        private sealed class AnonymousAtomicIterator : IAtomicIterator
        {

            private readonly IEnumerator<KeyValuePair> baseIter;
            public AnonymousAtomicIterator(HashTrieMap parent)
            {
                this.baseIter = MapTrie.Enumerate(parent.root);
            }
            public AtomicValue Next()
            {
                return baseIter.MoveNext() ? baseIter.Current.key : null;
            }
            IItem ISequenceIterator.Next() => Next(); // redirect StubGen hollow to the real covariant Next(); default = silent empty iteration
            public void Dispose() { }
        }

        private sealed class AnonymousIterable : IEnumerable<KeyValuePair>
        {

            private readonly HashTrieMap parent;
            public AnonymousIterable(HashTrieMap parent)
            {
                this.parent = parent;
            }
            public IEnumerator<KeyValuePair> GetEnumerator() => MapTrie.Enumerate(parent.root);
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
