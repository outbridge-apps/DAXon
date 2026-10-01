////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// StringView — a UnicodeString view over a native C# string.
//
// Originally a BMP-only stub (Length/CodePointAt in UTF-16 units), which silently mis-measured
// any string with astral characters (format pictures, xsl:number digit families, string-length).
// Now codepoint-true: when the wrapped string contains surrogate pairs, an int[] codepoint
// expansion is computed once and all indexed members (Length, CodePointAt, Substring, IndexOf,
// Copy32bit) operate on codepoints, matching the upstream UnicodeString contract. BMP strings
// (the overwhelmingly common case) keep the zero-copy fast path.

using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Collections;

namespace OutSmart.DAXon.Text
{
    internal sealed class StringView : UnicodeString
    {
        private readonly string _s;
        private readonly int[] _cps; // non-null only when _s contains a surrogate pair
        public override int Width => _cps != null ? 24 : 16;

        public StringView(string s)
        {
            _s = s ?? "";
            if (StringTool.ContainsSurrogates(_s))
            {
                var list = new List<int>(_s.Length);
                for (int i = 0; i < _s.Length; i++)
                {
                    if (char.IsHighSurrogate(_s[i]) && i + 1 < _s.Length && char.IsLowSurrogate(_s[i + 1]))
                    {
                        list.Add(char.ConvertToUtf32(_s[i], _s[i + 1]));
                        i++;
                    }
                    else
                    {
                        list.Add(_s[i]);
                    }
                }

                _cps = list.ToArray();
            }
        }

        public static StringView Of(string s) => new StringView(s);
        // Tidy(UnicodeString) - normalizes / interns the string.
        public static UnicodeString Tidy(UnicodeString us) => us ?? (UnicodeString)new StringView("");
        public static UnicodeString Tidy(string s) => new StringView(s);
        public override long Length() => _cps != null ? _cps.Length : _s.Length;
        public override bool IsEmpty() => _s.Length == 0;
        public override int CodePointAt(long index)
        {
            if (_cps != null)
                return index >= 0 && index < _cps.Length ? _cps[(int)index] : -1;
            return index >= 0 && index < _s.Length ? _s[(int)index] : -1;
        }
        public override UnicodeString Substring(long start, long end)
        {
            if (_cps == null)
                return new StringView(_s.Substring((int)start, (int)(end - start)));
            var sb = new System.Text.StringBuilder();
            for (long i = start; i < end; i++)
                sb.Append(char.ConvertFromUtf32(_cps[i]));
            return new StringView(sb.ToString());
        }
        public override UnicodeString Concat(UnicodeString other) => new StringView(_s + other?.ToString());
        // string.CompareOrdinal is codepoint-correct for BMP text but mis-orders astral characters — a high
        // surrogate 0xD800-0xDBFF sorts BEFORE 0xE000-0xFFFF, so "" would wrongly compare greater than
        // an astral char. When either operand is astral, defer to the base codepoint-by-codepoint comparison
        // (K2-StringLT-1). GetWidth()<=16 means the string is entirely BMP.
        public override int CompareTo(UnicodeString other)
        {
            if (Width <= 16 && other != null && other.Width <= 16)
            {
                return string.CompareOrdinal(_s, other.ToString());
            }

            return base.CompareTo(other);
        }
        public override long IndexOf(int codePoint) => IndexOf(codePoint, 0);
        public override long IndexOf(int codePoint, long from)
        {
            if (_cps != null)
            {
                for (long i = Math.Max(from, 0); i < _cps.Length; i++)
                {
                    if (_cps[i] == codePoint) return i;
                }
                return -1;
            }
            if (codePoint > char.MaxValue)
                return -1;
            return _s.IndexOf((char)codePoint, (int)Math.Max(from, 0));
        }
        public override IIntIterator CodePoints() => new StrCodePointIterator(_s);
        public override long IndexWhere(Func<int, bool> predicate, long from)
        {
            if (_cps != null)
            {
                for (long i = Math.Max(from, 0); i < _cps.Length; i++)
                {
                    if (predicate(_cps[i])) return i;
                }
                return -1;
            }
            for (int i = (int)from; i < _s.Length; i++)
            {
                if (predicate(_s[i])) return i;
            }
            return -1;
        }
        public override string ToString() => _s;
        public override void Copy16bit(char[] target, int offset) { _s.CopyTo(0, target, offset, _s.Length); }
        public override void Copy24bit(byte[] target, int offset)
        {
            long len = Length();
            for (int i = 0, j = offset; i < len; i++)
            {
                int cp = _cps != null ? _cps[i] : _s[i];
                target[j++] = (byte)((cp >> 16) & 0xff);
                target[j++] = (byte)((cp >> 8) & 0xff);
                target[j++] = (byte)(cp & 0xff);
            }
        }
        public override void Copy32bit(int[] target, int offset)
        {
            if (_cps != null)
            {
                Array.Copy(_cps, 0, target, offset, _cps.Length);
                return;
            }
            for (int i = 0; i < _s.Length; i++)
                target[offset + i] = _s[i];
        }
    }
}
