////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Collections;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Globalization;
using System.Text;
using OutSmart.DAXon.Text;
using System.IO;
namespace OutSmart.DAXon.Serialization
{
    internal sealed class UTF8Writer : TextWriter, IUnicodeWriter
    {
        private const int MIN_BUF_LEN = 32;
        private const int DEFAULT_BUF_LEN = 4096;
        static readonly int SURR1_FIRST = 0xD800;
        static readonly int SURR1_LAST = 0xDBFF;
        static readonly int SURR2_FIRST = 0xDC00;
        static readonly int SURR2_LAST = 0xDFFF;
        private System.IO.Stream _out;
        private byte[] _outBuffer;
        private readonly int _outBufferLast;
        private int _outPtr;
        int _surrogate = 0;
        public override Encoding Encoding => Encoding.UTF8;
        public UTF8Writer(System.IO.Stream @out) : this(@out, DEFAULT_BUF_LEN)
        {
        }

        public UTF8Writer(System.IO.Stream @out, int bufferLength)
        {
            if (bufferLength < MIN_BUF_LEN)
            {
                bufferLength = MIN_BUF_LEN;
            }

            _out = @out;
            _outBuffer = new byte[bufferLength];
            /* Max. expansion for a single Unicode code point is 4 bytes when
         * recombining UCS-2 surrogate pairs, so:
         */
            _outBufferLast = bufferLength - 4;
            _outPtr = 0;
        }
        public override void Write(char c) { Write((int)c); }

        /* Max. expansion for a single Unicode code point is 4 bytes when
         * recombining UCS-2 surrogate pairs, so:
         */
        /*
    ////////////////////////////////////////////////////////
    // OutSmart.DAXon.Internal.IO.Writer implementation
    ////////////////////////////////////////////////////////
     */
        /* Due to co-variance between Appendable and
     * TextWriter, this would not compile with javac 1.5, in 1.4 mode
     * (source and target set to "1.4". Not a huge deal, but since
     * the base impl is just fine, no point in overriding it.
     */
        /*
    public TextWriter append(char c) throws IOException {
    // note: this is a JDK 1.5 method
        write(c);
        return this;
    }
    */
        protected override void Dispose(bool disposing)
        {
            if (_out != null)
            {
                _flushBuffer();
                _outBuffer = null;
                _out.Dispose();
                _out = null;
                /* If we are left with partial surrogate we have a problem, but
             * let's not let it prevent closure of the underlying stream
             */
                if (_surrogate != 0)
                {
                    int code = _surrogate;

                    // but let's clear it, to get just one problem?
                    _surrogate = 0;
                    ThrowIllegal(code);
                }
            }
        }

        /* Max. expansion for a single Unicode code point is 4 bytes when
         * recombining UCS-2 surrogate pairs, so:
         */
        /*
    ////////////////////////////////////////////////////////
    // OutSmart.DAXon.Internal.IO.Writer implementation
    ////////////////////////////////////////////////////////
     */
        /* Due to co-variance between Appendable and
     * TextWriter, this would not compile with javac 1.5, in 1.4 mode
     * (source and target set to "1.4". Not a huge deal, but since
     * the base impl is just fine, no point in overriding it.
     */
        /*
    public TextWriter append(char c) throws IOException {
    // note: this is a JDK 1.5 method
        write(c);
        return this;
    }
    */
        /* If we are left with partial surrogate we have a problem, but
             * let's not let it prevent closure of the underlying stream
             */
        public override void Flush()
        {
            _flushBuffer();
            _out.Flush();
        }

        /* Max. expansion for a single Unicode code point is 4 bytes when
         * recombining UCS-2 surrogate pairs, so:
         */
        /*
    ////////////////////////////////////////////////////////
    // OutSmart.DAXon.Internal.IO.Writer implementation
    ////////////////////////////////////////////////////////
     */
        /* Due to co-variance between Appendable and
     * TextWriter, this would not compile with javac 1.5, in 1.4 mode
     * (source and target set to "1.4". Not a huge deal, but since
     * the base impl is just fine, no point in overriding it.
     */
        /*
    public TextWriter append(char c) throws IOException {
    // note: this is a JDK 1.5 method
        write(c);
        return this;
    }
    */
        /* If we are left with partial surrogate we have a problem, but
             * let's not let it prevent closure of the underlying stream
             */
        public override void Write(char[] cbuf)
        {
            Write(cbuf, 0, cbuf.Length);
        }

