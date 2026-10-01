////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal.Numerics;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
using System.Numerics;
namespace OutSmart.DAXon.Values
{
    internal sealed class Int64Value : IntegerValue
    {
        /// <summary>
        /// IntegerValue representing the value -1
        /// </summary>
        public static readonly Int64Value MINUS_ONE = new Int64Value(-1);
        /// <summary>
        /// IntegerValue representing the value zero
        /// </summary>
        public static readonly Int64Value ZERO = new Int64Value(0);
        /// <summary>
        /// IntegerValue representing the value +1
        /// </summary>
        public static readonly Int64Value PLUS_ONE = new Int64Value(+1);
        /// <summary>
        /// IntegerValue representing the maximum value for a long
        /// </summary>
        public static readonly Int64Value MAX_LONG = new Int64Value(long.MaxValue);
        /// <summary>
        /// Array of small integer values (immutable, so sharing is safe; sized to catch the
        /// common products of string-length, position, count-of-small-sets and similar)
        /// </summary>
        private static readonly Int64Value[] SMALL_INTEGERS = MakeSmallIntegers();

        private static Int64Value[] MakeSmallIntegers()
        {
            Int64Value[] cache = new Int64Value[1024];
            for (int i = 0; i < cache.Length; i++)
            {
                cache[i] = new Int64Value(i);
            }

            return cache;
        }

        private static readonly byte[] DIGITS = StringConstants.Bytes("0123456789");
        private static readonly byte[] DIGIT_TENS = StringConstants.Bytes("0000000000" + "1111111111" + "2222222222" + "3333333333" + "4444444444" + "5555555555" + "6666666666" + "7777777777" + "8888888888" + "9999999999");
        private static readonly byte[] DIGIT_ONES = StringConstants.Bytes("0123456789" + "0123456789" + "0123456789" + "0123456789" + "0123456789" + "0123456789" + "0123456789" + "0123456789" + "0123456789" + "0123456789");

        private static readonly long[] powersOfTen = new long[]
        {
            10,
            100,
            1000,
            10000,
            100000,
            1000000,
            10000000,
            100000000,
            1000000000,
            10000000000,
            100000000000,
            1000000000000,
            10000000000000,
            100000000000000,
            1000000000000000,
            10000000000000000,
            100000000000000000,
            1000000000000000000
        };
        /// <summary>
        /// IntegerValue representing the minimum value for a long
        /// </summary>
        private readonly long value;

        public override UnicodeString PrimitiveStringValue
        {
            get
            {

                // Copied from Long.toString(), but generating single-byte characters
                if (value == long.MinValue)
                {
                    return StringConstants.MIN_LONG;
                }

                int size = (value < 0) ? StringSize(-value) + 1 : StringSize(value);
                byte[] buf = new byte[size];
                GetDigits(value, size, buf);
                return new Twine8(buf); //return BMPString.of(Long.toString(value));
            }
        }
        public Int64Value(long value) : base(BuiltInAtomicType.INTEGER)
        {
            this.value = value;
        }

        public Int64Value(long value, IAtomicType typeLabel) : base(typeLabel)
        {
            this.value = value;
        }

        public static Int64Value MakeIntegerValue(long value)
        {
            if (value >= 0 && value < SMALL_INTEGERS.Length)
            {
                return SMALL_INTEGERS[(int)value];
            }
            else
            {
                return new Int64Value(value);
            }
        }

        public static Int64Value MakeDerived(long val, IAtomicType type)
        {
            return new Int64Value(val, type);
        }

        public override int AsSubscript()
        {
            if (value > 0 && value <= int.MaxValue)
            {
                return (int)value;
            }
            else
            {
                return -1;
            }
        }

        public override AtomicValue CopyAsSubType(IAtomicType typeLabel)
        {
            if (typeLabel.PrimitiveType == StandardNames.XS_INTEGER)
            {
                return new Int64Value(value, typeLabel);
            }
            else
            {
                return new BigDecimalValue(value).CopyAsSubType(typeLabel);
            }
        }

        public override ValidationFailure ValidateAgainstSubType(BuiltInAtomicType type)
        {
            if (CheckRange(value, type))
            {
                return null;
            }
            else
            {
                ValidationFailure err = new ValidationFailure("Value " + value + " cannot be converted to integer subtype " + type.Description);
                err.SetErrorCode("FORG0001");
                return err;
            }
        }

