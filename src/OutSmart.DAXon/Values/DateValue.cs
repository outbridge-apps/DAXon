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
using System;
using OutSmart.DAXon.Types;
namespace OutSmart.DAXon.Values
{
    /// <summary>
    /// A value of type Date. Note that a Date may include a TimeZone.
    /// </summary>
    public class DateValue : GDateValue, IXPathComparable
    {

        public override BuiltInAtomicType PrimitiveType => BuiltInAtomicType.DATE;

        public override UnicodeString PrimitiveStringValue
        {
            get
            {
                UnicodeBuilder sb = new UnicodeBuilder(16);
                int yr = year;
                if (year <= 0)
                {
                    yr = -yr + (hasNoYearZero ? 1 : 0); // no year zero in lexical space for XSD 1.0
                    if (yr != 0)
                    {
                        sb.Append('-');
                    }
                }

                AppendString(sb, yr, yr > 9999 ? (yr + "").Length : 4);
                sb.Append('-');
                AppendTwoDigits(sb, month);
                sb.Append('-');
                AppendTwoDigits(sb, day);
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
                DateValue target = this;
                if (HasTimezone())
                {
                    if (TimezoneInMinutes > 12 * 60)
                    {
                        target = (DateValue)AdjustTimezone(TimezoneInMinutes - 24 * 60);
                    }
                    else if (TimezoneInMinutes <= -12 * 60)
                    {
                        target = (DateValue)AdjustTimezone(TimezoneInMinutes + 24 * 60);
                    }
                }

                return target.UnicodeStringValue;
            }
        }

        public virtual int JulianDayNumber => GetJulianDayNumber(year, month, day);
        private DateValue(MutableGDateValue m) : base(m)
        {
        }

        public DateValue(int year, byte month, byte day) : this(new MutableGDateValue(year, month, day, true, NO_TIMEZONE, BuiltInAtomicType.DATE))
        {
        }

        public DateValue(int year, byte month, byte day, bool xsd10) : this(new MutableGDateValue(year, month, day, xsd10, NO_TIMEZONE, BuiltInAtomicType.DATE))
        {
        }

        public DateValue(int year, byte month, byte day, int tz, bool xsd10) : this(new MutableGDateValue(year, month, day, xsd10, tz, BuiltInAtomicType.DATE))
        {
        }

        public DateValue(int year, byte month, byte day, int tz, IAtomicType type) : this(new MutableGDateValue(year, month, day, false, tz, type))
        {
        }

        public DateValue(UnicodeString s) : this(s, ConversionRules.DEFAULT)
        {
        }

        public DateValue(UnicodeString s, ConversionRules rules) : this(FromUnicodeString(s, rules))
        {
        }

        private static MutableGDateValue FromUnicodeString(UnicodeString s, ConversionRules rules)
        {
            MutableGDateValue m = new MutableGDateValue();
            SetLexicalValue(m, s, rules.IsAllowYearZero());
            if (m.error == null)
            {
                return m;
            }
            else
            {
                throw m.error.MakeException();
            }
        }

        public static IConversionResult MakeDateValue(UnicodeString @in, ConversionRules rules)
        {
            MutableGDateValue g = new MutableGDateValue();
            g.typeLabel = BuiltInAtomicType.DATE;
            SetLexicalValue(g, @in, rules.IsAllowYearZero());
            return g.error == null ? new DateValue(g) : g.error;
        }

        public static DateValue Tomorrow(int year, byte month, byte day)
        {
            if (DateValue.IsValidDate(year, month, day + 1))
            {
                return new DateValue(year, month, (byte)(day + 1), true);
            }
            else if (month < 12)
            {
                return new DateValue(year, (byte)(month + 1), (byte)1, true);
            }
            else
            {
                return new DateValue(year + 1, (byte)1, (byte)1, true);
            }
        }

        public static DateValue Yesterday(int year, byte month, byte day)
        {
            if (day > 1)
            {
                return new DateValue(year, month, (byte)(day - 1), true);
            }
            else if (month > 1)
            {
                if (month == 3 && IsLeapYear(year))
                {
                    return new DateValue(year, (byte)2, (byte)29, true);
                }
                else
                {
                    return new DateValue(year, (byte)(month - 1), daysPerMonth[month - 2], true);
                }
            }
            else
            {
                return new DateValue(year - 1, (byte)12, (byte)31, true);
            }
        }

        public override AtomicValue CopyAsSubType(IAtomicType typeLabel)
        {
            MutableGDateValue m = MakeMutableCopy();
            m.typeLabel = typeLabel;
            return new DateValue(m);
        }

        public override CalendarValue AdjustTimezone(int timezone)
        {
            DateTimeValue dt = (DateTimeValue)ToDateTime().AdjustTimezone(timezone);
            return new DateValue(dt.Year, dt.Month, dt.Day, dt.TimezoneInMinutes, hasNoYearZero);
        }

