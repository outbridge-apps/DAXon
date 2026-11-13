////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using SysBigInteger = System.Numerics.BigInteger;

namespace OutSmart.DAXon.Internal
{
    /// <summary>
    /// Java-semantics helpers for <see cref="System.Numerics.BigInteger"/>: the factories
    /// (Java-specific constructor semantics) and the extension methods whose behaviour is
    /// NOT the BCL default. The pure BCL forwards (Add/Subtract/Multiply/Divide/Remainder/
    /// Negate/Abs/Min/Max/Gcd/Signum/ShiftLeft/ShiftRight — all exactly the corresponding
    /// operator or static) were inlined to operators at their call sites in P6b; only the
    /// documented non-equivalences remain here:
    ///   - Mod: Java mod is always NON-NEGATIVE; .NET % keeps the dividend's sign.
    ///   - FromString: Java rejects whitespace, parses unsigned magnitudes per radix and
    ///     throws NumberFormatException; .NET Parse is culture-sensitive, trims, and its
    ///     HexNumber style is two's-complement ("ff" => -1, Java says 255).
    ///   - IntValue/LongValue: Java NARROWS (keeps low bits); the .NET explicit cast throws.
    ///   - DoubleValue: Java rounds to nearest; the .NET cast truncates above 2^53.
    /// </summary>
    internal static class BigIntegers
    {

        // Java doubleValue(): IEEE round-to-nearest (ties-to-even), overflowing to +/-Infinity.
        // The .NET explicit (double) conversion TRUNCATES toward zero (documented BCL behaviour), so it
        // is up to 1 ULP low in magnitude above 2^53 — e.g. (double)Long.MAX_VALUE gives
        // 9.223372036854775E18 where Java gives 9.223372036854776E18. Match Java: integers that are
        // exactly representable (|v| <= 2^53) use the direct cast; larger magnitudes round correctly by
        // parsing the exact decimal string (on .NET Framework double.Parse is not always correctly rounded,
        // and StringToDouble.Nearest corrects it). net472's double.Parse throws OverflowException past
        // double.MaxValue where Java yields Infinity, so translate that.
        private static readonly SysBigInteger TwoPow53 = new SysBigInteger(9007199254740992L);

        // Factories (Java-specific constructor semantics)

        // Java new BigInteger(String): optional leading +/- then decimal digits ONLY.
        // NOT equivalent to SysBigInteger.Parse(s): .NET Parse is culture-sensitive
        // (NumberFormatInfo signs), allows leading/trailing whitespace, and throws
        // FormatException (which Java-port catch sites for NumberFormatException miss).
        public static SysBigInteger FromString(string s) => FromString(s, 10);

        // Java new BigInteger(String, radix): radix 2..36, digits 0-9 a-z A-Z, optional
        // leading sign, no whitespace; throws java.lang.NumberFormatException on any
        // violation. NOT equivalent to .NET parsing even for radix 16: NumberStyles.
        // HexNumber is two's-complement (Parse("ff") == -1) while Java parses an
        // unsigned magnitude (255); Convert.ToInt64 only supports bases 2/8/10/16 and
        // overflows past 64 bits.
        public static SysBigInteger FromString(string s, int radix)
        {
            if (s == null)
                throw new System.FormatException("null");
            if (radix < 2 || radix > 36)
                throw new System.FormatException("Radix out of range: " + radix);
            int i = 0;
            bool negative = false;
            if (s.Length > 0 && (s[0] == '+' || s[0] == '-')) { negative = s[0] == '-'; i = 1; }
            if (i >= s.Length)
                throw new System.FormatException("Zero length BigInteger");
            if (radix == 10)
            {
                return FromDecimalDigits(s, i, negative);
            }

            SysBigInteger v = SysBigInteger.Zero;
            SysBigInteger r = new SysBigInteger(radix);
            for (; i < s.Length; i++)
            {
                char c = s[i];
                int d;
                if (c >= '0' && c <= '9')
                {
                    d = c - '0';
                }
                else if (c >= 'a' && c <= 'z')
                {
                    d = c - 'a' + 10;
                }
                else if (c >= 'A' && c <= 'Z')
                {
                    d = c - 'A' + 10;
                }
                else
                {
                    d = -1;
                }
                if (d < 0 || d >= radix)
                    throw new System.FormatException("For input string: \"" + s + "\"");
                v = v * r + d;
            }
            return negative ? -v : v;
        }

