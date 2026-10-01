////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Lib;
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
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Values
{
    /// <summary>
    /// Abstract superclass for Date, Time, and DateTime.
    /// </summary>
    public abstract class CalendarValue : AtomicValue, IAtomicMatchKey
    {
        public const int NO_TIMEZONE = int.MinValue;
        public const int MISSING_TIMEZONE = int.MaxValue;
        private readonly int tzMinutes; // timezone offset in minutes: or the special value NO_TIMEZONE
        public int TimezoneInMinutes => tzMinutes;

        public CalendarValue(IAtomicType typeLabel) : base(typeLabel)
        {
            this.tzMinutes = NO_TIMEZONE;
        }

        public CalendarValue(IAtomicType typeLabel, int tzMinutes) : base(typeLabel)
        {
            this.tzMinutes = tzMinutes;
        }
        public static IConversionResult MakeCalendarValue(UnicodeString s, ConversionRules rules)
        {
            IConversionResult cr = DateTimeValue.MakeDateTimeValue(s, rules);
            IConversionResult firstError = cr;
            if (cr is ValidationFailure)
            {
                cr = DateValue.MakeDateValue(s, rules);
            }

            if (cr is ValidationFailure)
            {
                cr = TimeValue.MakeTimeValue(s);
            }

            if (cr is ValidationFailure)
            {
                cr = GYearValue.MakeGYearValue(s, rules);
            }

            if (cr is ValidationFailure)
            {
                cr = GYearMonthValue.MakeGYearMonthValue(s, rules);
            }

            if (cr is ValidationFailure)
            {
                cr = GMonthValue.MakeGMonthValue(s);
            }

            if (cr is ValidationFailure)
            {
                cr = GMonthDayValue.MakeGMonthDayValue(s);
            }

            if (cr is ValidationFailure)
            {
                cr = GDayValue.MakeGDayValue(s);
            }

            if (cr is ValidationFailure)
            {
                return firstError;
            }

            return cr;
        }

        public bool HasTimezone()
        {
            return tzMinutes != NO_TIMEZONE;
        }

        public abstract DateTimeValue ToDateTime();

        // .NET-idiomatic instant conversions -- replacement for the dissolved java.util.Calendar
        // interop surface (GetCalendar()). No-timezone values are treated as UTC.
        public global::System.DateTime ToSystemDateTimeUtc()
        {
            DateTimeValue dt = ToDateTime();
            long millis = (long)(dt.SecondsSinceEpoch().DoubleValue() * 1000);
            return new global::System.DateTime(1970, 1, 1, 0, 0, 0, global::System.DateTimeKind.Utc).AddMilliseconds(millis);
        }

        public global::System.DateTimeOffset ToSystemDateTimeOffset()
        {
            int tzMin = HasTimezone() ? TimezoneInMinutes : 0;
            return new global::System.DateTimeOffset(ToSystemDateTimeUtc()).ToOffset(global::System.TimeSpan.FromMinutes(tzMin));
        }

        public abstract CalendarValue Add(DurationValue duration);
        public virtual DayTimeDurationValue Subtract(CalendarValue other, IXPathContext context)
        {
            DateTimeValue dt1 = ToDateTime();
            DateTimeValue dt2 = other.ToDateTime();
            if (dt1.TimezoneInMinutes != dt2.TimezoneInMinutes)
            {
                int tz = CalendarValue.NO_TIMEZONE;
                if (context == null || (tz = context.GetImplicitTimezone()) == CalendarValue.MISSING_TIMEZONE)
                {
                    throw new NoDynamicContextException("Implicit timezone required");
                }

                dt1 = dt1.AdjustToUTC(tz);
                dt2 = dt2.AdjustToUTC(tz);
            }

            BigDecimal d1 = dt1.ToJulianInstant();
            BigDecimal d2 = dt2.ToJulianInstant();
            BigDecimal difference = d1 - d2;
            return DayTimeDurationValue.FromSeconds(difference);
        }

        public CalendarValue RemoveTimezone()
        {
            return AdjustTimezone(NO_TIMEZONE);
        }

        public abstract CalendarValue AdjustTimezone(int tz);
        public CalendarValue AdjustTimezone(DayTimeDurationValue tz)
        {
            long microseconds = tz.LengthInMicroseconds;
            if (microseconds % 60000000 != 0)
            {
                throw new XPathException("Timezone is not an integral number of minutes", "FODT0003");
            }

            int tzminutes = (int)(microseconds / 60000000);
            if (Math.Abs(tzminutes) > 14 * 60)
            {
                throw new XPathException("Timezone out of range (-14:00 to +14:00)", "FODT0003");
            }

            return AdjustTimezone(tzminutes);
        }

        public override IAtomicMatchKey GetXPathMatchKey(IStringCollator collator, int implicitTimezone)
        {
            if (HasTimezone())
            {
                return this;
            }

            if (implicitTimezone == MISSING_TIMEZONE)
            {
                throw new NoDynamicContextException("Unknown implicit timezone");
            }

            return HasTimezone() ? this : AdjustTimezone(implicitTimezone);
        }

        public override IAtomicMatchKey AsMapKey()
        {
            return new CalendarValueMapKey(this);
        }

        public override IXPathComparable GetXPathComparable(IStringCollator collator, int implicitTimezone)
        {
            return null;
        }

        public abstract int CompareTo(CalendarValue other, int implicitTimezone);
        public override bool IsIdentical(AtomicValue v)
        {
            return base.IsIdentical(v) && tzMinutes == ((CalendarValue)v).tzMinutes;
        }

        public override int IdentityHashCode()
        {
            return GetHashCode() ^ tzMinutes;
        }

        public void AppendTimezone(UnicodeBuilder sb)
        {
            if (HasTimezone())
            {
                AppendTimezone(TimezoneInMinutes, sb);
            }
        }

        public static void AppendTimezone(int tz, UnicodeBuilder sb)
        {
            if (tz == 0)
            {
                sb.Append('Z');
            }
            else
            {
                sb.Append(tz > 0 ? "+" : "-");
                tz = Math.Abs(tz);
                AppendTwoDigits(sb, tz / 60);
                sb.Append(':');
                AppendTwoDigits(sb, tz % 60);
            }
        }

        protected static void AppendString(UnicodeBuilder sb, int value, int size)
        {
            // Digits written by division - the old '"000000000" + value' + Substring allocated three
            // strings per field. Semantics unchanged: the last `size` decimal digits, zero-padded.
            if (value < 0)
            {
                string s = "000000000" + value;
                sb.Append(s.Substring(s.Length - size));
                return;
            }

            int div = 1;
            for (int i = 1; i < size; i++)
            {
                div *= 10;
            }

            while (div > 0)
            {
                sb.Append((char)(value / div % 10 + '0'));
                div /= 10;
            }
        }

        protected static void AppendTwoDigits(UnicodeBuilder sb, int value)
        {
            sb.Append((char)(value / 10 + '0'));
            sb.Append((char)(value % 10 + '0'));
        }

        private sealed class CalendarValueMapKey : IAtomicMatchKey
        {
            private readonly CalendarValue value;
            public CalendarValueMapKey(CalendarValue value)
            {
                this.value = value;
            }

            public AtomicValue AsAtomic()
            {
                return value;
            }

            public override bool Equals(object obj)
            {
                if (obj is CalendarValueMapKey)
                {
                    CalendarValue a = value;
                    CalendarValue b = ((CalendarValueMapKey)obj).value;
                    if (a.HasTimezone() == b.HasTimezone())
                    {
                        if (a.HasTimezone())
                        {
                            return a.AdjustTimezone(b.tzMinutes).IsIdentical(b);
                        }
                        else
                        {
                            return a.IsIdentical(b);
                        }
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }

            public override int GetHashCode()
            {
                return AsAtomic().GetHashCode();
            }
        }
    }
}