////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
using System.IO;
namespace OutSmart.DAXon.Text
{
    public sealed class LargeTextBuffer
    {
        private const int BITS = 16;
        private const int SEGLEN = 1 << BITS;
        private const int MASK = SEGLEN - 1;
        private static readonly int MAX_SEGMENTS = 1 << (31 - BITS);
        private static readonly ISegment EMPTY_SEGMENT = new Segment8(new byte[] { });
        private readonly IList<ISegment> completeSegments;
        private ISegment lastSegment;
        private int lastSegmentLength;
        private int initialSize;

        public LargeTextBuffer(int initialSize)
        {
            completeSegments = new List<ISegment>(4);
            lastSegment = EMPTY_SEGMENT;
            lastSegmentLength = 0;
            this.initialSize = Math.Max(initialSize, 65536);
        }

        private void AddSegment(ISegment segment)
        {
            if (completeSegments.Count == MAX_SEGMENTS)
            {
                throw new InvalidOperationException("TinyTree capacity exceeded: more than 2^31 characters of text data");
            }

            completeSegments.Add(segment);
        }

        private ISegment GetSegment(int n)
        {
            if (n == completeSegments.Count)
            {
                return lastSegment;
            }
            else
            {
                return completeSegments[n];
            }
        }

        public void AppendUnicodeString(UnicodeString chars)
        {
            if (chars.IsEmpty())
            {
                return;
            }

            long charsSupplied = chars.Length32(); // read once; reused below and for the first segment size
            if (lastSegment == EMPTY_SEGMENT)
            {

                // indicates this is the first string being added; a Latin1-content
                // BMPString (typical stylesheet literal) starts a byte segment
                int newWidth = chars.Width;
                if (newWidth == 16 && chars is BMPString firstBmp && firstBmp.IsLatin1)
                {
                    newWidth = 8;
                }

                int newLength = Math.Max(initialSize, (int)charsSupplied) & 65535;
                if (newWidth <= 8)
                {
                    lastSegment = new Segment8(new byte[newLength]);
                }
                else if (newWidth == 16)
                {
                    lastSegment = new Segment16(new char[newLength]);
                }
                else
                {
                    lastSegment = new Segment24(new byte[newLength * 3]);
                }
            }

            int spaceAvailableInLastSegment = SEGLEN - lastSegmentLength;

            if (charsSupplied < spaceAvailableInLastSegment)
            {
                ExtendLastSegment(chars);
            }
            else
            {
                long start = 0;
                ExtendLastSegment(chars.Substring(0, spaceAvailableInLastSegment));
                charsSupplied -= spaceAvailableInLastSegment;
                start += spaceAvailableInLastSegment;
                while (charsSupplied > SEGLEN)
                {
                    OutSmart.DAXon.Core.Controller.CheckActiveTimeoutNow();   // a text node of a long string, one segment at a time
                    ExtendLastSegment(chars.Substring(start, start + SEGLEN));
                    charsSupplied -= SEGLEN;
                    start += SEGLEN;
                }

                if (charsSupplied > 0)
                {

                    ExtendLastSegment(chars.Substring(start, start + charsSupplied));
                }
            }
        }

        private void ExtendLastSegment(UnicodeString chars)
        {

            // A Latin1-content BMPString (typical stylesheet literal) appends as bytes:
            // one ASCII literal must not widen the whole segment to char[] for good
            int width = chars.Width;
            if (width == 16 && lastSegment.Width <= 8 && chars is BMPString bmp && bmp.IsLatin1)
            {
                width = 8;
            }

            int addedLen = chars.Length32(); // hoisted: was called twice (Stretch + lastSegmentLength)
            lastSegment = lastSegment.Stretch(lastSegmentLength, lastSegmentLength + addedLen, width);
            if (lastSegment is Segment8)
            {
                chars.Copy8bit(((Segment8)lastSegment).bytes, lastSegmentLength);
            }
            else if (lastSegment is Segment16)
            {
                chars.Copy16bit(((Segment16)lastSegment).chars, lastSegmentLength);
            }
            else
            {
                chars.Copy24bit(((Segment24)lastSegment).bytes, lastSegmentLength * 3);
            }

            lastSegmentLength += addedLen;

            if (lastSegmentLength == SEGLEN)
            {
                AddSegment(lastSegment);
                lastSegment = new Segment8(new byte[1024]);
                lastSegmentLength = 0;
            } //showSegmentLengths();
        }

