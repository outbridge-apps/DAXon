////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
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
namespace OutSmart.DAXon.Values.Maps
{
    internal sealed class SingleEntryMap : MapItem
    {
        public AtomicValue key;
        public IGroundedValue value;

        public IGroundedValue Value => value;

        public override UType KeyUType => key.GetUType();
        public SingleEntryMap(AtomicValue key, IGroundedValue value)
        {
            this.key = key;
            this.value = value;
        }

        public AtomicValue GetKey()
        {
            return key;
        }

        public override IGroundedValue Get(AtomicValue key)
        {
            return this.key.AsMapKey().Equals(key.AsMapKey()) ? value : null;
        }

        public override int Size()
        {
            return 1;
        }

        public override bool IsEmpty()
        {
            return false;
        }

        public override IAtomicIterator Keys()
        {
            return SingleAtomicIterator.MakeIterator(key);
        }

        public override IEnumerable<KeyValuePair> KeyValuePairs()
        {
            IList<KeyValuePair> list = new List<KeyValuePair>(1);
            list.Add(new KeyValuePair(key, value));
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
            else
            {
                return new HashTrieMap();
            }
        }

        public override bool Conforms(IPlainType keyType, SequenceType valueType, TypeHierarchy th)
        {
            return keyType.Matches(key, th) && valueType.Matches(value, th);
        }

        public override ItemType GetItemType(TypeHierarchy th)
        {
            return new MapType(key.GetItemType(), SequenceType.MakeSequenceType(SequenceTool.GetItemType(value, th), SequenceTool.GetCardinality(value)));
        }

        /// <summary>
        /// Convert to a HashTrieMap
        /// </summary>
        private HashTrieMap ToHashTrieMap()
        {
            HashTrieMap target = new HashTrieMap();
            target.InitialPut(key, value);
            return target;
        }
    }
}