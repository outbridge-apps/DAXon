////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Collections.Generic;
using System.Text;

namespace OutSmart.DAXon.Internal
{
    // A single-byte code page of the platform with its table put right. .NET gives a byte the code page leaves undefined
    // a C1 control or a private-use character, so bytes that are no text read as text, and it carries iso-8859-7 and
    // iso-8859-8 before their later editions (no euro) and koi8-u with two letters of KOI8-RU.
    internal sealed class CorrectedCodePage : Encoding
    {
        private const char Undefined = '￿';

        private readonly char[] toChar = new char[256];
        private readonly Dictionary<char, byte> toByte = new Dictionary<char, byte>();
        private readonly string webName;

        internal CorrectedCodePage(int codePage, bool c1IsUndefined, params int[] changes)
            : this(null, codePage, c1IsUndefined, changes)
        {
        }

        // A name of its own for a table the platform reads under another code page's (tis-620 as windows-874).
        internal CorrectedCodePage(string name, int codePage, bool c1IsUndefined, params int[] changes)
            : base(codePage, new EncoderReplacementFallback("?"), new DecoderReplacementFallback("�"))
        {
            Encoding platform = (Encoding)GetEncoding(codePage).Clone();
            platform.DecoderFallback = DecoderFallback.ExceptionFallback;
            webName = name ?? platform.WebName;
            for (int b = 0; b < 256; b++)
            {
                toChar[b] = TableEntry(platform, (byte)b, c1IsUndefined);
            }

            for (int i = 0; i < changes.Length; i += 2)
            {
                toChar[changes[i]] = (char)changes[i + 1];
            }

            for (int b = 255; b >= 0; b--)
            {
                if (toChar[b] != Undefined)
                {
                    toByte[toChar[b]] = (byte)b;
                }
            }
        }

        private static char TableEntry(Encoding platform, byte b, bool c1IsUndefined)
        {
            string s;
            try
            {
                s = platform.GetString(new[] { b });
            }
            catch (DecoderFallbackException)
            {
                return Undefined;
            }

            char c = s.Length == 1 ? s[0] : Undefined;
            bool undefined = (c >= '' && c <= '') || (c1IsUndefined && c >= '\u0080' && c <= '\u009F');
            return undefined ? Undefined : c;
        }

        public override bool IsSingleByte => true;

        // The base class asks a table that on .NET 8 and later holds only the encodings of the core library.
        public override string WebName => webName;

        public override int GetMaxByteCount(int charCount)
        {
            return (charCount + 1) * Math.Max(1, EncoderFallback.MaxCharCount);
        }

        public override int GetMaxCharCount(int byteCount)
        {
            return Math.Max(1, byteCount) * Math.Max(1, DecoderFallback.MaxCharCount);
        }

        public override int GetByteCount(char[] chars, int index, int count)
        {
            return Encode(chars, index, count, null, 0);
        }

        // A writer encodes its buffer a chunk at a time: a pair split between two chunks is one character, not two lone halves.
        public override Encoder GetEncoder()
        {
            return new PairKeepingEncoder(this);
        }

        private sealed class PairKeepingEncoder : Encoder
        {
            private readonly CorrectedCodePage page;
            private char pending;   // a high surrogate that ended the last chunk

            internal PairKeepingEncoder(CorrectedCodePage page)
            {
                this.page = page;
            }

            public override int GetByteCount(char[] chars, int index, int count, bool flush)
            {
                return Encode(chars, index, count, flush, null, 0, out char _);
            }

            public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex, bool flush)
            {
                int written = Encode(chars, charIndex, charCount, flush, bytes, byteIndex, out char held);
                pending = held;
                return written;
            }

            public override void Reset()
            {
                pending = '\0';
            }

            private int Encode(char[] chars, int index, int count, bool flush, byte[] bytes, int byteIndex, out char held)
            {
                char[] source = chars;
                int start = index;
                int length = count;
                if (pending != '\0')
                {
                    source = new char[count + 1];
                    source[0] = pending;
                    Array.Copy(chars, index, source, 1, count);
                    start = 0;
                    length = count + 1;
                }

                held = '\0';
                if (!flush && length > 0 && char.IsHighSurrogate(source[start + length - 1]))
                {
                    held = source[start + length - 1];
                    length--;
                }

                return page.Encode(source, start, length, bytes, byteIndex);
            }
        }

        public override int GetBytes(char[] chars, int charIndex, int charCount, byte[] bytes, int byteIndex)
        {
            return Encode(chars, charIndex, charCount, bytes, byteIndex);
        }