        public override int GetHashCode()
        {
            if (value > int.MinValue && value < int.MaxValue)
            {
                return (int)value;
            }
            else
            {
                return (int)(double)(GetDoubleValue()).GetHashCode();
            }
        }

        public override long LongValue()
        {
            return value;
        }

        public override bool EffectiveBooleanValue()
        {
            return value != 0;
        }

        public override int CompareTo(IXPathComparable other)
        {
            if (other is NumericValue)
            {
                if (other is Int64Value)
                {
                    return value.CompareTo(((Int64Value)other).value);
                }
                else if (other is BigIntegerValue)
                {
                    return new BigInteger(value).CompareTo(((BigIntegerValue)other).AsBigInteger());
                }
                else if (other is BigDecimalValue)
                {
                    return BigDecimal.ValueOf(value).CompareTo(((BigDecimalValue)other).GetDecimalValue());
                }
                else
                {
                    return base.CompareTo(other);
                }
            }
            else
            {
                throw new InvalidCastException("Cannot compare xs:integer to " + other);
            }
        }

        public override int CompareTo(long other)
        {
            return value.CompareTo(other);
        }

        private static void GetDigits(long i, int index, byte[] buf)
        {

            // Derived from Long.getChars()
            long q;
            int r;
            int charPos = index;
            byte sign = 0;
            if (i < 0)
            {
                sign = (byte)'-';
                i = -i;
            }


            // Get 2 digits/iteration using longs until quotient fits into an int
            while (i > int.MaxValue)
            {
                q = i / 100;

                // really: r = i - (q * 100);
                r = (int)(i - ((q << 6) + (q << 5) + (q << 2)));
                i = q;
                buf[--charPos] = DIGIT_ONES[r];
                buf[--charPos] = DIGIT_TENS[r];
            }


            // Get 2 digits/iteration using ints
            int q2;
            int i2 = (int)i;
            while (i2 >= 65536)
            {
                q2 = i2 / 100;

                // really: r = i2 - (q * 100);
                r = i2 - ((q2 << 6) + (q2 << 5) + (q2 << 2));
                i2 = q2;
                buf[--charPos] = DIGIT_ONES[r];
                buf[--charPos] = DIGIT_TENS[r];
            }


            // Fall thru to fast mode for smaller numbers
            // assert(i2 <= 65536, i2);
            do
            {
                q2 = (i2 * 52429) >>> (16 + 3);
                r = i2 - ((q2 << 3) + (q2 << 1)); // r = i2-(q2*10) ...
                buf[--charPos] = DIGITS[r];
                i2 = q2;
            }
            while (i2 != 0);
            if (sign != 0)
            {
                buf[--charPos] = sign;
            }
        }
        private static int StringSize(long x)
        {
            for (int w = 0; w < 18; w++)
            {
                if (x < powersOfTen[w])
                {
                    return w + 1;
                }
            }

            return 19;
        }
        public override double GetDoubleValue()
        {
            return (double)value;
        }

        public override float GetFloatValue()
        {
            return (float)value;
        }

        public override BigDecimal GetDecimalValue()
        {
            return BigDecimal.ValueOf(value);
        }

        public override NumericValue Negate()
        {
            if (value == long.MinValue)
            {
                return BigIntegerValue.MakeIntegerValue(new BigInteger(value)).Negate();
            }
            else
            {
                return new Int64Value(-value);
            }
        }

        public override NumericValue Floor()
        {
            return this;
        }

        public override NumericValue Ceiling()
        {
            return this;
        }

        public override NumericValue Round(int scale)
        {
            if (scale >= 0 || value == 0)
            {
                return this;
            }
            else
            {
                if (scale < -15)
                {
                    return new BigIntegerValue(value).Round(scale);
                }

                long absolute = Math.Abs(value);
                long factor = 1;
                for (long i = 1; i <= -scale; i++)
                {
                    factor *= 10;
                }

                long modulus = absolute % factor;
                long rval = absolute - modulus;
                long d = modulus * 2;
                if (value > 0)
                {
                    if (d >= factor)
                    {
                        rval += factor;
                    }
                }
                else
                {
                    if (d > factor)
                    {
                        rval += factor;
                    }

                    rval = -rval;
                }

                return new Int64Value(rval);
            }
        }