        /* Max. expansion for a single Unicode code point is 4 bytes when
         * recombining UCS-2 surrogate pairs, so:
         */
        /*
    ////////////////////////////////////////////////////////
    // OutSmart.DAXon.Internal.IO.Writer implementation
    ////////////////////////////////////////////////////////
     */
        /* Due to co-variance between Appendable and
     * TextWriter, this would not compile with javac 1.5, in 1.4 mode
     * (source and target set to "1.4". Not a huge deal, but since
     * the base impl is just fine, no point in overriding it.
     */
        /*
    public TextWriter append(char c) throws IOException {
    // note: this is a JDK 1.5 method
        write(c);
        return this;
    }
    */
        /* If we are left with partial surrogate we have a problem, but
             * let's not let it prevent closure of the underlying stream
             */
        public override void Write(char[] cbuf, int off, int len)
        {
            if (len < 2)
            {
                if (len == 1)
                {
                    Write(cbuf[off]);
                }

                return;
            }


            // First: do we have a leftover surrogate to deal with?
            if (_surrogate > 0)
            {
                char second = cbuf[off++];
                --len;
                Write(_convertSurrogate(second)); // will have at least one more char
            }

            int outPtr = _outPtr;
            byte[] outBuf = _outBuffer;
            int outBufLast = _outBufferLast; // has 4 'spare' bytes

            // All right; can just loop it nice and easy now:
            len += off; // len will now be the end of input buffer
            while (off < len)
            {
                /* First, let's ensure we can output at least 4 bytes
             * (longest UTF-8 encoded codepoint):
             */
                if (outPtr >= outBufLast)
                {
                    _out.Write(outBuf, 0, outPtr);
                    outPtr = 0;
                }

                int c = cbuf[off++];

                // And then see if we have an Ascii char:
                if (c < 0x80)
                {

                    // If so, can do a tight inner loop:
                    outBuf[outPtr++] = (byte)c;

                    // Let's calc how many ascii chars we can copy at most:
                    int maxInCount = (len - off);
                    int maxOutCount = (outBufLast - outPtr);
                    if (maxInCount > maxOutCount)
                    {
                        maxInCount = maxOutCount;
                    }

                    maxInCount += off;
                    bool continueOuter = false;
                    while (true)
                    {
                        if (off >= maxInCount)
                        {

                            // done with max. ascii seq
                            continueOuter = true;
                            break;
                        }

                        c = cbuf[off++];
                        if (c >= 0x80)
                        {
                            break;
                        }

                        outBuf[outPtr++] = (byte)c;
                    }

                    if (continueOuter)
                    {
                        continue;
                    }
                }


                // Nope, multi-byte:
                if (c < 0x800)
                {

                    // 2-byte
                    outBuf[outPtr++] = (byte)(0xc0 | (c >> 6));
                    outBuf[outPtr++] = (byte)(0x80 | (c & 0x3f));
                } // 3 or 4 bytes
                else
                {

                    // 3 or 4 bytes
                    // Surrogates?
                    if (c < SURR1_FIRST || c > SURR2_LAST)
                    {
                        outBuf[outPtr++] = (byte)(0xe0 | (c >> 12));
                        outBuf[outPtr++] = (byte)(0x80 | ((c >> 6) & 0x3f));
                        outBuf[outPtr++] = (byte)(0x80 | (c & 0x3f));
                        continue;
                    }


                    // Yup, a surrogate:
                    if (c > SURR1_LAST)
                    {

                        // must be from first range
                        _outPtr = outPtr;
                        ThrowIllegal(c);
                    }

                    _surrogate = c;

                    // and if so, followed by another from next range
                    if (off >= len)
                    {

                        // unless we hit the end?
                        break;
                    }

                    c = _convertSurrogate(cbuf[off++]);
                    if (c > 0x10FFFF)
                    {

                        // illegal, as per RFC 3629
                        _outPtr = outPtr;
                        ThrowIllegal(c);
                    }

                    outBuf[outPtr++] = (byte)(0xf0 | (c >> 18));
                    outBuf[outPtr++] = (byte)(0x80 | ((c >> 12) & 0x3f));
                    outBuf[outPtr++] = (byte)(0x80 | ((c >> 6) & 0x3f));
                    outBuf[outPtr++] = (byte)(0x80 | (c & 0x3f));
                }
            }

            _outPtr = outPtr;
        }

