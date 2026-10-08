////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Collections;
using System;
namespace OutSmart.DAXon.Text
{
    /// <summary>
    /// Builder class to construct a UnicodeString by appending text incrementally
    /// </summary>
    public sealed class UnicodeBuilder : IUniStringConsumer, IUnicodeWriter
    {
        private int[] codepoints;
        private int used;
        private int bits;
        private ZenoString archive = ZenoString.EMPTY;

        private UnicodeString ActivePart
        {
            get
            {
                if ((bits & 0xff0000) != 0)
                {

                    // use 24-bit codes
                    return new Twine24(codepoints, used);
                }
                else if ((bits & 0xff00) != 0)
                {

                    // use 16-bit codes
                    char[] chars = new char[used];
                    for (int i = 0; i < used; i++)
                    {
                        chars[i] = (char)(codepoints[i] & 0xffff);
                    }

                    return new Twine16(chars);
                }
                else
                {
                    byte[] bytes = new byte[used];
                    for (int i = 0; i < used; i++)
                    {
                        bytes[i] = (byte)(codepoints[i] & 0xff);
                    }

                    return new Twine8(bytes);
                }
            }
        }
        public UnicodeBuilder() : this(256)
        {
        }

        public UnicodeBuilder(int allocate)
        {
            // Floor of 16: EnsureCapacity grows by doubling, so a zero-length array could
            // never grow (0 * 2 == 0) and the first Append spun forever.
            codepoints = new int[allocate < 16 ? 16 : allocate];
        }

        public UnicodeBuilder Append(char ch)
        {
            Append((int)ch);
            return this;
        }

        public UnicodeBuilder Append(int codePoint)
        {
            EnsureCapacity(1);
            codepoints[used++] = codePoint;
            bits |= codePoint;
            return this;
        }

        public UnicodeBuilder Append(IIntIterator codePoints)
        {
            while (codePoints.MoveNext())
            {
                Append(codePoints.Current);
            }

            return this;
        }

        public UnicodeBuilder AppendLatin(string str)
        {
            return Append(new BMPString(str));
        }

        public UnicodeBuilder AppendAll(ISequenceIterator iter)
        {

            // Note: used from bytecode
            for (IItem item; (item = iter.Next()) != null;)
            {
                Append(item.UnicodeStringValue);
            }

            return this;
        }

        public UnicodeBuilder Append(string str)
        {
            return Append(StringTool.FromCharSequence(str));
        }

        public UnicodeBuilder Append(UnicodeString str)
        {
            int len = str.Length32();
            if (len == 0)
            {
                return this;
            }

            // A long string joins the archive as it is. Copied in, it took 4 bytes a character in an array that doubled
            // to hold it, and as many seconds of one call, unchecked, as it has hundreds of millions of characters.
            // A rope joins as one flat copy: its segments, shared, made $a || $a twice as long holding nothing more,
            // and a string doubled in a loop past every limit.
            if (len > 65535)
            {
                if (used > 0)
                {
                    archive = (ZenoString)archive.Concat(ActivePart);
                    used = 0;
                    bits = 0xff;
                }

                archive = (ZenoString)archive.Concat(str is ZenoString ? StringTool.FromCharSequence(str.ToString()) : str);
                return this;
            }

            EnsureCapacity(len);
            str.Copy32bit(codepoints, used);
            used += len;
            int width = str.Width;
            if (width > 8)
            {
                if (width > 16)
                {
                    bits |= 0xffffff;
                }
                else
                {
                    bits |= 0xffff;
                }
            }

            return this;
        }

        public long Length()
        {
            return archive.Length() + used;
        }

        public bool IsEmpty()
        {
            return archive.IsEmpty() && used == 0;
        }

        private void EnsureCapacity(int required)
        {

            // For very long strings, archive what we've already accumulated as a ZenoString
            if (used > 65535)
            {
                archive = (ZenoString)archive.Concat(ActivePart);
                used = 0;
                bits = 0xff;
            }

            while (used + required > codepoints.Length)
            {
                Array.Resize(ref codepoints, codepoints.Length * 2);
            }
        }

        public UnicodeString ToUnicodeString()
        {
            if (archive.IsEmpty())
            {
                return ActivePart;
            }
            else
            {
                return archive.Concat(ActivePart);
            }
        }

        public StringValue ToStringItem(IAtomicType type)
        {
            return new StringValue(ToUnicodeString(), type);
        }

        public override string ToString()
        {
            return ToUnicodeString().ToString();
        }

        public void Clear()
        {
            archive = ZenoString.EMPTY;
            used = 0;
            bits = 0;
        }

        public static byte[] Expand1to2(byte[] @in, int start, int used, int allocate)
        {
            byte[] result = new byte[allocate * 2];
            for (int i = start, j = 0; i < used;)
            {
                result[j++] = 0;
                result[j++] = @in[i++];
            }

            return result;
        }

        public static char[] ExpandBytesToChars(byte[] @in, int start, int end)
        {
            char[] result = new char[end - start];
            for (int i = start, j = 0; i < end;)
            {
                result[j++] = (char)@in[i++];
            }

            return result;
        }

        public static byte[] Expand1to3(byte[] @in, int start, int used, int allocate)
        {
            byte[] result = new byte[allocate * 3];
            for (int i = start, j = 0; i < used;)
            {
                result[j++] = 0;
                result[j++] = 0;
                result[j++] = @in[i++];
            }

            return result;
        }

        public static byte[] Expand2to3(byte[] @in, int start, int used, int allocate)
        {
            byte[] result = new byte[allocate * 3];
            for (int i = start, j = 0; i < used;)
            {
                result[j++] = 0;
                result[j++] = @in[i++];
                result[j++] = @in[i++];
            }

            return result;
        }

        public static byte[] Expand(byte[] @in, int start, int end, int oldWidth, int newWidth, int allocate)
        {
            if (allocate <= (end - start) / oldWidth)
            {
                allocate = (end - start) / oldWidth;
            }

            if (newWidth <= oldWidth)
            {

                // leave the width unchanged; we don't narrow it
                byte[] @out = new byte[allocate * newWidth];
                Array.Copy(@in, start, @out, 0, end * oldWidth);
                return @out;
            }

            if (oldWidth == 1 && newWidth == 2)
            {
                return Expand1to2(@in, start, end, allocate);
            }

            if (oldWidth == 1 && newWidth == 3)
            {
                return Expand1to3(@in, start, end, allocate);
            }

            if (oldWidth == 2 && newWidth == 3)
            {
                return Expand2to3(@in, start, end, allocate);
            }

            throw new ArgumentException();
        }

        public UnicodeBuilder Accept(UnicodeString chars)
        {
            return Append(chars);
        }

        public void Write(UnicodeString chars)
        {
            Append(chars);
        }

        public void WriteAscii(byte[] content)
        {
            Accept(new Twine8(content));
        }

        public void Write(string chars)
        {
            Append(chars);
        }

        public void TrimToSize()
        {

            // used from bytecode
            Array.Resize(ref codepoints, used);
        }

        public void Dispose()
        {
        }
        // IUniStringConsumer end-of-stream event (distinct from IDisposable.Dispose above).
        public void Close()
        {
        }
        IUniStringConsumer IUniStringConsumer.Accept(UnicodeString arg0) => Append(arg0);

        // === Auto-generated stubs (StubGenerator Phase 3.1f) ===
        public void Open() { }
        public void WriteCodePoint(int codepoint) { Append(codepoint); }
        public void WriteRepeatedAscii(byte asciiChar, int count) { for (int __i = 0; __i < count; __i++) { Append((int)asciiChar); } }
        public void Flush() { }
    }
}