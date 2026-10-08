////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Text;
namespace OutSmart.DAXon.Collections
{
    internal sealed class IntRangeSet : IntSet
    {
        private int[] startPoints;
        private int[] endPoints;
        private int used = 0;
        private int _hashCode = -1;
        private int count = 0;

        public int[] StartPoints => startPoints;

        public int[] EndPoints => endPoints;

        public int NumberOfRanges => used;
        public IntRangeSet()
        {
            startPoints = new int[4];
            endPoints = new int[4];
            used = 0;
            count = 0;
            _hashCode = -1;
        }

        public IntRangeSet(int[] startPoints, int[] endPoints)
        {
            if (startPoints.Length != endPoints.Length)
            {
                throw new ArgumentException("Array lengths differ");
            }

            this.startPoints = startPoints;
            this.endPoints = endPoints;
            used = startPoints.Length;
            for (int i = 0; i < used; i++)
            {
                count += (endPoints[i] - startPoints[i] + 1);
            }
        }

        public override void Clear()
        {
            startPoints = new int[4];
            endPoints = new int[4];
            used = 0;
            _hashCode = -1;
        }

        public override IntSet Copy()
        {
            IntRangeSet s = new IntRangeSet();
            s.startPoints = new int[startPoints.Length];
            Array.Copy(startPoints, 0, s.startPoints, 0, startPoints.Length);

            //s.startPoints = Arrays.copyOf(startPoints, startPoints.length);
            s.endPoints = new int[endPoints.Length];
            Array.Copy(endPoints, 0, s.endPoints, 0, endPoints.Length);

            //s.endPoints = Arrays.copyOf(endPoints, endPoints.length);
            s.used = used;
            s.count = count;
            return s;
        }

        public override IntSet MutableCopy()
        {
            return Copy();
        }

        public override bool IsMutable()
        {
            return false;
        }

        public override int Size()
        {
            return count;
        }

        public override bool IsEmpty()
        {
            return count == 0;
        }

        public override bool Contains(int value)
        {
            if (used == 0)
            {
                return false;
            }

            if (value > endPoints[used - 1])
            {
                return false;
            }

            if (value < startPoints[0])
            {
                return false;
            }

            int i = 0;
            int j = used;
            do
            {
                int mid = i + (j - i) / 2;
                if (endPoints[mid] < value)
                {
                    i = Math.Max(mid, i + 1);
                }
                else if (startPoints[mid] > value)
                {
                    j = Math.Min(mid, j - 1);
                }
                else
                {
                    return true;
                }
            }
            while (i != j);
            return false;
        }

        public override bool Remove(int value)
        {
            throw new NotSupportedException("remove");
        }

        public override bool Add(int value)
        {
            _hashCode = -1;
            if (used == 0)
            {
                EnsureCapacity(1);
                startPoints[used - 1] = value;
                endPoints[used - 1] = value;
                count++;
                return true;
            }

            if (value > endPoints[used - 1])
            {
                if (value == endPoints[used - 1] + 1)
                {
                    endPoints[used - 1]++;
                }
                else
                {
                    EnsureCapacity(used + 1);
                    startPoints[used - 1] = value;
                    endPoints[used - 1] = value;
                }

                count++;
                return true;
            }

            if (value < startPoints[0])
            {
                if (value == startPoints[0] - 1)
                {
                    startPoints[0]--;
                }
                else
                {
                    EnsureCapacity(used + 1);
                    Array.Copy(startPoints, 0, startPoints, 1, used - 1);
                    Array.Copy(endPoints, 0, endPoints, 1, used - 1);
                    startPoints[0] = value;
                    endPoints[0] = value;
                }

                count++;
                return true;
            }

            int i = 0;
            int j = used;
            do
            {
                int mid = i + (j - i) / 2;
                if (endPoints[mid] < value)
                {
                    i = Math.Max(mid, i + 1);
                }
                else if (startPoints[mid] > value)
                {
                    j = Math.Min(mid, j - 1);
                }
                else
                {
                    return false; // value is already present
                }
            }
            while (i != j);
            if (i > 0 && endPoints[i - 1] + 1 == value)
            {
                i--;
            }
            else if (i < used - 1 && startPoints[i + 1] - 1 == value)
            {
                i++;
            }

            if (endPoints[i] + 1 == value)
            {
                if (value == startPoints[i + 1] - 1)
                {

                    // merge the two ranges
                    endPoints[i] = endPoints[i + 1];
                    Array.Copy(startPoints, i + 2, startPoints, i + 1, used - i - 2);
                    Array.Copy(endPoints, i + 2, endPoints, i + 1, used - i - 2);
                    used--;
                }
                else
                {
                    endPoints[i]++;
                }

                count++;
                return true;
            }
            else if (startPoints[i] - 1 == value)
            {
                if (value == endPoints[i - 1] + 1)
                {

                    // merge the two ranges
                    endPoints[i - 1] = endPoints[i];
                    Array.Copy(startPoints, i + 1, startPoints, i, used - i - 1);
                    Array.Copy(endPoints, i + 1, endPoints, i, used - i - 1);
                    used--;
                }
                else
                {
                    startPoints[i]--;
                }

                count++;
                return true;
            }
            else
            {
                if (value > endPoints[i])
                {
                    i++;
                }

                EnsureCapacity(used + 1);
                try
                {
                    Array.Copy(startPoints, i, startPoints, i + 1, used - i - 1);
                    Array.Copy(endPoints, i, endPoints, i + 1, used - i - 1);
                }
                catch (Exception err)
                {
                    // ignored, as upstream does (it only printed the stack trace)
                }

                startPoints[i] = value;
                endPoints[i] = value;
                count++;
                return true;
            }
        }

