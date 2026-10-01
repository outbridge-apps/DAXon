////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Serialization
{
    internal sealed class HTMLTagHashSet
    {
        string[] strings;
        int size;
        public HTMLTagHashSet(int size)
        {
            strings = new string[size];
            this.size = size;
        }

        public void Add(string s)
        {
            int hash = (GetHashCode(s) & 0x7fffffff) % size;
            while (true)
            {
                if (strings[hash] == null)
                {
                    strings[hash] = s;
                    return;
                }

                if (strings[hash].Equals(s, global::System.StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                hash = (hash + 1) % size;
            }
        }

        public bool Contains(string s)
        {
            int hash = (GetHashCode(s) & 0x7fffffff) % size;
            while (true)
            {
                if (strings[hash] == null)
                {
                    return false;
                }

                if (strings[hash].Equals(s, global::System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                hash = (hash + 1) % size;
            }
        }

        private int GetHashCode(string s)
        {

            // get a hashcode that doesn't depend on the case of characters.
            // This relies on the fact that char & 0xDF is case-blind in ASCII
            int hash = 0;
            int limit = s.Length;
            if (limit > 24)
                limit = 24;
            for (int i = 0; i < limit; i++)
            {
                hash = (hash << 1) + (s[i] & 0xdf);
            }

            return hash;
        }
    }
}