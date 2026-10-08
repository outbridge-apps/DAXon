////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Collections;
using OutSmart.DAXon.Internal.Collections;
using System;
namespace OutSmart.DAXon.Text
{
    internal sealed class CompressedWhitespace : WhitespaceString
    {
        private static readonly char[] WHITE_CHARS = new[]
        {
            '\t',
            '\n',
            '\r',
            ' '
        };
        private static readonly int[] CODES = new[]
        {
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            0,
            1,
            -1,
            -1,
            2,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            -1,
            3
        };
        private readonly long value;

        public long CompressedValue => value;
        public CompressedWhitespace(long compressedValue)
        {
            value = compressedValue;
        }

        public static UnicodeString CompressWS(char[] @in, int start, int len)
        {
            int runlength = 1;
            int outlength = 0;
            int end = start + len;
            for (int i = start; i < end; i++)
            {
                char c = @in[i];
                if (c <= 32 && CODES[c] >= 0)
                {
                    if (i == end - 1 || c != @in[i + 1] || runlength == 63)
                    {
                        runlength = 1;
                        outlength++;
                        if (outlength > 8)
                        {
                            return StringTool.Compress(@in, start, len, false);
                        }
                    }
                    else
                    {
                        runlength++;
                    }
                }
                else
                {
                    return StringTool.Compress(@in, start, len, false);
                }
            }

            int ix = 0;
            runlength = 1;
            int[] @out = new int[outlength];
            for (int i = start; i < end; i++)
            {
                char c = @in[i];
                if (i == end - 1 || c != @in[i + 1] || runlength == 63)
                {
                    @out[ix++] = (CODES[c] << 6) | runlength;
                    runlength = 1;
                }
                else
                {
                    runlength++;
                }
            }

            long value = 0;
            for (int i = 0; i < outlength; i++)
            {
                value = (value << 8) | (long)@out[i];
            }

            value = value << (8 * (8 - outlength));
            return new CompressedWhitespace(value);
        }

        public override UnicodeString Uncompress()
        {
            return Uncompress(value);
        }

        public static UnicodeString Uncompress(long value)
        {
            byte[] bytes = new byte[1000];
            int offset = 0;
            for (int s = 56; s >= 0; s -= 8)
            {
                byte b = (byte)((value >> s) & 0xff);
                if (b == 0)
                {
                    break;
                }

                byte c = (byte)(WHITE_CHARS[b >> 6 & 0x3] & 0xff);
                int len = b & 0x3f;
                for (int j = 0; j < len; j++)
                {
                    bytes[offset++] = c;
                }
            }

            return new Twine8(ArrayTools.CopyOf(bytes, offset));
        }

        public override long Length()
        {
            return Length(value);
        }

        public override int Length32()
        {
            return Length(value);
        }

        public static int Length(long value)
        {
            int count = 0;
            for (int s = 56; s >= 0; s -= 8)
            {
                int c = (int)((value >> s) & 0x3f);
                if (c == 0)
                {
                    break;
                }

                count += c;
            }

            return count;
        }

        public override int CodePointAt(long index)
        {
            int count = 0;
            for (int s = 56; s >= 0; s -= 8)
            {
                byte b = (byte)((value >> s) & 0xff);
                if (b == 0)
                {
                    break;
                }

                count += b & 0x3f;
                if (count > index)
                {
                    return WHITE_CHARS[b >> 6 & 0x3];
                }
            }

            throw new IndexOutOfRangeException(index + "");
        }

        public override IIntIterator CodePoints()
        {
            return Uncompress().CodePoints();
        }

        public override bool Equals(object obj)
        {
            if (obj is CompressedWhitespace)
            {
                return value == ((CompressedWhitespace)obj).value;
            }

            return Uncompress().Equals(obj);
        }

        public override int GetHashCode()
        {

            // Included to prevent C# compiler warnings
            return base.GetHashCode();
        }

        public override void Write(IUnicodeWriter writer)
        {
            for (int s = 56; s >= 0; s -= 8)
            {
                byte b = (byte)((value >> s) & 0xff);
                if (b == 0)
                {
                    break;
                }

                char c = WHITE_CHARS[b >> 6 & 0x3];
                int len = b & 0x3f;
                writer.WriteRepeatedAscii((byte)c, len);
            }
        }

        public override void WriteEscape(bool[] specialChars, IUnicodeWriter writer)
        {
            for (int s = 56; s >= 0; s -= 8)
            {
                byte b = (byte)((value >> s) & 0xff);
                if (b == 0)
                {
                    break;
                }

                char c = WHITE_CHARS[b >> 6 & 0x3];
                int len = b & 0x3f;
                if (specialChars[c])
                {
                    byte[] e = null;
                    if (c == '\n')
                    {
                        e = StringConstants.ESCAPE_NL; //"&#xA;";
                    }
                    else if (c == '\r')
                    {
                        e = StringConstants.ESCAPE_CR; //"&#xD;";
                    }
                    else if (c == '\t')
                    {
                        e = StringConstants.ESCAPE_TAB; //"&#x9;";
                    }

                    for (int j = 0; j < len; j++)
                    {
                        writer.WriteAscii(e);
                    }
                }
                else
                {
                    writer.WriteRepeatedAscii((byte)c, len);
                }
            }
        }
    }
}