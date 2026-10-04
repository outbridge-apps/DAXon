////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Regex;
using OutSmart.DAXon.Regex.CharClass;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Collections;
using OutSmart.DAXon.Internal.Numerics;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Globalization;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Numbering;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Caching;
namespace OutSmart.DAXon.Functions
{
    internal sealed class FormatDate : SystemFunction, ICallable
    {
        // Widths beyond this are rejected with FOFD1340: the padded component must fit the
        // engine's int[]-backed strings (hard ceiling ~536M codepoints), and a width of
        // int.MaxValue overflowed the picture builder's min+1 pre-size into a negative array.
        internal const int MAX_WIDTH = 100_000_000;

        // Ordinal order for the binary search (Saxon 12.9 lists AME before AM, so AM was never found)
        static readonly string[] knownCalendars = new[]
        {
            "AD",
            "AH",
            "AM",
            "AME",
            "AP",
            "AS",
            "BE",
            "CB",
            "CE",
            "CL",
            "CS",
            "EE",
            "FE",
            "ISO",
            "JE",
            "KE",
            "KY",
            "ME",
            "MS",
            "NS",
            "OS",
            "RS",
            "SE",
            "SH",
            "SS",
            "TE",
            "VE",
            "VS"
        };
        private static readonly UnicodeString STR_0 = BMPString.Of("0");
        private static readonly UnicodeString STR_01 = BMPString.Of("01");
        private static readonly UnicodeString STR_1 = BMPString.Of("1");
        private static readonly UnicodeString STR_f = BMPString.Of("f");
        private static readonly UnicodeString STR_F = BMPString.Of("F");
        private static readonly UnicodeString STR_i = BMPString.Of("i");
        private static readonly UnicodeString STR_I = BMPString.Of("I");
        private static readonly UnicodeString STR_J = BMPString.Of("J");
        private static readonly UnicodeString STR_M = BMPString.Of("M");
        private static readonly UnicodeString STR_N = BMPString.Of("N");
        private static readonly UnicodeString STR_Nn = BMPString.Of("Nn");
        private static readonly UnicodeString STR_n = BMPString.Of("n");
        private static readonly UnicodeString STR_P = BMPString.Of("P");
        private static readonly UnicodeString STR_s = BMPString.Of("s");
        private static readonly UnicodeString STR_Y = BMPString.Of("Y");
        private static readonly UnicodeString STR_Z = BMPString.Of("Z");

        private static readonly ARegularExpression componentPattern = ARegularExpression.Compile("([YMDdWwFHhmsfZzPCE])\\s*(.*)", "");

        private static readonly ClockCache<string, ComponentSpecifier> ComponentSpecifierCache
            = new ClockCache<string, ComponentSpecifier>(128);

        // year
        // month
        // minutes
        // seconds
        // era
        private static readonly ARegularExpression widthPattern = ARegularExpression.Compile(",(\\*|[0-9]+)(\\-(\\*|[0-9]+))?", "");
        // year
        // month
        // minutes
        // seconds
        // era
        private static readonly ARegularExpression digitsOrOptionalDigitsPattern = ARegularExpression.Compile("[#\\p{Nd}]+", "");
        private static readonly ARegularExpression fractionalDigitsPattern = ARegularExpression.Compile("\\p{Nd}+#*", "");
        // Unicode general category of a code point (BMP or supplementary).
        private static UnicodeCategory CategoryOf(int codePoint) =>
            codePoint <= 0xFFFF
                ? CharUnicodeInfo.GetUnicodeCategory((char)codePoint)
                : CharUnicodeInfo.GetUnicodeCategory(char.ConvertFromUtf32(codePoint), 0);
        private string AdjustCalendar(string calendarVal, string result, IXPathContext context)
        {
            StructuredQName cal;
            try
            {
                cal = StructuredQName.FromLexicalQName((calendarVal), false, true, GetRetainedStaticContext());
            }
            catch (XPathException e)
            {
                throw new XPathException("Invalid calendar name. " + e.Message).WithErrorCode("FOFD1340").WithXPathContext(context);
            }

            if (cal.HasURI(NamespaceUri.NULL))
            {
                string calLocal = cal.GetLocalPart();
                if (calLocal.Equals("AD") || calLocal.Equals("ISO"))
                {
                }
                else if (Array.BinarySearch(knownCalendars, calLocal, StringComparer.Ordinal) >= 0)
                {
                    result = "[Calendar: AD]" + result;
                }
                else
                {
                    throw new XPathException("Unknown no-namespace calendar: " + calLocal).WithErrorCode("FOFD1340").WithXPathContext(context);
                }
            }
            else
            {
                result = "[Calendar: AD]" + result;
            }

            return result;
        }