        public override int GetCharCount(byte[] bytes, int index, int count)
        {
            return Decode(bytes, index, count, null, 0);
        }

        public override int GetChars(byte[] bytes, int byteIndex, int byteCount, char[] chars, int charIndex)
        {
            return Decode(bytes, byteIndex, byteCount, chars, charIndex);
        }

        // Writes to output from at, or only counts when it is null. Returns the bytes written.
        private int Encode(char[] chars, int index, int count, byte[] output, int at)
        {
            int start = at;
            int end = index + count;
            EncoderFallbackBuffer fallback = null;
            for (int i = index; i < end; i++)
            {
                char c = chars[i];
                if (c < 0x80)
                {
                    Put(output, ref at, (byte)c);
                }
                else if (toByte.TryGetValue(c, out byte b))
                {
                    Put(output, ref at, b);
                }
                else
                {
                    fallback = fallback ?? EncoderFallback.CreateFallbackBuffer();
                    bool pair = char.IsHighSurrogate(c) && i + 1 < end && char.IsLowSurrogate(chars[i + 1]);
                    if (pair ? fallback.Fallback(c, chars[i + 1], i) : fallback.Fallback(c, i))
                    {
                        while (fallback.Remaining > 0)
                        {
                            char r = fallback.GetNextChar();
                            Put(output, ref at, r < 0x80 ? (byte)r : toByte.TryGetValue(r, out byte rb) ? rb : (byte)'?');
                        }
                    }

                    i += pair ? 1 : 0;
                }
            }

            return at - start;
        }

        private static void Put(byte[] output, ref int at, byte b)
        {
            if (output != null)
            {
                if (at >= output.Length)
                {
                    throw new ArgumentException("The output buffer is too small", nameof(output));
                }

                output[at] = b;
            }

            at++;
        }

        private int Decode(byte[] bytes, int index, int count, char[] output, int at)
        {
            int start = at;
            int end = index + count;
            char[] table = toChar;
            DecoderFallbackBuffer fallback = null;
            int i = index;
            while (true)
            {
                // A run of defined bytes, with no call and no test of the room per byte: the text decodes twice as fast.
                if (output == null)
                {
                    while (i < end && table[bytes[i]] != Undefined)
                    {
                        i++;
                        at++;
                    }
                }
                else
                {
                    int stop = end - i <= output.Length - at ? end : i + Math.Max(0, output.Length - at);
                    while (i < stop)
                    {
                        char c = table[bytes[i]];
                        if (c == Undefined)
                        {
                            break;
                        }

                        output[at++] = c;
                        i++;
                    }
                }

                if (i >= end)
                {
                    return at - start;
                }

                if (table[bytes[i]] != Undefined)
                {
                    throw new ArgumentException("The output buffer is too small", nameof(output));
                }

                fallback = fallback ?? DecoderFallback.CreateFallbackBuffer();
                if (fallback.Fallback(new[] { bytes[i] }, i))
                {
                    while (fallback.Remaining > 0)
                    {
                        Put(output, ref at, fallback.GetNextChar());
                    }
                }

                i++;
            }
        }

        private static void Put(char[] output, ref int at, char c)
        {
            if (output != null)
            {
                if (at >= output.Length)
                {
                    throw new ArgumentException("The output buffer is too small", nameof(output));
                }

                output[at] = c;
            }

            at++;
        }
    }

    // One '?' for a character the encoding cannot hold. The platform's own replacement writes two for a supplementary
    // character, and its default on .NET Framework writes a look-alike ('A' for U+0100) with no sign of a change.
    internal sealed class OneMarkPerCharacterFallback : EncoderFallback
    {
        internal static readonly OneMarkPerCharacterFallback Instance = new OneMarkPerCharacterFallback();

        public override int MaxCharCount => 1;

        public override EncoderFallbackBuffer CreateFallbackBuffer()
        {
            return new Buffer();
        }

        private sealed class Buffer : EncoderFallbackBuffer
        {
            private int left;

            public override int Remaining => left;

            public override bool Fallback(char charUnknown, int index)
            {
                left = 1;
                return true;
            }

            public override bool Fallback(char charUnknownHigh, char charUnknownLow, int index)
            {
                left = 1;
                return true;
            }

            public override char GetNextChar()
            {
                if (left == 0)
                {
                    return '\0';
                }

                left = 0;
                return '?';
            }

            public override bool MovePrevious()
            {
                if (left != 0)
                {
                    return false;
                }

                left = 1;
                return true;
            }

            public override void Reset()
            {
                left = 0;
            }
        }
    }
}
