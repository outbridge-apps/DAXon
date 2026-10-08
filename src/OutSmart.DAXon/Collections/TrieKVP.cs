////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2012 Michael Froh.
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace OutSmart.DAXon.Collections.Trie
{
    internal sealed class TrieKVP<K, V>
    {
        public readonly K key;
        public readonly V value;

        public V Value => value;
        public TrieKVP(K v1, V v2)
        {
            key = v1;
            value = v2;
        }

        public K GetKey()
        {
            return key;
        }

        public override bool Equals(object o)
        {
            if (this == o)
                return true;
            if (o == null || GetType() != o.GetType())
                return false;
            TrieKVP<K, V> tuple = (TrieKVP<K, V>)o;
            if (key != null ? !key.Equals(tuple.key) : tuple.key != null)
                return false;
            return value != null ? value.Equals(tuple.value) : tuple.value == null;
        }

        public override int GetHashCode()
        {
            int result = key != null ? key.GetHashCode() : 0;
            result = 31 * result + (value != null ? value.GetHashCode() : 0);
            return result;
        }

        public override string ToString()
        {
            return "(" + key + ',' + value + ')';
        }
    }
}