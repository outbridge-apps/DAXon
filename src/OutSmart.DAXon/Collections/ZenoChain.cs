////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Collections.Generic;
using System.Text;
namespace OutSmart.DAXon.Collections.Zeno
{
    internal sealed class ZenoChain<T> : IEnumerable<T>
    {
        private readonly List<List<T>> masterList;

        // C# indexer alias for Java's get(int).
        public T this[int n] => Get(n);
        public ZenoChain()
        {
            masterList = new List<List<T>>(8);
        }

        private ZenoChain(List<List<T>> masterList)
        {
            this.masterList = masterList;
        }

        public ZenoChain<T> Add(T item)
        {
            List<List<T>> masterList2 = new List<List<T>>(masterList);

            // If the list is empty, create a new singleton list
            if (masterList2.Count == 0)
            {
                List<T> newSegment = new List<T>(32);
                newSegment.Add(item);
                masterList2.Add(newSegment);
                return new ZenoChain<T>(masterList2);
            }

            int threshold = 32;
            int index = masterList2.Count - 1;

            // Get the last segment
            List<T> segment = masterList2[index];
            if (segment.Count < threshold)
            {

                // if the last segment is smaller than the threshold size, copy it,
                // add the item to the new copy, and change the master list to
                // refer to the new segment.
                List<T> segment2 = new List<T>(32);
                segment2.AddRange(segment);
                segment2.Add(item);
                masterList2[index] = segment2;
                return new ZenoChain<T>(masterList2);
            }
            else
            {

                // if the last segment has reached the threshold size, consider
                // combining it with the penultimate segment
                while (true)
                {
                    index--;
                    threshold *= 2;
                    if (index < 0)
                    {

                        // we've reached the start of the list. No combining of segments
                        // is possible, so just create a new final segment containing the new item alone
                        List<T> newFinalSegment = new List<T>();
                        newFinalSegment.Add(item);
                        masterList2.Add(newFinalSegment);
                        return new ZenoChain<T>(masterList2);
                    }

                    List<T> priorSegment = masterList2[index];
                    if (priorSegment.Count + segment.Count <= threshold)
                    {

                        // combine two adjacent segments into one
                        List<T> combinedSegment = new List<T>(priorSegment.Count + segment.Count);
                        combinedSegment.AddRange(priorSegment);
                        combinedSegment.AddRange(segment);

                        // add the combined segment to the master list, in place of the first of the pair
                        masterList2[index] = Held(combinedSegment);

                        // remove the second of the pair segment
                        masterList2.RemoveAt(index + 1);

                        // create a new final segment containing the new item alone
                        List<T> newFinalSegment = new List<T>();
                        newFinalSegment.Add(item);

                        // and add it to the master list
                        masterList2.Add(newFinalSegment);
                        return new ZenoChain<T>(masterList2);
                    }


                    // These two segments couldn't be combined because the total size was too large
                    // so we now consider merging earlier segments. For example if the segment sizes
                    // were (64, 64, 32) we will merge the first two to become (128, 32)
                    segment = priorSegment; // continue looping
                }
            }
        }

        public ZenoChain<T> Prepend(T item)
        {
            List<List<T>> masterList2 = new List<List<T>>(masterList);

            // If the list is empty, create a new singleton list
            if (masterList2.Count == 0)
            {
                return Add(item);
            }

            int threshold = 32;
            int index = 0;
            List<T> segment = masterList2[index];

            // If the first segment is small enough, extend it by creating a copy
            // with one extra item at the start
            if (segment.Count < threshold)
            {
                List<T> segment2 = new List<T>(32);
                segment2.Add(item);
                segment2.AddRange(segment);
                masterList2[index] = segment2;
                return new ZenoChain<T>(masterList2);
            }
            else
            {

                // Starting with the first two segments, see if there are two adjacent segments that
                // can be concatenated into a single segment without exceeding a threshold size. The
                // threshold size increases the further you are from the start of the sequence,
                while (true)
                {
                    index++;
                    threshold *= 2;
                    if (index >= masterList2.Count)
                    {

                        // We've got to the end without finding two segments to concatenate.
                        // Simply add a new singleton segment at the start.
                        List<T> newInitialSegment = new List<T>();
                        newInitialSegment.Add(item);
                        masterList2.Insert(0,newInitialSegment);
                        return new ZenoChain<T>(masterList2);
                    }

                    List<T> nextSegment = masterList2[index];

                    // Try joining this segment and the next segment
                    if (nextSegment.Count + segment.Count <= threshold)
                    {
                        List<T> combinedSegment = new List<T>();
                        combinedSegment.AddRange(segment);
                        combinedSegment.AddRange(nextSegment);
                        masterList2[index] = Held(combinedSegment);
                        masterList2.RemoveAt(index - 1);

                        // Now add a new singleton segment at the start
                        List<T> newInitialSegment = new List<T>();
                        newInitialSegment.Add(item);
                        masterList2.Insert(0,newInitialSegment);
                        return new ZenoChain<T>(masterList2);
                    }


                    // Continue looking for a pair of adjacent segments to combine
                    segment = nextSegment;
                }
            }
        }

