////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal.Numerics;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Internal;
using System.Numerics;
namespace OutSmart.DAXon.Values
{
    /// <summary>
    /// A value of type xs:duration
    /// </summary>
    public class DurationValue : AtomicValue, IAtomicMatchKey
    {
        protected readonly bool _negative;
        protected readonly int _months;
        protected readonly long _seconds;
        protected readonly int _nanoseconds;

        public override BuiltInAtomicType PrimitiveType => BuiltInAtomicType.DURATION;

        public virtual int Years => _months / 12;

        public virtual int Months => _months % 12;

        public virtual int Days => (int)(_seconds / (24 * 60 * 60));

        public virtual int Hours => (int)(_seconds % (24 * 60 * 60) / (60 * 60));

        public virtual int Minutes => (int)(_seconds % (60 * 60) / 60);

        public virtual int Seconds => (int)(_seconds % 60);

        public virtual int Microseconds => _nanoseconds / 1000;

        public virtual int Nanoseconds => _nanoseconds;

        public virtual int TotalMonths => _negative ? -_months : _months;

        public virtual BigDecimal TotalSeconds
        {
            get
            {
                BigDecimal dec = BigDecimal.ValueOf(_negative ? -_seconds : _seconds);
                if (_nanoseconds != 0)
                {
                    dec = dec + new BigDecimal(new BigInteger(_negative ? -_nanoseconds : _nanoseconds), 9);
                }

                return dec;
            }
        }

