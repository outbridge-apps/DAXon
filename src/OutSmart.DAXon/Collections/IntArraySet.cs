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
    internal sealed class IntArraySet : IntSet
    {
        public static readonly int[] EMPTY_INT_ARRAY = new int[0];
        /// <summary>
        /// The array of integers, which will always be sorted
        /// </summary>
        private int[] contents;
        /// <summary>
        /// Hashcode, evaluated lazily
        /// </summary>
        private int _hashCode = -1;

        public int[] Values => contents;

        public int First => contents[0];
        public IntArraySet()
        {
            contents = EMPTY_INT_ARRAY;
        }

        public IntArraySet(IntHashSet input)
        {

            // exploits the fact that getValues() constructs a new array
            contents = input.Values;

            Array.Sort(contents);
        }

        public IntArraySet(IntArraySet input)
        {
            contents = new int[input.contents.Length];
            Array.Copy(input.contents, 0, contents, 0, contents.Length);
        }

        private IntArraySet(int[] content)
        {
            contents = content;
        }

        public override IntSet Copy()
        {
            IntArraySet i2 = new IntArraySet();
            i2.contents = new int[contents.Length];
            Array.Copy(contents, 0, i2.contents, 0, contents.Length);

            //i2.contents = Arrays.copyOf(contents, contents.length);
            return i2;
        }

        public override IntSet MutableCopy()
        {
            return Copy();
        }

        public override void Clear()
        {
            contents = EMPTY_INT_ARRAY;
            _hashCode = -1;
        }

        public override int Size()
        {
            return contents.Length;
        }

        public override bool IsEmpty()
        {
            return contents.Length == 0;
        }

        public override bool Contains(int value)
        {
            return Array.BinarySearch(contents, value) >= 0;
        }

        public override bool Remove(int value)
        {
            _hashCode = -1;
            int pos = Array.BinarySearch(contents, value);
            if (pos < 0)
            {
                return false;
            }

            int[] newArray = new int[contents.Length - 1];
            if (pos > 0)
            {

                // copy the items before the one that's being removed
                Array.Copy(contents, 0, newArray, 0, pos);
            }

            if (pos < newArray.Length)
            {

                // copy the items after the one that's being removed
                Array.Copy(contents, pos + 1, newArray, pos, contents.Length - pos);
            }

            contents = newArray;
            return true;
        }

        public override bool Add(int value)
        {
            _hashCode = -1;
            if (contents.Length == 0)
            {
                contents = new int[]
                {
                    value
                };
                return true;
            }

            int pos = Array.BinarySearch(contents, value);
            if (pos >= 0)
            {
                return false; // value was already present
            }

            pos = -pos - 1; // new insertion point
            int[] newArray = new int[contents.Length + 1];
            if (pos > 0)
            {

                // copy the items before the insertion point
                Array.Copy(contents, 0, newArray, 0, pos);
            }

            newArray[pos] = value;
            if (pos < contents.Length)
            {

                // copy the items after the insertion point
                Array.Copy(contents, pos, newArray, pos + 1, newArray.Length - pos);
            }

            contents = newArray;
            return true;
        }

        public override IIntIterator IIterator()
        {
            return new IntArrayIterator(contents, contents.Length);
        }

        public override IntSet Union(IntSet other)
        {

            // Look for special cases: one set empty, or both sets equal
            if (Size() == 0)
            {
                return other.Copy();
            }
            else if (other.IsEmpty())
            {
                return Copy();
            }
            else if (other == IntUniversalSet.GetInstance())
            {
                return other;
            }
            else if (other is IntComplementSet)
            {
                return other.Union(this);
            }

            if (Equals(other))
            {
                return Copy();
            }

            if (other is IntArraySet)
            {

                // Form the union by a merge of the two sorted arrays
                int[] merged = new int[Size() + other.Count];
                int[] a = contents;
                int[] b = ((IntArraySet)other).contents;
                int m = a.Length, n = b.Length;
                int o = 0, i = 0, j = 0;
                while (true)
                {
                    if (a[i] < b[j])
                    {
                        merged[o++] = a[i++];
                    }
                    else if (b[j] < a[i])
                    {
                        merged[o++] = b[j++];
                    }
                    else
                    {
                        merged[o++] = a[i++];
                        j++;
                    }

                    if (i == m)
                    {
                        Array.Copy(b, j, merged, o, n - j);
                        o += (n - j);
                        return Make(merged, o);
                    }
                    else if (j == n)
                    {
                        Array.Copy(a, i, merged, o, m - i);
                        o += (m - i);
                        return Make(merged, o);
                    }
                }
            }
            else
            {
                return base.Union(other);
            }
        }

        public static IntArraySet Make(int[] @in, int size)
        {
            int[] @out;
            if (@in.Length == size)
            {
                @out = @in;
            }
            else
            {
                @out = new int[size];
                Array.Copy(@in, 0, @out, 0, size);
            }

            return new IntArraySet(@out);
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder(contents.Length * 4);
            for (int i = 0; i < contents.Length; i++)
            {
                if (i == contents.Length - 1)
                {
                    sb.Append(contents[i] + "");
                }
                else if (contents[i] + 1 != contents[i + 1])
                {
                    sb.Append(contents[i] + ",");
                }
                else
                {
                    int j = i + 1;
                    while (contents[j] == contents[j - 1] + 1)
                    {
                        j++;
                        if (j == contents.Length)
                        {
                            break;
                        }
                    }

                    sb.Append(contents[i] + "-" + contents[j - 1] + ",");
                    i = j - 1;
                }
            }

            return sb.ToString();
        }

        //    }
        /// <summary>
        /// Test whether this set has exactly the same members as another set
        /// </summary>
        public override bool Equals(object other)
        {
            if (other is IntArraySet)
            {
                IntArraySet s = (IntArraySet)other;
                return GetHashCode() == other.GetHashCode() && ArrayTools.Equals(contents, s.contents);
            }
            else
                return other is IntSet && contents.Length == ((IntSet)other).Count && ContainsAll((IntSet)other);
        }

        //    }
        /// <summary>
        /// Construct a hash key that supports the equals() test
        /// </summary>
        public override int GetHashCode()
        {

            // Note, hashcodes are the same as those used by IntHashSet
            if (_hashCode == -1)
            {
                int h = 936247625;
                IIntIterator it = IIterator();
                while (it.MoveNext())
                {
                    h += it.Current;
                }

                _hashCode = h;
            }

            return _hashCode;
        }

        //    }
        /// <summary>
        /// IIterator class: iterate over an array of integers
        /// </summary>
        internal sealed class IntArrayIterator : AbstractIntIterator
        {
            private readonly int[] contents;
            private readonly int limit;
            private int i;
            public IntArrayIterator(int[] contents, int limit)
            {
                i = 0;
                this.contents = contents;
                this.limit = limit;
            }

            public override bool HasNext()
            {
                return i < limit;
            }

            public override int Next()
            {
                return contents[i++];
            }
        }
    }
}
