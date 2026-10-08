////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
namespace OutSmart.DAXon.Model
{
    internal sealed class PrefixPool
    {
        private const int LIMIT = 2047;
        string[] prefixes = new string[8];
        int used;
        Dictionary<string, int> index;
        public PrefixPool()
        {
            prefixes[0] = "";
            used = 1;
        }

        public int ObtainPrefixCode(string prefix)
        {
            if ((prefix.Length == 0))
            {
                return 0;
            }


            // Create an index if it's going to be useful
            if (index == null && used > 8)
            {
                MakeIndex();
            }


            // See if the prefix is already known
            if (index != null)
            {
                int existing = index.GetOrDefault(prefix, -1);
                if (existing != -1)
                {
                    return existing;
                }
            }
            else
            {
                for (int i = 0; i < used; i++)
                {
                    if (prefixes[i].Equals(prefix))
                    {
                        return i;
                    }
                }
            }


            // Allocate a new code
            int code = used++;
            if (used > LIMIT)
            {
                throw new InvalidOperationException("Too many namespace prefixes - limit is " + LIMIT + " per document");
            }

            if (used >= prefixes.Length)
            {
                Array.Resize(ref prefixes, used * 2);
            }

            prefixes[code] = prefix;
            if (index != null)
            {
                index[prefix] = code;
            }

            return code;
        }

        private void MakeIndex()
        {
            index = new Dictionary<string, int>(used);
            for (int i = 0; i < used; i++)
            {
                index[prefixes[i]] = i;
            }
        }

        public string GetPrefix(int code)
        {
            if (code < used)
            {
                return prefixes[code];
            }

            throw new ArgumentException("Unknown prefix code " + code);
        }

        public void Condense()
        {
            Array.Resize(ref prefixes, used);
            index = null;
        }
    }
}