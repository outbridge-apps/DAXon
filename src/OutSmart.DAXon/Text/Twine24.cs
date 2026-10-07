////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Serialization.CharCodes;
using static OutSmart.DAXon.Text.StrHelpers;

using OutSmart.DAXon.Collections;

using OutSmart.DAXon.Internal.Collections;


using System;

using System.Collections.Generic;

using System.Collections.ObjectModel;

using System.Linq;

using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Text
{
    internal sealed class Twine24 : UnicodeString
    {
        protected byte[] bytes;
        protected int cachedHash = 0;

        public override int Width => 24;
        public Twine24(byte[] bytes)
        {
            this.bytes = bytes; //        if (Configuration.isAssertionsEnabled()) {
            Held();
        }

        public Twine24(int[] codePoints, int used)
        {
            bytes = new byte[used * 3];
            for (int i = 0, j = 0; i < used; i++, j += 3)
            {
                int c = codePoints[i];
                bytes[j] = (byte)((c >> 16) & 0xff);
                bytes[j + 1] = (byte)((c >> 8) & 0xff);
                bytes[j + 2] = (byte)(c & 0xff);
            }

            Held();
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void Held()
        {
            if (bytes.Length >= 3 * OutSmart.DAXon.Core.MemoryLedger.BigChars)
            {
                OutSmart.DAXon.Core.MemoryLedger.HoldText(bytes);
            }
        }

        public override long Length()
        {
            return bytes.Length / 3;
        }

        public override int Length32()
        {
            return bytes.Length / 3;
        }

        public override UnicodeString Substring(long start, long end)
        {
            int start32 = RequireInt(start);
            int end32 = RequireInt(end);
            int len = Length32();
            CheckSubstringBounds(start, end);
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
                return new Slice24(bytes, start32, end32);
            }
        }

        public override int CodePointAt(long index)
        {
            int index32 = RequireInt(index);
            if (index32 < 0 || index32 >= Length32())
            {
                throw new IndexOutOfRangeException();
            }

            int offset = index32 * 3;
            return ((bytes[offset] << 16 | (bytes[offset + 1] & 0xff) << 8) | (bytes[offset + 2] & 0xff)) & 0xffffff;
        }

        public override long IndexOf(int code, long from)
        {
            int from32 = RequireNonNegativeInt(from);
            if (from32 >= Length32())
            {
                return -1;
            }

            int last = bytes.Length;
            if (code < 0 || code > 0xffffff)
            {
                return -1;
            }

            byte a = (byte)(code >> 16 & 0xff);
            byte b = (byte)(code >> 8 & 0xff);
            byte c = (byte)(code & 0xff);
            for (int i = from32 * 3; i < last; i += 3)
            {
                if (bytes[i + 2] == c && bytes[i + 1] == b && bytes[i] == a)
                {
                    return (i / 3);
                }
            }

            return -1;
        }

        public override long IndexOf(UnicodeString other, long from)
        {
            int from32 = RequireInt(from);
            if (from32 < 0)
            {
                from32 = 0;
            }
            else if (from32 >= Length32())
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
            while (from32 <= lastPossible)
            {
                int i = RequireInt(IndexOf(initial, from32));
                if (i < 0)
                {
                    return -1;
                }

                if (HasSubstring(other, i))
                {
                    return i;
                }

                from32 = i + 1;
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

            int h = 0;
            int end = bytes.Length;
            for (int i = 0; i < end; i += 3)
            {
                int cp = ((bytes[i] << 16 | (bytes[i + 1] & 0xff) << 8) | (bytes[i + 2] & 0xff)) & 0xffffff;
                if ((cp & 0xff0000) != 0)
                {
                    h = 31 * h + UTF16CharacterSet.HighSurrogate(cp);
                    h = 31 * h + UTF16CharacterSet.LowSurrogate(cp);
                }
                else
                {
                    h = 31 * h + cp;
                }
            }

            return cachedHash = h;
        }

        public override bool Equals(object o)
        {
            if (o is Twine24)
            {
                if (GetHashCode() != o.GetHashCode())
                {
                    return false;
                }

                return ArrayTools.Equals(bytes, ((Twine24)o).bytes);
            }

            return base.Equals(o);
        }

        public override int CompareTo(UnicodeString other)
        {
            if (other is Twine24)
            {
                Twine24 o = (Twine24)other;
                byte[] a = bytes;
                byte[] b = o.bytes;
                int len = Math.Min(a.Length, b.Length);
                for (int i = 0; i < len; i++)
                {
                    int diff = (a[i] & 0xff) - (b[i] & 0xff);
                    if (diff != 0)
                    {
                        return diff;
                    }
                }

                return a.Length.CompareTo(b.Length);
            }
            else
            {
                return base.CompareTo(other);
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder(Length32());
            IIntIterator iter = CodePoints();
            while (iter.MoveNext())
            {
                int x = iter.Current;
                sb.AppendCodePoint(x);
            }

            return sb.ToString();
        }

        // Was `protected virtual` — hid the public virtual UnicodeString.Copy24bit instead of overriding
        // it, so a base-typed call (LargeTextBuffer.ExtendLastSegment on astral-plane text) hit the base
        // and threw. Override it.
        public override void Copy24bit(byte[] target, int offset)
        {
            Array.Copy(bytes, 0, target, offset, bytes.Length);
        }

        public override long IndexWhere(Func<int, bool> predicate, long from)
        {
            for (int i = requireNonNegativeInt(from); i < bytes.Length / 3; i++)
            {
                OutSmart.DAXon.Core.Controller.CheckActiveTimeoutEvery64K(i);
                int offset = i * 3;
                int cp = ((bytes[offset] << 16 | (bytes[offset + 1] & 0xff) << 8) | (bytes[offset + 2] & 0xff)) & 0xffffff;
                if (predicate(cp))
                {
                    return i;
                }
            }

            return -1;
        }

        private sealed class AnonymousIntIterator : AbstractIntIterator
        {

            private readonly Twine24 parent;
            int i = 0;
            public AnonymousIntIterator(Twine24 parent)
            {
                this.parent = parent;
            }
            public override bool HasNext()
            {
                return i < parent.bytes.Length;
            }

            public override int Next()
            {
                int result = ((parent.bytes[i] & 0xff) << 16) | ((parent.bytes[i + 1] & 0xff) << 8) | ((parent.bytes[i + 2] & 0xff));
                i += 3;
                return result;
            }
        }
    }
}