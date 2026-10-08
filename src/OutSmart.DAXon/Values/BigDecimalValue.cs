////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Internal.Numerics;
using System;
using System.Globalization;
using System.Text;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal;
using System.Numerics;
namespace OutSmart.DAXon.Values
{
    public sealed class BigDecimalValue : DecimalValue
    {
        public const int DIVIDE_PRECISION = 18;
        public static readonly BigDecimal BIG_DECIMAL_ONE_MILLION = BigDecimal.ValueOf(1000000);
        public static readonly BigDecimal BIG_DECIMAL_ONE_BILLION = BigDecimal.ValueOf(1000000000);
        public static readonly BigDecimalValue ZERO = new BigDecimalValue(BigDecimal.ValueOf(0));
        public static readonly BigDecimalValue ONE = new BigDecimalValue(BigDecimal.ValueOf(1));
        public static readonly BigDecimalValue TWO = new BigDecimalValue(BigDecimal.ValueOf(2));
        public static readonly BigDecimalValue THREE = new BigDecimalValue(BigDecimal.ValueOf(3));
        public static readonly BigDecimal MAX_INT = BigDecimal.ValueOf(int.MaxValue);

        private static readonly OutSmart.DAXon.Internal.Regex.Pattern decimalPattern = OutSmart.DAXon.Internal.Regex.Pattern.Compile("(\\-|\\+)?((\\.[0-9]+)|([0-9]+(\\.[0-9]*)?))");
        private readonly BigDecimal value;
        private double doubleValue = double.NaN; // meaning unknown

        public override BuiltInAtomicType PrimitiveType => BuiltInAtomicType.DECIMAL;

        //    }
        public override UnicodeString CanonicalLexicalRepresentation
        {
            get
            {
                UnicodeString s = this.UnicodeStringValue.Tidy();
                if (s.IndexOf('.') < 0)
                {
                    s = s.Concat(StringConstants.POINT_ZERO);
                }

                return s;
            }
        }

        //    }
        public override UnicodeString PrimitiveStringValue => BMPString.Of(DecimalToString(value, new StringBuilder(16)).ToString());
        public BigDecimalValue(BigDecimal value) : base(BuiltInAtomicType.DECIMAL)
        {
            this.value = value.StripTrailingZeros();
        }

        public BigDecimalValue(BigDecimal value, IAtomicType typeLabel) : base(typeLabel)
        {
            this.value = value.StripTrailingZeros();
        }

        public BigDecimalValue(double @in) : base(BuiltInAtomicType.DECIMAL)
        {
            try
            {
                BigDecimal d = new BigDecimal(@in);

                // Note, this gives a different result from BigDecimal.valueOf(@in) - it retains more precision.
                value = d.StripTrailingZeros();
            }
            catch (Exception err)
            {

                // Must be a special value such as NaN or infinity
                ValidationFailure e = new ValidationFailure("Cannot convert double " + Err.Wrap(@in + "", Err.VALUE) + " to decimal");
                e.SetErrorCode("FOCA0002");
                throw e.MakeException();
            }
        }

        public BigDecimalValue(long @in) : base(BuiltInAtomicType.DECIMAL)
        {
            value = BigDecimal.ValueOf(@in);
        }
        public static IConversionResult MakeDecimalValue(string @in, bool validate)
        {
            BigDecimalValue parsed = ParseCore(@in, out string error);
            if (error == null)
            {
                return parsed;
            }

            ValidationFailure e = new ValidationFailure("Cannot convert string " + Err.Wrap(@in, Err.VALUE) + " to xs:decimal: " + error);
            e.SetErrorCode("FORG0001");
            return e;
        }

        public static BigDecimalValue Parse(string @in)
        {
            BigDecimalValue parsed = ParseCore(@in, out string error);
            if (error != null)
            {
                throw new FormatException(error);
            }

            return parsed;
        }

