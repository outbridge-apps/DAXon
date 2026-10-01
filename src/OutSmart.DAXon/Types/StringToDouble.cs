////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Globalization;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Types
{
    /// <summary>
    /// This class converts a string to an xs:double according to the rules in XML Schema 1.0
    /// </summary>
    public class StringToDouble : StringConverter
    {
        private static readonly StringToDouble THE_INSTANCE = new StringToDouble();

        private static readonly double[] powers = new double[]
        {
            1,
            10,
            100,
            1000,
            10000,
            100000,
            1000000,
            10000000,
            100000000,
            1e9, 1e10, 1e11, 1e12, 1e13, 1e14, 1e15, 1e16
        };

        // Hand-parse exactness bound: while num < 10^15 (< 2^53) the accumulated integer and every
        // powers[] entry are EXACT doubles, so the single correctly-rounded division below equals the
        // correctly-rounded decimal->binary conversion (== Java's Double.parseDouble) bit-for-bit.
        // One more digit could make (double)num inexact and break that equivalence.
        private const long MAX_EXACT = 999_999_999_999_999;

        protected StringToDouble()
        {
        }
        public static StringToDouble GetInstance()
        {
            return THE_INSTANCE;
        }

        public virtual double StringToNumber(UnicodeString s)
        {
            string error = ParseOrError(s, out double result);
            if (error != null)
            {
                throw new FormatException(error);
            }

            return result;
        }

        // For callers that turn a bad value into NaN or a validation failure: dirty data (empty
        // fields, "N/A") is common, and a thrown exception costs tens of microseconds per value.
        internal bool TryStringToNumber(UnicodeString s, out double result)
        {
            return ParseOrError(s, out result) == null;
        }

        // null when s is an xs:double lexical form (result set), else why it is not. Never throws on
        // bad input. Culture-invariant: under the thread culture de-DE read "-1.5" as -15.
        internal string ParseOrError(UnicodeString s, out double result)
        {
            result = double.NaN;

            // first try to parse simple numbers by hand (it's cheaper)
            int len = s.Length32();
            bool containsDisallowedChars = false;
            bool containsWhitespace = false;
            // Latin1 fast path: tree text (Slice8) and 8-bit in-memory strings (Twine8) expose their
            // raw byte buffer, where byte value == codepoint. Index it directly and skip the per-char
            // virtual CodePointAt (+ long->int RequireInt + bounds re-check). Byte-identical: every
            // character this parser inspects is ASCII, and CodePointAt returns exactly (byte & 0xff)
            // for these widths. Width>8 strings keep b8==null and use CodePointAt unchanged.
            byte[] b8 = null;
            int off8 = 0;
            if (s is Slice8 sl)
            {
                b8 = sl.ByteArray;
                off8 = sl.Start;
            }
            else if (s is Twine8 tw)
            {
                b8 = tw.ByteArray;
            }
            // Was len < 9 (Java's cutoff). Extended to len < 17 with the MAX_EXACT guard in the digit
            // case: the guard keeps num within the exact-double range, which is the property that made
            // the < 9 cutoff byte-safe in the first place (see MAX_EXACT). Kills the ToString +
            // double.Parse round-trip for the common long-decimal tree values (e.g. "4497.1000000000").
            if (len < 17)
            {
                bool useJava = false;
                long num = 0;
                int dot = -1;
                int lastDigit = -1;
                bool onlySpaceAllowed = false;
                bool breakLoop = false;
                for (int i = 0; i < len; i++)
                {
                    int c = b8 != null ? (b8[off8 + i] & 0xff) : s.CodePointAt(i);
                    switch (c)
                    {
                        case ' ':
                        case '\n':
                        case '\t':
                        case '\r':
                            containsWhitespace = true;
                            if (lastDigit != -1)
                            {
                                onlySpaceAllowed = true;
                            }

                            break;
                        case '0':
                        case '1':
                        case '2':
                        case '3':
                        case '4':
                        case '5':
                        case '6':
                        case '7':
                        case '8':
                        case '9':
                            if (onlySpaceAllowed)
                            {
                                return "Numeric value contains embedded whitespace";
                            }

                            lastDigit = i;
                            if (num > MAX_EXACT / 10)
                            {
                                // appending would allow num past MAX_EXACT → (double)num inexact: slow
                                // train. Keep scanning (like default:) so disallowed-char detection runs.
                                useJava = true;
                            }

                            num = num * 10 + (c - '0');
                            break;
                        case '.':
                            if (onlySpaceAllowed)
                            {
                                return "Numeric value contains embedded whitespace";
                            }

                            if (dot != -1)
                            {
                                return "Only one decimal point allowed";
                            }

                            dot = i;
                            break;
                        case 'x':
                        case 'X':
                        case 'f':
                        case 'F':
                        case 'd':
                        case 'D':
                        case 'n':
                        case 'N':
                            containsDisallowedChars = true;
                            useJava = true;
                            breakLoop = true;
                            break;
                        default:

                            // there's something like a sign or an exponent: take the slow train instead
                            // But keep going to look for disallowed characters - bug 3495
                            useJava = true;
                            break;
                    }

                    if (breakLoop)
                    {
                        break;
                    }
                }

                if (!useJava)
                {
                    if (lastDigit == -1)
                    {
                        return "String to double conversion: no digits found";
                    }
                    else if (dot == -1 || dot > lastDigit)
                    {
                        result = (double)num;
                        return null;
                    }
                    else
                    {
                        int afterPoint = lastDigit - dot;
                        result = (double)num / powers[afterPoint];
                        return null;
                    }
                }
            }
            else
            {
                bool breakLoop2 = false;
                for (int i = 0; i < len; i++)
                {
                    int c = b8 != null ? (b8[off8 + i] & 0xff) : s.CodePointAt(i);
                    switch (c)
                    {
                        case ' ':
                        case '\n':
                        case '\t':
                        case '\r':
                            containsWhitespace = true;
                            break;
                        case '0':
                        case '1':
                        case '2':
                        case '3':
                        case '4':
                        case '5':
                        case '6':
                        case '7':
                        case '8':
                        case '9':
                        case '.':
                        case 'e':
                        case 'E':
                        case '+':
                        case '-':
                            break;
                        default:
                            containsDisallowedChars = true;
                            breakLoop2 = true;
                            break;
                    }

                    if (breakLoop2)
                    {
                        break;
                    }
                }
            }

            string n = containsWhitespace ? Whitespace.Trim(s).ToString() : s.ToString();
            if (n == "INF")
            {
                result = double.PositiveInfinity;
                return null;
            }
            else if (n == "+INF")
            {
                // Allowed in XSD 1.1 but not in XSD 1.0
                if (!PlusInfAllowed)
                {
                    return "the float/double value '+INF' is not allowed under XSD 1.0";
                }

                result = double.PositiveInfinity;
                return null;
            }
            else if (n == "-INF")
            {
                result = double.NegativeInfinity;
                return null;
            }
            else if (n == "NaN")
            {
                result = double.NaN;
                return null;
            }

            // reject strings containing characters such as (x, f, d) allowed in Java but not in XPath,
            // and other representations of NaN and Infinity such as 'Infinity'
            if (containsDisallowedChars)
            {
                return "invalid floating point value: " + s;
            }

            if (!double.TryParse(n, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
            {
                if (!IsFloatLiteral(n))
                {
                    return "Input string was not in a correct format.";
                }

                // Out of range: .NET Framework reports that as a failure, Java's Double.parseDouble (and
                // XPath xs:double) overflow to +/-INF.
                d = n[0] == '-' ? double.NegativeInfinity : double.PositiveInfinity;
            }

            // .NET's double.Parse collapses negative zero to +0.0; Java's Double.parseDouble (which
            // upstream Saxon relies on) preserves the sign. Restore it so e.g. xs:double("-0e0") keeps
            // string value "-0" (XPath/JSON canonical form).
            result = d == 0.0 && n[0] == '-' ? -0.0 : d;
            return null;
        }

        // [+-]? (digits [. digits*] | . digits) ([eE] [+-]? digits)? - what NumberStyles.Float takes
        // once the whitespace is trimmed.
        private static bool IsFloatLiteral(string n)
        {
            int i = 0;
            if (i < n.Length && (n[i] == '+' || n[i] == '-'))
            {
                i++;
            }

            int mantissa = 0;
            while (i < n.Length && n[i] >= '0' && n[i] <= '9')
            {
                i++;
                mantissa++;
            }

            if (i < n.Length && n[i] == '.')
            {
                i++;
                while (i < n.Length && n[i] >= '0' && n[i] <= '9')
                {
                    i++;
                    mantissa++;
                }
            }

            if (mantissa == 0)
            {
                return false;
            }

            if (i < n.Length && (n[i] == 'e' || n[i] == 'E'))
            {
                i++;
                if (i < n.Length && (n[i] == '+' || n[i] == '-'))
                {
                    i++;
                }

                int exponent = 0;
                while (i < n.Length && n[i] >= '0' && n[i] <= '9')
                {
                    i++;
                    exponent++;
                }

                if (exponent == 0)
                {
                    return false;
                }
            }

            return i == n.Length;
        }

        // Whether "+INF" is a legal lexical form: XSD 1.1 (StringToDouble11), not 1.0.
        private protected virtual bool PlusInfAllowed => false;

        protected virtual double SignedPositiveInfinity()
        {
            throw new FormatException("the float/double value '+INF' is not allowed under XSD 1.0");
        }

        public override IConversionResult ConvertString(UnicodeString input)
        {
            if (TryStringToNumber(input, out double d))
            {
                return new DoubleValue(d);
            }

            return new ValidationFailure("Cannot convert string " + Err.Wrap(input, Err.VALUE) + " to double");
        }
    }
}