        private static string FormatDateFn(CalendarValue value, string format, string language, string place, IXPathContext context)
        {
            Configuration config = context.GetConfiguration();
            bool languageDefaulted = language == null;
            if (language == null)
            {
                language = config.GetDefaultLanguage();
            }

            if (place == null)
            {
                place = config.DefaultCountry;
            }


            // if the value has a timezone and the place is a timezone name, the value is adjusted to that timezone
            if (value.HasTimezone() && place.Contains("/"))
            {
                OutSmart.DAXon.Internal.Collections.TimeZone tz = OutSmart.DAXon.Internal.Collections.TimeZone.GetTimeZone(place);
                if (tz != null)
                {
                    BigDecimal seconds = value.ToDateTime().SecondsSinceEpoch();
                    int milliOffset = tz.GetOffset(seconds.LongValue() * 1000);
                    value = value.AdjustTimezone(milliOffset / 60000);
                }
            }

            INumberer numberer = config.MakeNumberer(language, place);
            StringBuilder sb = new StringBuilder(64);
            if (!languageDefaulted && numberer.GetType() == typeof(Numberer_en) && !language.StartsWith("en", StringComparison.Ordinal))
            {

                // See bug #4582. We're not outputting the prefix in cases where ICU is used for numbering.
                // But the test on numberer.defaultedLocale() below may catch it...
                sb.Append("[Language: en]");
            }

            if (numberer.DefaultedLocale() != null)
            {
                sb.Append("[Language: " + numberer.DefaultedLocale().TwoLetterISOLanguageName + "]");
            }

            int i = 0;
            while (true)
            {
                while (i < format.Length && format[i] != '[')
                {
                    sb.Append(format[i]);
                    if (format[i] == ']')
                    {
                        i++;
                        if (i == format.Length || format[i] != ']')
                        {
                            throw new XPathException("Closing ']' in date picture must be written as ']]'").WithErrorCode("FOFD1340").WithXPathContext(context);
                        }
                    }

                    i++;
                }

                if (i == format.Length)
                {
                    break;
                }


                // look for '[['
                i++;
                if (i < format.Length && format[i] == '[')
                {
                    sb.Append('[');
                    i++;
                }
                else
                {
                    int close = i < format.Length ? format.IndexOf(']', i) : -1;
                    if (close == -1)
                    {
                        throw new XPathException("Date format contains a '[' with no matching ']'").WithErrorCode("FOFD1340").WithXPathContext(context);
                    }

                    string componentFormat = format.Substring(i, close - i);
                    sb.Append(FormatComponent(value, Whitespace.RemoveAllWhitespace(componentFormat), numberer, place, context));
                    i = close + 1;
                }
            }

            return sb.ToString();
        }

        private static ComponentSpecifier SplitComponentSpecifier(string specifier)
        {
            UnicodeString uSpecifier = StringView.Of(specifier).Tidy();
            ARegexIterator matcher = (ARegexIterator)componentPattern.Analyze(uSpecifier);
            IItem firstMatch = matcher.Next();
            if (firstMatch == null || firstMatch.UnicodeStringValue.Length32() != uSpecifier.Length32() || !matcher.IsMatching())
            {
                return null;
            }
            return new ComponentSpecifier(matcher.GetRegexGroup(1), matcher.GetRegexGroup(2));
        }