        private void EnsureCapacity(int n)
        {
            if (startPoints.Length < n)
            {
                int[] s = new int[startPoints.Length * 2];
                int[] e = new int[startPoints.Length * 2];
                Array.Copy(startPoints, 0, s, 0, used);
                Array.Copy(endPoints, 0, e, 0, used);
                startPoints = s;
                endPoints = e;
            }

            used = n;
        }

        public override IIntIterator IIterator()
        {
            return new IntRangeSetIterator(this);
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder(used * 8);
            for (int i = 0; i < used; i++)
            {
                sb.Append(startPoints[i] + "-" + endPoints[i] + ",");
            }

            return sb.ToString();
        }

        public override bool Equals(object other)
        {
            if (other is IntSet)
            {
                if (other is IntRangeSet)
                {
                    return used == ((IntRangeSet)other).used && ArrayTools.Equals(startPoints, ((IntRangeSet)other).startPoints) && ArrayTools.Equals(endPoints, ((IntRangeSet)other).endPoints);
                }
                else
                {
                    return ContainsAll((IntSet)other);
                }
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {

            // Note, hashcodes are NOT the same as those used by IntHashSet and IntArraySet
            if (_hashCode == -1)
            {
                int h = 0x436a89f1;
                for (int i = 0; i < used; i++)
                {
                    h ^= startPoints[i] + (endPoints[i] << 3);
                }

                _hashCode = h;
            }

            return _hashCode;
        }

        public void AddRange(int low, int high)
        {
            if (low == high)
            {
                Add(low);
                return;
            }

            _hashCode = -1;
            if (used == 0)
            {
                EnsureCapacity(1);
                startPoints[used - 1] = low;
                endPoints[used - 1] = high;
                count += (high - low + 1);
            }
            else if (low > endPoints[used - 1])
            {
                if (low == endPoints[used - 1] + 1)
                {
                    endPoints[used - 1] = high;
                }
                else
                {
                    EnsureCapacity(used + 1);
                    startPoints[used - 1] = low;
                    endPoints[used - 1] = high;
                }

                count += (high - low + 1);
            }
            else if (high < startPoints[0])
            {
                EnsureCapacity(used + 1);
                Array.Copy(startPoints, 0, startPoints, 1, used - 1);
                Array.Copy(endPoints, 0, endPoints, 1, used - 1);
                startPoints[0] = low;
                endPoints[0] = high;
            }
            else
            {
                for (int i = 1; i < used; i++)
                {
                    if (startPoints[i] > high && endPoints[i - 1] < low)
                    {
                        EnsureCapacity(used + 1);
                        Array.Copy(startPoints, i, startPoints, i + 1, used - i - 1);
                        Array.Copy(endPoints, i, endPoints, i + 1, used - i - 1);
                        startPoints[i] = low;
                        endPoints[i] = high;
                        return;
                    }
                }


                // otherwise do it the hard way
                for (int i = low; i <= high; i++)
                {
                    Add(i);
                }
            }
        }

        /// <summary>
        /// IIterator class
        /// </summary>
        private sealed class IntRangeSetIterator : AbstractIntIterator
        {
            private IntRangeSet intRangeSet;
            private int i = 0;
            private int current = 0;
            public IntRangeSetIterator(IntRangeSet intRangeSet)
            {
                this.intRangeSet = intRangeSet;
                i = -1;
                current = int.MinValue;
            }

            public override bool HasNext()
            {
                if (i < 0)
                {
                    return intRangeSet.count > 0;
                }
                else
                {
                    return current < intRangeSet.endPoints[intRangeSet.used - 1];
                }
            }

            public override int Next()
            {
                if (i < 0)
                {
                    i = 0;
                    current = intRangeSet.startPoints[0];
                    return current;
                }

                if (current == intRangeSet.endPoints[i])
                {
                    current = intRangeSet.startPoints[++i];
                    return current;
                }
                else
                {
                    return ++current;
                }
            }
        }
    }
}
