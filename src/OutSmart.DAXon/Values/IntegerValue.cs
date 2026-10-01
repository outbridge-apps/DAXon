////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Collections;
using OutSmart.DAXon.Internal.Numerics;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
using System.Numerics;
namespace OutSmart.DAXon.Values
{
    public abstract class IntegerValue : DecimalValue
    {
        private const long NO_LIMIT = -9999;
        private const long MAX_UNSIGNED_LONG = -9998;
        private static readonly long[] ranges = new[]
        {
            StandardNames.XS_INTEGER,
            NO_LIMIT,
            NO_LIMIT,
            StandardNames.XS_LONG,
            long.MinValue,
            long.MaxValue,
            StandardNames.XS_INT,
            int.MinValue,
            int.MaxValue,
            StandardNames.XS_SHORT,
            short.MinValue,
            short.MaxValue,
            StandardNames.XS_BYTE,
            sbyte.MinValue,
            sbyte.MaxValue,
            StandardNames.XS_NON_NEGATIVE_INTEGER,
            0,
            NO_LIMIT,
            StandardNames.XS_POSITIVE_INTEGER,
            1,
            NO_LIMIT,
            StandardNames.XS_NON_POSITIVE_INTEGER,
            NO_LIMIT,
            0,
            StandardNames.XS_NEGATIVE_INTEGER,
            NO_LIMIT,
            -1,
            StandardNames.XS_UNSIGNED_LONG,
            0,
            MAX_UNSIGNED_LONG,
            StandardNames.XS_UNSIGNED_INT,
            0,
            4294967295,
            StandardNames.XS_UNSIGNED_SHORT,
            0,
            65535,
            StandardNames.XS_UNSIGNED_BYTE,
            0,
            255
        };

        public override BuiltInAtomicType PrimitiveType => BuiltInAtomicType.INTEGER;
        public IntegerValue(IAtomicType typeLabel) : base(typeLabel)
        {
        }

        public static IntegerValue MakeIntegerValue(BigInteger value)
        {
            if (value.CompareTo(BigIntegerValue.MAX_LONG) > 0 || value.CompareTo(BigIntegerValue.MIN_LONG) < 0)
            {
                return new BigIntegerValue(value);
            }
            else
            {
                return Int64Value.MakeIntegerValue(value.LongValue());
            }
        }

        public static IConversionResult FromDouble(double value)
        {
            if (double.IsNaN(value))
            {
                ValidationFailure err = new ValidationFailure("Cannot convert double NaN to an integer");
                err.SetErrorCode("FOCA0002");
                return err;
            }

            if (double.IsInfinity(value))
            {
                ValidationFailure err = new ValidationFailure("Cannot convert double INF to an integer");
                err.SetErrorCode("FOCA0002");
                return err;
            }

            if (value > long.MaxValue || value < long.MinValue)
            {
                if (value == Math.Floor(value))
                {
                    return new BigIntegerValue(FormatNumber.AdjustToDecimal(value, 2).ToBigInteger());
                }
                else
                {
                    return new BigIntegerValue(BigDecimal.ValueOf(value).ToBigInteger());
                }
            }

            return Int64Value.MakeIntegerValue((long)value);
        }

        public abstract ValidationFailure ValidateAgainstSubType(BuiltInAtomicType type);
        public static bool CheckRange(long value, BuiltInAtomicType type)
        {
            int fp = type.Fingerprint;
            for (int i = 0; i < ranges.Length; i += 3)
            {
                if (ranges[i] == fp)
                {
                    long min = ranges[i + 1];
                    if (min != NO_LIMIT && value < min)
                    {
                        return false;
                    }

                    long max = ranges[i + 2];
                    return max == NO_LIMIT || max == MAX_UNSIGNED_LONG || value <= max;
                }
            }

            throw new ArgumentException("No range information found for integer subtype " + type.Description);
        }

        public static IntegerValue GetMinInclusive(BuiltInAtomicType type)
        {
            int fp = type.Fingerprint;
            for (int i = 0; i < ranges.Length; i += 3)
            {
                if (ranges[i] == fp)
                {
                    long min = ranges[i + 1];
                    if (min == NO_LIMIT)
                    {
                        return null;
                    }
                    else
                    {
                        return Int64Value.MakeIntegerValue(min);
                    }
                }
            }

            return null;
        }

