////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Serialization.CharCodes;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Collections;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Text;
namespace OutSmart.DAXon.Text
{
    internal sealed class StringTool
    {
        public static int GetStringLength(string s)
        {
            int n = 0;
            for (int i = 0; i < s.Length; i++)
            {
                int c = s[i];
                if (c < 55296 || c > 56319)
                {
                    n++; // don't count high surrogates, i.e. D800 to DBFF
                }
            }

            return n;
        }

        public static int[] Expand(UnicodeString s)
        {
            int[] array = new int[s.Length32()];
            s.Copy32bit(array, 0);
            return array;
        }

        public static bool ContainsSurrogates(string str)
        {
            for (int i = 0; i < str.Length; i++)
            {
                if (UTF16CharacterSet.IsSurrogate(str[i]))
                {
                    return true;
                }
            }

            return false;
        }

        public static UnicodeString FromCodePoints(int[] codes, int used)
        {
            UnicodeBuilder sb = new UnicodeBuilder();
            for (int i = 0; i < used; i++)
            {
                sb.Append(codes[i]);
            }

            return sb.ToUnicodeString();
        }

        // Half a surrogate pair is no character: U+FFFD stands where one is. A string that has none is returned as it is.
        public static string WithoutHalfPairs(string s)
        {
            int at = HalfPairAt(s, 0);
            if (at < 0)
            {
                return s;
            }

            char[] chars = s.ToCharArray();
            while (at >= 0)
            {
                chars[at] = (char)0xFFFD;
                at = HalfPairAt(s, at + 1);
            }

            return new string(chars);
        }