        // null with the reason in error when @in is not an xs:decimal lexical form; never throws on bad
        // input, so casts and castable over dirty data stay cheap.
        private static BigDecimalValue ParseCore(string @in, out string error)
        {
            error = null;
            // Compact fast path: one pass, no StringBuilder — the dominant case is a plain decimal
            // whose magnitude fits a long (≤18 significant digits). Same state machine and the same
            // FormatException texts as the general path; any overflow bails out BEFORE mutating
            // anything and re-parses on ParseSlow. Byte-identical: same (unscaled, scale).
            long acc = 0;
            int scale = 0;
            int state = 0;
            bool neg = false, foundDigit = false;
            int len = @in.Length;
            for (int i = 0; i < len; i++)
            {
                char c = @in[i];
                if (c >= '0' && c <= '9')
                {
                    if (state == 0)
                    {
                        state = 1;
                    }
                    else if (state >= 3)
                    {
                        scale++;
                    }

                    if (state == 5)
                    {
                        error = "contains embedded whitespace";
                        return null;
                    }

                    foundDigit = true;
                    if (acc > (long.MaxValue - 9) / 10)
                    {
                        return ParseSlow(@in, out error);
                    }

                    acc = acc * 10 + (c - '0');
                }
                else
                {
                    switch (c)
                    {
                        case ' ':
                        case '\t':
                        case '\r':
                        case '\n':
                            if (state != 0)
                            {
                                state = 5;
                            }

                            break;
                        case '+':
                            if (state != 0)
                            {
                                error = "unexpected sign";
                                return null;
                            }

                            state = 1;
                            break;
                        case '-':
                            if (state != 0)
                            {
                                error = "unexpected sign";
                                return null;
                            }

                            state = 1;
                            neg = true;
                            break;
                        case '.':
                            if (state == 5)
                            {
                                error = "contains embedded whitespace";
                                return null;
                            }

                            if (state >= 3)
                            {
                                error = "more than one decimal point";
                                return null;
                            }

                            state = 3;
                            break;
                        default:
                            error = "invalid character '" + c + "'";
                            return null;
                    }
                }
            }

            if (!foundDigit)
            {
                error = "no digits in value";
                return null;
            }

            // remove insignificant trailing zeroes
            while (scale > 0 && acc % 10 == 0)
            {
                acc /= 10;
                scale--;
            }

            if (acc == 0)
            {
                return BigDecimalValue.ZERO;
            }

            return new BigDecimalValue(BigDecimal.FromCompact(neg ? -acc : acc, scale));
        }

        private static BigDecimalValue ParseSlow(string @in, out string error)
        {
            error = null;
            StringBuilder digits = new StringBuilder(@in.Length);
            int scale = 0;
            int state = 0;

            // 0 - in initial whitespace; 1 - after sign
            // 3 - after decimal point; 5 - in final whitespace
            bool foundDigit = false;
            // long-compact fast path: accumulate the unsigned magnitude in a long in lock-step with
            // `digits`. If it never overflows we skip digits.ToString() + BigIntegers.FromString and
            // build the BigDecimal straight from the long. Byte-identical: same (unscaled, scale).
            long acc = 0;
            bool accOvf = false;
            bool neg = false;
            int len = @in.Length;
            for (int i = 0; i < len; i++)
            {
                char c = @in[i];
                switch (c)
                {
                    case ' ':
                    case '\t':
                    case '\r':
                    case '\n':
                        if (state != 0)
                        {
                            state = 5;
                        }

                        break;
                    case '+':
                        if (state != 0)
                        {
                            error = "unexpected sign";
                            return null;
                        }

                        state = 1;
                        break;
                    case '-':
                        if (state != 0)
                        {
                            error = "unexpected sign";
                            return null;
                        }

                        state = 1;
                        neg = true;
                        digits.Append(c);
                        break;
                    case '0':
                    case '1':
                    case '2':
                    case '3':
                    case '4':
                    case '5':
                    case '6':
                    case '7':
                    case '8':
                    case '9':
                        if (state == 0)
                        {
                            state = 1;
                        }
                        else if (state >= 3)
                        {
                            scale++;
                        }

                        if (state == 5)
                        {
                            error = "contains embedded whitespace";
                            return null;
                        }

                        digits.Append(c);
                        foundDigit = true;
                        if (!accOvf)
                        {
                            if (acc > (long.MaxValue - 9) / 10)
                            {
                                accOvf = true;
                            }
                            else
                            {
                                acc = acc * 10 + (c - '0');
                            }
                        }

                        break;
                    case '.':
                        if (state == 5)
                        {
                            error = "contains embedded whitespace";
                            return null;
                        }

                        if (state >= 3)
                        {
                            error = "more than one decimal point";
                            return null;
                        }

                        state = 3;
                        break;
                    default:
                        error = "invalid character '" + c + "'";
                        return null;
                }
            }

            if (!foundDigit)
            {
                error = "no digits in value";
                return null;
            }


            // remove insignificant trailing zeroes
            while (scale > 0)
            {
                if (digits[digits.Length - 1] == '0')
                {
                    digits.Length = digits.Length - 1;
                    scale--;
                    if (!accOvf)   // keep the long magnitude in lock-step with the stripped digits
                    {
                        acc /= 10;
                    }
                }
                else
                {
                    break;
                }
            }

            if (digits.Length == 0 || (digits.Length == 1 && digits[0] == '-'))
            {
                return BigDecimalValue.ZERO;
            }

            if (!accOvf)
            {
                // magnitude fit a long: build BigDecimal straight from it (skips ToString + FromString).
                BigInteger u = neg ? new BigInteger(-acc) : new BigInteger(acc);
                return new BigDecimalValue(new BigDecimal(u, scale));
            }

            BigInteger bigInt = BigIntegers.FromString(digits.ToString());
            BigDecimal bigDec = new BigDecimal(bigInt, scale);
            return new BigDecimalValue(bigDec);
        }

