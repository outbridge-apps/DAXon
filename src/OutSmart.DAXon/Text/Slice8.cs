////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Serialization;
using static OutSmart.DAXon.Text.StrHelpers;


using OutSmart.DAXon.Collections;

using OutSmart.DAXon.Internal.Charsets;


using System;

using System.Collections.Generic;

using System.Collections.ObjectModel;

using System.Linq;

using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
using System.IO;
namespace OutSmart.DAXon.Text
{
    internal sealed class Slice8 : UnicodeString
    {
        private readonly byte[] bytes;
        private readonly int start;
        private readonly int end;
        private int cachedHash;

        public override int Width => 8;

        public byte[] ByteArray => bytes;

        public int Start => start;

        public int End => end;
        public Slice8(byte[] bytes, int start, int end)
        {
            this.bytes = bytes;
            this.start = start;
            this.end = end;
        }

        public override long Length()
        {
            return end - start;
        }

        public override long IndexOf(int codePoint, long from)
        {
            if (codePoint > 255)
            {
                return -1;
            }

            byte b = (byte)(codePoint & 0xff);
            for (int i = start + requireNonNegativeInt(from); i < end; i++)
            {
                if (bytes[i] == b)
                {
                    return i - start;
                }
            }

            return -1;
        }

        public override int CodePointAt(long index)
        {
            int index32 = RequireInt(index);
            if (index32 < 0 || index32 >= Length32())
            {
                throw new IndexOutOfRangeException();
            }

            return (bytes[start + index32]) & 0xff;
        }

        public override UnicodeString Substring(long start, long end)
        {
            CheckSubstringBounds(start, end);
            if (end == start)
            {
                return EmptyUnicodeString.GetInstance();
            }
            else
            {
                return new Slice8(bytes, RequireInt(start) + this.start, RequireInt(end) + this.start);
            }
        }

        public override long IndexWhere(Func<int, bool> predicate, long from)
        {
            for (int i = requireNonNegativeInt(from) + start; i < end; i++)
            {
                if (predicate(bytes[i] & 0xff))
                {
                    return i - start;
                }
            }

            return -1;
        }

        public override IIntIterator CodePoints()
        {
            return new AnonymousIntIterator(this);
        }

        public override void Copy8bit(byte[] target, int offset)
        {
            Array.Copy(bytes, start, target, offset, end - start);
        }

        public override void Copy16bit(char[] target, int offset)
        {
            for (int i = start, j = offset; i < end;)
            {
                target[j++] = (char)(bytes[i++] & 0xff);
            }
        }

        public override void Copy24bit(byte[] target, int offset)
        {
            for (int i = start, j = offset; i < end;)
            {
                target[j++] = (byte)0;
                target[j++] = (byte)0;
                target[j++] = bytes[i++];
            }
        }

        public override void Copy32bit(int[] target, int offset)
        {
            for (int i = start, j = offset; i < end;)
            {
                target[j++] = bytes[i++] & 0xff;
            }
        }

        // Content equality against a raw Latin-1 byte span (fused group-by key probe).
        internal bool ContentEqualsSpan(byte[] b, int off, int len)
        {
            if (end - start != len)
            {
                return false;
            }

            for (int i = 0; i < len; i++)
            {
                if (bytes[start + i] != b[off + i])
                {
                    return false;
                }
            }

            return true;
        }

        public override int GetHashCode()
        {
            if (cachedHash != 0)
            {
                return cachedHash;
            }

            int h = 0;
            for (int i = start; i < end; i++)
            {
                byte b = bytes[i];
                h = 31 * h + (b & 0xff);
            }

            return cachedHash = h;
        }

