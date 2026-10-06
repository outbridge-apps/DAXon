////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System.Collections.Concurrent;
using System.Globalization;

namespace OutSmart.DAXon.Expressions.Numbering
{
    /// <summary>
    /// INumberer that takes month / day-of-week names from an OS-known CultureInfo, for languages
    /// beyond Saxon-HE's built-in English. Number words, ordinal suffixes, half-day and era names
    /// stay English (inherited) — full localization of those ships only in Saxon-PE/EE.
    /// </summary>
    internal sealed class Numberer_bcl : Numberer_en
    {
        // Made once for a culture whose own calendar is not Gregorian; null when it has no Gregorian one.
        private static readonly ConcurrentDictionary<string, DateTimeFormatInfo> GregorianByCulture = new ConcurrentDictionary<string, DateTimeFormatInfo>();

        private readonly DateTimeFormatInfo names;

        public Numberer_bcl(DateTimeFormatInfo names, string language)
        {
            this.names = names;
            SetLanguage(language);
        }

        // The names of the culture's Gregorian calendar, or null when it has none: by default ar counts the months
        // of Um al-Qura, fa and ps the Persian ones, and an xs:date is Gregorian.
        internal static DateTimeFormatInfo GregorianNames(CultureInfo culture)
        {
            DateTimeFormatInfo names = culture.DateTimeFormat;
            if (names.Calendar is GregorianCalendar)
            {
                return names;
            }

            return GregorianByCulture.GetOrAdd(culture.Name, _ => WithGregorianCalendar(culture));
        }

        private static DateTimeFormatInfo WithGregorianCalendar(CultureInfo culture)
        {
            GregorianCalendar gregorian = null;
            foreach (Calendar calendar in culture.OptionalCalendars)
            {
                if (calendar is GregorianCalendar g && (gregorian == null || g.CalendarType == GregorianCalendarTypes.Localized))
                {
                    gregorian = g;
                }
            }

            if (gregorian == null)
            {
                return null;
            }

            DateTimeFormatInfo names = (DateTimeFormatInfo)culture.DateTimeFormat.Clone();
            names.Calendar = gregorian;
            return DateTimeFormatInfo.ReadOnly(names);
        }

        public override string MonthName(int month, int minWidth, int maxWidth)
        {
            // Same width contract as Numberer_en: no month abbreviation table — truncate, then pad.
            // GetMonthName, not the MonthNames property: the property CLONES the array per access.
            return Widen(names.GetMonthName(month), null, minWidth, maxWidth < 3 ? 3 : maxWidth);
        }

        public override string DayName(int day, int minWidth, int maxWidth)
        {
            // day is ISO 1=Monday..7=Sunday; the BCL DayOfWeek is 0=Sunday..6=Saturday.
            var dow = (System.DayOfWeek)(day % 7);
            return Widen(names.GetDayName(dow), names.GetAbbreviatedDayName(dow), minWidth, maxWidth < 2 ? 2 : maxWidth);
        }

        private static string Widen(string name, string abbreviation, int minWidth, int maxWidth)
        {
            if (name.Length > maxWidth && abbreviation != null)
            {
                name = abbreviation;
            }

            if (name.Length > maxWidth)
            {
                name = name.Substring(0, maxWidth);
            }

            return name.Length < minWidth ? name.PadRight(minWidth) : name;
        }
    }
}