        public static bool CastableAsDecimal(string @in)
        {
            string trimmed = Whitespace.Trim(@in).ToString();
            return decimalPattern.Matcher(trimmed).Matches();
        }

        public override AtomicValue CopyAsSubType(IAtomicType typeLabel)
        {
            if (typeLabel.GetPrimitiveItemType() == BuiltInAtomicType.INTEGER)
            {
                return IntegerValue.MakeIntegerValue(value.ToBigInteger()).CopyAsSubType(typeLabel);
            }
            else
            {
                return new BigDecimalValue(value, typeLabel);
            }
        }

        public override double GetDoubleValue()
        {
            if (double.IsNaN(doubleValue))
            {
                doubleValue = value.DoubleValue();
            }

            return doubleValue;
        }

        public override float GetFloatValue()
        {
            return (float)value.DoubleValue();
        }

        public override long LongValue()
        {
            return JavaMath.ToLong(value.DoubleValue());
        }

        public override BigDecimal GetDecimalValue()
        {
            return value;
        }

        public override int GetHashCode()
        {
            BigDecimal round = value.SetScale(0, RoundingMode.DOWN);
            long longVal;
            try
            {
                longVal = round.LongValue();
            }
            catch (Exception e)
            {

                // This path is for C#, where converting BigDecimal to long gives an OverflowException if out of range
                longVal = long.MaxValue;
            }

            if (longVal > int.MinValue && longVal < int.MaxValue)
            {
                return (int)longVal;
            }
            else
            {
                return (int)(double)(GetDoubleValue()).GetHashCode();
            }
        }

        public override bool EffectiveBooleanValue()
        {
            return value.Sign != 0;
        }

        //    }
        public static StringBuilder DecimalToString(BigDecimal value, StringBuilder fsb)
        {

            // Compact mantissa: format the long directly — no BigInteger boxing, no intermediate
            // strings (format-number prints one decimal per row on hot text pipelines). Same
            // digits/point/zero placement as the general path below.
            if (value.TryGetCompactParts(out long compact, out int cscale))
            {
                if (compact == 0)
                {
                    fsb.Append('0');
                    return fsb;
                }

                if (compact < 0)
                {
                    fsb.Append('-');
                }

                char[] digits = new char[20];
                ulong u = compact < 0 ? (ulong)(-compact) : (ulong)compact;   // INFLATED is long.MinValue, never delivered
                int n = 0;
                while (u > 0)
                {
                    digits[n++] = (char)('0' + (int)(u % 10));
                    u /= 10;
                }

                if (cscale <= 0)
                {
                    for (int i = n - 1; i >= 0; i--)
                    {
                        fsb.Append(digits[i]);
                    }

                    for (int i = 0; i < -cscale; i++)
                    {
                        fsb.Append('0');
                    }
                }
                else if (cscale >= n)
                {
                    fsb.Append("0.");
                    for (int i = n; i < cscale; i++)
                    {
                        fsb.Append('0');
                    }

                    for (int i = n - 1; i >= 0; i--)
                    {
                        fsb.Append(digits[i]);
                    }
                }
                else
                {
                    for (int i = n - 1; i >= cscale; i--)
                    {
                        fsb.Append(digits[i]);
                    }

                    fsb.Append('.');
                    for (int i = cscale - 1; i >= 0; i--)
                    {
                        fsb.Append(digits[i]);
                    }
                }

                return fsb;
            }

            // Can't use BigDecimal#toString() under JDK 1.5 because this produces values like "1E-5".
            // Can't use BigDecimal#toPlainString() because it retains trailing zeroes to represent the scale
            int scale = value.Scale();
            if (scale == 0)
            {
                fsb.Append(value.ToString());
                return fsb;
            }
            else if (scale < 0)
            {
                string s = value.Abs().UnscaledValue().ToString(CultureInfo.InvariantCulture);
                if (s.Equals("0"))
                {
                    fsb.Append('0');
                    return fsb;
                }


                //StringBuilder sb = new StringBuilder(s.length() + (-scale) + 2);
                if (value.Sign < 0)
                {
                    fsb.Append('-');
                }

                fsb.Append(s);
                for (int i = 0; i < -scale; i++)
                {
                    fsb.Append('0');
                }

                return fsb;
            }
            else
            {
                string s = value.Abs().UnscaledValue().ToString(CultureInfo.InvariantCulture);
                if (s.Equals("0"))
                {
                    fsb.Append('0');
                    return fsb;
                }

                int len = s.Length;

                //StringBuilder sb = new StringBuilder(len+1);
                if (value.Sign < 0)
                {
                    fsb.Append('-');
                }

                if (scale >= len)
                {
                    fsb.Append("0.");
                    for (int i = len; i < scale; i++)
                    {
                        fsb.Append('0');
                    }

                    fsb.Append(s);
                }
                else
                {
                    fsb.Append(s.Substring(0, len - scale));
                    fsb.Append('.');
                    fsb.Append(s.Substring(len - scale));
                }

                return fsb;
            }
        }