        /* Max. expansion for a single Unicode code point is 4 bytes when
         * recombining UCS-2 surrogate pairs, so:
         */
        /*
    ////////////////////////////////////////////////////////
    // OutSmart.DAXon.Internal.IO.Writer implementation
    ////////////////////////////////////////////////////////
     */
        /* Due to co-variance between Appendable and
     * TextWriter, this would not compile with javac 1.5, in 1.4 mode
     * (source and target set to "1.4". Not a huge deal, but since
     * the base impl is just fine, no point in overriding it.
     */
        /*
    public TextWriter append(char c) throws IOException {
    // note: this is a JDK 1.5 method
        write(c);
        return this;
    }
    */
        /* If we are left with partial surrogate we have a problem, but
             * let's not let it prevent closure of the underlying stream
             */
        /* First, let's ensure we can output at least 4 bytes
             * (longest UTF-8 encoded codepoint):
             */
        // 2-byte
        public void WriteLatin1(byte[] bytes, int off, int len)
        {
            int outPtr = _outPtr;
            byte[] outBuf = _outBuffer;
            int outBufLast = _outBufferLast; // has 4 'spare' bytes

            // All right; can just loop it nice and easy now:
            len += off; // len will now be the end of input buffer
            while (off < len)
            {
                /* First, let's ensure we can output at least 4 bytes
             * (longest UTF-8 encoded codepoint):
             */
                if (outPtr >= outBufLast)
                {
                    _out.Write(outBuf, 0, outPtr);
                    outPtr = 0;
                }

                int c = bytes[off++] & 0xff;

                // And then see if we have an Ascii char:
                if (c < 0x80)
                {

                    // If so, can do a tight inner loop:
                    outBuf[outPtr++] = (byte)c;

                    // Let's calc how many ascii chars we can copy at most:
                    int maxInCount = (len - off);
                    int maxOutCount = (outBufLast - outPtr);
                    if (maxInCount > maxOutCount)
                    {
                        maxInCount = maxOutCount;
                    }

                    maxInCount += off;
                    bool continueOuter = false;
                    while (true)
                    {
                        if (off >= maxInCount)
                        {

                            // done with max. ascii seq
                            continueOuter = true;
                            break;
                        }

                        c = bytes[off++] & 0xff;
                        if (c >= 0x80)
                        {
                            break;
                        }

                        outBuf[outPtr++] = (byte)c;
                    }

                    if (continueOuter)
                    {
                        continue;
                    }
                }

                outBuf[outPtr++] = (byte)(0xc0 | (c >> 6));
                outBuf[outPtr++] = (byte)(0x80 | (c & 0x3f));
            }

            _outPtr = outPtr;
        }

        /* Max. expansion for a single Unicode code point is 4 bytes when
         * recombining UCS-2 surrogate pairs, so:
         */
        /*
    ////////////////////////////////////////////////////////
    // OutSmart.DAXon.Internal.IO.Writer implementation
    ////////////////////////////////////////////////////////
     */
        /* Due to co-variance between Appendable and
     * TextWriter, this would not compile with javac 1.5, in 1.4 mode
     * (source and target set to "1.4". Not a huge deal, but since
     * the base impl is just fine, no point in overriding it.
     */
        /*
    public TextWriter append(char c) throws IOException {
    // note: this is a JDK 1.5 method
        write(c);
        return this;
    }
    */
        /* If we are left with partial surrogate we have a problem, but
             * let's not let it prevent closure of the underlying stream
             */
        /* First, let's ensure we can output at least 4 bytes
             * (longest UTF-8 encoded codepoint):
             */
        // 2-byte
        /* First, let's ensure we can output at least 4 bytes
             * (longest UTF-8 encoded codepoint):
             */
        public void WriteAscii(byte[] content)
        {
            WriteAscii(content, 0, content.Length);
        }

        /* Max. expansion for a single Unicode code point is 4 bytes when
         * recombining UCS-2 surrogate pairs, so:
         */
        /*
    ////////////////////////////////////////////////////////
    // OutSmart.DAXon.Internal.IO.Writer implementation
    ////////////////////////////////////////////////////////
     */
        /* Due to co-variance between Appendable and
     * TextWriter, this would not compile with javac 1.5, in 1.4 mode
     * (source and target set to "1.4". Not a huge deal, but since
     * the base impl is just fine, no point in overriding it.
     */
        /*
    public TextWriter append(char c) throws IOException {
    // note: this is a JDK 1.5 method
        write(c);
        return this;
    }
    */
        /* If we are left with partial surrogate we have a problem, but
             * let's not let it prevent closure of the underlying stream
             */
        /* First, let's ensure we can output at least 4 bytes
             * (longest UTF-8 encoded codepoint):
             */
        /* First, let's ensure we can output at least 4 bytes
             * (longest UTF-8 encoded codepoint):
             */
        public void WriteAscii(byte[] chars, int off, int len)
        {
            int outPtr = _outPtr;
            byte[] outBuf = _outBuffer;
            int outBufLast = _outBufferLast; // has 4 'spare' bytes
            while (len > 0)
            {
                if (outPtr >= outBufLast)
                {
                    _out.Write(outBuf, 0, outPtr);
                    outPtr = 0;
                }

                int available = outBufLast - outPtr;
                int count = Math.Min(len, available);
                Array.Copy(chars, off, outBuf, outPtr, count);
                outPtr += count;
                off += count;
                len -= count;
            }

            _outPtr = outPtr;
        }

