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

    internal sealed class BMPString : UnicodeString
    {
        private readonly string _s;
        private sbyte latin1; // 0 = unknown, 1 = every char <= 0xFF, 2 = wider (benign race: recompute is idempotent)
        private int cachedHash; // 0 = not yet computed (benign race: recompute is idempotent)
        public override int Width => 16;

        // Literals are single instances appended many times, so the scan runs once
        public bool IsLatin1
        {
            get
            {
                sbyte v = latin1;
                if (v == 0)
                {
                    v = 1;
                    for (int i = 0; i < _s.Length; i++)
                    {
                        if (_s[i] > 'ÿ')
                        {
                            v = 2;
                            break;
                        }
                    }

                    latin1 = v;
                }

                return v == 1;
            }
        }
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public BMPString(string s)
        {
            _s = s ?? "";
            if (_s.Length >= OutSmart.DAXon.Core.MemoryLedger.BigChars)
            {
                OutSmart.DAXon.Core.MemoryLedger.HoldText(_s);
            }
        }

        public static UnicodeString Of(string s) => new BMPString(s);
        public override long Length() => _s.Length;
        public override bool IsEmpty() => _s.Length == 0;
        public override int CodePointAt(long index) => index >= 0 && index < _s.Length ? _s[(int)index] : -1;
        public override UnicodeString Substring(long start, long end) => new BMPString(_s.Substring((int)start, (int)(end - start)));
        // An operand wider than 16 bits brings surrogate pairs: the joined text is classified again, not labelled BMP.
        public override UnicodeString Concat(UnicodeString other)
        {
            if (other != null && other.Width > 16)
            {
                return StringTool.FromCharSequence(_s + other.ToString());
            }

            return new BMPString(_s + other?.ToString());
        }
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
        // The serializer scans every text node with it: a long string is one call, which looks at the limits as it goes.
        public override long IndexWhere(Func<int, bool> predicate, long from)
        {
            for (int i = (int)from; i < _s.Length; i++)
            {
                OutSmart.DAXon.Core.Controller.CheckActiveTimeoutEvery64K(i);
                if (predicate(_s[i]))
                {
                    return i;
                }
            }

            return -1;
        }
        public override string ToString() => _s;
        // Valid only for Latin1 content (the narrow-append path checks IsLatin1 first)
        public override void Copy8bit(byte[] target, int offset)
        {
            if (!IsLatin1)
            {
                throw new NotSupportedException();
            }

            for (int i = 0; i < _s.Length; i++)
            {
                target[offset + i] = (byte)_s[i];
            }
        }

        public override void Copy16bit(char[] target, int offset) { _s.CopyTo(0, target, offset, _s.Length); }
        // NOTE: no Copy24bit override - the former empty-body override shadowed the base
        // implementation, so appending a BMPString to a 24-bit segment wrote nothing
        public override void Copy32bit(int[] target, int offset)
        {
            for (int i = 0; i < _s.Length; i++)
            {
                target[offset + i] = _s[i];
            }
        }

        // Byte-identical to the base codepoint hash but without the per-char CodePoints() iterator:
        // BMPString is surrogate-free, so each UTF-16 unit IS its codepoint (< 0x10000). Cached because
        // map keys (HashTrieMap hashes the UnicodeString directly) are hashed on every lookup.
        public override int GetHashCode()
        {
            if (cachedHash != 0)
            {
                return cachedHash;
            }

            int h = 0;
            string s = _s;
            for (int i = 0; i < s.Length; i++)
            {
                h = 31 * h + s[i];
            }

            return cachedHash = h;
        }

        // Same-type ordinal equality (both operands surrogate-free) == the base codepoint compare,
        // without two CodePoints() iterators; other representations fall back to the base.
        public override bool Equals(object obj)
        {
            if (obj is BMPString other)
            {
                return string.Equals(_s, other._s, StringComparison.Ordinal);
            }

            // Byte reps carry their own BMP fast case; delegate so literal-vs-tree-text pairs
            // take it regardless of operand order (base Equals allocates codepoint iterators).
            if (obj is Slice8 || obj is Twine8)
            {
                return obj.Equals(this);
            }

            return base.Equals(obj);
        }

        // BMP chars are surrogate-free, so char index == codepoint index: ordinal string search
        // replaces the base codepoint-iterator scan. Guard order mirrors the base exactly.
        public override long IndexOf(UnicodeString other, long from)
        {
            if (from < 0 || from >= _s.Length)
            {
                return -1;
            }

            if (other.IsEmpty())
            {
                return from;
            }

            if (other is BMPString b)
            {
                return _s.IndexOf(b._s, (int)from, StringComparison.Ordinal);
            }

            return base.IndexOf(other, from);
        }

        public override bool HasSubstring(UnicodeString other, long offset)
        {
            if (offset < 0 || offset > _s.Length)
            {
                throw new IndexOutOfRangeException();
            }

            if (other is BMPString b)
            {
                return b._s.Length + offset <= _s.Length
                    && string.CompareOrdinal(_s, (int)offset, b._s, 0, b._s.Length) == 0;
            }

            return base.HasSubstring(other, offset);
        }
    }
}
