////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using OutSmart.DAXon.Collections;

namespace OutSmart.DAXon.Text
{
    /// <summary>
    /// A zero-copy slice of a surrogate-free .NET string: BMPString's contract over a
    /// [start, end) window of the backing string. Produced by the tokenizer fast path --
    /// a token keeps a view of its line instead of a copied substring, which is what
    /// matters when millions of tokens are retained at once (array{tokenize(...)}).
    /// </summary>
    internal sealed class BMPSlice : UnicodeString
    {
        private readonly string _s;
        private readonly int _start;
        private readonly int _end;
        private int cachedHash; // 0 = not yet computed (benign race: recompute is idempotent)

        public override int Width => 16;
        public string Backing => _s;
        public int Start => _start;
        public int End => _end;

        public BMPSlice(string s, int start, int end)
        {
            _s = s;
            _start = start;
            _end = end;
        }

        public override long Length() => _end - _start;
        public override bool IsEmpty() => _end == _start;
        public override int CodePointAt(long index)
        {
            long i = _start + index;
            return index >= 0 && i < _end ? _s[(int)i] : -1;
        }

        public override UnicodeString Substring(long start, long end) => new BMPSlice(_s, _start + (int)start, _start + (int)end);
        public override UnicodeString Concat(UnicodeString other) => new BMPString(ToString() + other?.ToString());
        public override long IndexOf(int codePoint) => IndexOf(codePoint, 0);
        public override long IndexOf(int codePoint, long from)
        {
            if (from < 0)
            {
                from = 0;
            }

            int abs = _start + (int)from;
            if (abs >= _end)
            {
                return -1;
            }

            int i = _s.IndexOf((char)codePoint, abs, _end - abs);
            return i < 0 ? -1 : i - _start;
        }

        public override IIntIterator CodePoints() => new StrCodePointIterator(ToString());
        public override long IndexWhere(Func<int, bool> predicate, long from)
        {
            for (int i = _start + (int)from; i < _end; i++)
            {
                OutSmart.DAXon.Core.Controller.CheckActiveTimeoutEvery64K(i);
                if (predicate(_s[i]))
                {
                    return i - _start;
                }
            }

            return -1;
        }

        public override string ToString() => _s.Substring(_start, _end - _start);
        public override void Copy16bit(char[] target, int offset)
        {
            _s.CopyTo(_start, target, offset, _end - _start);
        }

        public override void Copy32bit(int[] target, int offset)
        {
            for (int i = _start; i < _end; i++)
            {
                target[offset + i - _start] = _s[i];
            }
        }

        // Byte-identical to the base codepoint hash without the CodePoints() iterator: the window is
        // surrogate-free, so each UTF-16 unit IS its codepoint (< 0x10000).
        public override int GetHashCode()
        {
            if (cachedHash != 0)
            {
                return cachedHash;
            }

            int h = 0;
            string s = _s;
            for (int i = _start; i < _end; i++)
            {
                h = 31 * h + s[i];
            }

            return cachedHash = h;
        }

        // Same-type window compare == the base codepoint compare, without two CodePoints() iterators.
        // The window is surrogate-free (astral content uses Width 24), and Width<=16 reps are likewise
        // surrogate-free, so each UTF-16 unit IS its codepoint and an ordinal char compare is codepoint-
        // order-identical to the base. Hot in xsl:sort/fn:sort over tokenized text (millions of BMPSlice
        // keys); the base compare allocated two codepoint iterators per comparison. Mirrors BMPString.
        public override int CompareTo(UnicodeString other)
        {
            if (other is BMPSlice bs)
            {
                string a = _s, b = bs._s;
                int i = _start, j = bs._start;
                while (i < _end && j < bs._end)
                {
                    int d = a[i++] - b[j++];
                    if (d != 0)
                    {
                        return d;
                    }
                }

                return (_end - _start) - (bs._end - bs._start);
            }

            if (other != null && other.Width <= 16)
            {
                return string.CompareOrdinal(ToString(), other.ToString());
            }

            return base.CompareTo(other);
        }

        // Same-type window equality == the base codepoint compare, without two CodePoints() iterators.
        public override bool Equals(object obj)
        {
            if (obj is BMPSlice other)
            {
                int n = _end - _start;
                if (n != other._end - other._start)
                {
                    return false;
                }

                for (int i = 0; i < n; i++)
                {
                    if (_s[_start + i] != other._s[other._start + i])
                    {
                        return false;
                    }
                }

                return true;
            }

            return base.Equals(obj);
        }
    }
}