        /* Max. expansion for a single Unicode code point is 4 bytes when
         * recombining UCS-2 surrogate pairs, so:
         */
        /*
    ////////////////////////////////////////////////////////
    // OutSmart.DAXon.Internal.IO.Writer implementation
    ////////////////////////////////////////////////////////
     */
        /* Due to co-variance between Appendable and
     * TextWriter, this would not compile with javac 1.5, in 1.4 mode
     * (source and target set to "1.4". Not a huge deal, but since
     * the base impl is just fine, no point in overriding it.
     */
        /*
    public TextWriter append(char c) throws IOException {
    // note: this is a JDK 1.5 method
        write(c);
        return this;
    }
    */
        /* If we are left with partial surrogate we have a problem, but
             * let's not let it prevent closure of the underlying stream
             */
        /* First, let's ensure we can output at least 4 bytes
             * (longest UTF-8 encoded codepoint):
             */
        /* First, let's ensure we can output at least 4 bytes
             * (longest UTF-8 encoded codepoint):
             */
        public void WriteRepeatedAscii(byte ch, int repeat)
        {
            int outPtr = _outPtr;
            byte[] outBuf = _outBuffer;
            int outBufLast = _outBufferLast; // has 4 'spare' bytes
            while (repeat > 0)
            {
                if (outPtr >= outBufLast)
                {
                    _out.Write(outBuf, 0, outPtr);
                    outPtr = 0;
                }

                int available = outBufLast - outPtr;
                int count = Math.Min(repeat, available);
                ArrayTools.Fill(outBuf, outPtr, outPtr + count, ch);
                outPtr += count;
                repeat -= count;
            }

            _outPtr = outPtr;
        }

        /* Max. expansion for a single Unicode code point is 4 bytes when
         * recombining UCS-2 surrogate pairs, so:
         */
        /*
    ////////////////////////////////////////////////////////
    // OutSmart.DAXon.Internal.IO.Writer implementation
    ////////////////////////////////////////////////////////
     */
        /* Due to co-variance between Appendable and
     * TextWriter, this would not compile with javac 1.5, in 1.4 mode
     * (source and target set to "1.4". Not a huge deal, but since
     * the base impl is just fine, no point in overriding it.
     */
        /*
    public TextWriter append(char c) throws IOException {
    // note: this is a JDK 1.5 method
        write(c);
        return this;
    }
    */
        /* If we are left with partial surrogate we have a problem, but
             * let's not let it prevent closure of the underlying stream
             */
        /* First, let's ensure we can output at least 4 bytes
             * (longest UTF-8 encoded codepoint):
             */
        /* First, let's ensure we can output at least 4 bytes
             * (longest UTF-8 encoded codepoint):
             */
        public void WriteCodePoint(int codepoint)
        {

            // The implementation of write(int) in this class appears to handle astral characters, although
            // the interface definition for Java.io.Writer suggests otherwise
            Write(codepoint);
        }

