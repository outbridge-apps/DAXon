////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Serialization.CharCodes;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Collections;
using System;
using System.Text;
namespace OutSmart.DAXon.Text
{
    public abstract class UnicodeString : IAtomicMatchKey, IComparable<UnicodeString>
    {

        public abstract int Width { get; }

        private byte[] CodepointCollationKey
        {
            get
            {
                UnicodeString prep = Tidy();
                int len = RequireInt(prep.Length());
                byte[] result = new byte[len * 3];
                IIntIterator iter = prep.CodePoints();
                int j = 0;
                while (iter.MoveNext())
                {
                    int c = iter.Current;
                    result[j++] = (byte)(c >> 16);
                    result[j++] = (byte)(c >> 8);
                    result[j++] = (byte)c;
                }

                return result;
            }
        }
        public virtual UnicodeString Tidy()
        {
            return this;
        }

        public virtual UnicodeString Economize()
        {
            return this;
        }

        public abstract long Length();
        public virtual int Length32()
        {
            return RequireInt(Length());
        }

        public virtual long EstimatedLength()
        {
            return Length();
        }

        public virtual bool IsEmpty()
        {
            return Length() == 0;
        }
        public virtual long IndexOf(int codePoint)
        {
            return IndexOf(codePoint, 0);
        }

        public abstract long IndexOf(int codePoint, long from);
        public abstract long IndexWhere(Func<int, bool> predicate, long from);
        public virtual long IndexOf(UnicodeString other, long from)
        {
            if (from < 0 || from >= Length())
            {
                return -1;
            }

            if (other.IsEmpty())
            {
                return from;
            }

            int initial = other.CodePointAt(0);
            long len = other.Length();
            long lastPossible = Length() - len;
            while (from <= lastPossible)
            {
                long i = IndexOf(initial, from);
                if (i < 0)
                {
                    return -1;
                }

                if (HasSubstring(other, i))
                {
                    return i;
                }

                from = i + 1;
            }

            return -1;
        }

        public virtual bool HasSubstring(UnicodeString other, long offset)
        {
            if (offset < 0 || offset > Length())
            {
                throw new IndexOutOfRangeException();
            }

            long len = other.Length();
            if (len + offset > Length())
            {
                return false;
            }

            for (long k = 0; k < len; k++)
            {
                if (CodePointAt(offset + k) != other.CodePointAt(k))
                {
                    return false;
                }
            }

            return true;
        }

        public abstract IIntIterator CodePoints();
        public abstract int CodePointAt(long index);
        public virtual UnicodeString Substring(long start)
        {
            return Substring(start, Length());
        }

        public abstract UnicodeString Substring(long start, long end);
        public virtual UnicodeString Prefix(long end)
        {
            return Substring(0, end);
        }

        public virtual UnicodeString Concat(UnicodeString other)
        {
            return ZenoString.Of(this).Concat(other);
        }

        protected virtual void CheckSubstringBounds(long start, long end)
        {
            if (start < 0)
            {
                throw new IndexOutOfRangeException("UnicodeString.substring(): start (" + start + ") < 0");
            }

            if (end < start)
            {
                throw new IndexOutOfRangeException("UnicodeString.substring(): end (" + end + ") < start ( + start + ");
            }

            if (end > Length())
            {
                throw new IndexOutOfRangeException("UnicodeString.substring(): end (" + end + ") > length (" + Length() + ")");
            }
        }

        public virtual void VerifyCharacters()
        {
            IIntIterator iter = CodePoints();
            int p = 0;
            while (iter.MoveNext())
            {
                int x = iter.Current;
                if (!XMLCharacterData.IsValid11(x))
                {
                    throw new InvalidOperationException("Invalid char " + x + " in " + GetType() + " at offset " + p);
                }

                p++;
            }
        }

        public override bool Equals(object obj)
        {
            if (obj is UnicodeString)
            {
                UnicodeString other = (UnicodeString)obj;
                if (Length() != other.Length())
                {
                    return false;   // O(1) reject before allocating the codepoint iterators
                }

                IIntIterator iter1 = CodePoints();
                IIntIterator iter2 = other.CodePoints();
                while (true)
                {
                    bool more1 = iter1.MoveNext();
                    bool more2 = iter2.MoveNext();
                    if (more1 && more2)
                    {
                        int ch1 = iter1.Current;
                        int ch2 = iter2.Current;
                        if (ch1 != ch2)
                        {
                            return false;
                        }
                    }
                    else
                    {
                        return !(more1 || more2);
                    }
                }
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            int h = 0;
            IIntIterator iter = CodePoints();
            while (iter.MoveNext())
            {
                int cp = iter.Current;
                if ((cp & 0xff0000) != 0)
                {
                    h = 31 * h + UTF16CharacterSet.HighSurrogate(cp);
                    h = 31 * h + UTF16CharacterSet.LowSurrogate(cp);
                }
                else
                {
                    h = 31 * h + cp;
                }
            }

            return h;
        }

        public virtual int CompareTo(UnicodeString other)
        {
            IIntIterator iter1 = CodePoints();
            IIntIterator iter2 = other.CodePoints();
            while (true)
            {
                bool more1 = iter1.MoveNext();
                bool more2 = iter2.MoveNext();
                if (more1 && more2)
                {
                    int ch1 = iter1.Current;
                    int ch2 = iter2.Current;
                    int diff = ch1 - ch2;
                    if (diff != 0)
                    {
                        return diff;
                    }
                }
                else if (!more1 && !more2)
                {
                    return 0;
                }
                else
                {
                    return more1 ? 1 : -1;
                }
            }
        }

        public virtual AtomicValue AsAtomic()
        {
            return new Base64BinaryValue(CodepointCollationKey);
        }

        public override string ToString()
        {
            StringBuilder __sb = new StringBuilder();
            IIntIterator __it = CodePoints();
            while (__it != null && __it.MoveNext())
            {
                __sb.Append(char.ConvertFromUtf32(__it.Current));
            }
            return __sb.ToString();
        }

        public static int RequireInt(long value)
        {
            if (value > int.MaxValue)
            {
                throw new NotSupportedException("String offset exceeds 2^31 characters");
            }

            return (int)value;
        }

        public static int RequireNonNegativeInt(long value)
        {
            if (value > int.MaxValue)
            {
                throw new NotSupportedException("String exceeds 2^31 characters");
            }

            return (int)Math.Max(value, 0);
        }

        public virtual void Copy8bit(byte[] target, int offset)
        {
            throw new NotSupportedException();
        }

        public virtual void Copy16bit(char[] target, int offset)
        {
            throw new NotSupportedException();
        }

        // Pack each code point as 3 big-endian bytes (high, mid, low) — the layout Slice24/Twine24 read back
        // (Slice24: (b0<<16)|(b1<<8)|b2). Was a throwing stub, so concatenating a 24-bit-wide string whose
        // concrete class doesn't override this (ZenoString.ConcatSegments -> left.Copy24bit) failed for astral
        // (supplementary-plane) content — misc-Surrogates.
        public virtual void Copy24bit(byte[] target, int offset)
        {
            IIntIterator iter = CodePoints();
            while (iter.MoveNext())
            {
                int c = iter.Current;
                target[offset++] = (byte)(c >> 16);
                target[offset++] = (byte)(c >> 8);
                target[offset++] = (byte)c;
            }
        }

        public virtual void Copy32bit(int[] target, int offset)
        {
            IIntIterator iter = CodePoints();
            while (iter.MoveNext())
            {
                target[offset++] = iter.Current;
            }
        }
    }
}