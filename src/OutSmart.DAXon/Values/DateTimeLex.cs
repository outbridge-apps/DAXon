////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Text;

namespace OutSmart.DAXon.Values
{
    /// <summary>
    /// The lexical forms of xs:dateTime, xs:date (and the g* types read through it) and xs:time, read by one lexer: the
    /// token stream of upstream's StringTokenizer ("-:.+TZ" delimiter, or a maximal run of other characters) read
    /// positionally off the trimmed string, with the Latin1 byte fast path for Slice8/Twine8 (byte == codepoint;
    /// everything the parser inspects is ASCII). Upstream parsed the tail three times and only xs:dateTime refused
    /// a +14:30 timezone.
    /// </summary>
    internal struct DateTimeLex
    {
        private readonly UnicodeString str;
        private readonly byte[] b8;
        private readonly int off8;
        private readonly int len;
        private int pos;
        private int delim;      // the current token's character when it is a delimiter, else -1
        private int runStart;
        private int runLen;

        public DateTimeLex(UnicodeString trimmed)
        {
            str = trimmed;
            len = trimmed.Length32();
            Slice8.Latin1Span(trimmed, out b8, out off8, out _);

            pos = 0;
            delim = -1;
            runStart = 0;
            runLen = 0;
        }

        private int CharAt(int i)
        {
            return b8 != null ? b8[off8 + i] & 0xff : str.CodePointAt(i);
        }

        private static bool IsDelim(int c)
        {
            return c == '-' || c == ':' || c == '.' || c == '+' || c == 'T' || c == 'Z';
        }

        public bool HasMoreTokens()
        {
            return pos < len;
        }

        public void NextToken()
        {
            if (pos >= len)
            {
                // StringTokenizer's exhaustion contract; every parser call site is now guarded.
                throw new InvalidOperationException("No more tokens");
            }

            int c = CharAt(pos);
            if (IsDelim(c))
            {
                delim = c;
                pos++;
                return;
            }

            delim = -1;
            runStart = pos;
            do
            {
                pos++;
            }
            while (pos < len && !IsDelim(CharAt(pos)));
            runLen = pos - runStart;
        }

        public bool TokenIs(char c)
        {
            return delim == c;
        }

        public int TokenLength()
        {
            return delim >= 0 ? 1 : runLen;
        }

        public char TokenChar0()
        {
            return delim >= 0 ? (char)delim : (char)CharAt(runStart);
        }

        // DurationValue.SimpleInteger over the token: -1 for a delimiter/non-digit, -2 on overflow.
        public int TokenValue()
        {
            return delim >= 0 ? -1 : SpanValue(runStart, runLen);
        }

        public int TokenValuePrefix(int count)
        {
            return delim >= 0 ? -1 : SpanValue(runStart, count);
        }

        private int SpanValue(int start, int count)
        {
            long result = 0;
            for (int i = start; i < start + count; i++)
            {
                int c = CharAt(i);
                if (c < '0' || c > '9')
                {
                    return -1;
                }

                result = result * 10 + (c - '0');
                if (result > int.MaxValue)
                {
                    return -2;
                }
            }

            return (int)result;
        }

        public bool TokenAllDigits()
        {
            if (delim >= 0)
            {
                return false;
            }

            for (int i = runStart; i < runStart + runLen; i++)
            {
                int c = CharAt(i);
                if (c < '0' || c > '9')
                {
                    return false;
                }
            }

            return true;
        }

        public string TokenString(int count)
        {
            return delim >= 0 ? ((char)delim).ToString() : str.Substring(runStart, runStart + (long)count).ToString();
        }

        // The readers below each return why the value is wrong, or null; the caller names the type in its message.

        /// <summary>[-]yyyy-mm-dd. yearRange: the year is too large to hold (FODT0001, not FORG0001).</summary>
        public string ReadDate(bool allowYearZero, out int year, out byte month, out byte day, out bool yearRange)
        {
            year = 0;
            month = 0;
            day = 0;
            yearRange = false;
            if (!HasMoreTokens())
            {
                return "Too short";
            }

            NextToken();
            int era = +1;
            if (TokenIs('+'))
            {
                return "Date must not start with '+' sign";
            }
            else if (TokenIs('-'))
            {
                era = -1;
                if (!HasMoreTokens())
                {
                    return "No year after '-'";
                }

                NextToken();
            }

            int value = TokenValue();
            if (value < 0)
            {
                yearRange = value != -1;
                return yearRange ? "Year is outside the range that Saxon can handle" : "Non-numeric year component";
            }

            year = value * era;
            if (TokenLength() < 4)
            {
                return "Year is less than four digits";
            }

            if (TokenLength() > 4 && TokenChar0() == '0')
            {
                return "When year exceeds 4 digits, leading zeroes are not allowed";
            }

            if (year == 0 && !allowYearZero)
            {
                return "Year zero is not allowed";
            }

            if (era < 0 && !allowYearZero)
            {
                year++; // if year zero not allowed, -0001 is the year before +0001, represented as 0 internally.
            }

            string wrong = ReadDelimiter('-', "Wrong delimiter after year")
                ?? ReadTwoDigits("Month", 1, 12, out month)
                ?? ReadDelimiter('-', "Wrong delimiter after month")
                ?? ReadTwoDigits("Day", 1, 31, out day);
            return wrong;
        }

