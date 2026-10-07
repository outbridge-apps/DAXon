////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Collections;using OutSmart.DAXon.Functions;

using static OutSmart.DAXon.Text.StrHelpers;


using System;

using System.Collections.Generic;

using System.Collections.ObjectModel;

using System.Linq;

using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
using System.IO;
namespace OutSmart.DAXon.Text
{
    internal sealed class Slice16 : UnicodeString
    {
        private char[] chars;
        private int start;
        private int end;
        private int cachedHash;

        public override int Width => 16;

        public char[] CharArray => chars;

        public int Start => start;

        public int End => end;
        public Slice16(char[] chars, int start, int end)
        {
            this.chars = chars;
            this.start = start;
            this.end = end;
        }

        public override long Length()
        {
            return end - start;
        }

        public override long IndexOf(int codePoint, long from)
        {
            if (codePoint > 65535)
            {
                return -1;
            }

            char b = (char)(codePoint & 0xffff);
            int limit = end;
            for (int i = start + requireNonNegativeInt(from); i < limit; i++)
            {
                if (chars[i] == b)
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

            return (chars[start + index32]);
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
                return new Slice16(chars, RequireInt(start) + this.start, RequireInt(end) + this.start);
            }
        }

        public override long IndexWhere(Func<int, bool> predicate, long from)
        {
            for (int i = requireNonNegativeInt(from) + start; i < end; i++)
            {
                OutSmart.DAXon.Core.Controller.CheckActiveTimeoutEvery64K(i);
                if (predicate(chars[i]))
                {
                    return i - start;
                }
            }

            return -1;
        }

        public override void Copy16bit(char[] target, int offset)
        {
            Array.Copy(chars, start, target, offset, end - start);
        }

        public override void Copy24bit(byte[] target, int offset)
        {
            for (int i = start, j = offset; i < end;)
            {
                char c = chars[i++];
                target[j++] = 0;
                target[j++] = (byte)(c >> 8);
                target[j++] = (byte)(c & 0xff);
            }
        }

        public override void Copy32bit(int[] target, int offset)
        {
            for (int i = start, j = offset; i < end;)
            {
                target[j++] = chars[i++];
            }
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
            for (int i = start; i < end; i++)
            {
                int b = chars[i];
                h = 31 * h + b;
            }

            return cachedHash = h;
        }

        /// <summary>
        /// Convert to a string.
        /// </summary>
        public override string ToString()
        {
            return new string(chars, start, end - start);
        }

        private sealed class AnonymousIntIterator : AbstractIntIterator
        {

            private readonly Slice16 parent;
            int i;
            public AnonymousIntIterator(Slice16 parent)
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
                return parent.chars[i++];
            }
        }
    }
}
