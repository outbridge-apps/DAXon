////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal.Numerics;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Values
{
    /// <summary>
    /// A value of type xs:time
    /// </summary>
    public sealed class TimeValue : CalendarValue, IXPathComparable
    {
        private readonly byte hour;
        private readonly byte minute;
        private readonly byte second;
        private readonly int nanosecond;

        public override BuiltInAtomicType PrimitiveType => BuiltInAtomicType.TIME;

        public byte Hour => hour;

        public byte Minute => minute;

        public byte Second => second;

        public int Microsecond => nanosecond / 1000;

        public int Nanosecond => nanosecond;

        public override UnicodeString PrimitiveStringValue
        {
            get
            {
                UnicodeBuilder sb = new UnicodeBuilder(16);
                AppendTwoDigits(sb, hour);
                sb.Append(':');
                AppendTwoDigits(sb, minute);
                sb.Append(':');
                AppendTwoDigits(sb, second);
                if (nanosecond != 0)
                {
                    sb.Append('.');
                    int ms = nanosecond;
                    int div = 100000000;
                    while (ms > 0)
                    {
                        int d = ms / div;
                        sb.Append((char)(d + '0'));
                        ms = ms % div;
                        div /= 10;
                    }
                }

                if (HasTimezone())
                {
                    AppendTimezone(sb);
                }

                return sb.ToUnicodeString();
            }
        }

        public override UnicodeString CanonicalLexicalRepresentation
        {
            get
            {
                if (HasTimezone() && TimezoneInMinutes != 0)
                {
                    return AdjustTimezone(0).UnicodeStringValue;
                }
                else
                {
                    return this.UnicodeStringValue;
                }
            }
        }

        public TimeComparable SchemaComparable => new TimeComparable(this);
        public TimeValue(byte hour, byte minute, byte second, int microsecond, int tzMinutes) : base(BuiltInAtomicType.TIME, tzMinutes)
        {
            this.hour = hour;
            this.minute = minute;
            this.second = second;
            this.nanosecond = microsecond * 1000;
        }

        public TimeValue(byte hour, byte minute, byte second, int nanosecond, int tzMinutes, IAtomicType typeLabel) : base(typeLabel, tzMinutes)
        {
            this.hour = hour;
            this.minute = minute;
            this.second = second;
            this.nanosecond = nanosecond;
        }

        public TimeValue MakeTimeValue(byte hour, byte minute, byte second, int nanosecond, int tz)
        {
            return new TimeValue(hour, minute, second, nanosecond, tz, BuiltInAtomicType.TIME);
        }

        public static IConversionResult MakeTimeValue(UnicodeString s)
        {

            // input must have format hh:mm:ss[.fff*][([+|-]hh:mm | Z)]
            StringTokenizer tok = new StringTokenizer(Whitespace.Trim(s).ToString(), "-:.+Z", true);
            if (!tok.HasMoreTokens())
            {
                return BadTime("too short", s);
            }

            string part = tok.NextToken();
            if (part.Length != 2)
            {
                return BadTime("hour must be two digits", s);
            }

            int value = DurationValue.SimpleInteger(part);
            if (value < 0)
            {
                return BadTime("Non-numeric hour component", s);
            }

            byte hour = (byte)value;
            if (hour > 24)
            {
                return BadTime("hour is out of range", s);
            }

            if (!tok.HasMoreTokens())
            {
                return BadTime("too short", s);
            }

            if (!":".Equals(tok.NextToken()))
            {
                return BadTime("wrong delimiter after hour", s);
            }

            if (!tok.HasMoreTokens())
            {
                return BadTime("too short", s);
            }

            part = tok.NextToken();
            if (part.Length != 2)
            {
                return BadTime("minute must be two digits", s);
            }

            value = DurationValue.SimpleInteger(part);
            if (value < 0)
            {
                return BadTime("Non-numeric minute component", s);
            }

            byte minute = (byte)value;
            if (minute > 59)
            {
                return BadTime("minute is out of range", s);
            }

            if (hour == 24 && minute != 0)
            {
                return BadTime("If hour is 24, minute must be 00", s);
            }

            if (!tok.HasMoreTokens())
            {
                return BadTime("too short", s);
            }

            if (!":".Equals(tok.NextToken()))
            {
                return BadTime("wrong delimiter after minute", s);
            }

            if (!tok.HasMoreTokens())
            {
                return BadTime("too short", s);
            }

            part = tok.NextToken();
            if (part.Length != 2)
            {
                return BadTime("second must be two digits", s);
            }

            value = DurationValue.SimpleInteger(part);
            if (value < 0)
            {
                return BadTime("Non-numeric second component", s);
            }

            byte second = (byte)value;
            if (second > 59)
            {
                return BadTime("second is out of range", s);
            }

            if (hour == 24 && second != 0)
            {
                return BadTime("If hour is 24, second must be 00", s);
            }

            int tz;
            int nanosecond;
            ValidationFailure tzError = ParseTimezoneTail(tok, hour, s, out tz, out nanosecond);
            if (tzError != null)
            {
                return tzError;
            }

            if (hour == 24)
            {
                hour = 0;
            }

            return new TimeValue(hour, minute, second, nanosecond, tz, BuiltInAtomicType.TIME);
        }

        // Parse the optional fractional-seconds and timezone tail. hour is needed for the 24:00 checks;
        // tz (defaulting to NO_TIMEZONE) and nanosecond receive the results. Returns BadTime on error, null on success.
        private static ValidationFailure ParseTimezoneTail(StringTokenizer tok, byte hour, UnicodeString s, out int tz, out int nanosecond)
        {
            tz = NO_TIMEZONE;
            bool negativeTz = false;
            int state = 0;
            nanosecond = 0;
            string part;
            int value;
            while (tok.HasMoreTokens())
            {
                if (state == 9)
                {
                    return BadTime("characters after the end", s);
                }

                string delim = tok.NextToken();
                if (".".Equals(delim))
                {
                    if (state != 0)
                    {
                        return BadTime("decimal separator occurs twice", s);
                    }

                    if (!tok.HasMoreTokens())
                    {
                        return BadTime("decimal point must be followed by digits", s);
                    }

                    part = tok.NextToken();
                    if (part.Length > 9 && part.MatchesRegex("^[0-9]+$"))
                    {
                        part = part.Substring(0, 9);
                    }

                    value = DurationValue.SimpleInteger(part);
                    if (value < 0)
                    {
                        return BadTime("Non-numeric fractional seconds component", s);
                    }

                    double fractionalSeconds = double.Parse('.' + part, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
                    nanosecond = (int)JavaMath.Round(fractionalSeconds * 1000000000);
                    if (hour == 24 && nanosecond != 0)
                    {
                        return BadTime("If hour is 24, fractional seconds must be 0", s);
                    }

                    state = 1;
                }
                else if ("Z".Equals(delim))
                {
                    if (state > 1)
                    {
                        return BadTime("Z cannot occur here", s);
                    }

                    tz = 0;
                    state = 9; // we've finished
                }
                else if ("+".Equals(delim) || "-".Equals(delim))
                {
                    if (state > 1)
                    {
                        return BadTime(delim + " cannot occur here", s);
                    }

                    state = 2;
                    if (!tok.HasMoreTokens())
                    {
                        return BadTime("missing timezone", s);
                    }

                    part = tok.NextToken();
                    if (part.Length != 2)
                    {
                        return BadTime("timezone hour must be two digits", s);
                    }

                    value = DurationValue.SimpleInteger(part);
                    if (value < 0)
                    {
                        return BadTime("Non-numeric timezone hour component", s);
                    }

                    tz = value * 60;
                    if (tz > 14 * 60)
                    {
                        return BadTime("timezone hour is out of range", s);
                    }

                    if ("-".Equals(delim))
                    {
                        negativeTz = true;
                    }
                }
                else if (":".Equals(delim))
                {
                    if (state != 2)
                    {
                        return BadTime("colon cannot occur here", s);
                    }

                    state = 9;
                    if (!tok.HasMoreTokens())
                    {
                        // Upstream lets StringTokenizer throw here and the crash escapes the cast;
                        // a dangling ':' is a lexical error.
                        return BadTime("no minutes in timezone", s);
                    }

                    part = tok.NextToken();
                    value = DurationValue.SimpleInteger(part);
                    if (value < 0)
                    {
                        return BadTime("Non-numeric timezone minute component", s);
                    }

                    int tzminute = value;
                    if (part.Length != 2)
                    {
                        return BadTime("timezone minute must be two digits", s);
                    }

                    if (tzminute > 59)
                    {
                        return BadTime("timezone minute is out of range", s);
                    }

                    tz += tzminute;
                    if (negativeTz)
                    {
                        tz = -tz;
                    }
                }
                else
                {
                    return BadTime("timezone format is incorrect", s);
                }
            }

            if (state == 2 || state == 3)
            {
                return BadTime("timezone incomplete", s);
            }

            return null;
        }

        private static ValidationFailure BadTime(string msg, UnicodeString value)
        {
            ValidationFailure err = new ValidationFailure("Invalid time " + Err.Wrap(value, Err.VALUE) + " (" + msg + ")");
            err.SetErrorCode("FORG0001");
            return err;
        }

        public override DateTimeValue ToDateTime()
        {
            return new DateTimeValue(1972, (byte)12, (byte)31, hour, minute, second, nanosecond, TimezoneInMinutes);
        }

        public override AtomicValue CopyAsSubType(IAtomicType typeLabel)
        {
            return new TimeValue(hour, minute, second, nanosecond, TimezoneInMinutes, typeLabel);
        }

        public override CalendarValue AdjustTimezone(int timezone)
        {
            DateTimeValue dt = (DateTimeValue)ToDateTime().AdjustTimezone(timezone);
            return new TimeValue(dt.Hour, dt.Minute, dt.Second, dt.Nanosecond, dt.TimezoneInMinutes, BuiltInAtomicType.TIME);
        }

        public override AtomicValue GetComponent(AccessorFn.Component component)
        {
            switch (component)
            {
                case AccessorFn.Component.HOURS:
                    return Int64Value.MakeIntegerValue(hour);
                case AccessorFn.Component.MINUTES:
                    return Int64Value.MakeIntegerValue(minute);
                case AccessorFn.Component.SECONDS:
                    BigDecimal d = BigDecimal.ValueOf(nanosecond);
                    d = d.Divide(BigDecimalValue.BIG_DECIMAL_ONE_BILLION, 6, RoundingMode.HALF_UP);
                    d = d + BigDecimal.ValueOf(second);
                    return new BigDecimalValue(d);
                case AccessorFn.Component.WHOLE_SECONDS:
                    return Int64Value.MakeIntegerValue(second);
                case AccessorFn.Component.MICROSECONDS:
                    return new Int64Value(nanosecond / 1000);
                case AccessorFn.Component.NANOSECONDS:
                    return new Int64Value(nanosecond);
                case AccessorFn.Component.TIMEZONE:
                    if (HasTimezone())
                    {
                        return DayTimeDurationValue.FromMilliseconds(60000 * TimezoneInMinutes);
                    }
                    else
                    {
                        return null;
                    }

                default:
                    throw new ArgumentException("Unknown component for time: " + component);
            }
        }

        public override IXPathComparable GetXPathComparable(IStringCollator collator, int implicitTimezone)
        {
            if (HasTimezone())
            {
                return this;
            }
            else if (implicitTimezone == MISSING_TIMEZONE)
            {
                throw new NoDynamicContextException("Unknown implicit timezone");
            }
            else
            {
                return (IXPathComparable)AdjustTimezone(implicitTimezone);
            }
        }

        public int CompareTo(IXPathComparable other)
        {
            if (other is TimeValue)
            {
                TimeValue otherTime = (TimeValue)other;
                if (TimezoneInMinutes == otherTime.TimezoneInMinutes)
                {
                    if (hour != otherTime.hour)
                    {
                        return IntegerValue.Signum(hour - otherTime.hour);
                    }
                    else if (minute != otherTime.minute)
                    {
                        return IntegerValue.Signum(minute - otherTime.minute);
                    }
                    else if (second != otherTime.second)
                    {
                        return IntegerValue.Signum(second - otherTime.second);
                    }
                    else if (nanosecond != otherTime.nanosecond)
                    {
                        return IntegerValue.Signum(nanosecond - otherTime.nanosecond);
                    }
                    else
                    {
                        return 0;
                    }
                }
                else
                {
                    return ToDateTime().CompareTo(otherTime.ToDateTime());
                }
            }
            else
            {
                throw new InvalidCastException("Cannot compare xs:time to " + other);
            }
        }

        public override int CompareTo(CalendarValue other, int implicitTimezone)
        {
            if (!(other is TimeValue))
            {
                throw new InvalidCastException("Time values are not comparable to " + other.GetType());
            }

            TimeValue otherTime = (TimeValue)other;
            if (TimezoneInMinutes == otherTime.TimezoneInMinutes)
            {

                // The values have the same time zone, or neither has a timezone
                return CompareTo(otherTime);
            }
            else
            {
                return ToDateTime().CompareTo(otherTime.ToDateTime(), implicitTimezone);
            }
        }

        public override bool Equals(object other)
        {
            return other is TimeValue && CompareTo((TimeValue)other) == 0;
        }

        public override int GetHashCode()
        {
            return DateTimeValue.ComputeHashCode(1951, (byte)10, (byte)11, hour, minute, second, nanosecond, TimezoneInMinutes);
        }

        public override CalendarValue Add(DurationValue duration)
        {
            if (duration is DayTimeDurationValue)
            {
                DateTimeValue dt = (DateTimeValue)ToDateTime().Add(duration);
                return new TimeValue(dt.Hour, dt.Minute, dt.Second, dt.Nanosecond, TimezoneInMinutes, BuiltInAtomicType.TIME);
            }
            else
            {
                throw new XPathException("Time+Duration arithmetic is supported only for xs:dayTimeDuration", "XPTY0004").AsTypeError();
            }
        }

        public override DayTimeDurationValue Subtract(CalendarValue other, IXPathContext context)
        {
            if (!(other is TimeValue))
            {
                XPathException err = new XPathException("First operand of '-' is a time, but the second is not");
                err.SetIsTypeError(true);
                throw err;
            }

            return base.Subtract(other, context);
        }

        public class TimeComparable : IComparable<TimeComparable>
        {
            private readonly TimeValue value;
            public TimeComparable(TimeValue value)
            {
                this.value = value;
            }

            public virtual TimeValue AsTimeValue()
            {
                return value;
            }

            public virtual int CompareTo(TimeComparable o)
            {
                DateTimeValue dt0 = AsTimeValue().ToDateTime();
                DateTimeValue dt1 = o.AsTimeValue().ToDateTime();
                return dt0.SchemaComparable.CompareTo(dt1.SchemaComparable);
            }

            public override bool Equals(object o)
            {
                return o is TimeComparable && CompareTo((TimeComparable)o) == 0;
            }

            public override int GetHashCode()
            {
                return value.ToDateTime().SchemaComparable.GetHashCode();
            }
        }
    }
}