        public override NumericValue Round(int scale, Round.RoundingRule roundingRule)
        {
            if (scale >= 0 || value == 0)
            {
                return this;
            }
            else
            {
                if (scale < -15)
                {
                    return new BigIntegerValue(value).Round(scale, roundingRule);
                }

                bool negative = value < 0;

                // factor is 1 for scale=0, 10 for scale=-1, 100 for scale=-2, etc
                long factor = 1;
                for (long i = 1; i <= -scale; i++)
                {
                    factor *= 10;
                }

                long towardsZero = (value / factor) * factor;
                if (towardsZero == value)
                {
                    return this;
                }

                long awayFromZero = negative ? towardsZero - factor : towardsZero + factor;
                long floor = negative ? awayFromZero : towardsZero;
                long ceiling = negative ? towardsZero : awayFromZero;
                long midpoint = floor + (ceiling - floor) / 2;
                bool midway = value == midpoint;
                long nearest = value > midpoint ? ceiling : floor;
                switch (roundingRule)
                {
                    case Functions.Round.RoundingRule.FLOOR:
                        return Int64Value.MakeIntegerValue(floor);
                    case Functions.Round.RoundingRule.TOWARD_ZERO:
                        return Int64Value.MakeIntegerValue(towardsZero);
                    case Functions.Round.RoundingRule.CEILING:
                        return Int64Value.MakeIntegerValue(ceiling);
                    case Functions.Round.RoundingRule.AWAY_FROM_ZERO:
                        return Int64Value.MakeIntegerValue(awayFromZero);
                    case Functions.Round.RoundingRule.HALF_TO_FLOOR:
                        return Int64Value.MakeIntegerValue(midway ? floor : nearest);
                    case Functions.Round.RoundingRule.HALF_TO_CEILING:
                    default:
                        return Int64Value.MakeIntegerValue(midway ? ceiling : nearest);
                    case Functions.Round.RoundingRule.HALF_TOWARD_ZERO:
                        return Int64Value.MakeIntegerValue(midway ? towardsZero : nearest);
                    case Functions.Round.RoundingRule.HALF_AWAY_FROM_ZERO:
                        return Int64Value.MakeIntegerValue(midway ? awayFromZero : nearest);
                    case Functions.Round.RoundingRule.HALF_TO_EVEN:
                        return Int64Value.MakeIntegerValue(midway ? (floor / factor % 2 == 0 ? floor : ceiling) : nearest);
                }
            }
        }

        public override int Signum()
        {
            if (value > 0)
                return +1;
            if (value == 0)
                return 0;
            return -1;
        }

        public override NumericValue Abs()
        {
            if (value > 0)
            {
                return this;
            }
            else if (value == long.MinValue)
            {
                return new BigIntegerValue(BigIntegers.FromString("9223372036854775808"));
            }
            else
            {
                return MakeIntegerValue(-value);
            }
        }

        /// <summary>
        /// Add another integer
        /// </summary>
        public override IntegerValue Plus(IntegerValue other)
        {

            // if either of the values is large, we use BigInteger arithmetic to be on the safe side
            if (other is Int64Value)
            {
                long topa = (value >> 60) & 0xf;
                if (topa != 0 && topa != 0xf)
                {
                    return new BigIntegerValue(value).Plus(new BigIntegerValue(((Int64Value)other).value));
                }

                long topb = (((Int64Value)other).value >> 60) & 0xf;
                if (topb != 0 && topb != 0xf)
                {
                    return new BigIntegerValue(value).Plus(new BigIntegerValue(((Int64Value)other).value));
                }

                return MakeIntegerValue(value + ((Int64Value)other).value);
            }
            else
            {
                return new BigIntegerValue(value).Plus(other);
            }
        }

