////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Numbering;using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Text;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Lib
{
    public interface INumberer
    {
        string Country { get; set; }
        System.Globalization.CultureInfo DefaultedLocale();
        string Format(long number, UnicodeString picture, int groupSize, string groupSeparator, string letterValue, string cardinal, string ordinal);
        string Format(long number, UnicodeString picture, NumericGroupFormatter numGrpFormatter, string letterValue, string cardinal, string ordinal);
        string MonthName(int month, int minWidth, int maxWidth);
        string DayName(int day, int minWidth, int maxWidth);
        string HalfDayName(int minutes, int minWidth, int maxWidth);
        string GetOrdinalSuffixForDateTime(string component);
        string GetEraName(int year);
        string GetCalendarName(string code);
    }
}