        /* Max. expansion for a single Unicode code point is 4 bytes when
         * recombining UCS-2 surrogate pairs, so:
         */
        /*
    ////////////////////////////////////////////////////////
    // OutSmart.DAXon.Internal.IO.Writer implementation
    ////////////////////////////////////////////////////////
     */
        /* Due to co-variance between Appendable and
     * TextWriter, this would not compile with javac 1.5, in 1.4 mode
     * (source and target set to "1.4". Not a huge deal, but since
     * the base impl is just fine, no point in overriding it.
     */
        /*
    public TextWriter append(char c) throws IOException {
    // note: this is a JDK 1.5 method
        write(c);
        return this;
    }
    */
        /* If we are left with partial surrogate we have a problem, but
             * let's not let it prevent closure of the underlying stream
             */
        /* First, let's ensure we can output at least 4 bytes
             * (longest UTF-8 encoded codepoint):
             */
        /* First, let's ensure we can output at least 4 bytes
             * (longest UTF-8 encoded codepoint):
             */
        public override void Write(int c)
        {

            // First; do we have a left over surrogate?
            if (_surrogate > 0)
            {
                c = _convertSurrogate(c); // If not, do we start with a surrogate?
            }
            else if (c >= SURR1_FIRST && c <= SURR2_LAST)
            {

                // Illegal to get second part without first:
                if (c > SURR1_LAST)
                {
                    ThrowIllegal(c);
                }


                // First part just needs to be held for now
                _surrogate = c;
                return;
            }

            if (_outPtr >= _outBufferLast)
            {

                // let's require enough room, first
                _flushBuffer();
            }

            if (c < 0x80)
            {

                // ascii
                _outBuffer[_outPtr++] = (byte)c;
            }
            else
            {
                int ptr = _outPtr;
                if (c < 0x800)
                {

                    // 2-byte
                    _outBuffer[ptr++] = (byte)(0xc0 | (c >> 6));
                    _outBuffer[ptr++] = (byte)(0x80 | (c & 0x3f));
                } // 3 bytes
                else if (c <= 0xFFFF)
                {

                    // 3 bytes
                    _outBuffer[ptr++] = (byte)(0xe0 | (c >> 12));
                    _outBuffer[ptr++] = (byte)(0x80 | ((c >> 6) & 0x3f));
                    _outBuffer[ptr++] = (byte)(0x80 | (c & 0x3f));
                } // 4 bytes
                else
                {

                    // 4 bytes
                    if (c > 0x10FFFF)
                    {

                        // illegal, as per RFC 3629
                        ThrowIllegal(c);
                    }

                    _outBuffer[ptr++] = (byte)(0xf0 | (c >> 18));
                    _outBuffer[ptr++] = (byte)(0x80 | ((c >> 12) & 0x3f));
                    _outBuffer[ptr++] = (byte)(0x80 | ((c >> 6) & 0x3f));
                    _outBuffer[ptr++] = (byte)(0x80 | (c & 0x3f));
                }

                _outPtr = ptr;
            }
        }

        /* Max. expansion for a single Unicode code point is 4 bytes when
         * recombining UCS-2 surrogate pairs, so:
         */
        /*
    ////////////////////////////////////////////////////////
    // OutSmart.DAXon.Internal.IO.Writer implementation
    ////////////////////////////////////////////////////////
     */
        /* Due to co-variance between Appendable and
     * TextWriter, this would not compile with javac 1.5, in 1.4 mode
     * (source and target set to "1.4". Not a huge deal, but since
     * the base impl is just fine, no point in overriding it.
     */
        /*
    public TextWriter append(char c) throws IOException {
    // note: this is a JDK 1.5 method
        write(c);
        return this;
    }
    */
        /* If we are left with partial surrogate we have a problem, but
             * let's not let it prevent closure of the underlying stream
             */
        /* First, let's ensure we can output at least 4 bytes
             * (longest UTF-8 encoded codepoint):
             */
        /* First, let's ensure we can output at least 4 bytes
             * (longest UTF-8 encoded codepoint):
             */
        // ascii
        // 2-byte
        // 3 bytes
        // 4 bytes
        public void Write(UnicodeString chars)
        {
            if (chars is StringView || chars is BMPString)
            {
                Write(chars.ToString());
            }
            else if (chars is UnicodeChar)
            {
                WriteCodePoint(((UnicodeChar)chars).Codepoint);
            }
            else if (chars is ZenoString)
            {
                ((ZenoString)chars).WriteSegments(this);
            }
            else if (chars.Width <= 8)
            {
                WriteWidth8OrLower(chars);
            }
            else if (chars.Width == 16)
            {
                if (chars is Twine16)
                {
                    Write(((Twine16)chars).CharArray);
                }
                else if (chars is Slice16)
                {
                    Slice16 s16 = (Slice16)chars;
                    Write(s16.CharArray, s16.Start, s16.End - s16.Start);
                }
            }
            else
            {
                IIntIterator iter = chars.CodePoints();
                while (iter.MoveNext())
                {
                    Write(iter.Current);
                }
            }
        }