        /// <summary>
        /// Subtract another integer
        /// </summary>
        public override IntegerValue Minus(IntegerValue other)
        {

            // if either of the values is large, we use BigInteger arithmetic to be on the safe side
            if (other is Int64Value)
            {
                long topa = (value >> 60) & 0xf;
                if (topa != 0 && topa != 0xf)
                {
                    return new BigIntegerValue(value).Minus(new BigIntegerValue(((Int64Value)other).value));
                }

                long topb = (((Int64Value)other).value >> 60) & 0xf;
                if (topb != 0 && topb != 0xf)
                {
                    return new BigIntegerValue(value).Minus(new BigIntegerValue(((Int64Value)other).value));
                }

                return MakeIntegerValue(value - ((Int64Value)other).value);
            }
            else
            {
                return new BigIntegerValue(value).Minus(other);
            }
        }

        /// <summary>
        /// Multiply by another integer
        /// </summary>
        public override IntegerValue Times(IntegerValue other)
        {

            // if either of the values is large, we use BigInteger arithmetic to be on the safe side
            if (other is Int64Value)
            {
                if (IsLong() || ((Int64Value)other).IsLong())
                {
                    return new BigIntegerValue(value).Times(new BigIntegerValue(((Int64Value)other).value));
                }
                else
                {
                    return MakeIntegerValue(value * ((Int64Value)other).value);
                }
            }
            else
            {
                return new BigIntegerValue(value).Times(other);
            }
        }

        /// <summary>
        /// Divide by another integer
        /// </summary>
        public override NumericValue Div(IntegerValue other)
        {

            // if either of the values is large, we use BigInteger arithmetic to be on the safe side
            if (other is Int64Value)
            {
                long quotient = ((Int64Value)other).value;
                if (quotient == 0)
                {
                    throw new XPathException("Integer division by zero", "FOAR0001");
                }

                if (IsLong() || ((Int64Value)other).IsLong())
                {
                    return new BigIntegerValue(value).Div(new BigIntegerValue(quotient));
                }


                // the result of dividing two integers is a decimal; but if
                // one divides exactly by the other, we implement it as an integer
                if (value % quotient == 0)
                {
                    return MakeIntegerValue(value / quotient);
                }
                else
                {
                    return Calculator.DecimalDivide(new BigDecimalValue(value), new BigDecimalValue(quotient));
                }
            }
            else
            {
                return new BigIntegerValue(value).Div(other);
            }
        }

        /// <summary>
        /// Take modulo another integer
        /// </summary>
        public override IntegerValue Mod(IntegerValue other)
        {

            // if either of the values is large, we use BigInteger arithmetic to be on the safe side
            if (other is Int64Value)
            {
                long quotient = ((Int64Value)other).value;
                if (quotient == 0)
                {
                    throw new XPathException("Integer modulo zero", "FOAR0001");
                }

                if (IsLong() || ((Int64Value)other).IsLong())
                {
                    return new BigIntegerValue(value).Mod(new BigIntegerValue(((Int64Value)other).value));
                }
                else
                {
                    return MakeIntegerValue(value % quotient);
                }
            }
            else
            {
                return new BigIntegerValue(value).Mod(other);
            }
        }

        /// <summary>
        /// Integer divide by another integer
        /// </summary>
        public override IntegerValue Idiv(IntegerValue other)
        {

            // if either of the values is large, we use BigInteger arithmetic to be on the safe side
            if (other.Signum() == 0)
            {
                throw new XPathException("Integer division by zero", "FOAR0001");
            }

            if (other is Int64Value)
            {
                if (IsLong() || ((Int64Value)other).IsLong())
                {
                    return new BigIntegerValue(value).Idiv(new BigIntegerValue(((Int64Value)other).value));
                }

                return MakeIntegerValue(value / ((Int64Value)other).value);
            }
            else
            {
                return new BigIntegerValue(value).Idiv(other);
            }
        }

        /// <summary>
        /// Integer divide by another integer
        /// </summary>
        // True when the value does not fit in 32 bits — the BigInteger-promotion trigger for
        // Times/Div/Mod/Idiv. The sign-extension of an in-range NEGATIVE value is all ones, so
        // the -1 case must pass too (upstream isLong verbatim); without it every negative
        // operand was silently routed through BigInteger arithmetic (value-identical, but the
        // whole negative half of integer multiply/divide paid the big-number path).
        private bool IsLong()
        {
            long top = value >> 31;
            return top != 0 && top != -1;
        }

        /// <summary>
        /// Get the value as a BigInteger
        /// </summary>
        public override BigInteger AsBigInteger()
        {
            return new BigInteger(value);
        }
    }
}
