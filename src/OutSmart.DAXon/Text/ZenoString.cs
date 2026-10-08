////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Collections;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
using System.IO;
namespace OutSmart.DAXon.Text
{
    internal sealed class ZenoString : UnicodeString
    {

        public static readonly ZenoString EMPTY = new ZenoString();
        private List<UnicodeString> segments = new List<UnicodeString>();
        private List<long> offsets = new List<long>();

        // The segment of the last character asked for: a scan by index stays in one segment for its whole length.
        // Racing threads only ever store a valid index, and every use checks the bounds first.
        private int lastSegment;

        public override int Width
        {
            get
            {
                int maxWidth = 7;
                foreach (UnicodeString entry in segments)
                {
                    int width = entry.Width;
                    if (width == 24)
                    {
                        return 24;
                    }
                    else
                    {
                        maxWidth = Math.Max(maxWidth, width);
                    }
                }

                return maxWidth;
            }
        }
        /// <summary>
        /// Private constructor creating an empty ZenoString (containing an empty list of segments)
        /// </summary>
        private ZenoString()
        {
        }

        /// <summary>
        /// Private constructor creating a ZenoString with a single segment
        /// </summary>
        private ZenoString(UnicodeString content)
        {
            segments.Add(content);
            offsets.Add(0);
        }
        public static ZenoString Of(UnicodeString content)
        {
            if (content is ZenoString)
            {
                return (ZenoString)content;
            }
            else if (content.IsEmpty())
            {
                return new ZenoString();
            }
            else
            {
                return new ZenoString(content);
            }
        }

        private int SegmentForOffset(long offset)
        {
            if (segments.Count == 0)
            {
                throw new IndexOutOfRangeException("ZenoString is empty");
            }

            int start = 0;
            int end = offsets.Count - 1;
            while (start < end)
            {
                int mid = start + (end - start + 1) / 2;
                if (offsets[mid] > offset)
                {
                    end = mid - 1;
                }
                else
                {
                    start = mid;
                }
            }

            if (offset < offsets[start] || offset >= offsets[start] + segments[start].Length())
            {
                throw new IndexOutOfRangeException("Index " + offset + " out of range 0-" + (Length() - 1));
            }

            return start;
        }

        // SegmentForOffset, looking first in the segment found last time.
        private int Locate(long offset)
        {
            int entry = lastSegment;
            if (entry < segments.Count && offset >= offsets[entry]
                && (entry + 1 < offsets.Count ? offset < offsets[entry + 1] : offset < offsets[entry] + segments[entry].Length()))
            {
                return entry;
            }

            entry = SegmentForOffset(offset);
            lastSegment = entry;
            return entry;
        }

        public override IIntIterator CodePoints()
        {
            if (IsEmpty())
            {
                return EmptyIntIterator.GetInstance();
            }

            return new AnonymousIntIterator(this);
        }

        public override long Length()
        {
            int i = segments.Count - 1;
            return i < 0 ? 0 : offsets[i] + segments[i].Length();
        }

        public override bool IsEmpty()
        {
            return segments.Count == 0;
        }

        public override long IndexOf(int codePoint, long from)
        {
            from = Math.Max(from, 0);
            if (from >= Length())
            {
                return -1;
            }

            int first = Locate(from);
            for (int i = first; i < segments.Count; i++)
            {
                UnicodeString segment = segments[i];
                long offset = offsets[i];
                long pos = segment.IndexOf(codePoint, i == first ? from - offset : 0);
                if (pos >= 0)
                {
                    return pos + offset;
                }
            }

            return -1;
        }

        public override long IndexWhere(Func<int, bool> predicate, long from)
        {
            int first = Locate(from);
            for (int i = first; i < segments.Count; i++)
            {
                UnicodeString segment = segments[i];
                long offset = offsets[i];
                long pos = segment.IndexWhere(predicate, i == first ? from - offset : 0);
                if (pos >= 0)
                {
                    return pos + offset;
                }
            }

            return -1;
        }

        public override int CodePointAt(long index)
        {
            int entry = Locate(index);
            return segments[entry].CodePointAt(index - offsets[entry]);
        }

        public override UnicodeString Substring(long start, long end)
        {
            CheckSubstringBounds(start, end);
            if (start == end)
            {
                return EmptyUnicodeString.GetInstance();
            }
            else if (start + 1 == end)
            {
                return new UnicodeChar(CodePointAt(start));
            }

            int first = Locate(start);
            int last = Locate(end - 1);
            if (first == last)
            {
                UnicodeString segment = segments[first];
                long offset = offsets[first];
                return segment.Substring(start - offset, end - offset);
            }
            else
            {
                ZenoString z = ZenoString.Of(segments[first].Substring(start - offsets[first]));
                for (int i = first + 1; i < last; i++)
                {
                    z = (ZenoString)z.Concat(segments[i]);
                }

                return z.Concat(segments[last].Prefix(end - offsets[last]));
            }
        }

        public override bool HasSubstring(UnicodeString other, long offset)
        {

            // Override inherited implementation because codePointAt(n) is relatively expensive
            if (offset < 0 || offset > Length())
            {
                throw new IndexOutOfRangeException();
            }

            long len = other.Length();
            if (len + offset > Length())
            {
                return false;
            }

            long end = offset + len;
            int first = Locate(offset);
            int last = Locate(end - 1);
            if (first == last)
            {
                UnicodeString segment = segments[first];
                long segmentOffset = offsets[first];
                return segment.HasSubstring(other, offset - segmentOffset);
            }
            else
            {
                return Substring(offset, end).Equals(other);
            }
        }