        /* Max. expansion for a single Unicode code point is 4 bytes when
         * recombining UCS-2 surrogate pairs, so:
         */
        /*
    ////////////////////////////////////////////////////////
    // OutSmart.DAXon.Internal.IO.Writer implementation
    ////////////////////////////////////////////////////////
     */
        /* Due to co-variance between Appendable and
     * TextWriter, this would not compile with javac 1.5, in 1.4 mode
     * (source and target set to "1.4". Not a huge deal, but since
     * the base impl is just fine, no point in overriding it.
     */
        /*
    public TextWriter append(char c) throws IOException {
    // note: this is a JDK 1.5 method
        write(c);
        return this;
    }
    */
        /* If we are left with partial surrogate we have a problem, but
             * let's not let it prevent closure of the underlying stream
             */
        /* First, let's ensure we can output at least 4 bytes
             * (longest UTF-8 encoded codepoint):
             */
        /* First, let's ensure we can output at least 4 bytes
             * (longest UTF-8 encoded codepoint):
             */
        private void WriteWidth8OrLower(UnicodeString chars)
        {
            if (chars is Twine8)
            {
                int width = chars.Width;
                if (width == 7)
                {
                    WriteAscii(((Twine8)chars).ByteArray, 0, chars.Length32());
                }
                else if (width == 8)
                {
                    WriteLatin1(((Twine8)chars).ByteArray, 0, chars.Length32());
                }
            }
            else if (chars is Slice8)
            {
                Slice8 s8 = (Slice8)chars;
                int width = chars.Width;
                if (width == 7)
                {
                    WriteAscii(s8.ByteArray, s8.Start, s8.End - s8.Start);
                }
                else if (width == 8)
                {
                    WriteLatin1(s8.ByteArray, s8.Start, s8.End - s8.Start);
                }
            }
            else if (chars is WhitespaceString)
            {
                ((WhitespaceString)chars).Write(this);
            }
            else
            {

                // probably doesn't happen
                IIntIterator iter = chars.CodePoints();
                while (iter.MoveNext())
                {
                    Write(iter.Current);
                }
            }
        }

        /* Max. expansion for a single Unicode code point is 4 bytes when
         * recombining UCS-2 surrogate pairs, so:
         */
        /*
    ////////////////////////////////////////////////////////
    // OutSmart.DAXon.Internal.IO.Writer implementation
    ////////////////////////////////////////////////////////
     */
        /* Due to co-variance between Appendable and
     * TextWriter, this would not compile with javac 1.5, in 1.4 mode
     * (source and target set to "1.4". Not a huge deal, but since
     * the base impl is just fine, no point in overriding it.
     */
        /*
    public TextWriter append(char c) throws IOException {
    // note: this is a JDK 1.5 method
        write(c);
        return this;
    }
    */
        /* If we are left with partial surrogate we have a problem, but
             * let's not let it prevent closure of the underlying stream
             */
        /* First, let's ensure we can output at least 4 bytes
             * (longest UTF-8 encoded codepoint):
             */
        /* First, let's ensure we can output at least 4 bytes
             * (longest UTF-8 encoded codepoint):
             */
        // ascii
        // 2-byte
        // 3 bytes
        // 4 bytes
        public override void Write(string str)
        {
            Write(str, 0, str.Length);
        }

        /* Max. expansion for a single Unicode code point is 4 bytes when
         * recombining UCS-2 surrogate pairs, so:
         */
        /*
    ////////////////////////////////////////////////////////
    // OutSmart.DAXon.Internal.IO.Writer implementation
    ////////////////////////////////////////////////////////
     */
        /* Due to co-variance between Appendable and
     * TextWriter, this would not compile with javac 1.5, in 1.4 mode
     * (source and target set to "1.4". Not a huge deal, but since
     * the base impl is just fine, no point in overriding it.
     */
        /*
    public TextWriter append(char c) throws IOException {
    // note: this is a JDK 1.5 method
        write(c);
        return this;
    }
    */
        /* If we are left with partial surrogate we have a problem, but
             * let's not let it prevent closure of the underlying stream
             */
        /* First, let's ensure we can output at least 4 bytes
             * (longest UTF-8 encoded codepoint):
             */
        /* First, let's ensure we can output at least 4 bytes
             * (longest UTF-8 encoded codepoint):
             */
        public void Write(string str, int off, int len)
        {
            if (len < 2)
            {
                if (len == 1)
                {
                    Write(str[off]);
                }

                return;
            }


            // First: do we have a leftover surrogate to deal with?
            if (_surrogate > 0)
            {
                char second = str[off++];
                --len;
                Write(_convertSurrogate(second)); // will have at least one more char (case of 1 char was checked earlier on)
            }

            int outPtr = _outPtr;
            byte[] outBuf = _outBuffer;
            int outBufLast = _outBufferLast; // has 4 'spare' bytes

            // All right; can just loop it nice and easy now:
            len += off; // len will now be the end of input buffer
            while (off < len)
            {

                // First, let's ensure we can output at least 4 bytes
                // (longest UTF-8 encoded codepoint):
                if (outPtr >= outBufLast)
                {
                    _out.Write(outBuf, 0, outPtr);
                    outPtr = 0;
                }

                int c = str[off++];

                // And then see if we have an Ascii char:
                if (c < 0x80)
                {

                    // If so, can do a tight inner loop:
                    outBuf[outPtr++] = (byte)c;

                    // Let's calc how many ascii chars we can copy at most:
                    int maxInCount = (len - off);
                    int maxOutCount = (outBufLast - outPtr);
                    if (maxInCount > maxOutCount)
                    {
                        maxInCount = maxOutCount;
                    }

                    maxInCount += off;
                    bool continueOuter = false;
                    while (true)
                    {
                        if (off >= maxInCount)
                        {

                            // done with max. ascii seq
                            continueOuter = true;
                            break;
                        }

                        c = str[off++];
                        if (c >= 0x80)
                        {
                            break;
                        }

                        outBuf[outPtr++] = (byte)c;
                    }

                    if (continueOuter)
                    {
                        continue;
                    }
                }

                int[] result = WriteMultiByte(c, outBuf, outPtr, str, off, len);
                if (result == null)
                {
                    break;
                }
                else
                {
                    outPtr = result[0];
                    off = result[1];
                }
            }

            _outPtr = outPtr;
        }