        public override CalendarValue Add(DurationValue duration)
        {
            if (duration is DayTimeDurationValue)
            {
                long microseconds = ((DayTimeDurationValue)duration).LengthInMicroseconds;
                bool negative = microseconds < 0;
                microseconds = Math.Abs(microseconds);
                int days = (int)Math.Floor((double)microseconds / (1000000.0 * 60 * 60 * 24));
                bool partDay = (microseconds % (1000000.0 * 60 * 60 * 24)) > 0;
                int julian = JulianDayNumber;
                MutableGDateValue d = MutableDateFromJulianDayNumber(julian + (negative ? -days : days));
                if (partDay)
                {
                    if (negative)
                    {
                        d = Yesterday(d.year, d.month, d.day).MakeMutableCopy();
                    }
                }

                d.tzMinutes = TimezoneInMinutes;
                d.hasNoYearZero = this.hasNoYearZero;
                return new DateValue(d);
            }
            else if (duration is YearMonthDurationValue)
            {
                int months = ((YearMonthDurationValue)duration).LengthInMonths;
                int m = (month - 1) + months;
                int y = year + m / 12;
                m = m % 12;
                if (m < 0)
                {
                    m += 12;
                    y -= 1;
                }

                m++;
                int d = day;
                while (!IsValidDate(y, m, d))
                {
                    d -= 1;
                }

                return new DateValue(y, (byte)m, (byte)d, TimezoneInMinutes, hasNoYearZero);
            }
            else
            {
                throw new XPathException("Date arithmetic is not available for xs:duration, only for its subtypes").AsTypeError().WithErrorCode("XPTY0004");
            }
        }

        public override DayTimeDurationValue Subtract(CalendarValue other, IXPathContext context)
        {
            if (!(other is DateValue))
            {
                throw new XPathException("First operand of '-' is a date, but the second is not").AsTypeError().WithErrorCode("XPTY0004");
            }

            return base.Subtract(other, context);
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

        public int CompareTo(IXPathComparable v2)
        {
            if (v2 is DateValue)
            {
                try
                {
                    return CompareTo((DateValue)v2, MISSING_TIMEZONE);
                }
                catch (Exception err)
                {
                    throw new InvalidCastException("Date comparison requires access to implicit timezone");
                }
            }
            else
            {
                throw new InvalidCastException("Cannot compare xs:date to " + v2.ToString());
            }
        }

        public static int GetJulianDayNumber(int year, int month, int day)
        {
            int z = year - (month < 3 ? 1 : 0);
            short f = monthData[month - 1];
            if (z >= 0)
            {
                return day + f + 365 * z + z / 4 - z / 100 + z / 400 + 1721118;
            }
            else
            {

                // for negative years, add 12000 years and then subtract the days!
                z += 12000;
                int j = day + f + 365 * z + z / 4 - z / 100 + z / 400 + 1721118;
                return j - (365 * 12000 + 12000 / 4 - 12000 / 100 + 12000 / 400); // number of leap years in 12000 years
            }
        }

        public static DateValue DateFromJulianDayNumber(int julianDayNumber)
        {
            return new DateValue(MutableDateFromJulianDayNumber(julianDayNumber));
        }

        private static MutableGDateValue MutableDateFromJulianDayNumber(int julianDayNumber)
        {
            if (julianDayNumber >= 0)
            {
                int L = julianDayNumber + 68569 + 1; // +1 adjustment for days starting at noon
                int n = (4 * L) / 146097;
                L = L - (146097 * n + 3) / 4;
                int i = (4000 * (L + 1)) / 1461001;
                L = L - (1461 * i) / 4 + 31;
                int j = (80 * L) / 2447;
                int d = L - (2447 * j) / 80;
                L = j / 11;
                int m = j + 2 - (12 * L);
                int y = 100 * (n - 49) + i + L;
                return new MutableGDateValue(y, m, d, true, NO_TIMEZONE, BuiltInAtomicType.DATE);
            }
            else
            {

                // add 12000 years and subtract them again...
                MutableGDateValue dt = MutableDateFromJulianDayNumber(julianDayNumber + 365 * 12000 + 12000 / 4 - 12000 / 100 + 12000 / 400);
                dt.year -= 12000;
                return dt;
            }
        }

        public static int GetDayWithinYear(int year, int month, int day)
        {
            int j = GetJulianDayNumber(year, month, day);
            int k = GetJulianDayNumber(year, 1, 1);
            return j - k + 1;
        }

        public static int GetDayOfWeek(int year, int month, int day)
        {
            int d = GetJulianDayNumber(year, month, day);
            d -= 2378500; // 1800-01-05 - any Monday would do
            while (d <= 0)
            {
                d += 70000000; // any sufficiently high multiple of 7 would do
            }

            return (d - 1) % 7 + 1;
        }

        public static int GetWeekNumber(int year, int month, int day)
        {
            { int doy = GetDayWithinYear(year, month, day); int dow = GetDayOfWeek(year, month, day); int week = (doy - dow + 10) / 7; if (week < 1) { return GetWeekNumber(year - 1, 12, 31); } if (week == 53 && GetDayOfWeek(year, 12, 31) < 4) { return 1; } return week; }
        }

        public static int GetWeekNumberWithinMonth(int year, int month, int day)
        {
            int firstDay = GetDayOfWeek(year, month, 1);
            if (firstDay > 4 && (firstDay + day) <= 8)
            {

                // days before week one are part of the last week of the previous month (4 or 5)
                DateValue lastDayPrevMonth = Yesterday(year, (byte)month, (byte)1);
                return GetWeekNumberWithinMonth(lastDayPrevMonth.year, lastDayPrevMonth.month, lastDayPrevMonth.day);
            }

            int inc = firstDay < 5 ? 1 : 0; // implements the First Thursday rule
            return ((day + firstDay - 2) / 7) + inc;
        }

    }
}