////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Serialization.CharCodes;
using OutSmart.DAXon.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Text
{
    /// <summary>
    /// A UnicodeString containing a single codepoint
    /// </summary>
    internal sealed class UnicodeChar : UnicodeString
    {
        private readonly int codepoint;

        public int Codepoint => codepoint;

        public override int Width
        {
            get
            {
                if (codepoint < 128)
                {
                    return 7;
                }
                else if (codepoint < 256)
                {
                    return 8;
                }
                else if (codepoint < 65536)
                {
                    return 16;
                }
                else
                {
                    return 24;
                }
            }
        }
        public UnicodeChar(int codepoint)
        {
            this.codepoint = codepoint;
        }

        public override IIntIterator CodePoints()
        {
            return new IntSingletonIterator(codepoint);
        }

        public override long Length()
        {
            return 1;
        }

        public override long IndexOf(int codePoint, long from)
        {
            return (this.codepoint == codePoint && from <= 0) ? 0 : -1;
        }

        public override int CodePointAt(long index)
        {
            if (index == 0)
            {
                return codepoint;
            }
            else
            {
                throw new IndexOutOfRangeException("Only valid index for a single-character string is zero");
            }
        }

        public override UnicodeString Substring(long start, long end)
        {
            CheckSubstringBounds(start, end);
            if (start == 0 && end == 1)
            {
                return this;
            }
            else
            {
                return EmptyUnicodeString.GetInstance();
            }
        }

        public override long IndexWhere(Func<int, bool> predicate, long from)
        {
            return (from == 0 && predicate(codepoint)) ? 0 : -1;
        }

        public override string ToString()
        {
            if (codepoint < 65536)
            {
                return "" + (char)codepoint;
            }
            else
            {
                return "" + UTF16CharacterSet.HighSurrogate(codepoint) + UTF16CharacterSet.LowSurrogate(codepoint);
            }
        }

        public override void Copy8bit(byte[] target, int offset)
        {
            target[offset] = (byte)(codepoint & 0xFF);
        }

        public override void Copy16bit(char[] target, int offset)
        {
            target[offset] = (char)codepoint;
        }

        public override void Copy24bit(byte[] target, int offset)
        {
            target[offset] = (byte)(codepoint >> 16);
            target[offset + 1] = (byte)(codepoint >> 8);
            target[offset + 2] = (byte)(codepoint & 0xff);
        }

        public override void Copy32bit(int[] target, int offset)
        {
            target[offset] = codepoint;
        }
    }
}
