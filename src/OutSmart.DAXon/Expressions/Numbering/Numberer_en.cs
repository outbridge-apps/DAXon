////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Text;
namespace OutSmart.DAXon.Expressions.Numbering
{
    /// <summary>
    /// INumberer class for the English language.
    /// </summary>
    internal class Numberer_en : AbstractNumberer
    {

        private static readonly string[] englishUnits = new[]
        {
            "Zero",
            "One",
            "Two",
            "Three",
            "Four",
            "Five",
            "Six",
            "Seven",
            "Eight",
            "Nine",
            "Ten",
            "Eleven",
            "Twelve",
            "Thirteen",
            "Fourteen",
            "Fifteen",
            "Sixteen",
            "Seventeen",
            "Eighteen",
            "Nineteen"
        };
        private static readonly string[] englishTens = new[]
        {
            "",
            "Ten",
            "Twenty",
            "Thirty",
            "Forty",
            "Fifty",
            "Sixty",
            "Seventy",
            "Eighty",
            "Ninety"
        };
        private static readonly string[] englishOrdinalUnits = new[]
        {
            "Zeroth",
            "First",
            "Second",
            "Third",
            "Fourth",
            "Fifth",
            "Sixth",
            "Seventh",
            "Eighth",
            "Ninth",
            "Tenth",
            "Eleventh",
            "Twelfth",
            "Thirteenth",
            "Fourteenth",
            "Fifteenth",
            "Sixteenth",
            "Seventeenth",
            "Eighteenth",
            "Nineteenth"
        };
        private static readonly string[] englishOrdinalTens = new[]
        {
            "",
            "Tenth",
            "Twentieth",
            "Thirtieth",
            "Fortieth",
            "Fiftieth",
            "Sixtieth",
            "Seventieth",
            "Eightieth",
            "Ninetieth"
        };

        private static readonly string[] englishMonths = new[]
        {
            "January",
            "February",
            "March",
            "April",
            "May",
            "June",
            "July",
            "August",
            "September",
            "October",
            "November",
            "December"
        };

        private static readonly string[] englishDays = new[]
        {
            "Monday",
            "Tuesday",
            "Wednesday",
            "Thursday",
            "Friday",
            "Saturday",
            "Sunday"
        };
        private static readonly string[] englishDayAbbreviations = new[]
        {
            "Mon",
            "Tues",
            "Weds",
            "Thurs",
            "Fri",
            "Sat",
            "Sun"
        };
        private static readonly int[] minUniqueDayLength = new[]
        {
            1,
            2,
            1,
            2,
            1,
            2,
            2
        };
        private string tensUnitsSeparatorCardinal = " ";
        private string tensUnitsSeparatorOrdinal = "-";
        public virtual void SetTensUnitsSeparatorCardinal(string separator)
        {
            tensUnitsSeparatorCardinal = separator;
        }

        public virtual void SetTensUnitsSeparatorOrdinal(string separator)
        {
            tensUnitsSeparatorOrdinal = separator;
        }

        public override void SetLanguage(string language)
        {
            base.SetLanguage(language);
            if (language.EndsWith("-x-hyphen", StringComparison.Ordinal))
            {
                SetTensUnitsSeparatorOrdinal("-");
                SetTensUnitsSeparatorCardinal("-");
            }
            else if (language.EndsWith("-x-nohyphen", StringComparison.Ordinal))
            {
                SetTensUnitsSeparatorOrdinal(" ");
                SetTensUnitsSeparatorCardinal(" ");
            }
        }

        protected override string OrdinalSuffix(string ordinalParam, long number)
        {
            int penult = (int)(number % 100) / 10;
            int ult = (int)(number % 10);
            if (penult == 1)
            {

                // e.g. 11th, 12th, 13th
                return "th";
            }
            else
            {
                if (ult == 1)
                {
                    return "st";
                }
                else if (ult == 2)
                {
                    return "nd";
                }
                else if (ult == 3)
                {
                    return "rd";
                }
                else
                {
                    return "th";
                }
            }
        }