        /* Max. expansion for a single Unicode code point is 4 bytes when
         * recombining UCS-2 surrogate pairs, so:
         */
        /*
    ////////////////////////////////////////////////////////
    // OutSmart.DAXon.Internal.IO.Writer implementation
    ////////////////////////////////////////////////////////
     */
        /* Due to co-variance between Appendable and
     * TextWriter, this would not compile with javac 1.5, in 1.4 mode
     * (source and target set to "1.4". Not a huge deal, but since
     * the base impl is just fine, no point in overriding it.
     */
        /*
    public TextWriter append(char c) throws IOException {
    // note: this is a JDK 1.5 method
        write(c);
        return this;
    }
    */
        /* If we are left with partial surrogate we have a problem, but
             * let's not let it prevent closure of the underlying stream
             */
        /* First, let's ensure we can output at least 4 bytes
             * (longest UTF-8 encoded codepoint):
             */
        /* First, let's ensure we can output at least 4 bytes
             * (longest UTF-8 encoded codepoint):
             */
        // ascii
        // 2-byte
        // 3 bytes
        // 4 bytes
        private int[] WriteMultiByte(int c, byte[] outBuf, int outPtr, string str, int off, int len)
        {

            // Nope, multi-byte:
            if (c < 0x800)
            {

                // 2-byte
                outBuf[outPtr++] = (byte)(0xc0 | (c >> 6));
                outBuf[outPtr++] = (byte)(0x80 | (c & 0x3f));
            }
            else
            {

                // 3 or 4 bytes
                // Surrogates?
                if (c < SURR1_FIRST || c > SURR2_LAST)
                {
                    outBuf[outPtr++] = (byte)(0xe0 | (c >> 12));
                    outBuf[outPtr++] = (byte)(0x80 | ((c >> 6) & 0x3f));
                    outBuf[outPtr++] = (byte)(0x80 | (c & 0x3f));
                    return new int[]
                    {
                        outPtr,
                        off
                    };
                }


                // Yup, a surrogate:
                if (c > SURR1_LAST)
                {

                    // must be from first range
                    _outPtr = outPtr;
                    ThrowIllegal(c);
                }

                _surrogate = c;

                // and if so, followed by another from next range
                if (off >= len)
                {

                    // unless we hit the end?
                    return null;
                }

                c = _convertSurrogate(str[off++]);
                if (c > 0x10FFFF)
                {

                    // illegal, as per RFC 3629
                    _outPtr = outPtr;
                    ThrowIllegal(c);
                }

                outBuf[outPtr++] = (byte)(0xf0 | (c >> 18));
                outBuf[outPtr++] = (byte)(0x80 | ((c >> 12) & 0x3f));
                outBuf[outPtr++] = (byte)(0x80 | ((c >> 6) & 0x3f));
                outBuf[outPtr++] = (byte)(0x80 | (c & 0x3f));
            }

            return new int[]
            {
                outPtr,
                off
            };
        }

        /* Max. expansion for a single Unicode code point is 4 bytes when
         * recombining UCS-2 surrogate pairs, so:
         */
        /*
    ////////////////////////////////////////////////////////
    // OutSmart.DAXon.Internal.IO.Writer implementation
    ////////////////////////////////////////////////////////
     */
        /* Due to co-variance between Appendable and
     * TextWriter, this would not compile with javac 1.5, in 1.4 mode
     * (source and target set to "1.4". Not a huge deal, but since
     * the base impl is just fine, no point in overriding it.
     */
        /*
    public TextWriter append(char c) throws IOException {
    // note: this is a JDK 1.5 method
        write(c);
        return this;
    }
    */
        /* If we are left with partial surrogate we have a problem, but
             * let's not let it prevent closure of the underlying stream
             */
        /* First, let's ensure we can output at least 4 bytes
             * (longest UTF-8 encoded codepoint):
             */
        /* First, let's ensure we can output at least 4 bytes
             * (longest UTF-8 encoded codepoint):
             */
        // ascii
        // 2-byte
        // 3 bytes
        // 4 bytes
        // 2-byte
        /*
    ////////////////////////////////////////////////////////////
    // Internal methods
    ////////////////////////////////////////////////////////////
     */
        private void _flushBuffer()
        {
            if (_outPtr > 0 && _outBuffer != null)
            {
                _out.Write(_outBuffer, 0, _outPtr);
                _outPtr = 0;
            }
        }

