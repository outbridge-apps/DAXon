////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using static OutSmart.DAXon.Text.StrHelpers;


using OutSmart.DAXon.Collections;

using OutSmart.DAXon.Internal.Collections;


using System;
namespace OutSmart.DAXon.Text
{
    internal sealed class Twine8 : UnicodeString
    {

        private static readonly bool CHECKING = Configuration.IsAssertionsEnabled();
        protected byte[] bytes;
        protected int cachedHash;

        public byte[] ByteArray => bytes;

        public override int Width => 8;
        public Twine8(byte[] bytes)
        {
            this.bytes = bytes;
            Held();
        }

        public Twine8(char[] chars, int start, int len)
        {
            // upstream copied chars[start..len) to bytes[start..), wrong whenever start > 0
            bytes = new byte[len];
            for (int i = 0; i < len; i++)
            {
                int c = chars[start + i];
                if (CHECKING && c > 255)
                {
                    throw new ArgumentException();
                }

                bytes[i] = (byte)(c & 0xff);
            }

            Held();
        }
        public Twine8(string str)
        {
            bytes = FromString(str);
            Held();
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void Held()
        {
            if (bytes.Length >= MemoryLedger.BigChars)
            {
                MemoryLedger.HoldText(bytes);
            }
        }

        // Latin-1 narrowing, one byte per char (UTF-8 would double-encode 0x80-0xFF and corrupt the twine).
        private static byte[] FromString(string str)
        {
            byte[] result = new byte[str.Length];
            for (int i = 0; i < str.Length; i++)
            {
                char c = str[i];
                if (CHECKING && c > 255)
                {
                    throw new ArgumentException();
                }

                result[i] = (byte)(c & 0xff);
            }

            return result;
        }

        public override long Length()
        {
            return bytes.Length;
        }

        public override int Length32()
        {
            return bytes.Length;
        }

        public override void Copy8bit(byte[] target, int offset)
        {
            Array.Copy(bytes, 0, target, offset, bytes.Length);
        }

        public override void Copy16bit(char[] target, int offset)
        {
            for (int i = 0, j = offset; i < bytes.Length;)
            {
                target[j++] = (char)(bytes[i++] & 0xff);
            }
        }

        public override void Copy24bit(byte[] target, int offset)
        {
            for (int i = 0, j = offset; i < bytes.Length;)
            {
                target[j++] = (byte)0;
                target[j++] = (byte)0;
                target[j++] = bytes[i++];
            }
        }

        public override void Copy32bit(int[] target, int offset)
        {
            for (int i = 0, j = offset; i < bytes.Length;)
            {
                target[j++] = bytes[i++] & 0xff;
            }
        }

        public override UnicodeString Substring(long start, long end)
        {
            long len = Length();
            if (start < 0 || end < start || end > len)
            {
                throw new IndexOutOfRangeException();
            }

            if (end == start)
            {
                return EmptyUnicodeString.GetInstance();
            }
            else if (start == 0 && end == len)
            {
                return this;
            }
            else
            {
                return new Slice8(bytes, RequireInt(start), RequireInt(end));
            }
        }

        public override int CodePointAt(long index)
        {
            int index32 = RequireInt(index);
            if (index32 < 0 || index32 >= Length32())
            {
                throw new IndexOutOfRangeException();
            }

            return (int)bytes[index32] & 0xff;
        }

        public override long IndexOf(int codePoint, long from)
        {
            int from32 = RequireNonNegativeInt(from);
            if (from32 >= Length32())
            {
                return -1;
            }

            int last = bytes.Length;
            if (codePoint < 0 || codePoint > 255)
            {
                return -1;
            }

            for (int i = from32; i < last; i++)
            {
                if ((bytes[i] & 0xff) == codePoint)
                {
                    return i;
                }
            }

            return -1;
        }

        public override long IndexOf(UnicodeString other, long from)
        {
            if (from < 0)
            {
                from = 0;
            }
            else if (from >= Length())
            {
                return -1;
            }

            if (other.IsEmpty())
            {
                return from;
            }

            int initial = other.CodePointAt(0);
            int len = RequireInt(other.Length());
            int lastPossible = Length32() - len;
            while (from <= lastPossible)
            {
                long i = IndexOf(initial, from);
                if (i < 0)
                {
                    return -1;
                }

                if (HasSubstring(other, i))
                {
                    return i;
                }

                from = i + 1;
            }

            return -1;
        }

        public override bool IsEmpty()
        {
            return bytes.Length == 0;
        }

        public override IIntIterator CodePoints()
        {
            return new AnonymousIntIterator(this);
        }

        public override int GetHashCode()
        {
            if (cachedHash != 0)
            {
                return cachedHash;
            }

            // Byte-identical to the base codepoint hash (Latin1: each byte IS its codepoint < 0x100),
            // without the per-char CodePoints() iterator.
            int h = 0;
            byte[] b = bytes;
            for (int i = 0; i < b.Length; i++)
            {
                h = 31 * h + (b[i] & 0xff);
            }

            return cachedHash = h;
        }

        public override bool Equals(object o)
        {
            if (o is Twine8)
            {
                Twine8 other = (Twine8)o;
                if (this.Length32() != other.Length32())
                {
                    return false;
                }

                if (this.GetHashCode() != other.GetHashCode())
                {
                    return false;
                }

                return ArrayTools.Equals(bytes, other.bytes);
            }

            if (o is Slice8 sl8)
            {
                return sl8.Equals(this);   // Slice8 carries the byte-vs-byte fast case
            }

            if (o is BMPString bstr)
            {
                // Surrogate-free chars: char == codepoint, compare without iterator allocations.
                string os = bstr.ToString();   // backing string, no copy
                if (bytes.Length != os.Length)
                {
                    return false;
                }

                for (int i = 0; i < bytes.Length; i++)
                {
                    if ((bytes[i] & 0xff) != os[i])
                    {
                        return false;
                    }
                }

                return true;
            }

            return base.Equals(o);
        }

        // Byte-rep region compare — same rationale as Slice8 (see there). The existing
        // IndexOf(UnicodeString) override above keeps its Java-mirrored guard semantics and
        // probes candidates through this method, so it gets the byte scan automatically.
        public override bool HasSubstring(UnicodeString other, long offset)
        {
            if (offset < 0 || offset > bytes.Length)
            {
                throw new IndexOutOfRangeException();
            }

            if (other is Slice8 s)
            {
                return Slice8.ByteRegionEquals(bytes, 0, bytes.Length, s.ByteArray, s.Start, s.End - s.Start, (int)offset);
            }

            if (other is Twine8 t)
            {
                return Slice8.ByteRegionEquals(bytes, 0, bytes.Length, t.bytes, 0, t.bytes.Length, (int)offset);
            }

            if (other is BMPString bstr)
            {
                string os = bstr.ToString();   // backing string, no copy
                return Slice8.CharRegionEquals(bytes, 0, bytes.Length, os, 0, os.Length, (int)offset);
            }

            if (other is BMPSlice bsl)
            {
                return Slice8.CharRegionEquals(bytes, 0, bytes.Length, bsl.Backing, bsl.Start, bsl.End - bsl.Start, (int)offset);
            }

            return base.HasSubstring(other, offset);
        }

        public override int CompareTo(UnicodeString other)
        {
            byte[] b;
            int bs, be;
            if (other is Twine8 o)
            {
                b = o.bytes;
                bs = 0;
                be = b.Length;
            }
            else if (other is Slice8 sl)
            {
                // cross-width byte compare: tree text (Slice8) vs a materialized Twine8
                b = sl.ByteArray;
                bs = sl.Start;
                be = sl.End;
            }
            else
            {
                return base.CompareTo(other);
            }

            byte[] a = bytes;
            int i = 0;
            int j = bs;
            while (i < a.Length && j < be)
            {
                int diff = (a[i++] & 0xff) - (b[j++] & 0xff);
                if (diff != 0)
                {
                    return diff;
                }
            }

            return a.Length.CompareTo(be - bs);
        }

        public override string ToString()
        {
            return new string(Array.ConvertAll(bytes, (b) => (char)b));
        }

        public override long IndexWhere(Func<int, bool> predicate, long from)
        {
            for (int i = requireNonNegativeInt(from); i < bytes.Length; i++)
            {
                OutSmart.DAXon.Core.Controller.CheckActiveTimeoutEvery64K(i);
                if (predicate(bytes[i] & 0xff))
                {
                    return i;
                }
            }

            return -1;
        }

        private sealed class AnonymousIntIterator : AbstractIntIterator
        {

            private readonly Twine8 parent;
            int i;
            public AnonymousIntIterator(Twine8 parent)
            {
                this.parent = parent;
            }
            public override bool HasNext()
            {
                return i < parent.bytes.Length;
            }

            public override int Next()
            {
                return parent.bytes[i++] & 0xff;
            }
        }
    }
}