        public override string ToWords(string cardinal, long number)
        {
            if (number >= 1000000000)
            {
                long rem = number % 1000000000;
                return ToWords(cardinal, number / 1000000000) + " Billion" + (rem == 0 ? "" : (rem < 100 ? " and " : " ") + ToWords(cardinal, rem));
            }
            else if (number >= 1000000)
            {
                long rem = number % 1000000;
                return ToWords(cardinal, number / 1000000) + " Million" + (rem == 0 ? "" : (rem < 100 ? " and " : " ") + ToWords(cardinal, rem));
            }
            else if (number >= 1000)
            {
                long rem = number % 1000;
                return ToWords(cardinal, number / 1000) + " Thousand" + (rem == 0 ? "" : (rem < 100 ? " and " : " ") + ToWords(cardinal, rem));
            }
            else if (number >= 100)
            {
                long rem = number % 100;
                return ToWords(cardinal, number / 100) + " Hundred" + (rem == 0 ? "" : " and " + ToWords(cardinal, rem));
            }
            else
            {
                if (number < 20)
                {
                    return englishUnits[(int)number];
                }

                int rem = (int)(number % 10);
                return englishTens[(int)number / 10] + (rem == 0 ? "" : tensUnitsSeparatorCardinal + englishUnits[rem]);
            }
        }

        public override string ToOrdinalWords(string ordinalParam, long number, int wordCase)
        {
            string s;
            if (number >= 1000000000)
            {
                long rem = number % 1000000000;
                s = ToWords(ordinalParam, number / 1000000000) + " Billion" + (rem == 0 ? "th" : (rem < 100 ? " and " : " ") + ToOrdinalWords(ordinalParam, rem, wordCase));
            }
            else if (number >= 1000000)
            {
                long rem = number % 1000000;
                s = ToWords(ordinalParam, number / 1000000) + " Million" + (rem == 0 ? "th" : (rem < 100 ? " and " : " ") + ToOrdinalWords(ordinalParam, rem, wordCase));
            }
            else if (number >= 1000)
            {
                long rem = number % 1000;
                s = ToWords(ordinalParam, number / 1000) + " Thousand" + (rem == 0 ? "th" : (rem < 100 ? " and " : " ") + ToOrdinalWords(ordinalParam, rem, wordCase));
            }
            else if (number >= 100)
            {
                long rem = number % 100;
                s = ToWords(ordinalParam, number / 100) + " Hundred" + (rem == 0 ? "th" : " and " + ToOrdinalWords(ordinalParam, rem, wordCase));
            }
            else
            {
                if (number < 20)
                {
                    s = englishOrdinalUnits[(int)number];
                }
                else
                {
                    int rem = (int)(number % 10);
                    if (rem == 0)
                    {
                        s = englishOrdinalTens[(int)number / 10];
                    }
                    else
                    {
                        s = englishTens[(int)number / 10] + tensUnitsSeparatorOrdinal + englishOrdinalUnits[rem];
                    }
                }
            }

            if (wordCase == UPPER_CASE)
            {
                return s.ToUpperInvariant();
            }
            else if (wordCase == LOWER_CASE)
            {
                return s.ToLowerInvariant();
            }
            else
            {
                return s;
            }
        }
        public override string MonthName(int month, int minWidth, int maxWidth)
        {
            string name = englishMonths[month - 1];
            if (maxWidth < 3)
            {
                maxWidth = 3;
            }

            if (name.Length > maxWidth)
            {
                name = name.Substring(0, maxWidth);
            }

            StringBuilder nameBuilder = new StringBuilder(name);
            while (nameBuilder.Length < minWidth)
            {
                nameBuilder.Append(' ');
            }

            name = nameBuilder.ToString();
            return name;
        }
        public override string DayName(int day, int minWidth, int maxWidth)
        {
            string name = englishDays[day - 1];
            if (maxWidth < 2)
            {
                maxWidth = 2;
            }

            if (name.Length > maxWidth)
            {
                name = englishDayAbbreviations[day - 1];
                if (name.Length > maxWidth)
                {
                    name = name.Substring(0, maxWidth);
                }
            }

            StringBuilder nameBuilder = new StringBuilder(name);
            while (nameBuilder.Length < minWidth)
            {
                nameBuilder.Append(' ');
            }

            name = nameBuilder.ToString();
            if (minWidth == 1 && maxWidth == 2)
            {

                // special case
                name = name.Substring(0, minUniqueDayLength[day - 1]);
            }

            return name;
        }
    }
}