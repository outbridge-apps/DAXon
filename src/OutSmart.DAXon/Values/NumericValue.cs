////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal.Numerics;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Values
{
    public abstract class NumericValue : AtomicValue, IXPathComparable, IAtomicMatchKey, IContextFreeAtomicValue
    {

        public IXPathComparable XPathComparable => this;
        public NumericValue(IAtomicType typeLabel) : base(typeLabel)
        {
        }

        public static NumericValue ParseNumber(string @in)
        {
            if (@in.IndexOf('e') >= 0 || @in.IndexOf('E') >= 0)
            {
                try
                {
                    return new DoubleValue(double.Parse(@in, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture));
                }
                catch (OverflowException)
                {
                    // net472 double.Parse throws for out-of-range magnitudes (e.g. 2e308); Java's
                    // Double.parseDouble overflows to +/-INF, which is the correct xs:double literal value.
                    return new DoubleValue(@in[0] == '-' ? double.NegativeInfinity : double.PositiveInfinity);
                }
                catch (FormatException e)
                {
                    return DoubleValue.NaN;
                }
            }
            else if (@in.IndexOf('.') >= 0)
            {
                IConversionResult v = BigDecimalValue.MakeDecimalValue(@in, true);
                if (v is ValidationFailure)
                {
                    return DoubleValue.NaN;
                }
                else
                {
                    return (NumericValue)v;
                }
            }
            else
            {
                IConversionResult v = Int64Value.StringToInteger(@in);
                if (v is ValidationFailure)
                {
                    return DoubleValue.NaN;
                }
                else
                {
                    return (NumericValue)v;
                }
            }
        }

        public abstract double GetDoubleValue();
        public abstract float GetFloatValue();
        public abstract BigDecimal GetDecimalValue();
        public abstract override bool EffectiveBooleanValue();
        public static bool IsInteger(AtomicValue value)
        {
            return value is IntegerValue;
        }

        public abstract long LongValue();
        public abstract NumericValue Negate();
        public abstract NumericValue Floor();
        public abstract NumericValue Ceiling();
        public abstract NumericValue Round(int scale);
        public abstract NumericValue Round(int scale, Round.RoundingRule roundingRule);
        public abstract int Signum();
        public virtual bool IsNegativeZero()
        {
            return false;
        }

        public abstract bool IsWholeNumber();
        public abstract int AsSubscript();
        public abstract NumericValue Abs();
        public override IAtomicMatchKey GetXPathMatchKey(IStringCollator collator, int implicitTimezone)
        {
            return this;
        }

        public override IXPathComparable GetXPathComparable(IStringCollator collator, int implicitTimezone)
        {
            return this;
        }

        public virtual int CompareTo(IXPathComparable other)
        {
            if (other is NumericValue)
            {
                double a = GetDoubleValue();
                double b = ((NumericValue)other).GetDoubleValue();

                // IntelliJ says this can be replaced with Double.compare(). But it can't. Double.compare()
                // treats positive and negative zero as not equal; we want them treated as equal. XSLT3 test case
                // boolean-014.  MHK 2020-02-17
                if (a == b)
                {
                    return 0;
                }

                if (a < b)
                {
                    return -1;
                }

                return +1;
            }
            else
            {
                throw new InvalidCastException("Cannot compare numeric value to " + other.ToString());
            }
        }

        public abstract int CompareTo(long other);
        public override bool Equals(object other)
        {
            return other is NumericValue && CompareTo((NumericValue)other) == 0;
        }

        public abstract override int GetHashCode();
        public override string Show()
        {
            return GetStringValue();
        }
    }
}
