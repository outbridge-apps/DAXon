////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Internal.Numerics;
using System;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal;
using System.Numerics;
namespace OutSmart.DAXon.Values
{
    public sealed class DayTimeDurationValue : DurationValue, IXPathComparable, IContextFreeAtomicValue
    {

        public override BuiltInAtomicType PrimitiveType => BuiltInAtomicType.DAY_TIME_DURATION;

        public override UnicodeString PrimitiveStringValue
        {
            get
            {
                UnicodeBuilder sb = new UnicodeBuilder(16);
                if (_negative)
                {
                    sb.Append('-');
                }

                int days = Days;
                int hours = Hours;
                int minutes = Minutes;
                int seconds = Seconds;
                sb.Append('P');
                if (days != 0)
                {
                    sb.Append(days + "D");
                }

                if (days == 0 || hours != 0 || minutes != 0 || seconds != 0 || _nanoseconds != 0)
                {
                    sb.Append('T');
                }

                if (hours != 0)
                {
                    sb.Append(hours + "H");
                }

                if (minutes != 0)
                {
                    sb.Append(minutes + "M");
                }

                if (seconds != 0 || _nanoseconds != 0 || (days == 0 && minutes == 0 && hours == 0))
                {
                    if (_nanoseconds == 0)
                    {
                        sb.Append(seconds + "S");
                    }
                    else
                    {
                        FormatFractionalSeconds(sb, seconds, (seconds * 1000000000L) + _nanoseconds);
                    }
                }

                return sb.ToUnicodeString();
            }
        }

        public override double LengthInSeconds
        {
            get
            {
                double a = _seconds + ((double)_nanoseconds / 1000000000);

                return _negative ? -a : a;
            }
        }

        public long LengthInMicroseconds
        {
            get
            {
                if (_seconds > long.MaxValue / 1000000)
                {
                    throw new ArithmeticException("Value is too large to be expressed in microseconds");
                }

                long a = _seconds * 1000000 + (_nanoseconds / 1000);
                return _negative ? -a : a;
            }
        }

        public long LengthInNanoseconds
        {
            get
            {
                if (_seconds > long.MaxValue / 1000000000)
                {
                    throw new ArithmeticException("Value is too large to be expressed in nanoseconds");
                }

                long a = _seconds * 1000000000 + _nanoseconds;
                return _negative ? -a : a;
            }
        }

        public IXPathComparable XPathComparable => this;

        public DayTimeDurationValue(int sign, int days, int hours, int minutes, long seconds, int microseconds) : base(sign > 0, 0, 0, days, hours, minutes, seconds, microseconds, BuiltInAtomicType.DAY_TIME_DURATION)
        {
        }

        public DayTimeDurationValue(int days, int hours, int minutes, long seconds, int nanoseconds) : base(0, 0, days, hours, minutes, seconds, nanoseconds, BuiltInAtomicType.DAY_TIME_DURATION)
        {
        }

        public DayTimeDurationValue(int days, int hours, int minutes, long seconds, int nanoseconds, IAtomicType typeLabel) : base(0, 0, days, hours, minutes, seconds, nanoseconds, typeLabel)
        {
        }
        public static IConversionResult MakeDayTimeDurationValue(UnicodeString s)
        {
            IConversionResult d = DurationValue.MakeDuration(s, false, true);
            if (d is ValidationFailure)
            {
                return d;
            }

            DurationValue dv = (DurationValue)d;
            return Converter.DurationToDayTimeDuration.INSTANCE.Convert(dv);
        }

        public override AtomicValue CopyAsSubType(IAtomicType typeLabel)
        {
            return DayTimeDurationValue.FromSeconds(TotalSeconds, typeLabel);
        }

        public static DayTimeDurationValue FromSeconds(BigDecimal seconds)
        {
            return FromSeconds(seconds, BuiltInAtomicType.DAY_TIME_DURATION);
        }