        //    }
        /// <summary>
        /// Negate the value
        /// </summary>
        public override NumericValue Negate()
        {
            return new BigDecimalValue(-value);
        }

        //    }
        /// <summary>
        /// Implement the XPath floor() function
        /// </summary>
        public override NumericValue Floor()
        {
            return new BigDecimalValue(value.SetScale(0, RoundingMode.FLOOR));
        }

        //    }
        /// <summary>
        /// Implement the XPath ceiling() function
        /// </summary>
        public override NumericValue Ceiling()
        {
            return new BigDecimalValue(value.SetScale(0, RoundingMode.CEILING));
        }

        //    }
        /// <summary>
        /// Implement the XPath round() function
        /// </summary>
        public override NumericValue Round(int scale)
        {

            // The XPath rules say that we should round to the nearest integer, with .5 rounding towards
            // positive infinity. Unfortunately this is not one of the rounding modes that the Java BigDecimal
            // class supports, so we need different rules depending on the value.
            // If the value is positive, we use ROUND_HALF_UP; if it is negative, we use ROUND_HALF_DOWN (here "UP"
            // means "away from zero")
            if (scale >= value.Scale())
            {

                // no-op - see bug #4027
                return this;
            }

            // Same trap as the BigIntegerValue twin: this BigDecimal keeps scale >= 0, so
            // SetScale at 10^-scale coarser than the value materializes a Pow10 that can run
            // to two billion digits. Once the factor out-digits the value, zero is the
            // nearest multiple, so answer it without building the power.
            if (-(long)scale > (long)value.Precision() - value.Scale() + 2)
            {
                return ZERO;
            }

            switch (value.Sign)
            {
                case -1:
                    return new BigDecimalValue(value.SetScale(scale, RoundingMode.HALF_DOWN));
                case 0:
                    return this;
                case +1:
                    return new BigDecimalValue(value.SetScale(scale, RoundingMode.HALF_UP));
                default:

                    // can't happen
                    return this;
            }
        }

