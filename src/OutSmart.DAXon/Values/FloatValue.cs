////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Functions;
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
namespace OutSmart.DAXon.Values
{
    /// <summary>
    /// A numeric (single precision floating point) value
    /// </summary>
    internal sealed class FloatValue : NumericValue
    {
        public static readonly FloatValue NaN = new FloatValue(float.NaN);
        private readonly float value;

        public override BuiltInAtomicType PrimitiveType => BuiltInAtomicType.FLOAT;

        //    }
        public override UnicodeString PrimitiveStringValue => FloatToString(value);

        //    }
        public override UnicodeString CanonicalLexicalRepresentation
        {
            get
            {
                UnicodeBuilder fsb = new UnicodeBuilder(32);
                return FloatingPointConverter.AppendFloat(fsb, value, true);
            }
        }
        public FloatValue(float value) : base(BuiltInAtomicType.FLOAT)
        {
            this.value = value;
        }

        public FloatValue(float value, IAtomicType typeLabel) : base(typeLabel)
        {
            this.value = value;
        }

        public override AtomicValue CopyAsSubType(IAtomicType typeLabel)
        {
            return new FloatValue(value, typeLabel);
        }

        public override float GetFloatValue()
        {
            return value;
        }

        public override double GetDoubleValue()
        {
            return value;
        }

        public override //@CSharpReplaceBody(code="return Singulink.Numerics.BigDecimal.Parse(value.ToString(System.Globalization.CultureInfo.InvariantCulture));")
        BigDecimal GetDecimalValue()
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
                return (int)(double)(GetDoubleValue()).GetHashCode();
            }
        }

        public override bool IsNaN()
        {
            return float.IsNaN(value);
        }

        public override bool EffectiveBooleanValue()
        {
            return (value != 0 && !float.IsNaN(value));
        }

        //    }
        public static UnicodeString FloatToString(float value)
        {
            return FloatingPointConverter.AppendFloat(new UnicodeBuilder(), value, false);
        }

        //    }
        /// <summary>
        /// Negate the value
        /// </summary>
        public override NumericValue Negate()
        {
            return new FloatValue(-value);
        }

        //    }
        /// <summary>
        /// Implement the XPath floor() function
        /// </summary>
        public override NumericValue Floor()
        {
            return new FloatValue((float)Math.Floor(value));
        }

        //    }
        /// <summary>
        /// Implement the XPath ceiling() function
        /// </summary>
        public override NumericValue Ceiling()
        {
            return new FloatValue((float)Math.Ceiling(value));
        }

        //    }
        /// <summary>
        /// Implement the XPath round() function
        /// </summary>
        public override NumericValue Round(int scale)
        {
            if (float.IsNaN(value))
            {
                return this;
            }

            if (float.IsInfinity(value))
            {
                return this;
            }

            if (value == 0)
            {
                return this; // handles the negative zero case
            }

            if (scale == 0 && value > int.MinValue && value < int.MaxValue)
            {
                if (value >= -0.5 && value < 0)
                {
                    return new FloatValue(-0F);
                }

                return new FloatValue((float)JavaMath.Round(value));
            }

            DoubleValue d = new DoubleValue(GetDoubleValue());
            d = (DoubleValue)d.Round(scale);
            return new FloatValue(d.GetFloatValue());
        }

        //    }
        public override NumericValue Round(int scale, Round.RoundingRule roundingRule)
        {
            DoubleValue d = new DoubleValue(GetDoubleValue());
            d = (DoubleValue)d.Round(scale, roundingRule);
            return new FloatValue(d.GetFloatValue());
        }

        //    }
        public override int Signum()
        {
            if (float.IsNaN(value))
            {
                return 0;
            }

            return CompareTo(0);
        }

        //    }
        public override bool IsNegativeZero()
        {
            return value == 0 && (FloatingPointConverter.SingleToInt32Bits(value) & FloatingPointConverter.FLOAT_SIGN_MASK) != 0;
        }

        //    }
        public override bool IsWholeNumber()
        {
            return value == Math.Floor(value) && !float.IsInfinity(value);
        }

        //    }
        public override int AsSubscript()
        {
            if (IsWholeNumber() && value > 0 && value < 2147483648f)   // int.MaxValue as a float is 2^31, one past the range
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
                return new FloatValue(Math.Abs(value));
            }
        }

        //    }
        public override int CompareTo(IXPathComparable other)
        {
            if (other is NumericValue)
            {
                if (other is FloatValue)
                {
                    float otherFloat = ((FloatValue)other).value;

                    // Do not rewrite as Float.compare() - see IntelliJ bug IDEA-196419
                    if (value == otherFloat)
                    {
                        return 0;
                    }
                    else if (value < otherFloat)
                    {
                        return -1;
                    }
                    else
                    {
                        return +1;
                    }
                }

                if (other is DoubleValue)
                {
                    return base.CompareTo(other);
                }

                return CompareTo((FloatValue)Converter.NumericToFloat.INSTANCE.Convert((NumericValue)other));
            }
            else
            {
                throw new InvalidCastException("Cannot compare xs:float to " + other);
            }
        }

        //    }
        public override int CompareTo(long other)
        {
            float otherFloat = (float)other;
            if (value == otherFloat)
            {
                return 0;
            }

            return value < otherFloat ? -1 : +1;
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
                return new DoubleValue(value);
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
            return v is FloatValue && DoubleSortComparer.GetInstance().ComparesEqual(this, v);
        }

        //    }
        public override AtomicValue AsAtomic()
        {
            return this;
        }
    }
}