        public static DayTimeDurationValue FromSeconds(BigDecimal seconds, IAtomicType typeLabel)
        {
            BigInteger wholeSeconds = seconds.ToBigInteger();
            long wholeSecondsL = wholeSeconds.LongValueExact(); // ArithmeticException if out of range
            BigDecimal fractionalPart = seconds.Remainder(BigDecimal.One);
            BigDecimal nanoseconds = fractionalPart * BigDecimalValue.BIG_DECIMAL_ONE_BILLION;
            int nanosecondsL = nanoseconds.IntValue();
            return new DayTimeDurationValue(0, 0, 0, wholeSecondsL, nanosecondsL, typeLabel);
        }

        public static DayTimeDurationValue FromMilliseconds(long milliseconds)
        {
            int sign = Math.Sign(milliseconds);
            if (sign < 0)
            {
                milliseconds = -milliseconds;
            }

            try
            {
                return new DayTimeDurationValue(sign, 0, 0, 0, milliseconds / 1000, (int)(milliseconds % 1000) * 1000);
            }
            catch (ArgumentException err)
            {

                // limits exceeded
                throw new ValidationFailure("Duration exceeds limits").MakeException();
            }
        }

        public static DayTimeDurationValue FromMicroseconds(long microseconds)
        {
            int sign = Math.Sign(microseconds);
            if (sign < 0)
            {
                microseconds = -microseconds;
            }

            return new DayTimeDurationValue(sign, 0, 0, 0, microseconds / 1000000, (int)(microseconds % 1000000));
        }

        public static DayTimeDurationValue FromNanoseconds(long nanoseconds)
        {
            return new DayTimeDurationValue(0, 0, 0, nanoseconds / 1000000000, (int)(nanoseconds % 1000000000));
        }

        public override DurationValue Multiply(long factor)
        {

            // Fast path for simple cases
            if (Math.Abs(factor) < 0x7fffffff && Math.Abs(_seconds) < 0x7fffffff && _nanoseconds == 0)
            {
                return new DayTimeDurationValue(0, 0, 0, _seconds * factor * (_negative ? -1 : 1), 0);
            }
            else
            {
                return Multiply(BigDecimal.ValueOf(factor));
            }
        }

        public override DurationValue Multiply(double n)
        {
            if (double.IsNaN(n))
            {
                throw new XPathException("Cannot multiply a duration by NaN", "FOCA0005");
            }

            if (double.IsInfinity(n))
            {
                throw new XPathException("Cannot multiply a duration by infinity", "FODT0002");
            }

            BigDecimal factor = BigDecimal.ValueOf(n);
            return Multiply(factor);
        }

        public override DurationValue Multiply(BigDecimal factor)
        {
            BigDecimal secs = TotalSeconds;
            BigDecimal product = secs * factor;
            try
            {
                return FromSeconds(product);
            }
            catch (ArgumentException err)
            {
                if (err.InnerException is XPathException)
                {
                    throw (XPathException)err.InnerException;
                }
                else
                {
                    throw new XPathException("Overflow when multiplying a duration by a number", err).WithErrorCode("FODT0002");
                }
            }
            catch (ArithmeticException err)
            {
                if (err.InnerException is XPathException)
                {
                    throw (XPathException)err.InnerException;
                }
                else
                {
                    throw new XPathException("Overflow when multiplying a duration by a number", err).WithErrorCode("FODT0002");
                }
            }
        }

        public override DurationValue Divide(double n)
        {
            if (double.IsNaN(n))
            {
                throw new XPathException("Cannot divide a duration by NaN", "FOCA0005");
            }

            if (n == 0)
            {
                throw new XPathException("Cannot divide a duration by zero", "FODT0002");
            }

            BigDecimal secs = TotalSeconds;
            BigDecimal product = secs.Divide(BigDecimal.ValueOf(n));
            try
            {
                return FromSeconds(product);
            }
            catch (ArgumentException err)
            {
                if (err.InnerException is XPathException)
                {
                    throw (XPathException)err.InnerException;
                }
                else
                {
                    throw new XPathException("Overflow when dividing a duration by a number", err).WithErrorCode("FODT0002");
                }
            }
            catch (ArithmeticException err)
            {
                if (err.InnerException is XPathException)
                {
                    throw (XPathException)err.InnerException;
                }
                else
                {
                    throw new XPathException("Overflow when dividing a duration by a number", err).WithErrorCode("FODT0002");
                }
            }
        }

