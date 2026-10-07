////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Collections;

namespace OutSmart.DAXon.Text
{
    internal sealed class Twine16 : UnicodeString
    {
        private readonly string _s;
        public char[] CharArray => _s.ToCharArray(); // batch6: real UTF8Writer fast path
        public override int Width => 16;
        public Twine16(string s)
        {
            _s = s ?? "";
            Held();
        }

        public Twine16(char[] chars)
        {
            _s = new string(chars);
            Held();
        }

        public Twine16(char[] chars, int offset, int length)
        {
            _s = new string(chars, offset, length);
            Held();
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        private void Held()
        {
            if (_s.Length >= OutSmart.DAXon.Core.MemoryLedger.BigChars)
            {
                OutSmart.DAXon.Core.MemoryLedger.HoldText(_s);
            }
        }

        public override long Length() => _s.Length;
        public override bool IsEmpty() => _s.Length == 0;
        public override int CodePointAt(long index) => _s[(int)index];
        public override UnicodeString Substring(long start, long end) => new Twine16(_s.Substring((int)start, (int)(end - start)));
        public override UnicodeString Concat(UnicodeString other) => new Twine16(_s + other?.ToString());
        // CompareOrdinal is codepoint-correct only BMP-vs-BMP; against an astral operand (surrogates
        // 0xD800-0xDBFF < 0xE000-0xFFFF) it mis-orders, so defer to the base codepoint comparison then.
        public override int CompareTo(UnicodeString other)
        {
            if (other != null && other.Width <= 16)
            {
                return string.CompareOrdinal(_s, other.ToString());
            }

            return base.CompareTo(other);
        }
        public override long IndexOf(int codePoint) => _s.IndexOf((char)codePoint);
        public override long IndexOf(int codePoint, long from) => _s.IndexOf((char)codePoint, (int)from);
        public override IIntIterator CodePoints() => new StrCodePointIterator(ToString());
        public override long IndexWhere(Func<int, bool> predicate, long from) { for (int i = (int)from; i < _s.Length; i++) { if (predicate(_s[i])) return i; } return -1; }
        public override string ToString() => _s;
        public override void Copy16bit(char[] target, int offset) { _s.CopyTo(0, target, offset, _s.Length); }
        public override void Copy24bit(byte[] target, int offset) { }
        public override void Copy32bit(int[] target, int offset) { for (int i = 0; i < _s.Length; i++) target[offset + i] = _s[i]; }
    }
}
