////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal.Numerics;
using System;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Values
{
    /// <summary>
    /// A numeric (double precision floating point) value
    /// </summary>
    internal sealed class DoubleValue : NumericValue
    {
        public static readonly DoubleValue ZERO = new DoubleValue(0);
        public static readonly DoubleValue NEGATIVE_ZERO = new DoubleValue(-0.0);
        public static readonly DoubleValue NaN = new DoubleValue(double.NaN);
        private readonly double value;

        public override BuiltInAtomicType PrimitiveType => BuiltInAtomicType.DOUBLE;

        //    }
        public override UnicodeString PrimitiveStringValue => DoubleToString(value);

        public override UnicodeString CanonicalLexicalRepresentation => FloatingPointConverter.ConvertDouble(value, true);
        public DoubleValue(double value) : base(BuiltInAtomicType.DOUBLE)
        {
            this.value = value;
        }

        public DoubleValue(double value, IAtomicType typeLabel) : base(typeLabel)
        {
            this.value = value;
        }

        public override AtomicValue CopyAsSubType(IAtomicType typeLabel)
        {
            return new DoubleValue(value, typeLabel);
        }

        public override double GetDoubleValue()
        {
            return value;
        }

        public override float GetFloatValue()
        {
            return (float)value;
        }

        public override BigDecimal GetDecimalValue()
        {
            try
            {
                return BigDecimal.ValueOf(value);
            }
            catch (FormatException e)
            {
                throw new ValidationException(e);
            }
        }

        public override long LongValue()
        {
            return JavaMath.ToLong(value);
        }

        public override int GetHashCode()
        {
            if (value > int.MinValue && value < int.MaxValue)
            {
                return (int)value;
            }
            else
            {
                return (int)(double)(value).GetHashCode();
            }
        }

        public override bool IsNaN()
        {
            return double.IsNaN(value);
        }

        public override bool EffectiveBooleanValue()
        {
            return value != 0 && !double.IsNaN(value);
        }

        public static UnicodeString DoubleToString(double value)
        {
            double d = Math.Abs(value);
            return FloatingPointConverter.ConvertDouble(value, d != 0 && (d >= 1000000 || d < 1E-06));
        }

        //    }
        /// <summary>
        /// Negate the value
        /// </summary>
        public override NumericValue Negate()
        {
            return new DoubleValue(-value);
        }

        //    }
        /// <summary>
        /// Implement the XPath floor() function
        /// </summary>
        public override NumericValue Floor()
        {
            return new DoubleValue(Math.Floor(value));
        }

        //    }
        /// <summary>
        /// Implement the XPath ceiling() function
        /// </summary>
        public override NumericValue Ceiling()
        {
            // Java Math.ceil returns -0.0 for -1 < value < 0; .NET Math.Ceiling gives +0.0. Preserve the
            // sign so string(ceiling(-0.6e0)) is "-0".
            double c = Math.Ceiling(value);
            if (c == 0.0 && value < 0.0)
            {
                c = -0.0;
            }

            return new DoubleValue(c);
        }

        //    }
        /// <summary>
        /// Implement the XPath round() function
        /// </summary>
        public override NumericValue Round(int scale)
        {
            if (double.IsNaN(value))
            {
                return this;
            }

            if (double.IsInfinity(value))
            {
                return this;
            }

            if (value == 0)
            {
                return this; // handles the negative zero case
            }

            if (scale == 0 && value > long.MinValue && value < long.MaxValue)
            {
                if (value >= -0.5 && value < 0)
                {
                    return new DoubleValue(-0.0);
                }

                return new DoubleValue(JavaMath.Round(value));
            }

            return RoundViaDecimal(scale, Functions.Round.RoundingRule.HALF_TO_CEILING);
        }

        //    }
        public override NumericValue Round(int scale, Round.RoundingRule roundingRule)
        {
            if (value == 0 || double.IsNaN(value) || double.IsInfinity(value))
            {
                return this;
            }


            // Handle some simple special cases
            if (scale == 0)
            {
                if (roundingRule == Functions.Round.RoundingRule.CEILING)
                {
                    return Ceiling();
                }
                else if (roundingRule == Functions.Round.RoundingRule.FLOOR)
                {
                    return Floor();
                }
                else if (roundingRule == Functions.Round.RoundingRule.HALF_TO_CEILING)
                {
                    if (value > long.MinValue && value < long.MaxValue)
                    {
                        if (value >= -0.5 && value < 0)
                        {
                            return new DoubleValue(-0.0);
                        }

                        return new DoubleValue(JavaMath.Round(value));
                    }
                }
            }

            return RoundViaDecimal(scale, roundingRule);
        }

        //    }
        private DoubleValue RoundViaDecimal(int scale, Round.RoundingRule roundingRule)
        {
            if (value == 0 || double.IsNaN(value) || double.IsInfinity(value))
            {
                return this;
            }

            BigDecimalValue decimalValue = new BigDecimalValue(new BigDecimal(value));
            NumericValue rounded = decimalValue.Round(scale, roundingRule);
            double result = rounded.GetDoubleValue();
            if (result == 0)
            {

                // return negative zero if the original value was negative
                return value < 0 ? DoubleValue.NEGATIVE_ZERO : DoubleValue.ZERO;
            }

            return new DoubleValue(result);
        }

        //    }
        public override int Signum()
        {
            if (double.IsNaN(value))
            {
                return 0;
            }

            return value > 0 ? 1 : value == 0 ? 0 : -1;
        }

        //    }
        public override bool IsNegativeZero()
        {
            return value == 0 && (BitConverter.DoubleToInt64Bits(value) & FloatingPointConverter.DOUBLE_SIGN_MASK) != 0;
        }

        //    }
        public override bool IsWholeNumber()
        {
            return value == Math.Floor(value) && !double.IsInfinity(value);
        }

        //    }
        public override int AsSubscript()
        {
            if (IsWholeNumber() && value > 0 && value <= int.MaxValue)
            {
                return (int)value;
            }
            else
            {
                return -1;
            }
        }

        //    }
        public override NumericValue Abs()
        {
            if (value > 0)
            {
                return this;
            }
            else
            {
                return new DoubleValue(Math.Abs(value));
            }
        }

        //    }
        public override int CompareTo(long other)
        {
            double otherDouble = (double)other;
            if (value == otherDouble)
            {
                return 0;
            }

            return value < otherDouble ? -1 : +1;
        }

        //    }
        public override IAtomicMatchKey AsMapKey()
        {
            if (IsNaN())
            {
                return AtomicSortComparer.COLLATION_KEY_NaN;
            }
            else if (double.IsInfinity(value))
            {
                return this;
            }
            else
            {
                try
                {
                    return new BigDecimalValue(value);
                }
                catch (ValidationException e)
                {

                    // We have already ruled out the values that fail (NaN and INF)
                    throw new InvalidOperationException(e.Message, e);
                }
            }
        }

        //    }
        public override bool IsIdentical(AtomicValue v)
        {
            return v is DoubleValue && DoubleSortComparer.GetInstance().ComparesEqual(this, v);
        } /*
     * Diagnostic method: print the sign, exponent, and significand
     * @param d the double to be diagnosed
     */ //    public static void printInternalForm(double d) {
        //                             (bits & 0xfffffffffffffL) << 1 :
        //                             (bits & 0xfffffffffffffL) | 0x10000000000000L;
        //                dec = dec.multiply(new BigDecimal(System.Numerics.BigInteger.valueOf(2).pow(exponent)));
        //            } else {
        //                // Next line is sometimes failing, e.g. on -3.62e-5. Not investigated.
        //                dec = dec.divide(new BigDecimal(System.Numerics.BigInteger.valueOf(2).pow(-exponent)), BigDecimal.ROUND_HALF_EVEN);
        //    public static DoubleValue fromInternalForm(String hex) {
        //        return new DoubleValue(Double.longBitsToDouble(Long.parseLong(hex, 16)));
        //
        //    }
    }
}