        public override bool Equals(object o)
        {
            // Same-width fast compare (tree text is Slice8; the base compare allocates two
            // codepoint iterators and pays interface calls per char -- hot in distinct-values,
            // grouping and key lookups).
            if (o is Slice8 s)
            {
                int n = end - start;
                if (n != s.end - s.start)
                {
                    return false;
                }

                for (int i = 0; i < n; i++)
                {
                    if (bytes[start + i] != s.bytes[s.start + i])
                    {
                        return false;
                    }
                }

                return true;
            }

            if (o is BMPString obstr)
            {
                // BMP rep is surrogate-free, so char == codepoint: compare directly against the
                // backing string (comparisons against stylesheet literals otherwise fall to the
                // iterator-allocating base Equals — hot in [CHILD='lit'] predicate scans).
                string os = obstr.ToString();   // backing string, no copy
                return EqualsBmp(os, 0, os.Length);
            }

            if (o is BMPSlice obsl)
            {
                return EqualsBmp(obsl.Backing, obsl.Start, obsl.End);
            }

            if (o is Twine8 t)
            {
                byte[] tb = t.ByteArray;
                int n = end - start;
                if (n != tb.Length)
                {
                    return false;
                }

                for (int i = 0; i < n; i++)
                {
                    if (bytes[start + i] != tb[i])
                    {
                        return false;
                    }
                }

                return true;
            }

            return base.Equals(o);
        }

        // Same-width lexicographic compare (tree text is Slice8; the base compare allocates two
        // codepoint iterators and pays MoveNext/Current per char -- hot in xsl:sort / fn:sort).
        // Latin1 byte value == codepoint, so an unsigned byte compare is codepoint-order-identical.
        public override int CompareTo(UnicodeString other)
        {
            byte[] ob;
            int oj, oe;
            if (other is Slice8 s)
            {
                ob = s.bytes;
                oj = s.start;
                oe = s.end;
            }
            else if (other is Twine8 tw)
            {
                ob = tw.ByteArray;
                oj = 0;
                oe = ob.Length;
            }
            else if (other is BMPString bstr)
            {
                // BMP rep holds surrogate-free chars, so char order == codepoint order: compare
                // directly against the backing string instead of the base codepoint-iterator loop
                // (which allocates two iterators per comparison — hot in string comparisons
                // against stylesheet literals).
                string os = bstr.ToString();   // backing string, no copy
                return CompareToBmp(os, 0, os.Length);
            }
            else if (other is BMPSlice bsl)
            {
                return CompareToBmp(bsl.Backing, bsl.Start, bsl.End);
            }
            else
            {
                return base.CompareTo(other);
            }

            int i = start;
            int j = oj;
            while (i < end && j < oe)
            {
                int diff = (bytes[i++] & 0xff) - (ob[j++] & 0xff);
                if (diff != 0)
                {
                    return diff;
                }
            }

            return (end - start).CompareTo(oe - oj);
        }

        // Byte-rep substring search (haystack = this). The base IndexOf/HasSubstring run on
        // codepoint iterators (two allocations + per-char virtual calls per probe) — hot in
        // contains()/starts-with()/ends-with()/substring-before()/after() under the codepoint
        // collation. Latin1 byte value == codepoint, and BMP reps are surrogate-free, so direct
        // byte/char scans realise the same codepoint semantics. Guard order mirrors the base
        // exactly (bounds first, then the empty-needle case).
        public override long IndexOf(UnicodeString other, long from)
        {
            int len = end - start;
            if (from < 0 || from >= len)
            {
                return -1;
            }

            if (other.IsEmpty())
            {
                return from;
            }

            if (other is Slice8 s)
            {
                return ByteIndexOf(bytes, start, end, s.bytes, s.start, s.end, (int)from);
            }

            if (other is Twine8 t)
            {
                byte[] tb = t.ByteArray;
                return ByteIndexOf(bytes, start, end, tb, 0, tb.Length, (int)from);
            }

            if (other is BMPString bstr)
            {
                string os = bstr.ToString();   // backing string, no copy
                return CharIndexOf(bytes, start, end, os, 0, os.Length, (int)from);
            }

            if (other is BMPSlice bsl)
            {
                return CharIndexOf(bytes, start, end, bsl.Backing, bsl.Start, bsl.End, (int)from);
            }

            return base.IndexOf(other, from);
        }