        public override BigDecimalValue Divide(DurationValue other)
        {
            if (other is DayTimeDurationValue)
            {
                BigDecimal v1 = TotalSeconds;
                BigDecimal v2 = other.TotalSeconds;
                if (v2.Sign == 0)
                {
                    throw new XPathException("Divide by zero (durations)", "FOAR0001");
                }

                return new BigDecimalValue(v1.Divide(v2, 20, RoundingMode.HALF_EVEN));
            }
            else
            {
                throw new XPathException("Cannot divide two durations of different type", "XPTY0004");
            }
        }

        /// <summary>
        /// Add two dayTimeDurations
        /// </summary>
        public override DurationValue Add(DurationValue other)
        {
            if (other is DayTimeDurationValue)
            {
                DayTimeDurationValue d2 = (DayTimeDurationValue)other;
                if (((_seconds | d2._seconds) & 0x7fffffff00000000) != 0)
                {

                    // risk of complications, use BigDecimal arithmetic
                    try
                    {
                        BigDecimal v1 = TotalSeconds;
                        BigDecimal v2 = other.TotalSeconds;
                        return FromSeconds(v1 + v2);
                    }
                    catch (ArgumentException e)
                    {
                        throw new XPathException("Overflow when adding two durations", "FODT0002");
                    }
                }
                else
                {

                    // fast path for common case: no risk of overflow
                    return DayTimeDurationValue.FromNanoseconds(LengthInNanoseconds + d2.LengthInNanoseconds);
                }
            }
            else
            {
                throw new XPathException("Cannot add two durations of different type", "XPTY0004");
            }
        }

        public override DurationValue Subtract(DurationValue other)
        {
            if (other is DayTimeDurationValue)
            {
                DayTimeDurationValue d2 = (DayTimeDurationValue)other;
                if (((_seconds | d2._seconds) & 0x7fffffff00000000) != 0)
                {

                    // risk of complications, use BigDecimal arithmetic
                    try
                    {
                        BigDecimal v1 = TotalSeconds;
                        BigDecimal v2 = other.TotalSeconds;
                        return FromSeconds(v1 - v2);
                    }
                    catch (ArgumentException e)
                    {
                        throw new XPathException("Overflow when subtracting two durations", "FODT0002");
                    }
                }
                else
                {

                    // fast path for common case: no risk of overflow
                    return DayTimeDurationValue.FromNanoseconds(LengthInNanoseconds - d2.LengthInNanoseconds);
                }
            }
            else
            {
                throw new XPathException("Cannot subtract two durations of different type", "XPTY0004").AsTypeError();
            }
        }

        public override DurationValue Negate()
        {
            if (_negative)
            {
                return new DayTimeDurationValue(0, 0, 0, _seconds, _nanoseconds);
            }
            else
            {
                return new DayTimeDurationValue(0, 0, 0, -_seconds, -_nanoseconds);
            }
        }

        public override IXPathComparable GetXPathComparable(IStringCollator collator, int implicitTimezone)
        {
            return this;
        }

        public int CompareTo(IXPathComparable other)
        {
            if (other is DayTimeDurationValue)
            {
                if (other == null)
                    throw new NullReferenceException();
                DayTimeDurationValue dtd = (DayTimeDurationValue)other;
                if (this._negative != dtd._negative)
                {
                    return this._negative ? -1 : +1;
                }
                else if (this._seconds != dtd._seconds)
                {
                    return this._seconds.CompareTo(dtd._seconds) * (this._negative ? -1 : +1);
                }
                else
                {
                    return this._nanoseconds.CompareTo(dtd._nanoseconds) * (this._negative ? -1 : +1);
                }
            }
            else
            {
                throw new InvalidCastException("Cannot compare xs:dayTimeDuration to " + other);
            }
        }

        public override IAtomicMatchKey GetXPathMatchKey(IStringCollator collator, int implicitTimezone)
        {
            return this;
        }
    }
}