        /// <summary>hh:mm:ss; 24:00:00 is midnight at the end of the day.</summary>
        public string ReadClock(out byte hour, out byte minute, out byte second)
        {
            minute = 0;
            second = 0;
            string wrong = ReadTwoDigits("Hour", 0, 24, out hour)
                ?? ReadDelimiter(':', "Wrong delimiter after hour")
                ?? ReadTwoDigits("Minute", 0, 59, out minute);
            if (wrong != null)
            {
                return wrong;
            }

            if (hour == 24 && minute != 0)
            {
                return "If hour is 24, minute must be 00";
            }

            wrong = ReadDelimiter(':', "Wrong delimiter after minute") ?? ReadTwoDigits("Second", 0, 59, out second);
            if (wrong != null)
            {
                return wrong;
            }

            return hour == 24 && second != 0 ? "If hour is 24, second must be 00" : null;
        }

        /// <summary>
        /// The tail of a time or dateTime after the seconds (fraction: fractional seconds allowed), or of a date after
        /// the day: then Z or a timezone of -14:00 to +14:00. tzMinutes is NO_TIMEZONE when there is none.
        /// </summary>
        public string ReadTail(bool fraction, bool hour24, out int nanosecond, out int tzMinutes)
        {
            nanosecond = 0;
            tzMinutes = CalendarValue.NO_TIMEZONE;
            int tz = 0;
            bool negativeTz = false;
            int state = 0;
            while (HasMoreTokens())
            {
                if (state == 9)
                {
                    return "Characters after the end";
                }

                NextToken();
                if (TokenIs('.') && fraction)
                {
                    if (state != 0)
                    {
                        return "Decimal separator occurs twice";
                    }

                    if (!HasMoreTokens())
                    {
                        return "Decimal point must be followed by digits";
                    }

                    NextToken();
                    int effLen = TokenLength() > 9 && TokenAllDigits() ? 9 : TokenLength();
                    if (TokenValuePrefix(effLen) < 0)
                    {
                        return "Non-numeric fractional seconds component";
                    }

                    double fractionalSeconds = double.Parse('.' + TokenString(effLen), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture);
                    nanosecond = (int)JavaMath.Round(fractionalSeconds * 1000000000);
                    if (nanosecond == 1000000000)
                    {
                        nanosecond--; // truncate fractional seconds to .999_999_999 if nanoseconds rounds to 1_000_000_000
                    }

                    if (hour24 && nanosecond != 0)
                    {
                        return "If hour is 24, fractional seconds must be 0";
                    }

                    state = 1;
                }
                else if (TokenIs('T') && !fraction)
                {
                    return "Value includes time";
                }
                else if (TokenIs('Z'))
                {
                    if (state > 1)
                    {
                        return "Z cannot occur here";
                    }

                    state = 9; // we've finished
                    tzMinutes = 0;
                }
                else if (TokenIs('+') || TokenIs('-'))
                {
                    bool minus = TokenIs('-');
                    if (state > 1)
                    {
                        return (minus ? "-" : "+") + " cannot occur here";
                    }

                    state = 2;
                    if (!HasMoreTokens())
                    {
                        return "Missing timezone";
                    }

                    NextToken();
                    if (TokenLength() != 2)
                    {
                        return "Timezone hour must be two digits";
                    }

                    int value = TokenValue();
                    if (value < 0)
                    {
                        return "Non-numeric timezone hour component";
                    }

                    if (value > 14)
                    {
                        return "Timezone is out of range (-14:00 to +14:00)";
                    }

                    tz = value * 60;
                    negativeTz = minus;
                }
                else if (TokenIs(':'))
                {
                    if (state != 2)
                    {
                        return "Misplaced ':'";
                    }

                    state = 9;
                    if (!HasMoreTokens())
                    {
                        // Upstream lets StringTokenizer throw here and the crash escapes the cast
                        // (Java HE 12.5, DateTimeValue.java:598); a dangling ':' is a lexical error.
                        return "No minutes in timezone";
                    }

                    NextToken();
                    int tzminute = TokenValue();
                    if (tzminute < 0)
                    {
                        return "Non-numeric timezone minute component";
                    }

                    if (TokenLength() != 2)
                    {
                        return "Timezone minute must be two digits";
                    }

                    if (tzminute > 59)
                    {
                        return "Timezone minute is out of range";
                    }

                    // Upstream checked this for xs:dateTime only: +14:30 made a time, date or g* value.
                    if (tz == 14 * 60 && tzminute != 0)
                    {
                        return "Timezone is out of range (-14:00 to +14:00)";
                    }

                    tz += tzminute;
                    tzMinutes = negativeTz ? -tz : tz;
                }
                else
                {
                    return "Timezone format is incorrect";
                }
            }

            return state == 2 ? "Timezone incomplete" : null;
        }

        private string ReadDelimiter(char expected, string wrong)
        {
            if (!HasMoreTokens())
            {
                return "Too short";
            }

            NextToken();
            return TokenIs(expected) ? null : wrong;
        }

        private string ReadTwoDigits(string what, int min, int max, out byte field)
        {
            field = 0;
            if (!HasMoreTokens())
            {
                return "Too short";
            }

            NextToken();
            if (TokenLength() != 2)
            {
                return what + " must be two digits";
            }

            int value = TokenValue();
            if (value < 0)
            {
                return "Non-numeric " + what.ToLowerInvariant() + " component";
            }

            field = (byte)value;
            return value < min || value > max ? what + " is out of range" : null;
        }
    }
}