        // Where the first surrogate at or after an index is that is not one of a pair, or -1.
        private static int HalfPairAt(string s, int from)
        {
            for (int i = from; i < s.Length; i++)
            {
                char c = s[i];
                if (c >= 0xD800 && c <= 0xDFFF)
                {
                    if (c <= 0xDBFF && i + 1 < s.Length && s[i + 1] >= 0xDC00 && s[i + 1] <= 0xDFFF)
                    {
                        i++;
                    }
                    else
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        public static UnicodeString FromCharSequence(string chars)
        {
            // One pass tells the string of no surrogate at all - nearly every string - from the rest. Upstream counts
            // the high ones only and takes whatever follows one for its other half: a string cut through a pair was
            // an index out of range, or lost the character after it.
            int surrogates = 0;
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (c >= 0xD800 && c <= 0xDFFF)
                {
                    surrogates++;
                }
            }

            if (surrogates == 0)
            {
                return new BMPString(chars);
            }

            chars = WithoutHalfPairs(chars);
            int uLength = StringTool.GetStringLength(chars);
            if (uLength == chars.Length)
            {

                // No surrogate pairs
                return new BMPString(chars);
            }
            else
            {
                byte[] triples = new byte[uLength * 3];
                for (int i = 0, j = 0; i < chars.Length; i++)
                {
                    char c = chars[i];
                    if (UTF16CharacterSet.IsSurrogate(c))
                    {
                        int cp = UTF16CharacterSet.CombinePair(c, chars[++i]);
                        triples[j++] = (byte)((cp >> 16) & 0xff);
                        triples[j++] = (byte)((cp >> 8) & 0xff);
                        triples[j++] = (byte)(cp & 0xff);
                    }
                    else
                    {
                        triples[j++] = 0;
                        triples[j++] = (byte)((c >> 8) & 0xff);
                        triples[j++] = (byte)(c & 0xff);
                    }
                }

                return new Twine24(triples);
            }
        }

        public static IIntIterator CodePoints(string value)
        {
            return new AnonymousIntIterator(null, value);
        }

        public static string DiagnosticDisplay(string s)
        {
            StringBuilder fsb = new StringBuilder(s.Length);
            for (int i = 0, len = s.Length; i < len; i++)
            {
                char c = s[i];
                if (c >= 0x20 && c <= 0x7e)
                {
                    fsb.Append(c);
                }
                else
                {
                    fsb.Append("\\u");
                    for (int shift = 12; shift >= 0; shift -= 4)
                    {
                        fsb.Append("0123456789ABCDEF"[(c >> shift) & 0xF]);
                    }
                }
            }

            return fsb.ToString();
        }

        public static void PrependWideChar(StringBuilder builder, int ch)
        {
            if (ch > 0xffff)
            {
                char[] pair = new char[]
                {
                    UTF16CharacterSet.HighSurrogate(ch),
                    UTF16CharacterSet.LowSurrogate(ch)
                };
                builder.Insert(0, pair);
            }
            else
            {
                builder.Insert(0, (char)ch);
            }
        }

        public static void PrependRepeated(StringBuilder builder, char ch, int count)
        {
            char[] array = new char[count];
            ArrayTools.Fill(array, ch);
            builder.Insert(0, array);
        }

        /// <summary>
        /// Wide-character sibling of <see cref="PrependRepeated(StringBuilder, char, int)"/>. One
        /// array, one Insert: prepending in a loop re-shifts the whole buffer per character, which
        /// is quadratic in the count.
        /// </summary>
        public static void PrependRepeated(StringBuilder builder, int ch, int count)
        {
            if (ch <= 0xffff)
            {
                PrependRepeated(builder, (char)ch, count);
                return;
            }

            char high = UTF16CharacterSet.HighSurrogate(ch);
            char low = UTF16CharacterSet.LowSurrogate(ch);
            char[] array = new char[checked(count * 2)];
            for (int i = 0; i < array.Length; i += 2)
            {
                array[i] = high;
                array[i + 1] = low;
            }

            builder.Insert(0, array);
        }

        public static int LastCodePoint(UnicodeString str)
        {
            return str.CodePointAt(str.Length() - 1);
        }

        public static long LastIndexOf(UnicodeString str, int codePoint)
        {
            for (long i = str.Length() - 1; i >= 0; i--)
            {
                if (str.CodePointAt(i) == codePoint)
                {
                    return i;
                }
            }

            return -1;
        }

        public static UnicodeString Compress(char[] @in, int offset, int len, bool compressWS)
        {
            if (len == 0)
            {
                return EmptyUnicodeString.GetInstance();
            }

            int max = 255;
            int end = offset + len;
            bool allWhite = compressWS;
            int surrogates = 0;

            // Find the maximum code value, and test whether all-white or surrogate
            int k = offset;
            if (compressWS)
            {
                while (k < end)
                {
                    int c = @in[k];
                    if (!Whitespace.IsWhite(c))
                    {
                        allWhite = false;
                        break;
                    }

                    k++;
                }

                if (allWhite)
                {
                    return CompressedWhitespace.CompressWS(@in, offset, len);
                }
            }

            while (k < end)
            {
                int c = @in[k++];
                max |= c;
                if (UTF16CharacterSet.IsSurrogate(c))
                {
                    surrogates++;
                }
            }

            if (max < 256)
            {
                byte[] array = new byte[len];
                for (int i = offset, j = 0; i < end;)
                {
                    array[j++] = (byte)@in[i++];
                }

                return new Twine8(array); //Use of `new String(@in, offset, len).getBytes(StandardCharsets.ISO_8859_1)` is slower
            }

            if (surrogates == 0)
            {
                char[] array = ArrayTools.CopyOfRange(@in, offset, offset + len);
                return new Twine16(array);
            }
            else
            {
                byte[] array = new byte[3 * (len - surrogates / 2)];
                for (int i = offset, j = 0; i < end;)
                {
                    char c = @in[i++];
                    if (UTF16CharacterSet.IsSurrogate(c))
                    {
                        int cp = UTF16CharacterSet.CombinePair(c, @in[i++]);
                        array[j++] = (byte)((cp & 0xffffff) >> 16);
                        array[j++] = (byte)((cp & 0xffff) >> 8);
                        array[j++] = (byte)(cp & 0xff);
                    }
                    else
                    {
                        array[j++] = (byte)0;
                        array[j++] = (byte)((c & 0xffff) >> 8);
                        array[j++] = (byte)(c & 0xff);
                    }
                }

                return new Twine24(array);
            }
        }

        public static void Copy8to16(byte[] source, int sourcePos, char[] dest, int destPos, int count)
        {
            int last = sourcePos + count;
            for (int i = sourcePos, j = destPos; i < last;)
            {
                dest[j++] = (char)(source[i++] & 0xff);
            }
        }

        public static void Copy8to24(byte[] source, int sourcePos, byte[] dest, int destPos, int count)
        {
            int last = sourcePos + count;
            for (int i = sourcePos, j = destPos * 3; i < last;)
            {
                dest[j++] = 0;
                dest[j++] = 0;
                dest[j++] = source[i++];
            }
        }

        public static void Copy16to24(char[] source, int sourcePos, byte[] dest, int destPos, int count)
        {
            int last = sourcePos + count;
            for (int i = sourcePos, j = destPos * 3; i < last;)
            {
                char c = source[i++];
                dest[j++] = 0;
                dest[j++] = (byte)((c >> 8) & 0xff);
                dest[j++] = (byte)(c & 0xff);
            }
        }

        private sealed class AnonymousIntIterator : AbstractIntIterator
        {

            private readonly string value;
            int i = 0;
            public AnonymousIntIterator(StringTool parent, string value)
            {
                this.value = value;
            }
            public override bool HasNext()
            {
                return i < value.Length;
            }

            public override int Next()
            {
                int c = value[i++];
                if (UTF16CharacterSet.IsHighSurrogate(c))
                {
                    try
                    {
                        int d = HasNext() ? value[i++] : -1;
                        if (!UTF16CharacterSet.IsLowSurrogate(d))
                        {
                            throw new InvalidOperationException("Unmatched surrogate code value " + c + " at position " + i);
                        }

                        return UTF16CharacterSet.CombinePair((char)c, (char)d);
                    }
                    catch (IndexOutOfRangeException)
                    {
                        throw new InvalidOperationException("Invalid surrogate at end of string");
                    }
                }
                else
                {
                    return c;
                }
            }
        }
    }
}
