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
using System.Globalization;
using OutSmart.DAXon.Internal;
using System.Numerics;
namespace OutSmart.DAXon.Values
{
    internal sealed class BigIntegerValue : IntegerValue
    {
        private static readonly BigInteger MAX_INT = new BigInteger(int.MaxValue);
        private static readonly BigInteger MIN_INT = new BigInteger(int.MinValue);
        public static readonly BigInteger MAX_LONG = new BigInteger(long.MaxValue);
        public static readonly BigInteger MIN_LONG = new BigInteger(long.MinValue);
        // Not parsed: the parse reads the run's deadline, and a type initializer that throws breaks the type for good.
        public static readonly BigInteger MAX_UNSIGNED_LONG = new BigInteger(ulong.MaxValue);
        private readonly BigInteger value;

        public override UnicodeString PrimitiveStringValue => BMPString.Of(BigIntegers.ToDecimalString(value));   // a long one in pieces, under the time limit
        public BigIntegerValue(BigInteger value) : base(BuiltInAtomicType.INTEGER)
        {
            this.value = value;
        }

        public BigIntegerValue(BigInteger value, IAtomicType typeLabel) : base(typeLabel)
        {
            this.value = value;
        }

        public BigIntegerValue(long value) : base(BuiltInAtomicType.INTEGER)
        {
            this.value = new BigInteger(value);
        }

        public override AtomicValue CopyAsSubType(IAtomicType typeLabel)
        {
            if (typeLabel.PrimitiveType == StandardNames.XS_INTEGER)
            {
                return new BigIntegerValue(value, typeLabel);
            }
            else
            {
                return new BigDecimalValue(new BigDecimal(value), typeLabel);
            }
        }

        public override ValidationFailure ValidateAgainstSubType(BuiltInAtomicType type)
        {
            if (IntegerValue.CheckBigRange(value, type))
            {
                return null;
            }
            else
            {
                ValidationFailure err = new ValidationFailure("Integer value is out of range for subtype " + type.DisplayName);
                err.SetErrorCode("FORG0001");
                return err;
            }
        }

        public override int GetHashCode()
        {
            // Exclusive bounds, as Int64Value, DoubleValue and BigDecimalValue: equal values hash alike.
            if (value.CompareTo(MIN_INT) > 0 && value.CompareTo(MAX_INT) < 0)
            {
                return value.IntValue();
            }
            else
            {
                return (int)(double)(GetDoubleValue()).GetHashCode();
            }
        }

        public override long LongValue()
        {
            return value.LongValue();
        }

        public override BigInteger AsBigInteger()
        {
            return value;
        }

        public BigDecimal AsDecimal()
        {
            return new BigDecimal(value);
        }

        public override bool EffectiveBooleanValue()
        {
            return value.CompareTo(BigInteger.Zero) != 0;
        }