        //    }
        public override NumericValue Round(int scale, Round.RoundingRule roundingRule)
        {
            if (scale >= value.Scale())
            {
                return this;
            }

            // Mirror of the BigIntegerValue twin: rules that pick a nearest value (or truncate)
            // answer zero once the factor out-digits the value; the away-from-zero family would
            // need +/-10^-scale itself, which is the unbuildable power, so it gets FOAR0002.
            long k = -(long)scale;
            if (k > (long)value.Precision() - value.Scale() + 2)
            {
                switch (roundingRule)
                {
                    case Functions.Round.RoundingRule.FLOOR:
                        if (value.Sign > 0)
                        {
                            return ZERO;
                        }
                        break;
                    case Functions.Round.RoundingRule.CEILING:
                        if (value.Sign < 0)
                        {
                            return ZERO;
                        }
                        break;
                    case Functions.Round.RoundingRule.AWAY_FROM_ZERO:
                        break;
                    default:
                        return ZERO;
                }

                throw new XPathException(
                    "Rounding away from zero at a precision of 10^" + k + " overflows", "FOAR0002");
            }

            BigDecimal scaledValue;
            switch (roundingRule)
            {
                case Functions.Round.RoundingRule.FLOOR:
                    scaledValue = value.SetScale(scale, RoundingMode.FLOOR);
                    break;
                case Functions.Round.RoundingRule.CEILING:
                    scaledValue = value.SetScale(scale, RoundingMode.CEILING);
                    break;
                case Functions.Round.RoundingRule.AWAY_FROM_ZERO:
                    scaledValue = value.SetScale(scale, RoundingMode.UP);
                    break;
                case Functions.Round.RoundingRule.TOWARD_ZERO:
                    scaledValue = value.SetScale(scale, RoundingMode.DOWN);
                    break;
                case Functions.Round.RoundingRule.HALF_TO_FLOOR:
                    if (Signum() >= 0)
                    {
                        scaledValue = value.SetScale(scale, RoundingMode.HALF_DOWN);
                    }
                    else
                    {
                        scaledValue = value.SetScale(scale, RoundingMode.HALF_UP);
                    }

                    break;
                case Functions.Round.RoundingRule.HALF_TO_CEILING:
                default:
                    if (Signum() >= 0)
                    {
                        scaledValue = value.SetScale(scale, RoundingMode.HALF_UP);
                    }
                    else
                    {
                        scaledValue = value.SetScale(scale, RoundingMode.HALF_DOWN);
                    }

                    break;
                case Functions.Round.RoundingRule.HALF_TOWARD_ZERO:
                    scaledValue = value.SetScale(scale, RoundingMode.HALF_DOWN);
                    break;
                case Functions.Round.RoundingRule.HALF_AWAY_FROM_ZERO:
                    scaledValue = value.SetScale(scale, RoundingMode.HALF_UP);
                    break;
                case Functions.Round.RoundingRule.HALF_TO_EVEN:
                    scaledValue = value.SetScale(scale, RoundingMode.HALF_EVEN);
                    break;
            }

            return new BigDecimalValue(scaledValue.StripTrailingZeros());
        }

        //    }
        public override int Signum()
        {
            return value.Sign;
        }

        //    }
        public override bool IsWholeNumber()
        {
            return value.Scale() == 0 || value.CompareTo(value.SetScale(0, RoundingMode.DOWN)) == 0;
        }

        //    }
        public override int AsSubscript()
        {
            if (IsWholeNumber() && value.CompareTo(BigDecimal.Zero) > 0 && value.CompareTo(MAX_INT) <= 0)
            {
                try
                {
                    return (int)LongValue();
                }
                catch (XPathException e)
                {
                    return -1;
                }
            }
            else
            {
                return -1;
            }
        }

        //    }
        public override NumericValue Abs()
        {
            if (value.Sign > 0)
            {
                return this;
            }
            else
            {
                return new BigDecimalValue(-value);
            }
        }

        //    }
        public override IXPathComparable GetXPathComparable(IStringCollator collator, int implicitTimezone)
        {
            return this;
        }

        //    }
        public override int CompareTo(IXPathComparable other)
        {
            if (other is NumericValue)
            {
                if (NumericValue.IsInteger(((NumericValue)other)))
                {

                    // deliberately triggers a InvalidCastException if other value is the wrong type
                    try
                    {
                        return value.CompareTo(((NumericValue)other).GetDecimalValue());
                    }
                    catch (XPathException err)
                    {
                        throw new InvalidOperationException("Conversion of integer to decimal should never fail");
                    }
                }
                else if (other is BigDecimalValue)
                {
                    return value.CompareTo(((BigDecimalValue)other).value);
                }
                else if (other is FloatValue)
                {
                    return -other.CompareTo(this);
                }
                else
                {
                    return base.CompareTo(other);
                }
            }
            else
            {
                throw new InvalidCastException("Cannot compare xs:decimal to " + other.ToString());
            }
        }

        //    }
        public override int CompareTo(long other)
        {
            if (other == 0)
            {
                return value.Sign;
            }

            return value.CompareTo(BigDecimal.ValueOf(other));
        }

        //    }
        public override bool IsIdentical(AtomicValue v)
        {
            return (v is DecimalValue) && Equals(v);
        }
    }
}
