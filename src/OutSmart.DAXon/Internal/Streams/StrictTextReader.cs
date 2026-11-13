////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.IO;
using System.Text;
using OutSmart.DAXon.Lib;

namespace OutSmart.DAXon.Internal.Streams
{
    // Text from bytes that a decoder refuses when they are not text, DecoderFallbackException at the place. StreamReader
    // cannot do it: after a byte order mark it decodes with a lenient encoding of its own, and on .NET Framework it drops
    // an incomplete sequence at the end of the stream. A mark wins over an encoding that was only sniffed, and is not part
    // of the text. An encoding asked for by name decodes the bytes as they are, as F&O says, but for UTF-16 and UTF-32,
    // whose own marks tell their order. A mark such an encoding reads as U+FEFF is dropped by whoever reads the text.
    internal sealed class StrictTextReader : TextReader
    {
        private const int BufferSize = 4096;

        private readonly Stream stream;
        private byte[] bytes;
        private Encoding encoding;
        private Decoder decoder;
        private char[] chars;
        private int pos;
        private int length;
        private bool ended;

        // Which marks decide how the bytes are read.
        internal enum Marks
        {
            Any,
            OfUtf16,
            OfUtf32,
            None,

            // A document that names its encoding: a UTF-32 mark reads as one, a UTF-8 mark is passed over (Java's parser).
            Declared
        }

        // What a mark may do to an encoding asked for by this name, null when none was.
        internal static Marks MarksOf(string asked)
        {
            return string.IsNullOrEmpty(asked) ? Marks.Any
                : PlatformEncodings.IsUtf16(asked) ? Marks.OfUtf16
                : PlatformEncodings.IsUtf32(asked) ? Marks.OfUtf32
                : Marks.None;
        }

        private readonly Marks marks;

        internal StrictTextReader(Stream stream, Encoding strictEncoding, Marks marks = Marks.Any)
        {
            this.stream = stream;
            encoding = strictEncoding;
            this.marks = marks;
        }

        // The next chars into the buffer; false at the end of the text, the end of the bytes having been checked.
        private bool Fill()
        {
            pos = 0;
            length = 0;
            while (!ended)
            {
                int offset = 0;
                int count;
                if (decoder == null)
                {
                    bytes = new byte[SizeFor(stream)];
                    count = ReadHead(out offset);
                    decoder = encoding.GetDecoder();
                    chars = new char[encoding.GetMaxCharCount(bytes.Length)];
                    if (count == offset)
                    {
                        count = stream.Read(bytes, 0, bytes.Length);
                        offset = 0;
                    }
                    else
                    {
                        count -= offset;
                    }
                }
                else
                {
                    count = stream.Read(bytes, 0, bytes.Length);
                }

                if (count == 0)
                {
                    ended = true;
                    length = decoder.GetChars(bytes, 0, 0, chars, 0, true);   // throws for an incomplete sequence
                }
                else
                {
                    length = decoder.GetChars(bytes, offset, count, chars, 0, false);
                }

                if (length > 0)
                {
                    return true;
                }
            }

            return false;
        }

        // A small file gets buffers its size: most documents are, and the buffers were most of what reading one allocated.
        private static int SizeFor(Stream stream)
        {
            try
            {
                return stream.CanSeek ? (int)Math.Max(16, Math.Min(BufferSize, stream.Length - stream.Position + 1)) : BufferSize;
            }
            catch (Exception e) when (e is NotSupportedException || e is IOException)
            {
                return BufferSize;
            }
        }

        // The first bytes: enough to know a mark, whose encoding takes over. Returns how many were read; offset, the mark's length.
        private int ReadHead(out int offset)
        {
            int got = 0;
            int r;
            while (got < 4 && (r = stream.Read(bytes, got, bytes.Length - got)) > 0)
            {
                got += r;
            }

            offset = 0;
            bool utf16 = marks == Marks.Any || marks == Marks.OfUtf16;
            bool utf32 = marks == Marks.Any || marks == Marks.OfUtf32 || marks == Marks.Declared;
            if (utf16 && got >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                encoding = StandardUnparsedTextResolver.Strict(Encoding.BigEndianUnicode);
                offset = 2;
            }
            else if (utf32 && got >= 4 && bytes[0] == 0xFF && bytes[1] == 0xFE && bytes[2] == 0 && bytes[3] == 0)
            {
                encoding = StandardUnparsedTextResolver.Strict(Encoding.UTF32);
                offset = 4;
            }
            else if (utf16 && got >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                encoding = StandardUnparsedTextResolver.Strict(Encoding.Unicode);
                offset = 2;
            }
            else if (marks == Marks.Any && got >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                encoding = StandardUnparsedTextResolver.Strict(new UTF8Encoding(false));
                offset = 3;
            }
            else if (marks == Marks.Declared && got >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                offset = 3;
            }
            else if (utf32 && got >= 4 && bytes[0] == 0 && bytes[1] == 0 && bytes[2] == 0xFE && bytes[3] == 0xFF)
            {
                encoding = StandardUnparsedTextResolver.Strict(new UTF32Encoding(true, false));
                offset = 4;
            }

            return got;
        }

        public override int Peek()
        {
            return pos < length || Fill() ? chars[pos] : -1;
        }

        public override int Read()
        {
            return pos < length || Fill() ? chars[pos++] : -1;
        }

        public override int Read(char[] buffer, int index, int count)
        {
            if (pos >= length && !Fill())
            {
                return 0;
            }

            int n = Math.Min(count, length - pos);
            Array.Copy(chars, pos, buffer, index, n);
            pos += n;
            return n;
        }

        public override string ReadToEnd()
        {
            var text = new StringBuilder();
            while (pos < length || Fill())
            {
                text.Append(chars, pos, length - pos);
                pos = length;
            }

            return text.ToString();
        }

        // Up to \n, \r or \r\n, as StreamReader; null at the end.
        public override string ReadLine()
        {
            StringBuilder line = null;
            while (pos < length || Fill())
            {
                for (int i = pos; i < length; i++)
                {
                    char c = chars[i];
                    if (c == '\n' || c == '\r')
                    {
                        string s = line == null ? new string(chars, pos, i - pos) : line.Append(chars, pos, i - pos).ToString();
                        pos = i + 1;
                        if (c == '\r' && (pos < length || Fill()) && chars[pos] == '\n')
                        {
                            pos++;
                        }

                        return s;
                    }
                }

                line = line ?? new StringBuilder();
                line.Append(chars, pos, length - pos);
                pos = length;
            }

            return line?.ToString();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                stream.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