        public override UnicodeString PrimitiveStringValue
        {
            get
            {

                // Note, Schema does not define a canonical representation. We omit all zero components, unless
                // the duration is zero-length, in which case we output PT0S.
                if (_months == 0 && _seconds == 0 && _nanoseconds == 0)
                {
                    return BMPString.Of("PT0S");
                }

                UnicodeBuilder sb = new UnicodeBuilder(16);
                if (_negative)
                {
                    sb.Append('-');
                }

                int years = Years;
                int months = Months;
                int days = Days;
                int hours = Hours;
                int minutes = Minutes;
                int seconds = Seconds;
                sb.Append('P');
                if (years != 0)
                {
                    sb.Append(years + "Y");
                }

                if (months != 0)
                {
                    sb.Append(months + "M");
                }

                if (days != 0)
                {
                    sb.Append(days + "D");
                }

                if (hours != 0 || minutes != 0 || seconds != 0 || _nanoseconds != 0)
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

                if (seconds != 0 || _nanoseconds != 0)
                {
                    if (seconds != 0 && _nanoseconds == 0)
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

        public virtual double LengthInSeconds
        {
            get
            {
                double a = _months * (365.242199 / 12) * 24 * 60 * 60 + _seconds + ((double)_nanoseconds / 1000000000);
                return _negative ? -a : a;
            }
        }

        public virtual DurationComparable SchemaComparable
        {
            get
            {
                int m = this._months;
                long s = this._seconds;
                int n = this._nanoseconds;
                if (this._negative)
                {
                    s = -s;
                    m = -m;
                    n = -n;
                }

                return new DurationComparable(m, s, n);
            }
        }
        public DurationValue(bool positive, int years, int months, int days, int hours, int minutes, long seconds, int microseconds) : this(positive, years, months, days, hours, minutes, seconds, microseconds, BuiltInAtomicType.DURATION)
        {
        }

        public DurationValue(bool positive, int years, int months, int days, int hours, int minutes, long seconds, int microseconds, IAtomicType typeLabel) : base(typeLabel)
        {
            if (years < 0 || months < 0 || days < 0 || hours < 0 || minutes < 0 || seconds < 0 || microseconds < 0)
            {
                throw new ArgumentException("Negative component value");
            }

            if ((double)years * 12 + (double)months > int.MaxValue)
            {
                throw new ArgumentException("Duration months limit exceeded");
            }

            if ((double)days * (24 * 60 * 60) + (double)hours * (60 * 60) + (double)minutes * 60 + (double)seconds > long.MaxValue)
            {
                throw new ArgumentException("Duration seconds limit exceeded");
            }

            this._months = years * 12 + months;
            long h = days * 24L + hours;   // 24L: int overflow for large day counts (upstream days * 24L)
            long m = h * 60 + minutes;
            this._seconds = m * 60 + seconds;
            this._nanoseconds = microseconds * 1000;
            this._negative = IsNegativeDuration(!positive);
        }

        public DurationValue(int years, int months, int days, int hours, int minutes, long seconds, int nanoseconds, IAtomicType typeLabel) : base(typeLabel)
        {
            bool somePositive = years > 0 || months > 0 || days > 0 || hours > 0 || minutes > 0 || seconds > 0 || nanoseconds > 0;
            bool someNegative = years < 0 || months < 0 || days < 0 || hours < 0 || minutes < 0 || seconds < 0 || nanoseconds < 0;
            if (somePositive && someNegative)
            {
                throw new ArgumentException("Some component values are positive and some negative");
            }

            if (someNegative)
            {
                years = -years;
                months = -months;
                days = -days;
                hours = -hours;
                minutes = -minutes;
                seconds = -seconds;
                nanoseconds = -nanoseconds;
            }

            if ((double)years * 12 + (double)months > int.MaxValue)
            {
                throw new ArgumentException("Duration months limit exceeded");
            }

            if ((double)days * (24 * 60 * 60) + (double)hours * (60 * 60) + (double)minutes * 60 + (double)seconds > long.MaxValue)
            {
                throw new ArgumentException("Duration seconds limit exceeded");
            }

            this._months = years * 12 + months;
            long h = days * 24L + hours;   // 24L: int overflow for large day counts (upstream days * 24L)
            long m = h * 60 + minutes;
            this._seconds = m * 60 + seconds;
            this._nanoseconds = nanoseconds;
            this._negative = someNegative;
        }

        protected static void FormatFractionalSeconds(UnicodeBuilder sb, int seconds, long nanosecs)
        {
            string mss = nanosecs + "";
            if (seconds == 0)
            {
                mss = "0000000000" + mss;
                mss = mss.Substring(mss.Length - 10);
            }

            sb.Append(mss.Substring(0, mss.Length - 9));
            sb.Append('.');
            int lastSigDigit = mss.Length - 1;
            while (mss[lastSigDigit] == '0')
            {
                lastSigDigit--;
            }

            sb.Append(mss.Substring(mss.Length - 9, lastSigDigit + 10 - mss.Length) /*Java substring(begin,END) -> C# (start,LENGTH)*/);
            sb.Append('S');
        }

        protected virtual bool IsNegativeDuration(bool nonPositive)
        {
            if (_months == 0 && _seconds == 0 && _nanoseconds == 0)
            {
                return false;
            }
            else
            {
                return nonPositive;
            }
        }

        public static IConversionResult MakeDuration(UnicodeString s)
        {
            return MakeDuration(s, true, true);
        }

        protected static IConversionResult MakeDuration(UnicodeString s, bool allowYM, bool allowDT)
        {
            int years = 0, months = 0, days = 0, hours = 0, minutes = 0, seconds = 0, nanoseconds = 0;
            bool negative = false;
            StringTokenizer tok = new StringTokenizer(Whitespace.Trim(s).ToString(), "-+.PYMDTHS", true);
            int components = 0;
            if (!tok.HasMoreTokens())
            {
                return BadDuration("empty string", s);
            }

            string part = tok.NextToken();
            if (part == "+")
            {
                return BadDuration("+ sign not allowed in a duration", s);
            }
            else if (part == "-")
            {
                negative = true;
                if (tok.HasMoreTokens())
                {
                    part = tok.NextToken();
                }
                else
                {
                    return BadDuration("'-' on its own is not a valid duration", s);
                }
            }

            if (part != "P")
            {
                return BadDuration("missing 'P'", s);
            }

            int state = 0;
            while (tok.HasMoreTokens())
            {
                part = tok.NextToken();
                if (part == "T")
                {
                    state = 4;
                    if (!tok.HasMoreTokens())
                    {
                        return BadDuration("T must be followed by time components", s);
                    }

                    part = tok.NextToken();
                }

                int value = SimpleInteger(part);
                if (value < 0)
                {
                    if (value == -2)
                    {
                        return BadDuration("component of duration exceeds Saxon limits", s, "FODT0002");
                    }
                    else
                    {
                        return BadDuration("invalid or non-numeric component", s);
                    }
                }

                if (!tok.HasMoreTokens())
                {
                    return BadDuration("missing unit letter at end", s);
                }

                char delim = tok.NextToken()[0];
                switch (delim)
                {
                    case 'Y':
                        if (state > 0)
                        {
                            return BadDuration("Y is out of sequence", s);
                        }

                        if (!allowYM)
                        {
                            return BadDuration("Year component is not allowed in dayTimeDuration", s);
                        }

                        years = value;
                        state = 1;
                        components++;
                        break;
                    case 'M':
                        if (state == 4 || state == 5)
                        {
                            if (!allowDT)
                            {
                                return BadDuration("Minute component is not allowed in yearMonthDuration", s);
                            }

                            minutes = value;
                            state = 6;
                            components++;
                            break;
                        }
                        else if (state == 0 || state == 1)
                        {
                            if (!allowYM)
                            {
                                return BadDuration("Month component is not allowed in dayTimeDuration", s);
                            }

                            months = value;
                            state = 2;
                            components++;
                            break;
                        }
                        else
                        {
                            return BadDuration("M is out of sequence", s);
                        }

                    case 'D':
                        if (state > 2)
                        {
                            return BadDuration("D is out of sequence", s);
                        }

                        if (!allowDT)
                        {
                            return BadDuration("Day component is not allowed in yearMonthDuration", s);
                        }

                        days = value;
                        state = 3;
                        components++;
                        break;
                    case 'H':
                        if (state != 4)
                        {
                            return BadDuration("H is out of sequence", s);
                        }

                        if (!allowDT)
                        {
                            return BadDuration("Hour component is not allowed in yearMonthDuration", s);
                        }

                        hours = value;
                        state = 5;
                        components++;
                        break;
                    case '.':
                        if (state < 4 || state > 6)
                        {
                            return BadDuration("misplaced decimal point", s);
                        }

                        seconds = value;
                        state = 7;
                        break;
                    case 'S':
                        if (state < 4 || state > 7)
                        {
                            return BadDuration("S is out of sequence", s);
                        }

                        if (!allowDT)
                        {
                            return BadDuration("Seconds component is not allowed in yearMonthDuration", s);
                        }

                        if (state == 7)
                        {
                            StringBuilder frac = new StringBuilder(part);
                            while (frac.Length < 9)
                            {
                                frac.Append('0');
                            }

                            part = frac.ToString();
                            if (part.Length > 9)
                            {
                                part = part.Substring(0, 9);
                            }

                            value = SimpleInteger(part);
                            if (value < 0)
                            {
                                return BadDuration("non-numeric fractional seconds", s);
                            }

                            nanoseconds = value;
                        }
                        else
                        {
                            seconds = value;
                        }

                        state = 8;
                        components++;
                        break;
                    default:
                        return BadDuration("misplaced " + delim, s);
                }
            }

            if (components == 0)
            {
                return BadDuration("Duration specifies no components", s);
            }

            if (negative)
            {
                years = -years;
                months = -months;
                days = -days;
                hours = -hours;
                minutes = -minutes;
                seconds = -seconds;
                nanoseconds = -nanoseconds;
            }

            try
            {
                return new DurationValue(years, months, days, hours, minutes, seconds, nanoseconds, BuiltInAtomicType.DURATION);
            }
            catch (ArgumentException err)
            {

                // catch values that exceed limits
                return new ValidationFailure(err.Message);
            }
        }

        protected static ValidationFailure BadDuration(string msg, UnicodeString s)
        {
            ValidationFailure err = new ValidationFailure("Invalid duration value '" + s + "' (" + msg + ')');
            err.SetErrorCode("FORG0001");
            return err;
        }

        protected static ValidationFailure BadDuration(string msg, UnicodeString s, string errorCode)
        {
            ValidationFailure err = new ValidationFailure("Invalid duration value '" + s + "' (" + msg + ')');
            err.SetErrorCode(errorCode);
            return err;
        }

        public static int SimpleInteger(string s)
        {
            long result = 0;
            int len = s.Length;
            if (len == 0)
            {
                return -1;
            }

            for (int i = 0; i < len; i++)
            {
                char c = s[i];
                if (c >= '0' && c <= '9')
                {
                    result = result * 10 + (c - '0');
                    if (result > int.MaxValue)
                    {
                        return -2;
                    }
                }
                else
                {
                    return -1;
                }
            }

            return (int)result;
        }

        public override AtomicValue CopyAsSubType(IAtomicType typeLabel)
        {
            if (_negative)
            {
                return new DurationValue(0, -_months, 0, 0, 0, -_seconds, -_nanoseconds, typeLabel);
            }
            else
            {
                return new DurationValue(0, _months, 0, 0, 0, _seconds, _nanoseconds, typeLabel);
            }
        }

        public virtual int Signum()
        {
            if (_negative)
            {
                return -1;
            }

            if (_months == 0 && _seconds == 0 && _nanoseconds == 0)
            {
                return 0;
            }

            return +1;
        }

        public override AtomicValue GetComponent(AccessorFn.Component component)
        {
            switch (component)
            {
                case AccessorFn.Component.YEAR:
                    return Int64Value.MakeIntegerValue(_negative ? -Years : Years);
                case AccessorFn.Component.MONTH:
                    return Int64Value.MakeIntegerValue(_negative ? -Months : Months);
                case AccessorFn.Component.DAY:
                    return Int64Value.MakeIntegerValue(_negative ? -Days : Days);
                case AccessorFn.Component.HOURS:
                    return Int64Value.MakeIntegerValue(_negative ? -Hours : Hours);
                case AccessorFn.Component.MINUTES:
                    return Int64Value.MakeIntegerValue(_negative ? -Minutes : Minutes);
                case AccessorFn.Component.SECONDS:
                    StringBuilder sb = new StringBuilder(16);
                    string ms = "000000000" + _nanoseconds;
                    ms = ms.Substring(ms.Length - 9);
                    sb.Append(_negative ? "-" : "").Append(Seconds).Append('.').Append(ms);
                    return BigDecimalValue.Parse(sb.ToString());
                case AccessorFn.Component.WHOLE_SECONDS:
                    return Int64Value.MakeIntegerValue(_negative ? -_seconds : _seconds);
                case AccessorFn.Component.MICROSECONDS:
                    return new Int64Value((_negative ? -_nanoseconds : _nanoseconds) / 1000);
                case AccessorFn.Component.NANOSECONDS:
                    return new Int64Value(_negative ? -_nanoseconds : _nanoseconds);
                default:
                    throw new ArgumentException("Unknown component for duration: " + component);
            }
        }

        public override IAtomicMatchKey GetXPathMatchKey(IStringCollator collator, int implicitTimezone)
        {
            return this;
        }

        public override IXPathComparable GetXPathComparable(IStringCollator collator, int implicitTimezone)
        {
            return null;
        }

        public override bool Equals(object other)
        {
            if (other is DurationValue)
            {
                DurationValue d1 = this;
                DurationValue d2 = (DurationValue)other;
                return d1._negative == d2._negative && d1._months == d2._months && d1._seconds == d2._seconds && d1._nanoseconds == d2._nanoseconds;
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            return (int)(double)(LengthInSeconds).GetHashCode();
        }

        public virtual DurationValue Add(DurationValue other)
        {
            throw new XPathException("Only subtypes of xs:duration can be added", "XPTY0004").AsTypeError();
        }

        public virtual DurationValue Subtract(DurationValue other)
        {
            throw new XPathException("Only subtypes of xs:duration can be subtracted").WithErrorCode("XPTY0004").AsTypeError();
        }

        public virtual DurationValue Negate()
        {
            if (_negative)
            {
                return new DurationValue(0, _months, 0, 0, 0, _seconds, _nanoseconds, typeLabel);
            }
            else
            {
                return new DurationValue(0, -_months, 0, 0, 0, -_seconds, -_nanoseconds, typeLabel);
            }
        }

        public virtual DurationValue Multiply(long factor)
        {
            return Multiply((double)factor);
        }

        public virtual DurationValue Multiply(double factor)
        {
            throw new XPathException("Only subtypes of xs:duration can be multiplied by a number", "XPTY0004").AsTypeError();
        }

        public virtual DurationValue Multiply(BigDecimal factor)
        {
            throw new XPathException("Only subtypes of xs:duration can be multiplied by a number", "XPTY0004").AsTypeError();
        }

        public virtual DurationValue Divide(double factor)
        {
            throw new XPathException("Only subtypes of xs:duration can be divided by a number", "XPTY0004").AsTypeError();
        }

        public virtual BigDecimalValue Divide(DurationValue other)
        {
            throw new XPathException("Only subtypes of xs:duration can be divided by another duration", "XPTY0004").AsTypeError();
        }

        public class DurationComparable : IComparable<DurationComparable>
        {
            private readonly int months;
            private readonly long seconds;
            private readonly int nanoseconds;
            public DurationComparable(int m, long s, int nanos)
            {
                months = m;
                seconds = s;
                nanoseconds = nanos;
            }

            public virtual int CompareTo(DurationComparable other)
            {
                if (months == other.months)
                {
                    if (seconds == other.seconds)
                    {
                        return nanoseconds.CompareTo(other.nanoseconds);
                    }
                    else
                    {
                        return seconds.CompareTo(other.seconds);
                    }
                }
                else
                {

                    // The months figure varies, but the seconds figure might tip things over if it's high
                    // enough. We make the assumption, however, that the nanoseconds won't affect things.
                    double oneDay = 24 * 60 * 60;
                    double min0 = MonthsToDaysMinimum(months) * oneDay + seconds;
                    double max0 = MonthsToDaysMaximum(months) * oneDay + seconds;
                    double min1 = MonthsToDaysMinimum(other.months) * oneDay + other.seconds;
                    double max1 = MonthsToDaysMaximum(other.months) * oneDay + other.seconds;
                    if (max0 < min1)
                    {
                        return -1;
                    }
                    else if (min0 > max1)
                    {
                        return +1;
                    }
                    else
                    {

                        return SequenceTool.INDETERMINATE_ORDERING;
                    }
                }
            }

            public override bool Equals(object o)
            {
                return o is DurationComparable && CompareTo((DurationComparable)o) == 0;
            }

            public override int GetHashCode()
            {
                return months ^ (int)seconds;
            }

            private int MonthsToDaysMinimum(int months)
            {
                if (months < 0)
                {
                    return -MonthsToDaysMaximum(-months);
                }

                if (months < 12)
                {
                    int[] shortest = new[]
                    {
                        0,
                        28,
                        59,
                        89,
                        120,
                        150,
                        181,
                        212,
                        242,
                        273,
                        303,
                        334
                    };
                    return shortest[months];
                }
                else
                {
                    int years = months / 12;
                    int remainingMonths = months % 12;

                    // the -1 is to allow for the fact that we might miss a leap day if we time the start badly
                    int yearDays = years * 365 + (years % 4) - (years % 100) + (years % 400) - 1;
                    return yearDays + MonthsToDaysMinimum(remainingMonths);
                }
            }

            private int MonthsToDaysMaximum(int months)
            {
                if (months < 0)
                {
                    return -MonthsToDaysMinimum(-months);
                }

                if (months < 12)
                {
                    int[] longest = new[]
                    {
                        0,
                        31,
                        62,
                        92,
                        123,
                        153,
                        184,
                        215,
                        245,
                        276,
                        306,
                        337
                    };
                    return longest[months];
                }
                else
                {
                    int years = months / 12;
                    int remainingMonths = months % 12;

                    // the +1 is to allow for the fact that we might miss a leap day if we time the start badly
                    int yearDays = years * 365 + (years % 4) - (years % 100) + (years % 400) + 1;
                    return yearDays + MonthsToDaysMaximum(remainingMonths);
                }
            }
        }
    }
}