        /* Max. expansion for a single Unicode code point is 4 bytes when
         * recombining UCS-2 surrogate pairs, so:
         */
        /*
    ////////////////////////////////////////////////////////
    // OutSmart.DAXon.Internal.IO.Writer implementation
    ////////////////////////////////////////////////////////
     */
        /* Due to co-variance between Appendable and
     * TextWriter, this would not compile with javac 1.5, in 1.4 mode
     * (source and target set to "1.4". Not a huge deal, but since
     * the base impl is just fine, no point in overriding it.
     */
        /*
    public TextWriter append(char c) throws IOException {
    // note: this is a JDK 1.5 method
        write(c);
        return this;
    }
    */
        /* If we are left with partial surrogate we have a problem, but
             * let's not let it prevent closure of the underlying stream
             */
        /* First, let's ensure we can output at least 4 bytes
             * (longest UTF-8 encoded codepoint):
             */
        /* First, let's ensure we can output at least 4 bytes
             * (longest UTF-8 encoded codepoint):
             */
        /*
    ////////////////////////////////////////////////////////////
    // Internal methods
    ////////////////////////////////////////////////////////////
     */
        /// <summary>
        /// Method called to calculate UTF codepoint, from a surrogate pair.
        /// </summary>
        private int _convertSurrogate(int secondPart)
        {
            int firstPart = _surrogate;
            _surrogate = 0;

            // Ok, then, is the second part valid?
            if (secondPart < SURR2_FIRST || secondPart > SURR2_LAST)
            {
                throw new IOException("Broken surrogate pair: first char 0x" + (firstPart).ToString("x", CultureInfo.InvariantCulture) + ", second 0x" + (secondPart).ToString("x", CultureInfo.InvariantCulture) + "; illegal combination");
            }

            return 0x10000 + ((firstPart - SURR1_FIRST) << 10) + (secondPart - SURR2_FIRST);
        }

        /* Max. expansion for a single Unicode code point is 4 bytes when
         * recombining UCS-2 surrogate pairs, so:
         */
        /*
    ////////////////////////////////////////////////////////
    // OutSmart.DAXon.Internal.IO.Writer implementation
    ////////////////////////////////////////////////////////
     */
        /* Due to co-variance between Appendable and
     * TextWriter, this would not compile with javac 1.5, in 1.4 mode
     * (source and target set to "1.4". Not a huge deal, but since
     * the base impl is just fine, no point in overriding it.
     */
        /*
    public TextWriter append(char c) throws IOException {
    // note: this is a JDK 1.5 method
        write(c);
        return this;
    }
    */
        /* If we are left with partial surrogate we have a problem, but
             * let's not let it prevent closure of the underlying stream
             */
        /* First, let's ensure we can output at least 4 bytes
             * (longest UTF-8 encoded codepoint):
             */
        /* First, let's ensure we can output at least 4 bytes
             * (longest UTF-8 encoded codepoint):
             */
        /*
    ////////////////////////////////////////////////////////////
    // Internal methods
    ////////////////////////////////////////////////////////////
     */
        /// <summary>
        /// Method called to calculate UTF codepoint, from a surrogate pair.
        /// </summary>
        private void ThrowIllegal(int code)
        {
            if (code > 0x10FFFF)
            {

                // over max?
                throw new IOException("Illegal character point (0x" + (code).ToString("x", CultureInfo.InvariantCulture) + ") to output; max is 0x10FFFF as per RFC 3629");
            }

            if (code >= SURR1_FIRST)
            {
                if (code <= SURR1_LAST)
                {

                    // Unmatched first part (closing without second part?)
                    throw new IOException("Unmatched first part of surrogate pair (0x" + (code).ToString("x", CultureInfo.InvariantCulture) + ")");
                }

                throw new IOException("Unmatched second part of surrogate pair (0x" + (code).ToString("x", CultureInfo.InvariantCulture) + ")");
            }


            // should we ever get this?
            throw new IOException("Illegal character point (0x" + (code).ToString("x", CultureInfo.InvariantCulture) + ") to output");
        }
    }
}