        private static UnicodeString FormatComponent(CalendarValue value, string specifier, INumberer numberer, string country, IXPathContext context)
        {
            bool ignoreDate = value is TimeValue;
            bool ignoreTime = value is DateValue;
            DateTimeValue dtvalue = value.ToDateTime();
            ComponentSpecifier split = ComponentSpecifierCache.GetOrAdd(specifier, SplitComponentSpecifier);
            if (split == null)
            {
                throw new XPathException("Unrecognized date/time component [" + specifier + ']').WithErrorCode("FOFD1340").WithXPathContext(context);
            }

            UnicodeString component = split.Component;
            UnicodeString format = ApplyDefaultPicture(component, split.FormatGroup, out bool defaultFormat);

            switch (component.CodePointAt(0))
            {
                case 'Y':
                    if (ignoreDate)
                    {
                        throw new XPathException("In format-time(): an xs:time value does not contain a year component").WithErrorCode("FOFD1350").WithXPathContext(context);
                    }
                    else
                    {
                        int year = dtvalue.Year;
                        if (year < 0)
                        {
                            year = -year;
                        }

                        return FormatNumber(component, year, format, defaultFormat, numberer, context);
                    }

                case 'M':
                    if (ignoreDate)
                    {
                        throw new XPathException("In format-time(): an xs:time value does not contain a month component").WithErrorCode("FOFD1350").WithXPathContext(context);
                    }
                    else
                    {
                        int month = dtvalue.Month;
                        return FormatNumber(component, month, format, defaultFormat, numberer, context);
                    }

                case 'D':
                    if (ignoreDate)
                    {
                        throw new XPathException("In format-time(): an xs:time value does not contain a day component").WithErrorCode("FOFD1350").WithXPathContext(context);
                    }
                    else
                    {
                        int day = dtvalue.Day;
                        return FormatNumber(component, day, format, defaultFormat, numberer, context);
                    }

                case 'd':
                    if (ignoreDate)
                    {
                        throw new XPathException("In format-time(): an xs:time value does not contain a day component").WithErrorCode("FOFD1350").WithXPathContext(context);
                    }
                    else
                    {
                        int day = DateValue.GetDayWithinYear(dtvalue.Year, dtvalue.Month, dtvalue.Day);
                        return FormatNumber(component, day, format, defaultFormat, numberer, context);
                    }

                case 'W':
                    if (ignoreDate)
                    {
                        throw new XPathException("In format-time(): cannot obtain the week number from an xs:time value").WithErrorCode("FOFD1350").WithXPathContext(context);
                    }
                    else
                    {
                        int week = DateValue.GetWeekNumber(dtvalue.Year, dtvalue.Month, dtvalue.Day);
                        return FormatNumber(component, week, format, defaultFormat, numberer, context);
                    }

                case 'w':
                    if (ignoreDate)
                    {
                        throw new XPathException("In format-time(): cannot obtain the week number from an xs:time value").WithErrorCode("FOFD1350").WithXPathContext(context);
                    }
                    else
                    {
                        int week = DateValue.GetWeekNumberWithinMonth(dtvalue.Year, dtvalue.Month, dtvalue.Day);
                        return FormatNumber(component, week, format, defaultFormat, numberer, context);
                    }

                case 'H':
                    if (ignoreTime)
                    {
                        throw new XPathException("In format-date(): an xs:date value does not contain an hour component").WithErrorCode("FOFD1350").WithXPathContext(context);
                    }
                    else
                    {
                        Int64Value hour = (Int64Value)value.GetComponent(AccessorFn.Component.HOURS);
                        return FormatNumber(component, (int)hour.LongValue(), format, defaultFormat, numberer, context);
                    }

                case 'h':
                    if (ignoreTime)
                    {
                        throw new XPathException("In format-date(): an xs:date value does not contain an hour component").WithErrorCode("FOFD1350").WithXPathContext(context);
                    }
                    else
                    {
                        Int64Value hour = (Int64Value)value.GetComponent(AccessorFn.Component.HOURS);
                        int hr = (int)hour.LongValue();
                        if (hr > 12)
                        {
                            hr = hr - 12;
                        }

                        if (hr == 0)
                        {
                            hr = 12;
                        }

                        return FormatNumber(component, hr, format, defaultFormat, numberer, context);
                    }

                case 'm':
                    if (ignoreTime)
                    {
                        throw new XPathException("In format-date(): an xs:date value does not contain a minutes component").WithErrorCode("FOFD1350").WithXPathContext(context);
                    }
                    else
                    {
                        Int64Value minutes = (Int64Value)value.GetComponent(AccessorFn.Component.MINUTES);
                        return FormatNumber(component, (int)minutes.LongValue(), format, defaultFormat, numberer, context);
                    }

                case 's':
                    if (ignoreTime)
                    {
                        throw new XPathException("In format-date(): an xs:date value does not contain a seconds component").WithErrorCode("FOFD1350").WithXPathContext(context);
                    }
                    else
                    {
                        IntegerValue seconds = (IntegerValue)value.GetComponent(AccessorFn.Component.WHOLE_SECONDS);
                        return FormatNumber(component, (int)seconds.LongValue(), format, defaultFormat, numberer, context);
                    }

                case 'f':

                    // ignore the format
                    if (ignoreTime)
                    {
                        throw new XPathException("In format-date(): an xs:date value does not contain a fractional seconds component").WithErrorCode("FOFD1350").WithXPathContext(context);
                    }
                    else
                    {
                        Int64Value micros = (Int64Value)value.GetComponent(AccessorFn.Component.MICROSECONDS);
                        return FormatNumber(component, (int)micros.LongValue(), format, defaultFormat, numberer, context);
                    }

                case 'z':
                case 'Z':
                    DateTimeValue dtv = value is TimeValue
                        ? PadTimeToDateTime((TimeValue)value, country, context)
                        : value.ToDateTime();
                    return FormatTimeZone(dtv, (char)component.CodePointAt(0), format, country);
                case 'F':
                    if (ignoreDate)
                    {
                        throw new XPathException("In format-time(): an xs:time value does not contain day-of-week component").WithErrorCode("FOFD1350").WithXPathContext(context);
                    }
                    else
                    {
                        int day = DateValue.GetDayOfWeek(dtvalue.Year, dtvalue.Month, dtvalue.Day);
                        return FormatNumber(component, day, format, defaultFormat, numberer, context);
                    }

                case 'P':
                    if (ignoreTime)
                    {
                        throw new XPathException("In format-date(): an xs:date value does not contain an am/pm component").WithErrorCode("FOFD1350").WithXPathContext(context);
                    }
                    else
                    {
                        int minuteOfDay = dtvalue.Hour * 60 + dtvalue.Minute;
                        return FormatNumber(component, minuteOfDay, format, defaultFormat, numberer, context);
                    }

                case 'C':
                    return StringView.Of(numberer.GetCalendarName("AD")).Tidy();
                case 'E':
                    if (ignoreDate)
                    {
                        throw new XPathException("In format-time(): an xs:time value does not contain an AD/BC component").WithErrorCode("FOFD1350").WithXPathContext(context);
                    }
                    else
                    {
                        int year = dtvalue.Year;
                        return StringView.Of(numberer.GetEraName(year)).Tidy();
                    }

                default:
                    throw new XPathException("Unknown format-date/time component specifier '" + format.Substring(0, 1) + '\'').WithErrorCode("FOFD1340").WithXPathContext(context);
            }
        }

