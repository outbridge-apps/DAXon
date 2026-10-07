////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
//
// Faithful port of net.sf.saxon.str.Slice24 (was a hollow stub that dropped astral
// content: Length()=>0, CodePoints() threw). Fixes str1-007 (a\u{1F600}b -> string-length 3).
// Mirrors sibling Slice8.cs / Twine24.cs for the C# UnicodeString API.
using OutSmart.DAXon.Serialization.CharCodes;
using OutSmart.DAXon.Collections;
using OutSmart.DAXon.Internal;
using System;
using System.Text;
using static OutSmart.DAXon.Text.StrHelpers;

namespace OutSmart.DAXon.Text
{
    /// <summary>
    /// A Unicode string consisting of 24-bit characters, implemented as a range
    /// of an underlying byte array holding three bytes per codepoint.
    /// </summary>
    internal sealed class Slice24 : UnicodeString
    {
        private readonly byte[] bytes;
        private readonly int start;
        private readonly int end;
        private int cachedHash;

        public override int Width => 24;

        /// <summary>
        /// Create a slice of an underlying byte array.
        /// </summary>
        /// <param name="bytes">the byte array, containing Unicode codepoints, three bytes per codepoint</param>
        /// <param name="start">the codepoint offset of the first character within the byte array</param>
        /// <param name="end">the codepoint offset of the first excluded character, so the length of the string
        /// is <c>end-start</c></param>
        public Slice24(byte[] bytes, int start, int end)
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
            byte b0 = (byte)((codePoint >> 16) & 0xff);
            byte b1 = (byte)((codePoint >> 8) & 0xff);
            byte b2 = (byte)(codePoint & 0xff);
            for (int i = (start + RequireNonNegativeInt(from)) * 3; i < end * 3; i += 3)
            {
                if (bytes[i + 2] == b2 && bytes[i + 1] == b1 && bytes[i] == b0)
                {
                    return i / 3 - start;
                }
            }

            return -1;
        }

        /// <summary>
        /// Get the position of the first codepoint satisfying the given predicate,
        /// starting the search at a given position in the string.
        /// </summary>
        /// <param name="predicate">condition that the codepoint must satisfy</param>
        /// <param name="from">the position from which the search should start (0-based)</param>
        /// <returns>the position (0-based) of the first codepoint to match the predicate, or -1 if not found</returns>
        public override long IndexWhere(Func<int, bool> predicate, long from)
        {
            for (int i = (start + RequireInt(from)) * 3; i < end * 3; i += 3)
            {
                OutSmart.DAXon.Core.Controller.CheckActiveTimeoutEvery64K(i);
                int cp = ((bytes[i] << 16 | (bytes[i + 1] & 0xff) << 8) | (bytes[i + 2] & 0xff)) & 0xffffff;
                if (predicate(cp))
                {
                    return i / 3 - start;
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

            int offset = (start + index32) * 3;
            return ((bytes[offset] << 16 | (bytes[offset + 1] & 0xff) << 8) | (bytes[offset + 2] & 0xff)) & 0xffffff;
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
                return new Slice24(bytes, RequireInt(start) + this.start, RequireInt(end) + this.start);
            }
        }

        public override void Copy24bit(byte[] target, int offset)
        {
            Array.Copy(bytes, start * 3, target, offset, (end - start) * 3);
        }

        public override IIntIterator CodePoints()
        {
            return new AnonymousIntIterator(this);
        }

        /// <summary>
        /// Compute a hashCode. All implementations of <c>UnicodeString</c> use compatible hash codes and the
        /// hashing algorithm is therefore identical to that for <c>java.lang.String</c>. This means
        /// that for strings containing Astral characters, the hash code needs to be computed by decomposing
        /// an Astral character into a surrogate pair.
        /// </summary>
        public override int GetHashCode()
        {
            if (cachedHash != 0)
            {
                return cachedHash;
            }

            int h = 0;
            for (int i = start * 3; i < end * 3; i += 3)
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

        /// <summary>
        /// Display as a string.
        /// </summary>
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

        private sealed class AnonymousIntIterator : AbstractIntIterator
        {

            private readonly Slice24 parent;
            int i;
            int j;
            public AnonymousIntIterator(Slice24 parent)
            {
                this.parent = parent;
                this.i = parent.start * 3;
                this.j = parent.end * 3;
            }
            public override bool HasNext()
            {
                return i < j;
            }

            public override int Next()
            {
                int result = ((parent.bytes[i] & 0xff) << 16)
                        | ((parent.bytes[i + 1] & 0xff) << 8)
                        | ((parent.bytes[i + 2] & 0xff));
                i += 3;
                return result;
            }
        }
    }
}