        public override int CompareTo(IXPathComparable other)
        {
            if (other is NumericValue)
            {
                if (other is BigIntegerValue)
                {
                    return value.CompareTo(((BigIntegerValue)other).value);
                }
                else if (other is Int64Value)
                {
                    return value.CompareTo(new BigInteger(((Int64Value)other).LongValue()));
                }
                else if (other is BigDecimalValue)
                {
                    return AsDecimal().CompareTo(((BigDecimalValue)other).GetDecimalValue());
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
            if (other == 0)
            {
                return value.Sign;
            }

            return value.CompareTo(new BigInteger(other));
        }

        public override double GetDoubleValue()
        {
            return value.DoubleValue();
        }

        public override BigDecimal GetDecimalValue()
        {
            return new BigDecimal(value);
        }

        public override float GetFloatValue()
        {
            return (float)GetDoubleValue();
        }

        public override NumericValue Negate()
        {
            return new BigIntegerValue(-value);
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
            return Round(scale, Functions.Round.RoundingRule.HALF_TO_CEILING);
        }

        public override NumericValue Round(int scale, Round.RoundingRule roundingRule)
        {
            if (scale >= 0 || value.Sign == 0)
            {
                return this;
            }
            else
            {
                bool negative = value.Sign < 0;

                // factor is 1 for scale=0, 10 for scale=-1, 100 for scale=-2, etc. It has to be
                // exact: accumulated in a long, 10^19 already wraps to a wrong value and 10^64
                // wraps to exactly zero, which made the division below throw DivideByZeroException.
                long k = -(long)scale;

                // Once the factor has more digits than the value, zero is nearer than any other
                // multiple of it, so every rule that picks a nearest value (or truncates) answers
                // zero without building a power that can run to two billion digits. Log10 is used
                // only as an upper bound on the digit count, so fp slack can delay this
                // short-circuit but never take it early.
                if (k > (long)BigInteger.Log10(BigInteger.Abs(value)) + 2)
                {
                    switch (roundingRule)
                    {
                        case Functions.Round.RoundingRule.FLOOR:
                            if (!negative)
                            {
                                return IntegerValue.MakeIntegerValue(BigInteger.Zero);
                            }

                            break;
                        case Functions.Round.RoundingRule.CEILING:
                            if (negative)
                            {
                                return IntegerValue.MakeIntegerValue(BigInteger.Zero);
                            }

                            break;
                        case Functions.Round.RoundingRule.AWAY_FROM_ZERO:
                            break;
                        default:
                            return IntegerValue.MakeIntegerValue(BigInteger.Zero);
                    }
                }

                if (k > int.MaxValue)
                {
                    throw new XPathException(
                        "Rounding away from zero at a precision of 10^" + k + " overflows", "FOAR0002");
                }

                BigInteger factorB = BigInteger.Pow(10, (int)k);
                BigInteger floor = value / factorB * factorB;
                if (floor.Equals(value))
                {
                    return this;
                }

                if (negative)
                {
                    floor -= factorB;
                }

                bool up = Functions.Round.RoundsUp(roundingRule, negative, value.CompareTo(floor + factorB / 2), (floor / factorB).IsEven);
                return IntegerValue.MakeIntegerValue(up ? floor + factorB : floor);
            }
        }

        public override int Signum()
        {
            return value.Sign;
        }

        public override NumericValue Abs()
        {
            if (value.Sign >= 0)
            {
                return this;
            }
            else
            {
                return new BigIntegerValue(BigInteger.Abs(value));
            }
        }

        public override bool IsWholeNumber()
        {
            return true;
        }

        public override int AsSubscript()
        {
            if (value.CompareTo(BigInteger.Zero) > 0 && value.CompareTo(MAX_INT) <= 0)
            {
                return (int)LongValue();
            }
            else
            {
                return -1;
            }
        }

        /// <summary>
        /// Add another integer
        /// </summary>
        public override IntegerValue Plus(IntegerValue other)
        {
            if (other is BigIntegerValue)
            {
                return MakeIntegerValue(value + ((BigIntegerValue)other).value);
            }
            else
            {

                return MakeIntegerValue(value + new BigInteger(((Int64Value)other).LongValue()));
            }
        }

        /// <summary>
        /// Subtract another integer
        /// </summary>
        public override IntegerValue Minus(IntegerValue other)
        {
            if (other is BigIntegerValue)
            {
                return MakeIntegerValue(value - ((BigIntegerValue)other).value);
            }
            else
            {

                return MakeIntegerValue(value - new BigInteger(((Int64Value)other).LongValue()));
            }
        }

        public override IntegerValue Times(IntegerValue other)
        {
            if (other is BigIntegerValue)
            {
                return MakeIntegerValue(Products.Multiply(value, ((BigIntegerValue)other).value));   // megadigit operands: see Products
            }
            else
            {

                return MakeIntegerValue(value * new BigInteger(((Int64Value)other).LongValue()));
            }
        }

        public override NumericValue Div(IntegerValue other)
        {
            BigInteger oi;
            if (other is BigIntegerValue)
            {
                oi = ((BigIntegerValue)other).value;
            }
            else
            {
                oi = new BigInteger(other.LongValue());
            }

            BigDecimalValue a = new BigDecimalValue(new BigDecimal(value));
            BigDecimalValue b = new BigDecimalValue(new BigDecimal(oi));
            return Calculator.DecimalDivide(a, b);
        }

        public override IntegerValue Mod(IntegerValue other)
        {
            if (other.Signum() == 0)
            {
                throw new XPathException("Integer modulo zero", "FOAR0001");
            }

            if (other is BigIntegerValue)
            {
                return MakeIntegerValue(value % ((BigIntegerValue)other).value);
            }
            else
            {
                return MakeIntegerValue(value % new BigInteger(other.LongValue()));
            }
        }

        public override IntegerValue Idiv(IntegerValue other)
        {
            if (other.Signum() == 0)
            {
                throw new XPathException("Integer division by zero", "FOAR0001");
            }

            BigInteger oi;
            if (other is BigIntegerValue)
            {
                oi = ((BigIntegerValue)other).value;
            }
            else
            {
                oi = new BigInteger(other.LongValue());
            }

            return MakeIntegerValue(value / oi);
        }

        /// <summary>
        /// Reduce a value to its simplest form.
        /// </summary>
        public override IGroundedValue Reduce()
        {
            if (CompareTo(long.MaxValue) < 0 && CompareTo(long.MinValue) > 0)
            {
                return new Int64Value(LongValue(), typeLabel);
            }

            return this;
        }
    }
}