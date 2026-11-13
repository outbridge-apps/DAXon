////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Text;

using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using System;
using System.Globalization;
using System.Numerics;
using System.Text;
namespace OutSmart.DAXon.Types
{
    /// <summary>
    /// This class converts a string to an xs:double according to the rules in XML Schema 1.0
    /// </summary>
    public class StringToDouble : StringConverter
    {
        private static readonly StringToDouble THE_INSTANCE = new StringToDouble();

        // 10^0 .. 10^22, every one an exact double.
        private static readonly double[] ExactPowers =
        {
            1, 1e1, 1e2, 1e3, 1e4, 1e5, 1e6, 1e7, 1e8, 1e9, 1e10, 1e11, 1e12, 1e13, 1e14, 1e15, 1e16, 1e17, 1e18, 1e19, 1e20, 1e21, 1e22,
        };

        // Hand-parse exactness bound: while num < 10^15 (< 2^53) the accumulated integer and every
        // powers[] entry are EXACT doubles, so the single correctly-rounded division below equals the
        // correctly-rounded decimal->binary conversion (== Java's Double.parseDouble) bit-for-bit.
        // One more digit could make (double)num inexact and break that equivalence.
        internal const long MAX_EXACT = 999_999_999_999_999;

        // num * 10^exponent10 as the double nearest to it, where num (at most MAX_EXACT) and the power (within 10^+-22)
        // are both exact: one correctly rounded operation, as Java's Double.parseDouble gives. False otherwise. The one
        // conversion of the plain-number fast paths: each kept a power table of its own, and one ran past its end.
        internal static bool TryExact(long num, int exponent10, bool negative, out double value)
        {
            if (num < 0 || num > MAX_EXACT || exponent10 > 22 || exponent10 < -22)
            {
                value = 0;
                return false;
            }

            double v = exponent10 >= 0 ? num * ExactPowers[exponent10] : num / ExactPowers[-exponent10];
            value = negative ? -v : v;
            return true;
        }

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
            Slice8.Latin1Span(s, out byte[] b8, out int off8, out _);
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
                bool signed = false;
                bool negative = false;
                for (int i = 0; i < len; i++)
                {
                    int c = b8 != null ? (b8[off8 + i] & 0xff) : s.CodePointAt(i);
                    switch (c)
                    {
                        case '-':
                        case '+':
                            // A sign right before the first digit is read here too: on .NET Framework the slow train's
                            // double.Parse is not correctly rounded, and every negative amount took it.
                            int next = i + 1 < len ? (b8 != null ? (b8[off8 + i + 1] & 0xff) : s.CodePointAt(i + 1)) : -1;
                            if (signed || lastDigit != -1 || dot != -1 || next < '0' || next > '9')
                            {
                                useJava = true;
                                break;
                            }

                            signed = true;
                            negative = c == '-';
                            break;
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
                    else if (TryExact(num, dot == -1 || dot > lastDigit ? 0 : dot - lastDigit, negative, out result))
                    {
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

            if (n.Length > 100)
            {
                n = Shortened(n);
            }

#if NETFRAMEWORK
            if (TryInDoubleDouble(n, out double quick))
            {
                result = quick;
                return null;
            }
#endif
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
            else
            {
                d = Nearest(n, d);
            }

            // .NET's double.Parse collapses negative zero to +0.0; Java's Double.parseDouble (which
            // upstream Saxon relies on) preserves the sign. Restore it so e.g. xs:double("-0e0") keeps
            // string value "-0" (XPath/JSON canonical form).
            result = d == 0.0 && n[0] == '-' ? -0.0 : d;
            return null;
        }

        // A long numeral as its significant digits (at most KeptDigits and a sticky one standing for the rest) and the
        // exponent of the first: the same double. .NET Framework's double.TryParse takes an exponent only up to 1000 and
        // scales by the digits written, so 1 with 1100 zeros and e-1100 gave 0, and 0. with 3000 zeros 1e3002 failed (INF).
        // A text of another form is returned as it is.
        internal static string Shortened(string n)
        {
            int i = 0;
            bool negative = false;
            if (n[0] == '+' || n[0] == '-')
            {
                negative = n[0] == '-';
                i++;
            }

            var digits = new StringBuilder(KeptDigits + 1);
            long exponent = 0;   // of the last kept digit
            bool point = false;
            bool any = false;
            bool sticky = false;
            for (; i < n.Length; i++)
            {
                char c = n[i];
                if (c >= '0' && c <= '9')
                {
                    any = true;
                    if (digits.Length == 0 && c == '0')
                    {
                        exponent -= point ? 1 : 0;
                    }
                    else if (digits.Length < KeptDigits)
                    {
                        digits.Append(c);
                        exponent -= point ? 1 : 0;
                    }
                    else
                    {
                        sticky |= c != '0';
                        exponent += point ? 0 : 1;
                    }
                }
                else if (c == '.' && !point)
                {
                    point = true;
                }
                else
                {
                    break;
                }
            }

            if (!any)
            {
                return n;
            }

            if (i < n.Length)
            {
                if (n[i] != 'e' && n[i] != 'E' || i + 1 == n.Length)
                {
                    return n;
                }

                int j = i + 1;
                bool minus = n[j] == '-';
                if (n[j] == '+' || n[j] == '-')
                {
                    j++;
                }

                if (j == n.Length)
                {
                    return n;
                }

                long e = 0;
                for (; j < n.Length; j++)
                {
                    if (n[j] < '0' || n[j] > '9')
                    {
                        return n;
                    }

                    e = Math.Min(e * 10 + (n[j] - '0'), 1L << 40);   // far past any double; no overflow
                }

                exponent += minus ? -e : e;
            }

            string sign = negative ? "-" : "";
            if (digits.Length == 0)
            {
                return sign + "0";
            }

            if (sticky)
            {
                digits.Append('1');
                exponent--;
            }

            long first = exponent + digits.Length - 1;   // the exponent of the first digit
            if (first > 400)
            {
                return sign + "1e400";   // past the largest double: an overflow, as the long text is
            }

            if (first < -400)
            {
                return sign + "0";
            }

            if (digits.Length > 1)
            {
                digits.Insert(1, '.');
            }

            return sign + digits + "e" + first.ToString(CultureInfo.InvariantCulture);
        }

        // Significant digits Nearest measures: a halfway point between two doubles has at most 767, so the digits after
        // these only say whether the text is above the kept ones (more than 800 were left to double.Parse, inexact).
        private const int KeptDigits = 780;

        /// <summary>The double nearest to a decimal text; as double.Parse, it throws for what that refuses.</summary>
        internal static double ParseNearest(string text, NumberStyles styles)
        {
#if NETFRAMEWORK
            if (TryInDoubleDouble(text, out double quick))
            {
                return quick;
            }

            return Nearest(text, double.Parse(text, styles, CultureInfo.InvariantCulture));
#else
            return double.Parse(text, styles, CultureInfo.InvariantCulture);
#endif
        }

#if NETFRAMEWORK
        // Most texts need neither double.Parse (not correctly rounded here) nor big integers: up to 19 leading digits and a
        // power within 10^+-44 give the value in double-double, to about 100 bits, and that rounds one way unless it lies
        // within its error of a halfway point (about 2^-40 of texts; of those with more digits, 1 in 50). False: Nearest decides.
        private static bool TryInDoubleDouble(string text, out double value)
        {
            value = 0;
            int i = 0;
            int n = text.Length;
            bool negative = false;
            if (i < n && (text[i] == '+' || text[i] == '-'))
            {
                negative = text[i] == '-';
                i++;
            }

            ulong mantissa = 0;
            int significant = 0;
            int exponent10 = 0;
            bool point = false;
            bool any = false;
            bool dropped = false;   // a digit past the 19th was not zero
            for (; i < n; i++)
            {
                char c = text[i];
                if (c >= '0' && c <= '9')
                {
                    any = true;
                    if (significant == 19)
                    {
                        dropped |= c != '0';
                        exponent10 += point ? 0 : 1;
                        continue;
                    }

                    if (significant > 0 || c != '0')
                    {
                        mantissa = mantissa * 10 + (ulong)(c - '0');
                        significant++;
                    }

                    exponent10 -= point ? 1 : 0;
                }
                else if (c == '.' && !point)
                {
                    point = true;
                }
                else
                {
                    break;
                }
            }

            if (!any || mantissa == 0)
            {
                return false;
            }

            if (i < n)
            {
                if ((text[i] != 'e' && text[i] != 'E') || ++i == n)
                {
                    return false;
                }

                bool minus = text[i] == '-';
                if ((text[i] == '+' || text[i] == '-') && ++i == n)
                {
                    return false;
                }

                int e = 0;
                for (; i < n; i++)
                {
                    if (text[i] < '0' || text[i] > '9' || e > 9999)
                    {
                        return false;
                    }

                    e = e * 10 + (text[i] - '0');
                }

                exponent10 += minus ? -e : e;
            }

            if (exponent10 < -44 || exponent10 > 44)
            {
                return false;
            }

            double a = (double)(uint)(mantissa >> 32) * 4294967296.0;
            double b = (uint)mantissa;
            double hi = a + b;
            double lo = b - (hi - a);
            int power = Math.Abs(exponent10);
            if (power > 22)
            {
                Scale(ref hi, ref lo, 1e22, exponent10 < 0);
                power -= 22;
            }

            Scale(ref hi, ref lo, ExactPowers[power], exponent10 < 0);
            long bits = BitConverter.DoubleToInt64Bits(hi);
            if ((bits & 0xFFFFFFFFFFFFFL) == 0)
            {
                return false;   // a power of two: the neighbour below is nearer than the one above
            }

            double halfUlp = (BitConverter.Int64BitsToDouble(bits + 1) - hi) / 2;
            double error = hi * (dropped ? 1.1e-18 : 1.3e-29);   // the digits dropped, or 2^-96 for four rounded operations
            if (Math.Abs(lo) + error >= halfUlp)
            {
                return false;
            }

            value = negative ? -hi : hi;
            return true;
        }

        // hi + lo times or over an exact double, to about 100 bits. Dekker's product: no FMA on .NET Framework.
        private static void Scale(ref double hi, ref double lo, double d, bool divide)
        {
            double q = divide ? hi / d : hi;
            double x = divide ? q : hi;
            double t = 134217729.0 * x;
            double xh = t - (t - x);
            double xl = x - xh;
            t = 134217729.0 * d;
            double dh = t - (t - d);
            double dl = d - dh;
            double p = x * d;
            double e = ((xh * dh - p) + xh * dl + xl * dh) + xl * dl;
            if (divide)
            {
                double q2 = ((hi - p) - e + lo) / d;
                hi = q + q2;
                lo = q2 - (hi - q);
            }
            else
            {
                e += lo * d;
                hi = p + e;
                lo = e - (hi - p);
            }
        }
#endif

        /// <summary>
        /// The double nearest to a decimal text (ties to even), given what double.Parse made of it. .NET Framework's
        /// double.Parse is not correctly rounded - one ulp off for about 0.1% of inputs, where .NET Core and Java's
        /// Double.parseDouble are exact - so there the answer and its two neighbours are measured against the text in
        /// integers. Elsewhere, and for a zero, an infinity, a text not of the form [+-]digits[.digits][(e|E)[+-]digits]
        /// or one too long to measure cheaply, the parsed value is returned as it is.
        /// </summary>
        internal static double Nearest(string text, double parsed)
        {
#if NETFRAMEWORK
            if (parsed == 0 || double.IsInfinity(parsed) || double.IsNaN(parsed))
            {
                return parsed;
            }

            // text = mantissa * 10^exponent10, the mantissa's digits without leading zeros
            int i = 0;
            int n = text.Length;
            if (i < n && (text[i] == '+' || text[i] == '-'))
            {
                i++;
            }

            long small = 0;            // the first 18 significant digits
            StringBuilder big = null;  // all of them, once there are more
            int significant = 0;
            int exponent10 = 0;
            bool point = false;
            bool any = false;
            bool sticky = false;       // a dropped digit was not zero
            for (; i < n; i++)
            {
                char c = text[i];
                if (c >= '0' && c <= '9')
                {
                    any = true;
                    if (significant == KeptDigits)
                    {
                        // Past what decides the rounding: only whether a dropped digit is non-zero matters, and its place
                        sticky |= c != '0';
                        if (!point)
                        {
                            exponent10++;
                        }

                        continue;
                    }

                    if (significant > 0 || c != '0')
                    {
                        if (significant < 18)
                        {
                            small = small * 10 + (c - '0');
                        }
                        else
                        {
                            big = big ?? new StringBuilder(small.ToString(CultureInfo.InvariantCulture), n);
                            big.Append(c);
                        }

                        significant++;
                    }

                    if (point)
                    {
                        exponent10--;
                    }
                }
                else if (c == '.' && !point)
                {
                    point = true;
                }
                else
                {
                    break;
                }
            }

            if (i < n)
            {
                if (!any || (text[i] != 'e' && text[i] != 'E') || !int.TryParse(text.Substring(i + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int e))
                {
                    return parsed;
                }

                exponent10 += e;
            }

            if (sticky)
            {
                big.Append('1');   // stands for the dropped tail: on the same side of every halfway point as it
                significant++;
                exponent10--;
            }

            if (significant == 0 || significant > 800 || Math.Abs(exponent10) > 800)
            {
                return parsed;
            }

            // Fifteen digits and a power of ten up to 10^22 are both exact doubles: one correctly rounded operation.
            double magnitude = Math.Abs(parsed);
            if (significant <= 15 && TryExact(small, exponent10, parsed < 0, out double exact))
            {
                return exact;
            }

            BigInteger mantissa = big != null ? BigInteger.Parse(big.ToString(), CultureInfo.InvariantCulture) : new BigInteger(small);

            // The answer and its neighbours c = m * 2^e against x = mantissa * 10^exponent10. With P = 10^max(0, -exponent10),
            // Q = 10^max(0, exponent10) and emin the least of 0 and the three e: |c - x| * P * 2^-emin
            // = |m * 2^(e - emin) * P - mantissa * Q * 2^-emin|, an integer at one scale for all three.
            long bits = BitConverter.DoubleToInt64Bits(magnitude);
            var ms = new long[3];
            var es = new int[3];
            var values = new double[3];
            int count = 0;
            int emin = 0;
            for (long candidate = bits - 1; candidate <= bits + 1; candidate++)
            {
                double c = BitConverter.Int64BitsToDouble(candidate);
                if (candidate < 0 || double.IsInfinity(c))
                {
                    continue;
                }

                int biased = (int)((candidate >> 52) & 0x7FF);
                long m = candidate & 0xFFFFFFFFFFFFFL;
                if (biased != 0)
                {
                    m |= 1L << 52;
                }

                ms[count] = m;
                es[count] = (biased == 0 ? 1 : biased) - 1075;
                values[count] = c;
                emin = Math.Min(emin, es[count]);
                count++;
            }

            BigInteger p = BigInteger.Pow(10, Math.Max(0, -exponent10));
            BigInteger x = (mantissa * BigInteger.Pow(10, Math.Max(0, exponent10))) << -emin;
            double best = magnitude;
            BigInteger bestDistance = BigInteger.MinusOne;
            bool bestEven = false;
            for (int k = 0; k < count; k++)
            {
                BigInteger distance = BigInteger.Abs(((new BigInteger(ms[k]) * p) << (es[k] - emin)) - x);
                bool even = (ms[k] & 1) == 0;
                int order = bestDistance.Sign < 0 ? -1 : distance.CompareTo(bestDistance);
                if (order < 0 || (order == 0 && even && !bestEven))
                {
                    best = values[k];
                    bestDistance = distance;
                    bestEven = even;
                }
            }

            return parsed < 0 ? -best : best;
#else
            return parsed;
#endif
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