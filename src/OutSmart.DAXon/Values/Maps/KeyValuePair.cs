////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;namespace OutSmart.DAXon.Values.Maps
{
    /// <summary>
    /// A key and a corresponding value to be held in a IMap.
    /// </summary>
    public class KeyValuePair
    {
        public AtomicValue key;
        public IGroundedValue value;
        // Match-key cache for MapTrie, where this pair doubles as the trie leaf. Lazy racy-init is
        // benign: AsMapKey is deterministic and equal-by-value, so competing writes are equivalent.
        internal Expressions.Sorting.IAtomicMatchKey amk;

        public KeyValuePair(AtomicValue key, IGroundedValue value)
        {
            this.key = key;
            this.value = value;
        }

        internal KeyValuePair(AtomicValue key, IGroundedValue value, Expressions.Sorting.IAtomicMatchKey amk)
        {
            this.key = key;
            this.value = value;
            this.amk = amk;
        }

        internal Expressions.Sorting.IAtomicMatchKey MatchKey
        {
            get { return amk ?? (amk = key.AsMapKey()); }
        }
    }
}