        public ZenoChain<T> AddAll(IEnumerable<T> items)
        {
            ZenoChain<T> result = this;
            int count = 0;
            foreach (T item in items)
            {
                result = result.Add(item);
                if ((++count & 0xFFFF) == 0)
                {
                    OutSmart.DAXon.Core.Controller.CheckActiveTimeoutNow();   // as in Reorganize
                }
            }

            return result;
        }

        public ZenoChain<T> Concat(ZenoChain<T> other)
        {
            List<List<T>> newMaster = new List<List<T>>(masterList.Count + other.masterList.Count);
            newMaster.AddRange(masterList);
            newMaster.AddRange(other.masterList);
            return new ZenoChain<T>(newMaster).Reorganize();
        }

        public ZenoChain<T> Replace(int n, T value)
        {
            if (n < 0)
            {
                throw new IndexOutOfRangeException("Index " + n + " is negative");
            }

            int offset = 0;
            List<List<T>> masterList2 = new List<List<T>>(masterList.Count);
            bool done = false;
            foreach (List<T> segment in masterList)
            {
                if (offset + segment.Count > n && !done)
                {
                    List<T> replacementSegment = new List<T>(segment.Count);
                    replacementSegment.AddRange(segment.GetRange(0, (n - offset) - (0)));
                    replacementSegment.Add(value);
                    replacementSegment.AddRange(segment.GetRange(n - offset + 1, (segment.Count) - (n - offset + 1)));
                    masterList2.Add(Held(replacementSegment));
                    done = true;
                }
                else
                {
                    masterList2.Add(segment);
                }

                offset += segment.Count;
            }

            if (!done)
            {
                throw new IndexOutOfRangeException("Index " + n + " is too large");
            }

            return new ZenoChain<T>(masterList2);
        }

        public ZenoChain<T> Remove(int n)
        {
            if (n < 0)
            {
                throw new IndexOutOfRangeException("Index " + n + " is negative");
            }

            int offset = 0;
            List<List<T>> masterList2 = new List<List<T>>(masterList.Count);
            bool done = false;
            foreach (List<T> segment in masterList)
            {
                if (offset + segment.Count > n && !done)
                {
                    if (segment.Count > 1)
                    {
                        List<T> replacementSegment = new List<T>(segment.Count - 1);
                        replacementSegment.AddRange(segment.GetRange(0, (n - offset) - (0)));
                        replacementSegment.AddRange(segment.GetRange(n - offset + 1, (segment.Count) - (n - offset + 1)));
                        masterList2.Add(Held(replacementSegment));
                    }

                    done = true;
                }
                else
                {
                    masterList2.Add(segment);
                }

                offset += segment.Count;
            }

            if (!done)
            {
                throw new IndexOutOfRangeException("Index " + n + " is too large");
            }

            return new ZenoChain<T>(masterList2);
        }

        public ZenoChain<T> Insert(int n, T value)
        {
            if (n < 0)
            {
                throw new IndexOutOfRangeException("Index " + n + " is negative");
            }

            if (n == 0)
            {
                return Prepend(value);
            }

            int length = Size();
            if (n == length)
            {
                return Add(value);
            }

            if (n > length)
            {
                throw new IndexOutOfRangeException("Index " + n + " is too large");
            }

            int offset = 0;
            List<List<T>> masterList2 = new List<List<T>>(masterList.Count);
            bool done = false;
            foreach (List<T> segment in masterList)
            {
                if (offset + segment.Count > n && !done)
                {
                    List<T> replacementSegment = new List<T>(segment.Count + 1);
                    replacementSegment.AddRange(segment.GetRange(0, (n - offset) - (0)));
                    replacementSegment.Add(value);
                    replacementSegment.AddRange(segment.GetRange(n - offset, (segment.Count) - (n - offset)));
                    masterList2.Add(Held(replacementSegment));
                    done = true;
                }
                else
                {
                    masterList2.Add(segment);
                }

                offset += segment.Count;
            }

            if (!done)
            {

                // Shouldn't happen
                throw new IndexOutOfRangeException("Index " + n + " is too large");
            }

            return new ZenoChain<T>(masterList2);
        }

        private ZenoChain<T> Reorganize()
        {

            // Useful after concatenating multiple chains, to reduce the number of segments.
            // Starting from the right, if we find a segment that is smaller than both its
            // neighbours, merge it with its left-hand neighbour.
            for (int i = masterList.Count - 2; i >= 1; i--)
            {
                int priorSize = masterList[i - 1].Count;
                int segSize = masterList[i].Count;
                int nextSize = masterList[i + 1].Count;
                if (segSize <= priorSize && segSize <= nextSize)
                {
                    List<T> combinedSegment = new List<T>(priorSize + segSize);
                    combinedSegment.AddRange(masterList[i - 1]);
                    combinedSegment.AddRange(masterList[i]);
                    masterList[i - 1] = Held(combinedSegment);
                    masterList.RemoveAt(i);

                    // A sequence doubling from step to step of a loop outgrows the loop's own checks, whose stride grew
                    // while the steps were cheap: a big merge looks at the time and memory limits.
                    if (priorSize + segSize >= 65536)
                    {
                        OutSmart.DAXon.Core.Controller.CheckActiveTimeoutNow();
                    }
                }
            }

            return new ZenoChain<T>(masterList);
        }

