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
    ///   - ToString(radix): a division loop (Convert.ToString overflows past 64 bits).
    ///
    /// NOT overridable (instance member always beats an extension): ToByteArray() binds
    /// the BCL little-endian two's-complement form. Java's big-endian toByteArray() is
    /// available as ToJavaByteArray().
    /// </summary>
    internal static class BigIntegers
    {
        // Java BigInteger.TEN (ZERO/ONE map to SysBigInteger.Zero/One; there is no .Ten).
        public static readonly SysBigInteger Ten = new SysBigInteger(10);

        // Java doubleValue(): IEEE round-to-nearest (ties-to-even), overflowing to +/-Infinity.
        // The .NET explicit (double) conversion TRUNCATES toward zero (documented BCL behaviour), so it
        // is up to 1 ULP low in magnitude above 2^53 — e.g. (double)Long.MAX_VALUE gives
        // 9.223372036854775E18 where Java gives 9.223372036854776E18. Match Java: integers that are
        // exactly representable (|v| <= 2^53) use the direct cast; larger magnitudes round correctly by
        // parsing the exact decimal string (double.Parse is correctly rounded). net472's double.Parse
        // throws OverflowException past double.MaxValue where Java yields Infinity, so translate that.
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
            SysBigInteger v = SysBigInteger.Zero;
            SysBigInteger r = new SysBigInteger(radix);
            for (; i < s.Length; i++)
            {
                char c = s[i];
                int d;
                if (c >= '0' && c <= '9')
                    d = c - '0';
                else if (c >= 'a' && c <= 'z') d = c - 'a' + 10;
                else if (c >= 'A' && c <= 'Z') d = c - 'A' + 10;
                else
                    d = -1;
                if (d < 0 || d >= radix)
                    throw new System.FormatException("For input string: \"" + s + "\"");
                v = v * r + d;
            }
            return negative ? -v : v;
        }

        // Java new BigInteger(int signum, byte[] magnitude): BIG-ENDIAN UNSIGNED
        // magnitude plus an explicit sign (-1/0/1). NOT equivalent to
        // new SysBigInteger(byte[]) which is little-endian two's-complement.
        // Java contract: zero-length/all-zero magnitude => ZERO for any signum;
        // signum 0 with a non-zero magnitude => NumberFormatException; signum outside
        // -1..1 => NumberFormatException. (DigestMaker uses (1, sha256) to format hashes.)
        public static SysBigInteger FromSignumMagnitude(int signum, byte[] magnitude)
        {
            if (signum < -1 || signum > 1)
                throw new System.FormatException("Invalid signum value");
            SysBigInteger v = SysBigInteger.Zero;
            if (magnitude != null)
                for (int i = 0; i < magnitude.Length; i++)
                    v = (v << 8) + magnitude[i];
            if (v.IsZero)
                return SysBigInteger.Zero;
            if (signum == 0)
                throw new System.FormatException("signum-magnitude mismatch");
            return signum < 0 ? -v : v;
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

        // Java testBit() rejects a negative index with ArithmeticException; the shifted
        // probe itself is exact because >> is arithmetic (infinite two's complement).
        public static bool TestBit(this SysBigInteger v, int n)
        {
            if (n < 0)
                throw new System.ArithmeticException("Negative bit address");
            return !((v >> n) & SysBigInteger.One).IsZero;
        }

        // Java bitLength(): bits in the minimal two's-complement form EXCLUDING the sign
        // bit. For n>0 that is floor(log2 n)+1; for n<0 it is ceil(log2(-n)) (one LESS
        // for exact powers of two); for 0 it is 0; bitLength(-1) == 0. (The old wrapper's
        // log-based formula broke on 0 and on negative powers of two.)
        public static int BitLength(this SysBigInteger v)
        {
            if (v.IsZero)
                return 0;
            SysBigInteger m = v.Sign < 0 ? -v : v;
            int bits = -1;
            for (SysBigInteger t = m; !t.IsZero; t >>= 1) // floor(log2 m)
            {
                bits++;
            }
            bool powerOfTwo = (m & (m - SysBigInteger.One)).IsZero;
            return v.Sign > 0 ? bits + 1 : (powerOfTwo ? bits : bits + 1);
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

        // Java intValueExact()/longValueExact(): java.lang.ArithmeticException on overflow
        // (the .NET cast would throw OverflowException - different type; thrown as
        // System.ArithmeticException per the tree's catch convention).
        public static int IntValueExact(this SysBigInteger v)
        {
            if (v < int.MinValue || v > int.MaxValue)
                throw new System.ArithmeticException("BigInteger out of int range");
            return (int)v;
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

            try
            {
                return double.Parse(
                    v.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture);
            }
            catch (System.OverflowException)
            {
                return v.Sign < 0 ? double.NegativeInfinity : double.PositiveInfinity;
            }
        }

        // Java toString(radix): out-of-range radix silently falls back to 10 (Java uses
        // Character.MIN_RADIX/MAX_RADIX), lowercase digits, leading '-' for negatives.
        // The old wrapper funneled through Convert.ToString((long)v, radix), which threw
        // for values beyond 64 bits (e.g. DigestMaker's 256-bit hashes) and only
        // supported bases 2/8/10/16 - implemented faithfully with a division loop.
        // (Named ToString(int) is callable as an extension because the instance method
        // overloads take string/IFormatProvider, never int.)
        public static string ToString(this SysBigInteger v, int radix)
        {
            if (radix < 2 || radix > 36) // Java falls back to base 10
            {
                radix = 10;
            }
            if (v.IsZero)
                return "0";
            bool negative = v.Sign < 0;
            SysBigInteger m = negative ? -v : v;
            SysBigInteger r = new SysBigInteger(radix);
            var sb = new System.Text.StringBuilder();
            while (!m.IsZero)
            {
                SysBigInteger rem;
                m = SysBigInteger.DivRem(m, r, out rem);
                int d = (int)rem;
                sb.Insert(0, (char)(d < 10 ? '0' + d : 'a' + d - 10));
            }
            if (negative)
                sb.Insert(0, '-');
            return sb.ToString();
        }

        // Java toByteArray(): minimal BIG-ENDIAN two's complement (incl. sign bit). The
        // BCL instance ToByteArray() is the same representation LITTLE-endian, and an
        // extension can never shadow an instance method - so faithful Java byte order
        // gets its own name. (Call sites that kept .ToByteArray() behave exactly like
        // the old wrapper, which also forwarded to the little-endian BCL form.)
        public static byte[] ToJavaByteArray(this SysBigInteger v)
        {
            byte[] little = v.ToByteArray();
            byte[] big = new byte[little.Length];
            for (int i = 0; i < little.Length; i++)
                big[i] = little[little.Length - 1 - i];
            return big;
        }
    }
}