        // Decimal digits as a number. .NET Framework's BigInteger.Parse and a multiply per digit are quadratic with a large
        // constant (a million digits: 272 s, and nothing looks at the clock): there the digits go nine at a time into
        // the words of the result, in place, the clock read per nine. .NET's own Parse is fast (1 s).
        private static SysBigInteger FromDecimalDigits(string s, int start, bool negative)
        {
            for (int k = start; k < s.Length; k++)
            {
                if (s[k] < '0' || s[k] > '9')
                {
                    throw new System.FormatException("For input string: \"" + s + "\"");
                }
            }
#if NET
            SysBigInteger v = SysBigInteger.Parse(System.MemoryExtensions.AsSpan(s, start), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture);
#else
            int n = s.Length - start;
            uint[] words = new uint[n / 9 + 2];
            int len = 0;
            int i = start;
            int chunk = n % 9 == 0 ? 9 : n % 9;
            while (i < s.Length)
            {
                uint part = 0;
                for (int end = i + chunk; i < end; i++)
                {
                    part = part * 10 + (uint)(s[i] - '0');
                }

                ulong carry = part;
                for (int k = 0; k < len; k++)
                {
                    ulong t = (ulong)words[k] * 1000000000UL + carry;
                    words[k] = (uint)t;
                    carry = t >> 32;
                }

                if (carry != 0)
                {
                    words[len++] = (uint)carry;
                }

                chunk = 9;
                OutSmart.DAXon.Core.Controller.CheckActiveTimeout();
            }

            byte[] bytes = new byte[len * 4 + 1];   // little-endian, a zero top byte: never negative
            System.Buffer.BlockCopy(words, 0, bytes, 0, len * 4);
            SysBigInteger v = new SysBigInteger(bytes);
#endif
            return negative ? -v : v;
        }

        // The decimal digits of v. BigInteger.ToString is quadratic on every runtime, one call the time limit cannot stop
        // (a million digits: 33 s on .NET Framework, 109 s on .NET 10). A long number is divided by 10^9 in place, one
        // pass per nine digits, the clock read per pass: as fast on .NET Framework, three times faster on .NET 10.
        public static string ToDecimalString(SysBigInteger v)
        {
            byte[] bytes = SysBigInteger.Abs(v).ToByteArray();
            if (bytes.Length < 1024)   // under 2466 digits
            {
                return v.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            int len = (bytes.Length + 3) / 4;
            uint[] words = new uint[len];
            System.Buffer.BlockCopy(bytes, 0, words, 0, bytes.Length);
            while (len > 0 && words[len - 1] == 0)
            {
                len--;
            }

            uint[] nines = new uint[len * 10 / 9 + 2];   // 9 decimal digits per 29.9 bits
            int count = 0;
            while (len > 0)
            {
                ulong rem = 0;
                for (int k = len - 1; k >= 0; k--)
                {
                    ulong cur = (rem << 32) | words[k];
                    ulong q = cur / 1000000000UL;
                    words[k] = (uint)q;
                    rem = cur - q * 1000000000UL;
                }

                nines[count++] = (uint)rem;
                while (len > 0 && words[len - 1] == 0)
                {
                    len--;
                }

                OutSmart.DAXon.Core.Controller.CheckActiveTimeout();
            }

            var sb = new System.Text.StringBuilder(count * 9 + 1);
            if (v.Sign < 0)
            {
                sb.Append('-');
            }

            sb.Append(nines[count - 1].ToString(System.Globalization.CultureInfo.InvariantCulture));
            for (int k = count - 2; k >= 0; k--)
            {
                sb.Append(nines[k].ToString("D9", System.Globalization.CultureInfo.InvariantCulture));
            }

            return sb.ToString();
        }

        // Extensions: faithful Java semantics (NOT forwards - documented gaps)

        // Java BigInteger.mod(m) is ALWAYS NON-NEGATIVE and requires m > 0:
        // (-7).mod(3) == 2 in Java but (-7) % 3 == -1 in .NET. Do NOT forward to %.
        public static SysBigInteger Mod(this SysBigInteger v, SysBigInteger m)
        {
            if (m.Sign <= 0)
                throw new System.ArithmeticException("BigInteger: modulus not positive");
            SysBigInteger r = v % m;
            return r.Sign < 0 ? r + m : r;
        }

        // Java intValue()/longValue() NARROW: they keep the low 32/64 bits (two's
        // complement) of an out-of-range value. The .NET explicit (int)/(long) casts
        // THROW OverflowException instead - NOT equivalent, so mask faithfully.
        public static int IntValue(this SysBigInteger v)
        {
            uint low = (uint)(v & 0xFFFFFFFFu);
            return unchecked((int)low);
        }

        public static long LongValue(this SysBigInteger v)
        {
            ulong low = (ulong)(v & ulong.MaxValue);
            return unchecked((long)low);
        }

        public static long LongValueExact(this SysBigInteger v)
        {
            if (v < long.MinValue || v > long.MaxValue)
                throw new System.ArithmeticException("BigInteger out of long range");
            return (long)v;
        }
        public static double DoubleValue(this SysBigInteger v)
        {
            if (v >= -TwoPow53 && v <= TwoPow53)
            {
                return (double)v;
            }

            // Past 2^1031 no double comes near, and the text of such a number is what a time limit cannot stop.
            if (v.ToByteArray().Length > 129)
            {
                return v.Sign < 0 ? double.NegativeInfinity : double.PositiveInfinity;
            }

            try
            {
                string text = v.ToString(System.Globalization.CultureInfo.InvariantCulture);
                return OutSmart.DAXon.Types.StringToDouble.ParseNearest(text, System.Globalization.NumberStyles.Integer);
            }
            catch (System.OverflowException)
            {
                return v.Sign < 0 ? double.NegativeInfinity : double.PositiveInfinity;
            }
        }
    }
}