        // Single-pass fast lane for the dominant case: Latin1 text into an existing 8-bit segment
        // with no spill. Scans and copies in one loop; bailing on the first wide char leaves only
        // dead bytes beyond lastSegmentLength (never bumped), so the caller can rerun the generic
        // two-pass route. Returns false without side effects visible to any reader.
        internal bool TryAppendLatin1(char[] chars, int len)
        {
            // EMPTY_SEGMENT is itself a (shared, static) Segment8 — stretching it would corrupt the
            // sentinel for every buffer. First appends take the generic route, which replaces it.
            if (lastSegment == EMPTY_SEGMENT || !(lastSegment is Segment8) || len >= SEGLEN - lastSegmentLength)
            {
                return false;
            }

            lastSegment = lastSegment.Stretch(lastSegmentLength, lastSegmentLength + len, 8);
            byte[] b = ((Segment8)lastSegment).bytes;
            int off = lastSegmentLength;
            for (int j = 0; j < len; j++)
            {
                char c = chars[j];
                if (c > 255)
                {
                    return false;
                }

                b[off + j] = (byte)c;
            }

            lastSegmentLength += len;
            return true;
        }

        // Fused append from the pump's raw char buffer (XmlReaderToReceiver -> TinyBuilder): same
        // content and Length() outcome as Compress-to-Twine + AppendUnicodeString, minus that
        // intermediate allocation+copy. max/surrogates come from the caller's scan (the same scan
        // Compress performs), so the width verdict per append is identical.
        internal void AppendCharSpan(char[] chars, int len, int max, int surrogates)
        {
            if (len == 0)
            {
                return;
            }

            int width = max < 256 ? 8 : (surrogates == 0 ? 16 : 24);
            int cpLeft = len - surrogates / 2;
            if (lastSegment == EMPTY_SEGMENT)
            {
                int newLength = Math.Max(initialSize, cpLeft) & 65535;
                if (width == 8)
                {
                    lastSegment = new Segment8(new byte[newLength]);
                }
                else if (width == 16)
                {
                    lastSegment = new Segment16(new char[newLength]);
                }
                else
                {
                    lastSegment = new Segment24(new byte[newLength * 3]);
                }
            }

            int i = 0;
            while (cpLeft > 0)
            {
                int take = Math.Min(cpLeft, SEGLEN - lastSegmentLength);
                lastSegment = lastSegment.Stretch(lastSegmentLength, lastSegmentLength + take, width);
                if (lastSegment is Segment8 s8)
                {
                    byte[] b = s8.bytes;
                    for (int j = lastSegmentLength, e = lastSegmentLength + take; j < e;)
                    {
                        b[j++] = (byte)chars[i++];
                    }
                }
                else if (lastSegment is Segment16 s16)
                {
                    Array.Copy(chars, i, s16.chars, lastSegmentLength, take);
                    i += take;
                }
                else
                {
                    byte[] b = ((Segment24)lastSegment).bytes;
                    int o = lastSegmentLength * 3;
                    for (int j = 0; j < take; j++)
                    {
                        char c = chars[i++];
                        int cp = Serialization.CharCodes.UTF16CharacterSet.IsSurrogate(c)
                            ? Serialization.CharCodes.UTF16CharacterSet.CombinePair(c, chars[i++]) : c;
                        b[o++] = (byte)((cp & 0xffffff) >> 16);
                        b[o++] = (byte)((cp & 0xffff) >> 8);
                        b[o++] = (byte)(cp & 0xff);
                    }
                }

                lastSegmentLength += take;
                cpLeft -= take;
                if (lastSegmentLength == SEGLEN)
                {
                    AddSegment(lastSegment);
                    lastSegment = new Segment8(new byte[1024]);
                    lastSegmentLength = 0;
                }
            }
        }