        // Fill in the default picture for a component whose caller supplied none (empty, or starting
        // at the ',' width modifier), per the format-date component-specifier defaults.
        private static UnicodeString ApplyDefaultPicture(UnicodeString component, UnicodeString format, out bool defaultFormat)
        {
            defaultFormat = false;
            if (format.IsEmpty() || format.CodePointAt(0) == ',')
            {
                defaultFormat = true;
                switch (component.CodePointAt(0))
                {
                    case 'F':
                        format = STR_Nn.Concat(format);
                        break;
                    case 'P':
                        format = STR_n.Concat(format);
                        break;
                    case 'C':
                    case 'E':
                        format = STR_N.Concat(format);
                        break;
                    case 'm':
                    case 's':
                        format = STR_01.Concat(format);
                        break;
                    case 'z':
                    case 'Z':
                        break;
                    default:
                        format = STR_1.Concat(format);
                        break;
                }
            }

            return format;
        }

        // format-timezone on an xs:time has no date; pad it with 1 January (or 1 July if that is in
        // summer time) of the current year so DST rules resolve to the right offset (bug 3761).
        private static DateTimeValue PadTimeToDateTime(TimeValue value, string country, IXPathContext context)
        {
            int year = DateTimeValue.GetCurrentDateTime(context).Year;
            int tzoffset = value.TimezoneInMinutes;
            DateTimeValue baseDate = new DateTimeValue(year, (byte)1, (byte)1, (byte)0, (byte)0, (byte)0, 0, tzoffset, false);
            bool? b = NamedTimeZone.InSummerTime(baseDate, country);
            if (b.HasValue && b.Value)
            {
                baseDate = new DateTimeValue(year, (byte)7, (byte)1, (byte)0, (byte)0, (byte)0, 0, tzoffset, false);
            }

            return DateTimeValue.MakeDateTimeValue(baseDate.ToDateValue(), value);
        }
        // Byte-identical replacements for the former `\p{Nd}+` regex (digitsPattern): Categories.ESCAPE_d
        // IS GetCategory("Nd") -- the exact class the regex compiled to. Direct codepoint tests avoid a
        // fresh REMatcher per numeric component per date value on this hot path.
        private static bool IsAllDecimalDigits(UnicodeString s)  // == digitsPattern.Matches(s): \p{Nd}+ anchored
        {
            if (s.IsEmpty())
            {
                return false;
            }
            IIntIterator it = s.CodePoints();
            while (it.MoveNext())
            {
                if (!Categories.ESCAPE_d.Test(it.Current))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool ContainsDecimalDigit(UnicodeString s)  // == digitsPattern.ContainsMatch(s)
        {
            IIntIterator it = s.CodePoints();
            while (it.MoveNext())
            {
                if (Categories.ESCAPE_d.Test(it.Current))
                {
                    return true;
                }
            }
            return false;
        }

        private static UnicodeString FormatNumber(UnicodeString component, int value, UnicodeString format, bool defaultFormat, INumberer numberer, IXPathContext context)
        {
            int comma = (int)StringTool.LastIndexOf(format, ',');
            UnicodeString widths = EmptyUnicodeString.GetInstance();
            if (comma >= 0)
            {
                widths = format.Substring(comma);
                format = format.Prefix(comma);
            }

            UnicodeString primary = format;
            string letterValue = null;
            string ordinal = null;
            int lastCP = StringTool.LastCodePoint(primary);
            if (lastCP == 't')
            {
                primary = primary.Prefix(primary.Length() - 1);
                letterValue = "traditional";
            }
            else if (lastCP == 'o')
            {
                primary = primary.Prefix(primary.Length() - 1);
                ordinal = numberer.GetOrdinalSuffixForDateTime(component.ToString());
            }

            int min = 1;
            int max = int.MaxValue;
            if (IsAllDecimalDigits(primary))
            {
                int primaryLen = primary.Length32();
                if (primaryLen > 1)
                {

                    // "A format token containing leading zeroes, such as 001, sets the minimum and maximum width..."
                    // We interpret this literally: a format token of "1" does not set a maximum, because it would
                    // cause the year 2006 to be formatted as "6".
                    min = primaryLen;
                    max = primaryLen;
                }
            }

            if (STR_Y.Equals(component))
            {
                min = max = 0;
                if (!widths.IsEmpty())
                {
                    max = GetWidths(widths)[1];
                }
                else if (ContainsDecimalDigit(primary))
                {
                    IIntIterator primaryIter = primary.CodePoints();
                    while (primaryIter.MoveNext())
                    {
                        int c = primaryIter.Current;
                        if (c == '#')
                        {
                            max++;
                        }
                        else if ((c >= '0' && c <= '9') || Categories.ESCAPE_d.Test(c))
                        {
                            min++;
                            max++;
                        }
                    }
                }

                if (max <= 1)
                {
                    max = int.MaxValue;
                }

                if (max < 4 || (max < int.MaxValue && value > 9999))
                {
                    value = value % (int)Math.Pow(10, max);
                }
            }

            if (primary.Equals(STR_I) || primary.Equals(STR_i))
            {
                int[] range = GetWidths(widths);
                min = range[0];

                //max = int.MaxValue;
                string roman = numberer.Format(value, primary, null, letterValue, "", ordinal);
                UnicodeBuilder s = new UnicodeBuilder(32);
                s.Append(roman);
                int len = StringTool.GetStringLength(roman);
                while (len < min)
                {
                    s.Append(' ');
                    len++;
                }

                return s.ToUnicodeString();
            }
            else if (!widths.IsEmpty())
            {
                int[] range = GetWidths(widths);
                min = Math.Max(min, range[0]);
                if (max == int.MaxValue)
                {
                    max = range[1];
                }
                else
                {
                    max = Math.Max(max, range[1]);
                }

                if (defaultFormat)
                {

                    // if format was defaulted, the explicit widths override the implicit format
                    if (StringTool.LastCodePoint(primary) == '1' && min != primary.Length())
                    {
                        // Pre-size is only a hint: the builder archives its active part every 64K
                        // codepoints anyway, so anything bigger is wasted allocation (and min + 1
                        // used to overflow at min = int.MaxValue before GetWidths capped the width).
                        UnicodeBuilder sb = new UnicodeBuilder(Math.Min(min + 1, 65536));
                        for (int i = 1; i < min; i++)
                        {
                            sb.Append('0');
                        }

                        sb.Append('1');
                        primary = sb.ToUnicodeString();
                    }
                }
            }

            if (STR_P.Equals(component))
            {

                // A.M./P.M. can only be formatted as a name
                if (!(STR_N.Equals(primary) || STR_n.Equals(primary) || STR_Nn.Equals(primary)))
                {
                    primary = STR_n;
                }

                if (max == int.MaxValue)
                {

                    // if no max specified, use 4. An explicit greater value allows use of "noon" and "midnight"
                    max = 4;
                }
            }
            else if (STR_Y.Equals(component))
            {
                if (max < int.MaxValue)
                {
                    value = value % (int)Math.Pow(10, max);
                }
            }
            else if (STR_f.Equals(component))
            {
                return FormatFractionalSeconds(component, value, format, primary, min, max, defaultFormat, numberer, context);
            }

            if (STR_N.Equals(primary) || STR_n.Equals(primary) || STR_Nn.Equals(primary))
            {
                return FormatByName(component, primary, value, min, max, numberer);
            }


            // deal with grouping separators, decimal digit family, etc. for numeric values
            return FormatNumericPicture(primary, value, min, numberer, letterValue, ordinal);
        }

        // Fractional-seconds ('f') component: value is an integer count of microseconds. Handles the
        // no-digit, grouping-separator (reverse-integer, 3.1 spec) and normal cases, plus non-standard
        // decimal digit families.
        private static UnicodeString FormatFractionalSeconds(UnicodeString component, int value, UnicodeString format, UnicodeString primary, int min, int max, bool defaultFormat, INumberer numberer, IXPathContext context)
        {
            // If there is no Unicode digit in the pattern, output is implementation defined, so do what comes easily
            if (!ContainsDecimalDigit(primary))
            {
                return FormatNumber(component, value, STR_1, defaultFormat, numberer, context);
            }


            // if there are grouping separators, handle as a reverse integer as described in the 3.1 spec
            if (!digitsOrOptionalDigitsPattern.Matches(primary))
            {
                UnicodeString reverseFormat = Reverse(format);
                UnicodeString reverseValue = Reverse(BMPString.Of("" + value));
                UnicodeString reverseResult = FormatNumber(STR_s, int.Parse(reverseValue.ToString()), reverseFormat, false, numberer, context);
                UnicodeString correctedResult = Reverse(reverseResult);
                if (correctedResult.Length() > max)
                {
                    correctedResult = correctedResult.Prefix(max);
                }

                return correctedResult;
            }

            if (!fractionalDigitsPattern.Matches(primary))
            {
                throw new XPathException("Invalid picture for fractional seconds: " + primary, "FOFD1340");
            }

            UnicodeString str;
            if (value == 0)
            {
                str = STR_0;
            }
            else
            {
                str = BMPString.Of(((1000000 + value) + "").Substring(1));
                if (str.Length() > max)
                {

                    // Spec bug 29749 says we should truncate rather than rounding
                    str = str.Prefix(max);
                }
            }

            if (str.Length() < min)
            {

                // One concat: Concat copies the whole accumulated string, so padding a digit at a
                // time is quadratic in a width the picture alone decides.
                UnicodeBuilder pad = new UnicodeBuilder((int)(min - str.Length()));
                for (long i = str.Length(); i < min; i++)
                {
                    pad.Append('0');
                }

                str = str.Concat(pad.ToUnicodeString());
            }

            if (str.Length() > min)
                while (str.Length() > min && str.CodePointAt(str.Length() - 1) == '0')
                {
                    str = str.Prefix(str.Length() - 1);
                }


            // for non standard decimal digit family
            int zeroDigit = Alphanumeric.GetDigitFamily(format.CodePointAt(0));
            if (zeroDigit >= 0 && zeroDigit != '0')
            {
                int[] digits = new int[10];
                for (int z = 0; z <= 9; z++)
                {
                    digits[z] = zeroDigit + z;
                }

                long n = long.Parse(str.ToString());
                int requiredLength = str.Length32();
                str = StringView.Tidy(AbstractNumberer.ConvertDigitSystem(n, digits, requiredLength));
            }

            return str;
        }

        // Named ('N'/'n'/'Nn') formatting of month / day-of-week / am-pm components, in the requested case.
        private static UnicodeString FormatByName(UnicodeString component, UnicodeString primary, int value, int min, int max, INumberer numberer)
        {
            string s = "";
            if (STR_M.Equals(component))
            {
                s = numberer.MonthName(value, min, max);
            }
            else if (STR_F.Equals(component))
            {
                s = numberer.DayName(value, min, max);
            }
            else if (STR_P.Equals(component))
            {
                s = numberer.HalfDayName(value, min, max);
            }
            else
            {
                primary = STR_1;
            }

            if (STR_N.Equals(primary))
            {
                return StringView.Tidy(s.ToUpperInvariant());
            }
            else if (STR_n.Equals(primary))
            {
                return StringView.Tidy(s.ToLowerInvariant());
            }
            else
            {
                return StringView.Tidy(s);
            }
        }

        // Numeric picture path: apply grouping separators / decimal digit family, then left-pad with the
        // picture's zero digit up to the minimum width.
        private static UnicodeString FormatNumericPicture(UnicodeString primary, int value, int min, INumberer numberer, string letterValue, string ordinal)
        {
            NumericGroupFormatter picGroupFormat;
            try
            {
                picGroupFormat = FormatInteger.GetPicSeparators(primary, false);
            }
            catch (XPathException e)
            {
                throw e.ReplacingErrorCode("FODF1310", "FOFD1340");
            }

            UnicodeString adjustedPicture = picGroupFormat.AdjustedPicture;
            string formattedStr = numberer.Format(value, adjustedPicture, picGroupFormat, letterValue, "", ordinal);
            int formattedLen = StringTool.GetStringLength(formattedStr);
            int digitZero;
            if (formattedLen < min)
            {
                digitZero = Alphanumeric.GetDigitFamily(adjustedPicture.CodePointAt(0));
                StringBuilder fsb = new StringBuilder(formattedStr);

                // In one insert. The width comes from the picture and is bounded only by int, so
                // prepending one digit at a time was quadratic: '[Y1,10000000]' outran a 3s
                // deadline by minutes, and this whole call is a single step no deadline check
                // reaches into.
                StringTool.PrependRepeated(fsb, digitZero, min - formattedLen);
                formattedStr = fsb.ToString();
            }

            return StringView.Tidy(formattedStr);
        }

        // year
        // month
        // minutes
        // seconds
        // era
        private static UnicodeString Reverse(UnicodeString @in)
        {
            UnicodeBuilder builder = new UnicodeBuilder(@in.Length32());
            for (long i = @in.Length() - 1; i >= 0; i--)
            {
                builder.Append(@in.CodePointAt(i));
            }

            return builder.ToUnicodeString();
        }

        private static int[] GetWidths(UnicodeString widths)
        {
            try
            {
                int min = -1;
                int max = -1;
                if (!widths.IsEmpty())
                {
                    IRegexIterator widthIter = widthPattern.Analyze(widths);
                    StringValue firstMatch = widthIter.Next();
                    if (firstMatch != null && firstMatch.Length() == widths.Length() && widthIter.IsMatching())
                    {
                        UnicodeString smin = widthIter.GetRegexGroup(1);
                        if (smin == null || smin.IsEmpty() || StringConstants.ASTERISK.Equals(smin))
                        {
                            min = 1;
                        }
                        else
                        {
                            min = int.Parse(smin.ToString());
                        }

                        UnicodeString smax = widthIter.GetRegexGroup(3);
                        if (smax == null || smax.IsEmpty() || StringConstants.ASTERISK.Equals(smax))
                        {
                            max = int.MaxValue;
                        }
                        else
                        {
                            max = int.Parse(smax.ToString());
                        }

                        if (min < 1)
                        {
                            throw new XPathException("Invalid min value in format picture " + Err.Wrap(widths, Err.VALUE), "FOFD1340");
                        }

                        if (min > MAX_WIDTH)
                        {
                            // The padded output must fit the engine's int[]-backed strings; without the
                            // cap a width of 2^31-1 overflowed the picture builder's min+1 pre-size.
                            throw new XPathException("Width in format picture exceeds the implementation limit of " + MAX_WIDTH + " " + Err.Wrap(widths, Err.VALUE), "FOFD1340");
                        }

                        if (max < 1 || max < min)
                        {
                            throw new XPathException("Invalid max value in format picture " + Err.Wrap(widths, Err.VALUE), "FOFD1340");
                        }
                    }
                    else
                    {
                        throw new XPathException("Unrecognized width specifier in format picture " + Err.Wrap(widths, Err.VALUE), "FOFD1340");
                    }
                }


                //            if (min > max) {
                //                XPathException e = new XPathException("Minimum width in date/time picture exceeds maximum width");
                //                throw e;
                //            }
                int[] result = new int[2];
                result[0] = min;
                result[1] = max;
                return result;
            }
            catch (Exception err) when (err is FormatException || err is OverflowException)
            {
                // Java's Integer.parseInt raises one exception for malformed AND out-of-range
                // input; .NET splits them, and only the malformed half was being caught.
                throw new XPathException("Invalid integer used as width in date/time picture", "FOFD1340");
            }
        }

        // year
        // month
        // minutes
        // seconds
        // era
        private static UnicodeString FormatTimeZone(DateTimeValue value, char component, UnicodeString format, string country)
        {
            int comma = (int)StringTool.LastIndexOf(format, ',');
            UnicodeString widthModifier = EmptyUnicodeString.GetInstance();
            if (comma >= 0)
            {
                widthModifier = format.Substring(comma);
                format = format.Prefix(comma);
            }

            if (!value.HasTimezone())
            {
                if (format.Equals(STR_Z))
                {

                    // military "local time"
                    return STR_J;
                }
                else
                {
                    return EmptyUnicodeString.GetInstance();
                }
            }

            if (format.IsEmpty() && !widthModifier.IsEmpty())
            {
                int[] widths = GetWidths(widthModifier);
                int min = widths[0];
                int max = widths[1];
                if (min <= 1)
                {
                    format = BMPString.Of(max >= 4 ? "0:00" : "0");
                }
                else if (min <= 4)
                {
                    format = BMPString.Of(max >= 5 ? "00:00" : "00");
                }
                else
                {
                    format = BMPString.Of("00:00");
                }
            }

            if (format.IsEmpty())
            {
                format = BMPString.Of("00:00");
            }

            int tz = value.TimezoneInMinutes;
            bool useZforZero = StringTool.LastCodePoint(format) == 't';
            if (useZforZero && tz == 0)
            {
                return STR_Z;
            }

            if (useZforZero)
            {
                format = format.Prefix(format.Length() - 1);
            }

            int digits = 0;
            int separators = 0;
            int separatorChar = ':';
            int zeroDigit = -1;
            int[] expandedFormat = StringTool.Expand(format);
            foreach (int ch in expandedFormat)
            {
                if (CategoryOf(ch) == UnicodeCategory.DecimalDigitNumber)
                {
                    digits++;
                    if (zeroDigit < 0)
                    {
                        zeroDigit = Alphanumeric.GetDigitFamily(ch);
                    }
                }
                else
                {
                    separators++;
                    separatorChar = ch;
                }
            }

            int[] buffer = new int[10];
            int used = 0;
            if (digits > 0)
            {

                // Numeric timezone formatting
                if (component == 'z')
                {
                    buffer[0] = 'G';
                    buffer[1] = 'M';
                    buffer[2] = 'T';
                    used = 3;
                }

                bool negative = tz < 0;
                tz = Math.Abs(tz);
                buffer[used++] = negative ? '-' : '+';
                int hour = tz / 60;
                int minute = tz % 60;
                bool includeMinutes = minute != 0 || digits >= 3 || separators > 0;
                bool includeSep = (minute != 0 && digits <= 2) || (separators > 0 && (minute != 0 || digits >= 3));
                int hourDigits = digits <= 2 ? digits : digits - 2;
                if (hour > 9 || hourDigits >= 2)
                {
                    buffer[used++] = zeroDigit + hour / 10;
                }

                buffer[used++] = (hour % 10) + zeroDigit;
                if (includeSep)
                {
                    buffer[used++] = separatorChar;
                }

                if (includeMinutes)
                {
                    buffer[used++] = minute / 10 + zeroDigit;
                    buffer[used++] = minute % 10 + zeroDigit;
                }

                return StringTool.FromCodePoints(buffer, used);
            }
            else if (format.Equals(BMPString.Of("Z")))
            {

                // military timezone formatting
                int hour = tz / 60;
                int minute = tz % 60;
                if (hour < -12 || hour > 12 || minute != 0)
                {
                    return FormatTimeZone(value, 'Z', BMPString.Of("00:00"), country);
                }
                else
                {
                    return BMPString.Of("" + "YXWVUTSRQPONZABCDEFGHIKLM"[hour + 12]);
                }
            }
            else if (format.CodePointAt(0) == 'N' || format.CodePointAt(0) == 'n')
            {
                return StringView.Of(GetNamedTimeZone(value, country, format)).Tidy();
            }
            else
            {
                return FormatTimeZone(value, 'Z', BMPString.Of("00:00"), country);
            }
        }

        // year
        // month
        // minutes
        // seconds
        // era
        private static string GetNamedTimeZone(DateTimeValue value, string country, UnicodeString format)
        {
            int min = 1;
            int comma = (int)format.IndexOf(',');
            if (comma > 0)
            {
                UnicodeString widths = format.Substring(comma);
                int[] range = GetWidths(widths);
                min = range[0];
            }

            if (format.CodePointAt(0) == 'N' || format.CodePointAt(0) == 'n')
            {
                if (min <= 5)
                {
                    string tzname = NamedTimeZone.GetTimeZoneNameForDate(value, country);
                    if (tzname == null)
                    {
                        return FormatTimeZone(value, 'Z', BMPString.Of("Z00:00t"), country).ToString();
                    }

                    if (format.CodePointAt(0) == 'n')
                    {
                        tzname = tzname.ToLowerInvariant();
                    }

                    return tzname;
                }
                else
                {
                    return NamedTimeZone.GetOlsonTimeZoneName(value, country);
                }
            }

            UnicodeBuilder sbz = new UnicodeBuilder(16);
            value.AppendTimezone(sbz);
            return sbz.ToString();
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            CalendarValue value = (CalendarValue)arguments[0].Head();
            if (value == null)
            {
                return EmptySequence.GetInstance();
            }

            string format = arguments[1].Head().GetStringValue();
            StringValue calendarVal = null;
            StringValue countryVal = null;
            StringValue languageVal = null;
            if (GetArity() > 2)
            {
                languageVal = (StringValue)arguments[2].Head();
                calendarVal = (StringValue)arguments[3].Head();
                countryVal = (StringValue)arguments[4].Head();
            }

            string calendar = calendarVal == null ? null : calendarVal.GetStringValue();
            string language = languageVal == null ? null : languageVal.GetStringValue();
            string place = countryVal == null ? null : countryVal.GetStringValue();
            if (place != null)
            {
                value = AdjustTimezoneToPlace(value, place);
            }

            string result = FormatDateFn(value, format, language, place, context);
            if (calendarVal != null)
            {
                result = AdjustCalendar(calendar, result, context);
            }

            return new StringValue(result);
        }

        private CalendarValue AdjustTimezoneToPlace(CalendarValue value, string place)
        {
            if (place.Contains("/") && value.HasTimezone() && !(value is TimeValue))
            {
                TimeZoneInfo zone = NamedTimeZone.GetNamedTimeZone(place);
                if (zone != null)
                {
                    // preserves the legacy offset-at-now behaviour (old ToJavaInstant() returned Instant.now())
                    int offsetSeconds = (int)zone.GetUtcOffset(DateTimeOffset.UtcNow).TotalSeconds;
                    return value.AdjustTimezone(offsetSeconds / 60);
                }
            }

            return value;
        }
        // The (component-letter, format-modifier) split of a specifier is a pure function of the
        // specifier string; the same picture recurs across many date values, so memoize it. Cold
        // specifiers still run componentPattern; a specifier that is not a valid component caches as
        // null and the caller raises FOFD1340 with its own context. ~short strings, 128 entries.
        private sealed class ComponentSpecifier
        {
            public readonly UnicodeString Component;
            public readonly UnicodeString FormatGroup;
            public ComponentSpecifier(UnicodeString component, UnicodeString formatGroup)
            {
                Component = component;
                FormatGroup = formatGroup;
            }
        }
    }
}
