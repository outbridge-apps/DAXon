////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2012 Michael Froh.
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.Collections.Generic;
namespace OutSmart.DAXon.Collections.Trie
{
    // Original author: Michael Froh (published on Github). Released under MPL 2.0
    // by Saxonica Limited with permission from the author
    internal interface IImmutableMap<K, V> : IEnumerable<TrieKVP<K, V>>
    {
        IImmutableMap<K, V> Put(K key, V value);
        IImmutableMap<K, V> Remove(K key);
        V this[K key] { get; }
    }
}