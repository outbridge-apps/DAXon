////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System.Collections.Generic;
using OutSmart.DAXon.Text;

namespace OutSmart.DAXon.Trees.Tiny
{
    // String keys found by their Latin-1 bytes in place, a value each: the fused paths over Tiny text (group-by,
    // distinct-values, distinct prefixes) look a key up without making a string for one seen before - three copies of
    // this table were kept, one a path. Open addressing, grown past two thirds full. A key off the byte path goes in as a
    // string; the hash is UnicodeString's (h = 31 * h + codepoint), so its bytes and its string meet.
    internal sealed class SpanKeyTable<T>
    {
        private int mask = 127;
        private int[] hashes = new int[128];
        private UnicodeString[] keys = new UnicodeString[128];
        private T[] values = new T[128];

        internal int Count { get; private set; }

        internal T this[int slot] => values[slot];

        internal static int Hash(byte[] b, int off, int len)
        {
            int h = 0;
            for (int i = 0; i < len; i++)
            {
                h = 31 * h + (b[off + i] & 0xff);
            }

            return h;
        }

        // The slot of the key the bytes spell, or the complement of the free slot where it goes.
        internal int Find(byte[] b, int off, int len, int h)
        {
            int slot = h & mask;
            for (UnicodeString k; (k = keys[slot]) != null; slot = (slot + 1) & mask)
            {
                if (hashes[slot] == h && (k is Slice8 s8 ? s8.ContentEqualsSpan(b, off, len) : new Slice8(b, off, off + len).Equals(k)))
                {
                    return slot;
                }
            }

            return ~slot;
        }

        // The same for a key as a string.
        internal int Find(UnicodeString key, int h)
        {
            int slot = h & mask;
            for (UnicodeString k; (k = keys[slot]) != null; slot = (slot + 1) & mask)
            {
                if (hashes[slot] == h && key.Equals(k))
                {
                    return slot;
                }
            }

            return ~slot;
        }

        // Puts the key in the free slot that Find gave (the complement of its answer).
        internal void Add(int free, int h, UnicodeString key, T value)
        {
            hashes[free] = h;
            keys[free] = key;
            values[free] = value;
            if (++Count * 3 > (mask + 1) * 2)
            {
                Grow();
            }
        }

        internal IEnumerable<UnicodeString> Keys()
        {
            foreach (UnicodeString k in keys)
            {
                if (k != null)
                {
                    yield return k;
                }
            }
        }

        private void Grow()
        {
            int cap = (mask + 1) << 1;
            var h2 = new int[cap];
            var k2 = new UnicodeString[cap];
            var v2 = new T[cap];
            for (int s = 0; s <= mask; s++)
            {
                if (keys[s] != null)
                {
                    int ns = hashes[s] & (cap - 1);
                    while (k2[ns] != null)
                    {
                        ns = (ns + 1) & (cap - 1);
                    }

                    h2[ns] = hashes[s];
                    k2[ns] = keys[s];
                    v2[ns] = values[s];
                }
            }

            mask = cap - 1;
            hashes = h2;
            keys = k2;
            values = v2;
        }
    }
}
