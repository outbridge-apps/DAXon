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
using System;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Values
{
    public abstract class GDateValue : CalendarValue
    {

        protected static byte[] daysPerMonth = new byte[]
        {
            31,
            28,
            31,
            30,
            31,
            30,
            31,
            31,
            30,
            31,
            30,
            31
        };
        protected static readonly short[] monthData = new short[]
        {
            306,
            337,
            0,
            31,
            61,
            92,
            122,
            153,
            184,
            214,
            245,
            275
        };
        protected readonly int year; // unlike the lexical representation, includes a year zero
        protected readonly byte month;
        protected readonly byte day;
        public readonly bool hasNoYearZero;
        public virtual int Year => year;

        public virtual byte Month => month;

        public virtual byte Day => day;

        public virtual GDateComparable SchemaComparable => new GDateComparable(this);
        public GDateValue(int year, byte month, byte day, bool hasNoYearZero, int tzMinutes, IAtomicType typeLabel) : base(typeLabel, tzMinutes)
        {
            this.year = year;
            this.month = month;
            this.day = day;
            this.hasNoYearZero = hasNoYearZero;
        }

        protected GDateValue(MutableGDateValue m) : base(m.typeLabel, m.tzMinutes)
        {
            this.year = m.year;
            this.month = m.month;
            this.day = m.day;
            this.hasNoYearZero = m.hasNoYearZero;
        }

        protected virtual MutableGDateValue MakeMutableCopy()
        {
            MutableGDateValue m = new MutableGDateValue();
            m.year = year;
            m.month = month;
            m.day = day;
            m.hasNoYearZero = hasNoYearZero;
            m.tzMinutes = TimezoneInMinutes;
            m.typeLabel = typeLabel;
            return m;
        }

        protected static void SetLexicalValue(MutableGDateValue m, UnicodeString s, bool allowYearZero)
        {
            m.hasNoYearZero = !allowYearZero;
            DateTimeLex tok = new DateTimeLex(Whitespace.Trim(s));
            string wrong = tok.ReadDate(allowYearZero, out m.year, out m.month, out m.day, out bool yearRange)
                ?? tok.ReadTail(false, false, out _, out m.tzMinutes);
            if (wrong != null)
            {
                m.error = BadDate(wrong, s, yearRange ? "FODT0001" : "FORG0001");
            }
            else if (!IsValidDate(m.year, m.month, m.day))
            {
                m.error = BadDate("Non-existent date", s);
            }
        }

        private static ValidationFailure BadDate(string msg, UnicodeString value)
        {
            ValidationFailure err = new ValidationFailure("Invalid date " + Err.Wrap(value, Err.VALUE) + " (" + msg + ")");
            err.SetErrorCode("FORG0001");
            return err;
        }

        private static ValidationFailure BadDate(string msg, UnicodeString value, string errorCode)
        {
            ValidationFailure err = new ValidationFailure("Invalid date " + Err.Wrap(value, Err.VALUE) + " (" + msg + ")");
            err.SetErrorCode(errorCode);
            return err;
        }

        public static bool IsValidDate(int year, int month, int day)
        {
            return month > 0 && month <= 12 && day > 0 && day <= daysPerMonth[month - 1] || month == 2 && day == 29 && IsLeapYear(year);
        }

        public static bool IsLeapYear(int year)
        {
            return (year % 4 == 0) && !(year % 100 == 0 && !(year % 400 == 0));
        }

        public override bool Equals(object o)
        {
            if (o is GDateValue)
            {
                GDateValue gdv = (GDateValue)o;
                return PrimitiveType == gdv.PrimitiveType && ToDateTime().Equals(gdv.ToDateTime());
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            return DateTimeValue.ComputeHashCode(year, month, day, (byte)12, (byte)0, (byte)0, 0, TimezoneInMinutes);
        }

        public override int CompareTo(CalendarValue other, int implicitTimezone)
        {
            if (PrimitiveType != other.PrimitiveType)
            {
                throw new InvalidCastException("Cannot compare dates of different types"); // covers, for example, comparing a gYear to a gYearMonth
            }

            GDateValue v2 = (GDateValue)other;
            if (TimezoneInMinutes == other.TimezoneInMinutes)
            {

                // both values are in the same timezone (explicitly or implicitly)
                if (year != v2.year)
                {
                    return year.CompareTo(v2.year);   // not year - v2.year: it overflowed across +-2^31 and compared the wrong way
                }

                if (month != v2.month)
                {
                    return IntegerValue.Signum(month - v2.month);
                }

                if (day != v2.day)
                {
                    return IntegerValue.Signum(day - v2.day);
                }

                return 0;
            }

            return ToDateTime().CompareTo(other.ToDateTime(), implicitTimezone);
        }

        public override DateTimeValue ToDateTime()
        {
            return new DateTimeValue(year, month, day, (byte)0, (byte)0, (byte)0, 0, TimezoneInMinutes, hasNoYearZero);
        }

        public override IXPathComparable GetXPathComparable(IStringCollator collator, int implicitTimezone)
        {
            return null;
        }

        public override AtomicValue GetComponent(AccessorFn.Component component)
        {
            switch (component)
            {
                case AccessorFn.Component.YEAR_ALLOWING_ZERO:
                    return Int64Value.MakeIntegerValue(year);
                case AccessorFn.Component.YEAR:
                    return Int64Value.MakeIntegerValue(year > 0 || !hasNoYearZero ? year : year - 1);
                case AccessorFn.Component.MONTH:
                    return Int64Value.MakeIntegerValue(month);
                case AccessorFn.Component.DAY:
                    return Int64Value.MakeIntegerValue(day);
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
                    throw new ArgumentException("Unknown component for date: " + component);
            }
        }

        protected class MutableGDateValue
        {
            public int year; // the year as written, +1 for BC years
            public byte month; // the month as written, range 1-12
            public byte day; // the day as written, range 1-31
            public bool hasNoYearZero; // true if XSD 1.0 rules apply for negative years
            public int tzMinutes = NO_TIMEZONE;
            public IAtomicType typeLabel = BuiltInAtomicType.DATE_TIME;
            public ValidationFailure error;
            public MutableGDateValue()
            {
            }

            public MutableGDateValue(int year, int month, int day, bool hasNoYearZero, int tzMinutes, IAtomicType typeLabel)
            {
                this.year = year;
                this.month = (byte)month;
                this.day = (byte)day;
                this.hasNoYearZero = hasNoYearZero;
                this.tzMinutes = tzMinutes;
                this.typeLabel = typeLabel;
            }
        }

        public class GDateComparable : IComparable<GDateComparable>
        {
            private readonly GDateValue value;
            public GDateComparable(GDateValue value)
            {
                this.value = value;
            }

            public virtual GDateValue AsGDateValue()
            {
                return value;
            }

            public virtual int CompareTo(GDateComparable o)
            {
                if (AsGDateValue().PrimitiveType != o.AsGDateValue().PrimitiveType)
                {
                    return SequenceTool.INDETERMINATE_ORDERING;
                }

                DateTimeValue dt0 = value.ToDateTime();
                DateTimeValue dt1 = o.value.ToDateTime();
                return dt0.SchemaComparable.CompareTo(dt1.SchemaComparable);
            }

            public override bool Equals(object o)
            {
                return o is GDateComparable && CompareTo((GDateComparable)o) == 0;
            }

            public override int GetHashCode()
            {
                return value.ToDateTime().SchemaComparable.GetHashCode();
            }
        }
    }
}