        public override UnicodeString Concat(UnicodeString other)
        {

            // Here's the critical decision - whether or not to merge the new string with the last
            // segment of the previous one
            if (IsEmpty())
            {
                return other is ZenoString ? (ZenoString)other : new ZenoString(other);
            }
            else if (other.IsEmpty())
            {
                return this;
            }

            if (other is ZenoString)
            {
                ZenoString z = new ZenoString();
                z.segments = new List<UnicodeString>(segments);
                z.segments.AddRange(((ZenoString)other).segments);
                z.offsets = new List<long>(offsets);
                long len = Length();
                foreach (long offset in ((ZenoString)other).offsets)
                {
                    z.offsets.Add(offset + len);
                }

                return (len < 32 || other.Length() < 32 ? z.Consolidate0() : z);
            }
            else
            {
                ZenoString z = new ZenoString();
                z.segments = new List<UnicodeString>(segments);
                z.offsets = new List<long>(offsets);
                z.segments.Add(other);
                z.offsets.Add(Length());
                return z.Consolidate0();
            }
        }

        public override void Copy8bit(byte[] target, int offset)
        {
            foreach (UnicodeString us in segments)
            {
                us.Copy8bit(target, offset);
                offset += us.Length32();
            }
        }

        public override void Copy16bit(char[] target, int offset)
        {
            foreach (UnicodeString us in segments)
            {
                us.Copy16bit(target, offset);
                offset += us.Length32();
            }
        }

        public override void Copy24bit(byte[] target, int offset)
        {
            foreach (UnicodeString us in segments)
            {
                us.Copy24bit(target, offset);
                offset += (us.Length32() * 3);
            }
        }

        public override void Copy32bit(int[] target, int offset)
        {
            foreach (UnicodeString us in segments)
            {
                us.Copy32bit(target, offset);
                offset += us.Length32();
            }
        }

        public void WriteSegments(IUnicodeWriter writer)
        {
            foreach (UnicodeString str in segments)
            {
                writer.Write(str);
            }
        }

        public static UnicodeString ConcatSegments(UnicodeString left, UnicodeString right)
        {
            if (left.Width <= 8 && right.Width <= 8)
            {
                byte[] newByteArray = new byte[left.Length32() + right.Length32()];
                left.Copy8bit(newByteArray, 0);
                right.Copy8bit(newByteArray, left.Length32());
                return new Twine8(newByteArray);
            }
            else if (left.Width <= 16 && right.Width <= 16)
            {
                char[] newCharArray = new char[left.Length32() + right.Length32()];
                left.Copy16bit(newCharArray, 0);
                right.Copy16bit(newCharArray, left.Length32());
                return new Twine16(newCharArray);
            }
            else
            {
                byte[] newByteArray = new byte[(left.Length32() + right.Length32()) * 3];
                left.Copy24bit(newByteArray, 0);
                right.Copy24bit(newByteArray, left.Length32() * 3);
                return new Twine24(newByteArray);
            }
        }

        private ZenoString Consolidate0()
        {

            // internal, so works in-situ
            for (int i = segments.Count - 2; i >= 0; i--)
            {
                double nextLength = segments[i + 1].Length() * 1.1;
                if (segments[i].Length() < nextLength)
                {
                    segments[i] = ConcatSegments(segments[i], segments[i + 1]);
                    segments.RemoveAt(i + 1);
                    offsets.RemoveAt(i + 1);

                    // A string doubling from step to step of a loop outgrows the loop's own checks (see ZenoChain.Reorganize):
                    // a big merge looks at the time and memory limits.
                    if (segments[i].Length() >= 65536)
                    {
                        OutSmart.DAXon.Core.Controller.CheckActiveTimeoutNow();
                    }
                }
            }

            return this;
        }

        public override UnicodeString Economize()
        {
            int segs = segments.Count;
            if (segs == 0)
            {
                return EmptyUnicodeString.GetInstance();
            }
            else if (segs == 1)
            {
                return segments[0];
            }
            else if (segs < 32 && Length() < 256 && Width <= 16)
            {

                // Return a single wrapped Java String, for economy of any subsequent toString() operations.
                return new BMPString(ToString());
            }
            else
            {
                return this;
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            foreach (UnicodeString str in segments)
            {
                sb.Append(str.ToString());
            }

            return sb.ToString();
        }

        private sealed class AnonymousIntIterator : AbstractIntIterator
        {

            private readonly ZenoString parent;
            private IEnumerator<UnicodeString> outerIterator;
            private UnicodeString outerLookahead;
            private bool outerLookaheadFilled;
            IIntIterator innerIterator;
            int cpLookahead;
            bool cpLookaheadFilled;
            public AnonymousIntIterator(ZenoString parent)
            {
                this.parent = parent;
                this.outerIterator = parent.segments.GetEnumerator();
            }

            private bool OuterHasNext()
            {
                if (!outerLookaheadFilled && outerIterator.MoveNext())
                {
                    outerLookahead = outerIterator.Current;
                    outerLookaheadFilled = true;
                }

                return outerLookaheadFilled;
            }

            private UnicodeString OuterNext()
            {
                if (!outerLookaheadFilled)
                {
                    outerIterator.MoveNext();
                    outerLookahead = outerIterator.Current;
                }

                outerLookaheadFilled = false;
                return outerLookahead;
            }

            public override bool HasNext()
            {
                if (cpLookaheadFilled)
                {
                    return true;
                }

                while (true)
                {
                    if (innerIterator != null && innerIterator.MoveNext())
                    {
                        cpLookahead = innerIterator.Current;
                        cpLookaheadFilled = true;
                        return true;
                    }
                    else if (OuterHasNext())
                    {
                        innerIterator = OuterNext().CodePoints();
                    }
                    else
                    {
                        return false;
                    }
                }
            }

            public override int Next()
            {
                cpLookaheadFilled = false;
                return cpLookahead;
            }
        }
    }
}