        public T Get(int n)
        {
            if (n < 0)
            {
                throw new IndexOutOfRangeException("Index " + n + " is negative");
            }

            int offset = 0;
            foreach (List<T> segment in masterList)
            {
                if (offset + segment.Count > n)
                {
                    return segment[n - offset];
                }

                offset += segment.Count;
            }

            throw new IndexOutOfRangeException("Index " + n + " is too large");
        }

        public ZenoChain<T> SubList(int start, int end)
        {

            // The implementation approach is as follows. We always create a new master list.
            // Segments of the original list that are fully in the range of the sublist are
            // referenced from the new master list directly, without copying. Segments that
            // overlap the requested start and end points are partially "copied", as required,
            // using the Java {@List.sublist()} mechanism - which does not actually create a
            // copy.
            if (start < 0 || start > end)
            {
                throw new IndexOutOfRangeException("start position for subList is out of range");
            }

            List<List<T>> newMaster = new List<List<T>>();
            int offset = 0;
            int remainingLength = end - start;
            bool active = false;

            // Process all the segments, with different treatment for (a) segments before the
            // start position, (b) segments overlapping the start position, (c) segments wholly
            // included in the sublist, (d) segments overlapping the end position, (e) segments
            // beyond the end position.
            foreach (List<T> segment in masterList)
            {
                if (active)
                {
                    if (remainingLength > segment.Count)
                    {

                        // ISegment is wholly included
                        remainingLength -= segment.Count;
                        newMaster.Add(segment); // No need to copy the segment, because it's immutable
                    }
                    else
                    {

                        // ISegment spans the end position
                        newMaster.Add(Held(new List<T>(segment.GetRange(0, (remainingLength) - (0)))));
                        return new ZenoChain<T>(newMaster);
                    }
                }
                else if (offset + segment.Count > start)
                {

                    // segment spans the start position
                    int localStart = start - offset;
                    if (remainingLength > segment.Count - localStart)
                    {

                        // we copy this segment to the end
                        if (start == 1 && segment.Count > 128)
                        {

                            // special case for tail() - break a long first segment to reduce the cost next time.
                            // This assumes it's likely tail() will be called again on the sublist
                            newMaster.Add(Held(new List<T>(segment.GetRange(localStart, (localStart + 64) - (localStart)))));
                            newMaster.Add(Held(new List<T>(segment.GetRange(localStart + 64, (segment.Count) - (localStart + 64)))));
                        }
                        else
                        {
                            newMaster.Add(Held(new List<T>(segment.GetRange(localStart, (segment.Count) - (localStart)))));
                        }

                        remainingLength -= (segment.Count - localStart);
                        active = true;
                    }
                    else
                    {

                        // segment spans both the start and end positions
                        newMaster.Add(Held(new List<T>(segment.GetRange(localStart, (localStart + remainingLength) - (localStart)))));
                        return new ZenoChain<T>(newMaster);
                    }
                }
                else if (remainingLength == 0)
                {
                    break; // do nothing; we're past the end position.
                }

                offset += segment.Count;
            }

            if (remainingLength > 0)
            {
                throw new IndexOutOfRangeException("end position for subList is out of range");
            }

            return new ZenoChain<T>(newMaster);
        }

        // A large segment is entered in the ledger of the call making it, once: the chains that share it hold it.
        private static List<T> Held(List<T> segment)
        {
            if (segment.Count >= OutSmart.DAXon.Core.MemoryLedger.BigCount)
            {
                OutSmart.DAXon.Core.MemoryLedger.Hold(segment, SegmentSizer);
            }

            return segment;
        }

        private static readonly Func<object, long> SegmentSizer = o => OutSmart.DAXon.Core.MemoryLedger.ListBytes((List<T>)o);

        // copy.
        public int Size()
        {
            int total = 0;
            foreach (List<T> segment in masterList)
            {
                total += segment.Count;
            }

            return total;
        }

        // copy.
        public bool IsEmpty()
        {
            return masterList.Count == 0 || (masterList.Count == 1 && masterList[0].Count == 0);
        }

        // copy.
        public IEnumerator<T> IIterator()
        {
            return new ZenoChainIterator<T>(masterList);
        }

        // copy.
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            foreach (IList<T> segment in masterList)
            {
                sb.Append('(');
                foreach (T item in segment)
                {
                    sb.Append(item).Append(',');
                }

                sb[sb.Length - 1] = ')';
            }

            return sb.ToString();
        }
        public IEnumerator<T> GetEnumerator() => new ZenoChainIterator<T>(masterList);
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}