////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using static OutSmart.DAXon.Text.StrHelpers;


using OutSmart.DAXon.Collections;


using System;
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
                OutSmart.DAXon.Core.Controller.CheckActiveTimeoutEvery64K(i);
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

            return cachedHash = Hash(bytes, start, end);
        }

        public override bool Equals(object o)
        {
            return o is UnicodeString u && TryEquals(bytes, start, end, u, out bool equal) ? equal : base.Equals(o);
        }

        public override int CompareTo(UnicodeString other)
        {
            return TryCompare(bytes, start, end, other, out int order) ? order : base.CompareTo(other);
        }

        // Guard order mirrors the base exactly (bounds first, then the empty-needle case).
        public override long IndexOf(UnicodeString other, long from)
        {
            if (from < 0 || from >= end - start)
            {
                return -1;
            }

            if (other.IsEmpty())
            {
                return from;
            }

            return TryIndexOf(bytes, start, end, other, (int)from, out long index) ? index : base.IndexOf(other, from);
        }

        public override bool HasSubstring(UnicodeString other, long offset)
        {
            if (offset < 0 || offset > end - start)
            {
                throw new IndexOutOfRangeException();
            }

            return TryHasSubstring(bytes, start, end, other, (int)offset, out bool has) ? has : base.HasSubstring(other, offset);
        }

        // The Latin-1 bytes of either 8-bit form that keeps them in one array: a Slice8, or a Twine8 from 0 to its end.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        internal static bool Latin1Span(UnicodeString s, out byte[] bytes, out int start, out int end)
        {
            if (s is Slice8 s8)
            {
                bytes = s8.bytes;
                start = s8.start;
                end = s8.end;
                return true;
            }

            if (s is Twine8 t8)
            {
                bytes = t8.ByteArray;
                start = 0;
                end = bytes.Length;
                return true;
            }

            bytes = null;
            start = 0;
            end = 0;
            return false;
        }

        // The comparisons of a Latin-1 span (b, s, e) with a string that has a byte or char span; false for any other,
        // which the base compares codepoint by codepoint (two iterator allocations and virtual calls a character).
        // A byte is its codepoint and the 16-bit forms are surrogate-free, so a byte or char scan is a codepoint scan.
        internal static int Hash(byte[] b, int s, int e)
        {
            int h = 0;
            for (int i = s; i < e; i++)
            {
                h = 31 * h + b[i];
            }

            return h;
        }

        internal static bool TryEquals(byte[] b, int s, int e, UnicodeString other, out bool equal)
        {
            if (Latin1Span(other, out byte[] ob, out int os, out int oe))
            {
                equal = e - s == oe - os && ByteRegionEquals(b, s, e - s, ob, os, oe - os, 0);
                return true;
            }

            if (BMPSlice.CharSpan(other, out string oc, out os, out oe))
            {
                equal = e - s == oe - os && CharRegionEquals(b, s, e - s, oc, os, oe - os, 0);
                return true;
            }

            equal = false;
            return false;
        }

        internal static bool TryCompare(byte[] b, int s, int e, UnicodeString other, out int order)
        {
            int i = s;
            if (Latin1Span(other, out byte[] ob, out int j, out int oe))
            {
                int oj = j;
                while (i < e && j < oe)
                {
                    int diff = b[i++] - ob[j++];
                    if (diff != 0)
                    {
                        order = diff;
                        return true;
                    }
                }

                order = (e - s).CompareTo(oe - oj);
                return true;
            }

            if (BMPSlice.CharSpan(other, out string oc, out j, out oe))
            {
                int oj = j;
                while (i < e && j < oe)
                {
                    int diff = b[i++] - oc[j++];
                    if (diff != 0)
                    {
                        order = diff;
                        return true;
                    }
                }

                order = (e - s).CompareTo(oe - oj);
                return true;
            }

            order = 0;
            return false;
        }

        // Searches a needle that is not empty from a position inside the haystack.
        internal static bool TryIndexOf(byte[] b, int s, int e, UnicodeString other, int from, out long index)
        {
            if (Latin1Span(other, out byte[] ob, out int os, out int oe))
            {
                index = ByteIndexOf(b, s, e, ob, os, oe, from);
                return true;
            }

            if (BMPSlice.CharSpan(other, out string oc, out os, out oe))
            {
                index = CharIndexOf(b, s, e, oc, os, oe, from);
                return true;
            }

            index = -1;
            return false;
        }

        internal static bool TryHasSubstring(byte[] b, int s, int e, UnicodeString other, int offset, out bool has)
        {
            if (Latin1Span(other, out byte[] ob, out int os, out int oe))
            {
                has = ByteRegionEquals(b, s, e - s, ob, os, oe - os, offset);
                return true;
            }

            if (BMPSlice.CharSpan(other, out string oc, out os, out oe))
            {
                has = CharRegionEquals(b, s, e - s, oc, os, oe - os, offset);
                return true;
            }

            has = false;
            return false;
        }

        internal static long ByteIndexOf(byte[] hb, int hs, int he, byte[] nb, int ns, int ne, int from)
        {
            int nlen = ne - ns;
            byte first = nb[ns];
            long work = 0;
            for (int i = hs + from; i <= he - nlen; i++)
            {
                if (hb[i] == first)
                {
                    int k = 1;
                    while (k < nlen && hb[i + k] == nb[ns + k])
                    {
                        k++;
                    }

                    OutSmart.DAXon.Core.Controller.CheckActiveTimeoutAfterWork(ref work, k);   // a candidate compares up to the needle's length

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

            long work = 0;
            for (int i = hs + from; i <= he - nlen; i++)
            {
                if (hb[i] == first)
                {
                    int k = 1;
                    while (k < nlen && (hb[i + k] & 0xff) == ns_[nj + k])
                    {
                        k++;
                    }

                    OutSmart.DAXon.Core.Controller.CheckActiveTimeoutAfterWork(ref work, k);   // a candidate compares up to the needle's length

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