        // Whiteness test without materializing the text: the stripped-view walk asked this of
        // every text node via Substring + IsAllWhite, allocating a string per node. Scans the
        // byte segments in place; non-white content fails on its first codepoint.
        public bool IsAllWhite(int start, int end)
        {
            int i = start;
            while (i < end)
            {
                int segNr = i >> BITS;
                int offset = i & MASK;
                int segEnd = Math.Min(end - (segNr << BITS), SEGLEN);
                ISegment seg = GetSegment(segNr);
                if (seg is Segment8 s8)
                {
                    byte[] bytes = s8.bytes;
                    for (int k = offset; k < segEnd; k++)
                    {
                        if (!Values.Whitespace.IsWhite(bytes[k] & 0xff))
                        {
                            return false;
                        }
                    }
                }
                else if (!Values.Whitespace.IsAllWhite(seg.Substring(offset, segEnd)))
                {
                    return false;
                }

                i = (segNr << BITS) + segEnd;
            }

            return true;
        }

        public UnicodeString Substring(int start, int end)
        {
            int firstSeg = start >> BITS;
            int lastSeg = (end - 1) >> BITS;
            int lastCP = end & MASK;
            if (lastCP == 0)
            {
                lastCP = SEGLEN;
            }

            if (firstSeg == lastSeg)
            {

                // String falls entirely within one segment
                try
                {
                    ISegment seg = GetSegment(firstSeg);
                    return seg.Substring(start & MASK, lastCP);
                }
                catch (IndexOutOfRangeException e)
                {
                    throw;
                }
            }
            else
            {

                // Concatenate strings from two or more segments
                UnicodeBuilder ub = new UnicodeBuilder();
                int segNr = firstSeg;
                ub.Accept(GetSegment(segNr++).Substring(start & MASK, SEGLEN));
                while (segNr < lastSeg)
                {
                    ub.Accept(GetSegment(segNr++).AsUnicodeString());
                }

                ub.Accept(GetSegment(lastSeg).Substring(0, lastCP));
                return ub.ToUnicodeString();
            }
        }

        // Span accessor for fused byte-path consumers: the backing byte array and local offsets
        // when [start,end) lies within one 8-bit segment; false otherwise (16/24-bit text or a
        // segment split), sending the caller down the generic string route.
        internal bool TryGetByteSpan(int start, int end, out byte[] bytes, out int off, out int len)
        {
            bytes = null;
            off = 0;
            len = 0;
            if (start >= end)
            {
                return false;
            }

            int firstSeg = start >> BITS;
            if (firstSeg != (end - 1) >> BITS || !(GetSegment(firstSeg) is Segment8 s8))
            {
                return false;
            }

            bytes = s8.bytes;
            off = start & MASK;
            len = end - start;
            return true;
        }

        public void Dispose()
        {
        }

        public int Length()
        {
            if (lastSegment == EMPTY_SEGMENT)
            {
                return 0;
            }
            else if (lastSegment == null)
            {
                return (completeSegments.Count - 1) * SEGLEN + lastSegmentLength;
            }
            else
            {
                return completeSegments.Count * SEGLEN + lastSegmentLength;
            }
        }

        public void SetLength(int newLength)
        {

            // used to remove a text node if it's found to be a duplicate
            if (newLength < Length())
            {
                int segCount = completeSegments.Count;
                if (newLength <= segCount * SEGLEN)
                {

                    // drop the current "last segment", and make the last segment in the completed list
                    // the new "last segment"
                    lastSegment = completeSegments[segCount - 1];
                    completeSegments.RemoveAt(segCount - 1);
                }

                lastSegmentLength = newLength & MASK;
            }
        }
        // Storage the buffer holds, in bytes: what a tree's text costs a call's memory limit.
        internal long RetainedBytes()
        {
            long bytes = lastSegment.Bytes;
            foreach (ISegment segment in completeSegments)
            {
                bytes += segment.Bytes;
            }

            return bytes;
        }