        public override bool HasSubstring(UnicodeString other, long offset)
        {
            int len = end - start;
            if (offset < 0 || offset > len)
            {
                throw new IndexOutOfRangeException();
            }

            if (other is Slice8 s)
            {
                return ByteRegionEquals(bytes, start, len, s.bytes, s.start, s.end - s.start, (int)offset);
            }

            if (other is Twine8 t)
            {
                byte[] tb = t.ByteArray;
                return ByteRegionEquals(bytes, start, len, tb, 0, tb.Length, (int)offset);
            }

            if (other is BMPString bstr)
            {
                string os = bstr.ToString();   // backing string, no copy
                return CharRegionEquals(bytes, start, len, os, 0, os.Length, (int)offset);
            }

            if (other is BMPSlice bsl)
            {
                return CharRegionEquals(bytes, start, len, bsl.Backing, bsl.Start, bsl.End - bsl.Start, (int)offset);
            }

            return base.HasSubstring(other, offset);
        }

        internal static long ByteIndexOf(byte[] hb, int hs, int he, byte[] nb, int ns, int ne, int from)
        {
            int nlen = ne - ns;
            byte first = nb[ns];
            for (int i = hs + from; i <= he - nlen; i++)
            {
                if (hb[i] == first)
                {
                    int k = 1;
                    while (k < nlen && hb[i + k] == nb[ns + k])
                    {
                        k++;
                    }

                    if (k == nlen)
                    {
                        return i - hs;
                    }
                }
            }

            return -1;
        }

        internal static long CharIndexOf(byte[] hb, int hs, int he, string ns_, int nj, int ne, int from)
        {
            int nlen = ne - nj;
            char first = ns_[nj];
            if (first > 0xff)
            {
                return -1;   // a >Latin1 codepoint can never occur in a byte haystack
            }

            for (int i = hs + from; i <= he - nlen; i++)
            {
                if (hb[i] == first)
                {
                    int k = 1;
                    while (k < nlen && (hb[i + k] & 0xff) == ns_[nj + k])
                    {
                        k++;
                    }

                    if (k == nlen)
                    {
                        return i - hs;
                    }
                }
            }

            return -1;
        }

        internal static bool ByteRegionEquals(byte[] hb, int hs, int hlen, byte[] nb, int ns, int nlen, int offset)
        {
            if (nlen + offset > hlen)
            {
                return false;
            }

            for (int k = 0; k < nlen; k++)
            {
                if (hb[hs + offset + k] != nb[ns + k])
                {
                    return false;
                }
            }

            return true;
        }

        internal static bool CharRegionEquals(byte[] hb, int hs, int hlen, string ns_, int nj, int nlen, int offset)
        {
            if (nlen + offset > hlen)
            {
                return false;
            }

            for (int k = 0; k < nlen; k++)
            {
                if ((hb[hs + offset + k] & 0xff) != ns_[nj + k])
                {
                    return false;
                }
            }

            return true;
        }

        private bool EqualsBmp(string os, int oj, int oe)
        {
            int n = end - start;
            if (n != oe - oj)
            {
                return false;
            }

            for (int i = 0; i < n; i++)
            {
                if ((bytes[start + i] & 0xff) != os[oj + i])
                {
                    return false;
                }
            }

            return true;
        }

        private int CompareToBmp(string os, int oj, int oe)
        {
            int i = start;
            int j = oj;
            while (i < end && j < oe)
            {
                int diff = (bytes[i++] & 0xff) - os[j++];
                if (diff != 0)
                {
                    return diff;
                }
            }

            return (end - start).CompareTo(oe - oj);
        }

        //    @Override
        //    public UnicodeString concat(UnicodeString other) {
        /// <summary>
        /// Display as a string.
        /// </summary>
        public override string ToString()
        {
            // Convert only the slice range: ConvertAll over the whole backing array made every
            // ToString O(buffer) — a tree-text slice copied its entire 64K segment per call.
            int len = end - start;
            char[] buf = new char[len];
            StringTool.Copy8to16(bytes, start, buf, 0, len);
            return new string(buf);
        }

        private sealed class AnonymousIntIterator : AbstractIntIterator
        {

            private readonly Slice8 parent;
            int i;
            public AnonymousIntIterator(Slice8 parent)
            {
                this.parent = parent;
                this.i = parent.start;
            }
            public override bool HasNext()
            {
                return i < parent.end;
            }

            public override int Next()
            {
                return parent.bytes[i++] & 0xff;
            }
        }
    }
}
