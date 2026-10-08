////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using System;
using System.Globalization;
using System.Collections.Generic;
using System.Text;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// This class implements the function parse-ietf-date(), which is a standard function in XPath 3.1
    /// </summary>
    internal sealed class ParseIetfDate : SystemFunction, ICallable
    {

        /* what should this return? */
        /* Now expect either day number or month name */
        /* Now expect time string */
        /* Now expect time string ("after..." may differ) */
        /*the number of microseconds, 0-999999*/
        /*the timezone displacement in minutes from UTC.*/
        /* the final token index, returned by the method */
        /* seconds, microseconds, timezones not given*/
        /* microseconds, timezones not given*/
        /* no timezone is given in the time, we must have reached a year */
        /* we must have reached the year */
        private static readonly string EOF = "";

        private readonly string[] dayNames = new string[]
        {
            "Mon",
            "Tue",
            "Wed",
            "Thu",
            "Fri",
            "Sat",
            "Sun",
            "Monday",
            "Tuesday",
            "Wednesday",
            "Thursday",
            "Friday",
            "Saturday",
            "Sunday"
        };

        private readonly string[] monthNames = new string[]
        {
            "Jan",
            "Feb",
            "Mar",
            "Apr",
            "May",
            "Jun",
            "Jul",
            "Aug",
            "Sep",
            "Oct",
            "Nov",
            "Dec"
        };

        private readonly string[] timezoneNames = new string[]
        {
            "UT",
            "UTC",
            "GMT",
            "EST",
            "EDT",
            "CST",
            "CDT",
            "MST",
            "MDT",
            "PST",
            "PDT"
        };
        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            StringValue stringValue = (StringValue)arguments[0].Head();
            if (stringValue == null)
            {
                return EmptySequence.GetInstance();
            }

            return SequenceTool.ItemOrEmpty(Parse(stringValue.GetStringValue(), context));
        }
        private bool IsDayName(string str)
        {
            foreach (string s in dayNames)
            {
                if (s.Equals(str, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
        private bool IsMonthName(string str)
        {
            foreach (string s in monthNames)
            {
                if (s.Equals(str, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private byte GetMonthNumber(string str)
        {
            if ("Jan".Equals(str, StringComparison.OrdinalIgnoreCase))
            {
                return (byte)1;
            }
            else if ("Feb".Equals(str, StringComparison.OrdinalIgnoreCase))
            {
                return (byte)2;
            }
            else if ("Mar".Equals(str, StringComparison.OrdinalIgnoreCase))
            {
                return (byte)3;
            }
            else if ("Apr".Equals(str, StringComparison.OrdinalIgnoreCase))
            {
                return (byte)4;
            }
            else if ("May".Equals(str, StringComparison.OrdinalIgnoreCase))
            {
                return (byte)5;
            }
            else if ("Jun".Equals(str, StringComparison.OrdinalIgnoreCase))
            {
                return (byte)6;
            }
            else if ("Jul".Equals(str, StringComparison.OrdinalIgnoreCase))
            {
                return (byte)7;
            }
            else if ("Aug".Equals(str, StringComparison.OrdinalIgnoreCase))
            {
                return (byte)8;
            }
            else if ("Sep".Equals(str, StringComparison.OrdinalIgnoreCase))
            {
                return (byte)9;
            }
            else if ("Oct".Equals(str, StringComparison.OrdinalIgnoreCase))
            {
                return (byte)10;
            }
            else if ("Nov".Equals(str, StringComparison.OrdinalIgnoreCase))
            {
                return (byte)11;
            }
            else if ("Dec".Equals(str, StringComparison.OrdinalIgnoreCase))
            {
                return (byte)12;
            }

            return (byte)0;
        }

        private int RequireDSep(IList<string> tokens, int i, string input)
        {
            bool found = false;
            if (tokens[i] == " ")
            {
                i++;
                found = true;
            }

            if (tokens[i] == "-")
            {
                i++;
                found = true;
            }

            if (tokens[i] == " ")
            {
                i++;
                found = true;
            }

            if (!found)
            {
                BadDate("Date separator missing", input);
            }

            return i;
        }

        private static void BadDate(string msg, string value)
        {
            throw new XPathException("Invalid IETF date value " + value + " (" + msg + ")", "FORG0010");
        }
        private bool IsTimezoneName(string str)
        {
            foreach (string s in timezoneNames)
            {
                if (s.Equals(str, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private int GetTimezoneOffsetFromName(string str)
        {
            if ("UT".Equals(str, StringComparison.OrdinalIgnoreCase) | "UTC".Equals(str, StringComparison.OrdinalIgnoreCase) | "GMT".Equals(str, StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }
            else if ("EST".Equals(str, StringComparison.OrdinalIgnoreCase))
            {
                return -5 * 60;
            }
            else if ("EDT".Equals(str, StringComparison.OrdinalIgnoreCase))
            {
                return -4 * 60;
            }
            else if ("CST".Equals(str, StringComparison.OrdinalIgnoreCase))
            {
                return -6 * 60;
            }
            else if ("CDT".Equals(str, StringComparison.OrdinalIgnoreCase))
            {
                return -5 * 60;
            }
            else if ("MST".Equals(str, StringComparison.OrdinalIgnoreCase))
            {
                return -7 * 60;
            }
            else if ("MDT".Equals(str, StringComparison.OrdinalIgnoreCase))
            {
                return -6 * 60;
            }
            else if ("PST".Equals(str, StringComparison.OrdinalIgnoreCase))
            {
                return -8 * 60;
            }
            else if ("PDT".Equals(str, StringComparison.OrdinalIgnoreCase))
            {
                return -7 * 60;
            }

            return 0; /* what should this return? */
        }

        /* what should this return? */
        public DateTimeValue Parse(string input, IXPathContext context)
        {
            IList<string> tokens = Tokenize(input);
            int year = 0;
            byte month = 0;
            byte day = 0;
            IList<TimeValue> timeValue = new List<TimeValue>();
            int i = 0;
            string currentToken = tokens[i];
            if (currentToken.MatchesRegex("[A-Za-z]+") && IsDayName(currentToken))
            {
                currentToken = tokens[++i];
                if (currentToken == ",")
                {
                    currentToken = tokens[++i];
                }

                if (currentToken != " ")
                {
                    BadDate("Space missing after day name", input);
                }

                currentToken = tokens[++i]; /* Now expect either day number or month name */
            }

            if (IsMonthName(currentToken))
            {
                month = GetMonthNumber(currentToken);
                i = RequireDSep(tokens, i + 1, input);
                currentToken = tokens[i];
                if (!currentToken.MatchesRegex("[0-9]+"))
                {
                    BadDate("Day number expected after month name", input);
                }

                if (currentToken.Length > 2)
                {
                    BadDate("Day number exceeds two digits", input);
                }

                day = (byte)int.Parse(currentToken, CultureInfo.InvariantCulture);
                currentToken = tokens[++i];
                if (currentToken != " ")
                {
                    BadDate("Space missing after day number", input);
                }

                /* Now expect time string */
                i = ParseTime(tokens, ++i, timeValue, input);
                currentToken = tokens[++i];
                if (currentToken != " ")
                {
                    BadDate("Space missing after time string", input);
                }

                currentToken = tokens[++i];
                if (currentToken.MatchesRegex("[0-9]+"))
                {
                    year = CheckTwoOrFourDigits(input, currentToken);
                }
                else
                {
                    BadDate("Year number expected after time", input);
                }
            }
            else if (currentToken.MatchesRegex("[0-9]+"))
            {
                if (currentToken.Length > 2)
                {
                    BadDate("First number in string expected to be day in two digits", input);
                }

                day = (byte)int.Parse(currentToken, CultureInfo.InvariantCulture);
                i = RequireDSep(tokens, ++i, input);
                currentToken = tokens[i];
                if (!IsMonthName(currentToken))
                {
                    BadDate("Abbreviated month name expected after day number", input);
                }

                month = GetMonthNumber(currentToken);
                i = RequireDSep(tokens, ++i, input);
                currentToken = tokens[i];
                if (currentToken.MatchesRegex("[0-9]+"))
                {
                    year = CheckTwoOrFourDigits(input, currentToken);
                }
                else
                {
                    BadDate("Year number expected after month name", input);
                }

                currentToken = tokens[++i];
                if (currentToken != " ")
                {
                    BadDate("Space missing after year number", input);
                }

                /* Now expect time string ("after..." may differ) */
                i = ParseTime(tokens, ++i, timeValue, input);
            }
            else
            {
                BadDate("String expected to begin with month name or day name (or day number)", input);
            }

            if (!GDateValue.IsValidDate(year, month, day))
            {
                BadDate("Date is not valid", input);
            }

            currentToken = tokens[++i];
            if (!currentToken.Equals(EOF))
            {
                BadDate("Extra content found in string after date", input);
            }

            DateValue date = new DateValue(year, month, day);
            TimeValue time = timeValue[0];
            if (time.Hour == 24)
            {
                date = DateValue.Tomorrow(date.Year, date.Month, date.Day);
                time = new TimeValue((byte)0, (byte)0, (byte)0, 0, time.TimezoneInMinutes, BuiltInAtomicType.TIME);
            }

            return DateTimeValue.MakeDateTimeValue(date, time);
        }

        /* what should this return? */
        /* Now expect either day number or month name */
        /* Now expect time string */
        /* Now expect time string ("after..." may differ) */
        private int CheckTwoOrFourDigits(string input, string currentToken)
        {
            int year;
            if (currentToken.Length == 4)
            {
                year = int.Parse(currentToken, CultureInfo.InvariantCulture);
            }
            else if (currentToken.Length == 2)
            {
                year = int.Parse(currentToken, CultureInfo.InvariantCulture) + 1900;
            }
            else
            {
                BadDate("Year number must be two or four digits", input);
                year = 0;
            }

            return year;
        }

        /* what should this return? */
        /* Now expect either day number or month name */
        /* Now expect time string */
        /* Now expect time string ("after..." may differ) */
        public int ParseTime(IList<string> tokens, int currentPosition, IList<TimeValue> result, string input)
        {
            byte hour;
            byte minute;
            byte second = 0;
            int microsecond = 0; /*the number of microseconds, 0-999999*/
            int tz = 0; /*the timezone displacement in minutes from UTC.*/
            int i = currentPosition;
            int n = currentPosition; /* the final token index, returned by the method */
            StringBuilder currentToken = new StringBuilder(tokens[i]);
            if (!currentToken.ToString().MatchesRegex("[0-9]+"))
            {
                BadDate("Hour number expected", input);
            }

            if (currentToken.Length > 2)
            {
                BadDate("Hour number exceeds two digits", input);
            }

            hour = (byte)int.Parse(currentToken.ToString(), CultureInfo.InvariantCulture);
            currentToken = new StringBuilder(tokens[++i]);
            if (currentToken.ToString() != ":")
            {
                BadDate("Separator ':' missing after hour", input);
            }

            currentToken = new StringBuilder(tokens[++i]);
            if (!currentToken.ToString().MatchesRegex("[0-9]+"))
            {
                BadDate("Minutes expected after hour", input);
            }

            if (currentToken.Length != 2)
            {
                BadDate("Minutes must be exactly two digits", input);
            }

            minute = (byte)int.Parse(currentToken.ToString(), CultureInfo.InvariantCulture);
            currentToken = new StringBuilder(tokens[++i]);
            bool finished = false;
            if (currentToken.ToString().Equals(EOF))
            {
                /* seconds, microseconds, timezones not given*/
                n = i - 1;
                finished = true;
            }
            else if (currentToken.ToString() == ":")
            {
                currentToken = new StringBuilder(tokens[++i]);
                if (!currentToken.ToString().MatchesRegex("[0-9]+"))
                {
                    BadDate("Seconds expected after ':' separator after minutes", input);
                }

                if (currentToken.Length != 2)
                {
                    BadDate("Seconds number must have exactly two digits (before decimal point)", input);
                }

                second = (byte)int.Parse(currentToken.ToString(), CultureInfo.InvariantCulture);
                currentToken = new StringBuilder(tokens[++i]);
                if (currentToken.ToString().Equals(EOF))
                {
                    /* microseconds, timezones not given*/
                    n = i - 1;
                    finished = true;
                }
                else if (currentToken.ToString() == ".")
                {
                    currentToken = new StringBuilder(tokens[++i]);
                    if (!currentToken.ToString().MatchesRegex("[0-9]+"))
                    {
                        BadDate("Fractional part of seconds expected after decimal point", input);
                    }

                    int len = Math.Min(6, currentToken.Length);
                    currentToken = new StringBuilder(currentToken.ToString(0, (len) - (0)));
                    while (currentToken.Length < 6)
                    {
                        currentToken.Append('0');
                    }

                    microsecond = int.Parse(currentToken.ToString(), CultureInfo.InvariantCulture);
                    if (i < tokens.Count - 1)
                    {
                        currentToken = new StringBuilder(tokens[++i]);
                    }
                }
            }

            if (!finished)
            {
                if (currentToken.ToString() == " ")
                {
                    currentToken = new StringBuilder(tokens[++i]);
                    if (currentToken.ToString().MatchesRegex("[0-9]+"))
                    {
                        /* no timezone is given in the time, we must have reached a year */
                        n = i - 2;
                        finished = true;
                    }
                }

                if (!finished)
                {
                    if (currentToken.ToString().MatchesRegex("[A-Za-z]+"))
                    {
                        if (!IsTimezoneName(currentToken.ToString()))
                        {
                            BadDate("Timezone name not recognised", input);
                        }

                        tz = GetTimezoneOffsetFromName(currentToken.ToString());
                        n = i;
                        finished = true;
                    }
                    else if (currentToken.ToString() == "+" | currentToken.ToString() == "-")
                    {
                        string sign = currentToken.ToString();
                        int tzOffsetHours = 0;
                        int tzOffsetMinutes = 0;
                        currentToken = new StringBuilder(tokens[++i]);
                        if (!currentToken.ToString().MatchesRegex("[0-9]+"))
                        {
                            BadDate("Parsing timezone offset, number expected after '" + sign + "'", input);
                        }

                        int tLength = currentToken.Length;
                        if (tLength > 4)
                        {
                            BadDate("Timezone offset does not have the correct number of digits", input);
                        }
                        else if (tLength >= 3)
                        {
                            tzOffsetHours = int.Parse(currentToken.ToString(0, (tLength - 2) - (0)), CultureInfo.InvariantCulture);
                            tzOffsetMinutes = int.Parse(currentToken.ToString(tLength - 2, (tLength) - (tLength - 2)), CultureInfo.InvariantCulture);
                            currentToken = new StringBuilder(tokens[++i]);
                        }
                        else
                        {
                            tzOffsetHours = int.Parse(currentToken.ToString(), CultureInfo.InvariantCulture);
                            currentToken = new StringBuilder(tokens[++i]);
                            if (currentToken.ToString() == ":")
                            {
                                currentToken = new StringBuilder(tokens[++i]);
                                if (currentToken.ToString().MatchesRegex("[0-9]+"))
                                {
                                    if (currentToken.Length != 2)
                                    {
                                        BadDate("Parsing timezone offset, minutes must be two digits", input);
                                    }
                                    else
                                    {
                                        tzOffsetMinutes = int.Parse(currentToken.ToString(), CultureInfo.InvariantCulture);
                                    }

                                    currentToken = new StringBuilder(tokens[++i]);
                                }
                            }
                        }

                        if (tzOffsetMinutes > 59)
                        {
                            BadDate("Timezone offset minutes out of range", input);
                        }

                        tz = tzOffsetHours * 60 + tzOffsetMinutes;
                        if (sign.Equals("-"))
                        {
                            tz = -tz;
                        }

                        if (currentToken.ToString().Equals(EOF))
                        {
                            n = i - 1;
                            finished = true;
                        }
                        else if (currentToken.ToString() == " ")
                        {
                            currentToken = new StringBuilder(tokens[++i]);
                            if (currentToken.ToString().MatchesRegex("[0-9]+"))
                            {
                                /* we must have reached the year */
                                n = i - 2;
                                finished = true;
                            }
                        }

                        if (!finished && currentToken.ToString() == "(")
                        {
                            currentToken = new StringBuilder(tokens[++i]);
                            if (currentToken.ToString() == " ")
                            {
                                currentToken = new StringBuilder(tokens[++i]);
                            }

                            if (!currentToken.ToString().MatchesRegex("[A-Za-z]+"))
                            {
                                BadDate("Timezone name expected after '('", input);
                            }
                            else if (currentToken.ToString().MatchesRegex("[A-Za-z]+"))
                            {
                                if (!IsTimezoneName(currentToken.ToString()))
                                {
                                    BadDate("Timezone name not recognised", input);
                                }

                                currentToken = new StringBuilder(tokens[++i]);
                            }

                            if (currentToken.ToString() == " ")
                            {
                                currentToken = new StringBuilder(tokens[++i]);
                            }

                            if (currentToken.ToString() != ")")
                            {
                                BadDate("Expected ')' after timezone name", input);
                            }

                            n = i;
                            finished = true;
                        }
                        else if (!finished)
                        {
                            BadDate("Unexpected content after timezone offset", input);
                        }
                    }
                    else
                    {
                        BadDate("Unexpected content in time (after minutes)", input);
                    }
                }
            }

            if (!finished)
            {
                throw new InvalidOperationException("Should have finished");
            }

            if (!IsValidTime(hour, minute, second, microsecond, tz))
            {
                BadDate("Time/timezone is not valid", input);
            }

            TimeValue timeValue = new TimeValue(hour, minute, second, microsecond * 1000, tz, BuiltInAtomicType.TIME);
            result.Add(timeValue);
            return n;
        }

        /* what should this return? */
        /* Now expect either day number or month name */
        /* Now expect time string */
        /* Now expect time string ("after..." may differ) */
        /*the number of microseconds, 0-999999*/
        /*the timezone displacement in minutes from UTC.*/
        /* the final token index, returned by the method */
        /* seconds, microseconds, timezones not given*/
        /* microseconds, timezones not given*/
        /* no timezone is given in the time, we must have reached a year */
        /* we must have reached the year */
        public static bool IsValidTime(int hour, int minute, int second, int microsecond, int tz)
        {
            return (hour >= 0 && hour <= 23 && minute >= 0 && minute < 60 && second >= 0 && second < 60 && microsecond >= 0 && microsecond < 1000000 || hour == 24 && minute == 0 && second == 0 && microsecond == 0) && tz >= -14 * 60 && tz <= 14 * 60;
        }

        /* what should this return? */
        /* Now expect either day number or month name */
        /* Now expect time string */
        /* Now expect time string ("after..." may differ) */
        /*the number of microseconds, 0-999999*/
        /*the timezone displacement in minutes from UTC.*/
        /* the final token index, returned by the method */
        /* seconds, microseconds, timezones not given*/
        /* microseconds, timezones not given*/
        /* no timezone is given in the time, we must have reached a year */
        /* we must have reached the year */
        private IList<string> Tokenize(string input)
        {
            IList<string> tokens = new List<string>();
            input = input.Trim();
            if ((input.Length == 0))
            {
                BadDate("Input is empty", input);
                return tokens;
            }

            int i = 0;
            input = input + (char)0;
            while (true)
            {
                char c = input[i];
                if (c == 0)
                {
                    tokens.Add(EOF);
                    return tokens;
                }

                if (Whitespace.IsWhite(c))
                {
                    int j = i;
                    while (Whitespace.IsWhite(input[j++]))
                    {
                    }

                    tokens.Add(" ");
                    i = j - 1;
                }
                else if (char.IsLetter(c))
                {
                    int j = i;
                    while (char.IsLetter(input[j++]))
                    {
                    }

                    tokens.Add(input.Substring(i, j - 1 - i));
                    i = j - 1;
                }
                else if (char.IsDigit(c))
                {
                    int j = i;
                    while (char.IsDigit(input[j++]))
                    {
                    }

                    tokens.Add(input.Substring(i, j - 1 - i));
                    i = j - 1;
                }
                else
                {
                    tokens.Add(input.Substring(i, 1));
                    i++;
                }
            }
        }
    }
}