        private interface ISegment
        {
            int Width { get; }
            long Bytes { get; }
            ISegment Stretch(int oldLength, int newLength, int newWidth);
            UnicodeString AsUnicodeString();
            UnicodeString Substring(int start, int end);
        }

        /// <summary>
        /// A ISegment comprising 8-bit characters (codepoints in the range 0-255)
        /// </summary>
        private sealed class Segment8 : ISegment
        {
            public byte[] bytes;

            public int Width => 8;
            public long Bytes => bytes.Length;
            public Segment8(byte[] bytes)
            {
                this.bytes = bytes;
            }

            public ISegment Stretch(int oldLength, int newLength, int newWidth)
            {
                if (newWidth <= 8)
                {
                    if (newLength > bytes.Length)
                    {
                        bytes = ArrayTools.CopyOf(bytes, Math.Max(newLength, Math.Min(oldLength * 2, SEGLEN)));
                    }

                    return this;
                }
                else if (newWidth == 16)
                {
                    char[] array16 = new char[newLength];
                    StringTool.Copy8to16(bytes, 0, array16, 0, oldLength);
                    return new Segment16(array16);
                }
                else
                {
                    byte[] array24 = new byte[newLength * 3];
                    StringTool.Copy8to24(bytes, 0, array24, 0, oldLength);
                    return new Segment24(array24);
                }
            }

            public UnicodeString AsUnicodeString()
            {
                return new Twine8(bytes);
            }

            public UnicodeString Substring(int start, int end)
            {
                return new Slice8(bytes, start, end);
            }
        }

        /// <summary>
        /// A ISegment comprising 16-bit characters (codepoints in the range 0-65535)
        /// </summary>
        private sealed class Segment16 : ISegment
        {
            public char[] chars;

            public int Width => 16;
            public long Bytes => chars.Length * 2L;
            public Segment16(char[] chars)
            {
                this.chars = chars;
            }

            public ISegment Stretch(int oldLength, int newLength, int newWidth)
            {
                if (newWidth <= 16)
                {
                    if (newLength > chars.Length)
                    {
                        chars = ArrayTools.CopyOf(chars, Math.Max(newLength, Math.Min(oldLength * 2, SEGLEN)));
                    }

                    return this;
                }
                else
                {
                    byte[] array24 = new byte[newLength * 3];
                    StringTool.Copy16to24(chars, 0, array24, 0, oldLength);
                    return new Segment24(array24);
                }
            }

            public UnicodeString AsUnicodeString()
            {
                return new Twine16(chars);
            }

            public UnicodeString Substring(int start, int end)
            {
                return new Slice16(chars, start, end);
            }
        }

        /// <summary>
        /// A ISegment comprising 24-bit characters (any Unicode codepoints)
        /// </summary>
        private sealed class Segment24 : ISegment
        {
            public byte[] bytes;

            public int Width => 24;
            public long Bytes => bytes.Length;
            public Segment24(byte[] bytes)
            {
                this.bytes = bytes;
            }

            public ISegment Stretch(int oldLength, int newLength, int newWidth)
            {
                if (newLength * 3 > bytes.Length)
                {
                    bytes = ArrayTools.CopyOf(bytes, Math.Max(newLength * 3, Math.Min(oldLength * 6, SEGLEN * 3)));
                }

                return this;
            }

            public UnicodeString Substring(int start, int length)
            {
                return new Slice24(bytes, start, length);
            }

            public UnicodeString AsUnicodeString()
            {
                return new Twine24(bytes);
            }
        }
    }
}