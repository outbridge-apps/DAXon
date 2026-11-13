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
            DateTimeLex tok = new DateTimeLex(Whitespace.Trim(s));
            int nanosecond = 0;
            int tz = NO_TIMEZONE;
            string wrong = tok.ReadClock(out byte hour, out byte minute, out byte second) ?? tok.ReadTail(true, hour == 24, out nanosecond, out tz);
            if (wrong != null)
            {
                return BadTime(wrong, s);
            }

            if (hour == 24)
            {
                hour = 0;
            }

            return new TimeValue(hour, minute, second, nanosecond, tz, BuiltInAtomicType.TIME);
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
                    // All nine digits: to six, half up, 59.9999995 became 60 and the 7th-9th were lost (Saxon the same).
                    return new BigDecimalValue(new BigDecimal(new System.Numerics.BigInteger(second * 1000000000L + nanosecond), 9).StripTrailingZeros());
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
                // Only the time of day moves, so the duration counts modulo a day, as the spec wraps: in full it went
                // through a dateTime in 1972 and that range's check (P500000000D raised FODT0001).
                long seconds = duration.AbsoluteSeconds % (24 * 60 * 60);
                int nanos = duration.AbsoluteNanoseconds;
                bool back = duration.IsNegative;
                duration = new DayTimeDurationValue(0, 0, 0, back ? -seconds : seconds, back ? -nanos : nanos);
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