        public static IntegerValue GetMaxInclusive(BuiltInAtomicType type)
        {
            int fp = type.Fingerprint;
            for (int i = 0; i < ranges.Length; i += 3)
            {
                if (ranges[i] == fp)
                {
                    long max = ranges[i + 2];
                    if (max == NO_LIMIT)
                    {
                        return null;
                    }
                    else if (max == MAX_UNSIGNED_LONG)
                    {
                        return IntegerValue.MakeIntegerValue(BigIntegerValue.MAX_UNSIGNED_LONG);
                    }
                    else
                    {
                        return Int64Value.MakeIntegerValue(max);
                    }
                }
            }

            return null;
        }

        public static bool CheckBigRange(BigInteger big, BuiltInAtomicType type)
        {
            for (int i = 0; i < ranges.Length; i += 3)
            {
                if (ranges[i] == type.Fingerprint)
                {
                    long min = ranges[i + 1];
                    if (min != NO_LIMIT && new BigInteger(min).CompareTo(big) > 0)
                    {
                        return false;
                    }

                    long max = ranges[i + 2];
                    if (max == NO_LIMIT)
                    {
                        return true;
                    }
                    else if (max == MAX_UNSIGNED_LONG)
                    {
                        return BigIntegerValue.MAX_UNSIGNED_LONG.CompareTo(big) >= 0;
                    }
                    else
                    {
                        return new BigInteger(max).CompareTo(big) >= 0;
                    }
                }
            }

            throw new ArgumentException("No range information found for integer subtype " + type.Description);
        }

        public static IConversionResult StringToInteger(string s)
        {
            int len = s.Length;
            int start = 0;
            int last = len - 1;
            while (start < len && s[start] <= 0x20)
            {
                start++;
            }

            while (last > start && s[last] <= 0x20)
            {
                last--;
            }

            if (start > last)
            {
                return new ValidationFailure("Cannot convert zero-length string to an integer");
            }

            if (last - start < 16)
            {

                // for short numbers, we do the conversion ourselves, to avoid throwing unnecessary exceptions
                bool negative = false;
                long value = 0;
                int i = start;
                if (s[i] == '+')
                {
                    i++;
                }
                else if (s[i] == '-')
                {
                    negative = true;
                    i++;
                }

                if (i > last)
                {
                    return new ValidationFailure("Cannot convert string " + Err.Wrap(s, Err.VALUE) + " to integer: no digits after the sign");
                }

                while (i <= last)
                {
                    int d = s[i++];
                    if (d >= '0' && d <= '9')
                    {
                        value = 10 * value + (d - '0');
                    }
                    else
                    {
                        return new ValidationFailure("Cannot convert string " + Err.Wrap(s, Err.VALUE) + " to an integer");
                    }
                }

                return Int64Value.MakeIntegerValue(negative ? -value : value);
            }
            else
            {

                // For longer numbers, the library routine once the digits are known good: a
                // FormatException per value made `castable as xs:integer` on long text ~35 us a test.
                if (start > 0 || last < len - 1)
                {
                    s = s.Substring(start, last + 1 - start);
                }

                for (int i = s[0] == '+' || s[0] == '-' ? 1 : 0; i < s.Length; i++)
                {
                    if (s[i] < '0' || s[i] > '9')
                    {
                        return new ValidationFailure("Cannot convert string " + Err.Wrap(s[0] == '+' ? s.Substring(1) : s, Err.VALUE) + " to an integer");
                    }
                }

                return new BigIntegerValue(BigIntegers.FromString(s[0] == '+' ? s.Substring(1) : s));
            }
        }

        // Latin1 fast path mirroring StringToDouble.StringToNumber: tree text (Slice8) and 8-bit
        // strings (Twine8) parse straight from their byte buffer - no ToString per xs:integer()
        // cast. Byte-identical: every character this parser inspects is ASCII, and for these widths
        // CodePointAt returns exactly (byte & 0xff); other widths take the CodePointAt fallback.
        public static IConversionResult StringToInteger(UnicodeString s)
        {
            int len = s.Length32();
            byte[] b8 = null;
            int off8 = 0;
            if (s is Slice8 sl)
            {
                b8 = sl.ByteArray;
                off8 = sl.Start;
            }
            else if (s is Twine8 tw)
            {
                b8 = tw.ByteArray;
            }

            int start = 0;
            int last = len - 1;
            while (start < len && (b8 != null ? (b8[off8 + start] & 0xff) : s.CodePointAt(start)) <= 0x20)
            {
                start++;
            }

            while (last > start && (b8 != null ? (b8[off8 + last] & 0xff) : s.CodePointAt(last)) <= 0x20)
            {
                last--;
            }

            if (start > last)
            {
                return new ValidationFailure("Cannot convert zero-length string to an integer");
            }

            if (last - start >= 16)
            {
                // 16+ digits are rare - keep the library/BigInteger path on the string form.
                return StringToInteger(s.ToString());
            }

            bool negative = false;
            long value = 0;
            int i = start;
            int c = b8 != null ? (b8[off8 + i] & 0xff) : s.CodePointAt(i);
            if (c == '+')
            {
                i++;
            }
            else if (c == '-')
            {
                negative = true;
                i++;
            }

            if (i > last)
            {
                return new ValidationFailure("Cannot convert string " + Err.Wrap(s.ToString(), Err.VALUE) + " to integer: no digits after the sign");
            }

            while (i <= last)
            {
                int d = b8 != null ? (b8[off8 + i] & 0xff) : s.CodePointAt(i);
                i++;
                if (d >= '0' && d <= '9')
                {
                    value = 10 * value + (d - '0');
                }
                else
                {
                    return new ValidationFailure("Cannot convert string " + Err.Wrap(s.ToString(), Err.VALUE) + " to an integer");
                }
            }

            return Int64Value.MakeIntegerValue(negative ? -value : value);
        }

