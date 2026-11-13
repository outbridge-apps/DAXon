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

            return Slice8.TryIndexOf(bytes, 0, bytes.Length, other, (int)from, out long index) ? index : base.IndexOf(other, from);
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

            return cachedHash = Slice8.Hash(bytes, 0, bytes.Length);
        }

        public override bool Equals(object o)
        {
            return o is UnicodeString u && Slice8.TryEquals(bytes, 0, bytes.Length, u, out bool equal) ? equal : base.Equals(o);
        }

        public override bool HasSubstring(UnicodeString other, long offset)
        {
            if (offset < 0 || offset > bytes.Length)
            {
                throw new IndexOutOfRangeException();
            }

            return Slice8.TryHasSubstring(bytes, 0, bytes.Length, other, (int)offset, out bool has) ? has : base.HasSubstring(other, offset);
        }

        public override int CompareTo(UnicodeString other)
        {
            return Slice8.TryCompare(bytes, 0, bytes.Length, other, out int order) ? order : base.CompareTo(other);
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