        public static ValidationFailure CastableAsInteger(UnicodeString input)
        {
            IIntIterator iter = input.CodePoints();
            int state = 0; // 0 - initial whitespace;

            // 1 - expecting digits;
            // 2 - expecting digits or final whitespace or EOS
            // 3 - expecting final whitespace or EOS
            while (iter.MoveNext())
            {
                int c = iter.Current;
                switch (state)
                {
                    case 0:
                        if (Whitespace.IsWhite(c))
                        {
                            state = 0;
                        }
                        else if (c == '+' || c == '-')
                        {
                            state = 1;
                        }
                        else if (c >= '0' && c <= '9')
                        {
                            state = 2;
                        }
                        else
                        {
                            return new ValidationFailure("Cannot convert string " + Err.Wrap(input, Err.VALUE) + " to an integer: contains a character " + Err.DepictCodepoint(c) + " that is not a digit");
                        }

                        break;
                    case 1:
                        if (c >= '0' && c <= '9')
                        {
                            state = 2;
                        }
                        else
                        {
                            return new ValidationFailure("Cannot convert string " + Err.Wrap(input, Err.VALUE) + " to an integer: expected a digit, found " + Err.DepictCodepoint(c));
                        }

                        break;
                    case 2:
                        if (c >= '0' && c <= '9')
                        {
                            state = 2;
                        }
                        else if (Whitespace.IsWhite(c))
                        {
                            state = 3;
                        }
                        else
                        {
                            return new ValidationFailure("Cannot convert string " + Err.Wrap(input, Err.VALUE) + " to an integer: expected a digit, found " + Err.DepictCodepoint(c));
                        }

                        break;
                    case 3:
                        if (Whitespace.IsWhite(c))
                        {
                            state = 3;
                        }
                        else
                        {
                            return new ValidationFailure("Cannot convert string " + Err.Wrap(input, Err.VALUE) + " to an integer: found " + c + " after final whitespace");
                        }

                        break;
                    default:
                        throw new InvalidOperationException();
                }
            }

            if (state == 0 || state == 1)
            {
                return new ValidationFailure("Cannot convert string " + Err.Wrap(input, Err.VALUE) + " to an integer: no digits found");
            }

            return null;
        }

        public abstract override BigDecimal GetDecimalValue();
        public override bool IsWholeNumber()
        {
            return true;
        }

        public abstract IntegerValue Plus(IntegerValue other);
        public abstract IntegerValue Minus(IntegerValue other);
        public abstract IntegerValue Times(IntegerValue other);
        public abstract NumericValue Div(IntegerValue other);
        public virtual NumericValue Div(IntegerValue other, ILocation locator)
        {
            try
            {
                return Div(other);
            }
            catch (XPathException err)
            {
                throw err.MaybeWithLocation(locator);
            }
        }

        public abstract IntegerValue Mod(IntegerValue other);
        public virtual IntegerValue Mod(IntegerValue other, ILocation locator)
        {
            try
            {
                return Mod(other);
            }
            catch (XPathException err)
            {
                throw err.MaybeWithLocation(locator);
            }
        }

        public abstract IntegerValue Idiv(IntegerValue other);
        public virtual IntegerValue Idiv(IntegerValue other, ILocation locator)
        {
            try
            {
                return Idiv(other);
            }
            catch (XPathException err)
            {
                throw err.MaybeWithLocation(locator);
            }
        }

        public abstract BigInteger AsBigInteger();
        public static int Signum(int i)
        {
            return (i >> 31) | (-i >>> 31);
        }

        public override bool IsIdentical(AtomicValue v)
        {
            return (v is IntegerValue) && Equals(v);
        }
    }
}