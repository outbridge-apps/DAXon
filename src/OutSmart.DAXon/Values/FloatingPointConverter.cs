////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Internal;
using System;
using System.Numerics;
using System.Globalization;
namespace OutSmart.DAXon.Values
{
    internal sealed class FloatingPointConverter
    {
        public const long DOUBLE_SIGN_MASK = unchecked((long)0x8000000000000000);
        private const long doubleExpMask = 0x7ff0000000000000;
        private const int doubleExpShift = 52;
        private const int doubleExpBias = 1023;
        private const long doubleFractMask = 0xfffffffffffff;
        public const int FLOAT_SIGN_MASK = unchecked((int)0x80000000);

        private const int floatExpMask = 0x7f800000;
        private const int floatExpShift = 23;
        private const int floatExpBias = 127;
        private const int floatFractMask = 0x7fffff;

        /// <summary>
        /// char array holding the characters for the string "-Infinity".
        /// </summary>
        private static readonly string NEGATIVE_INFINITY = "-INF";
        /// <summary>
        /// char array holding the characters for the string "Infinity".
        /// </summary>
        private static readonly string POSITIVE_INFINITY = "INF";
        /// <summary>
        /// char array holding the characters for the string "NaN".
        /// </summary>
        private static readonly string NaN = "NaN";
        private static readonly char[] charForDigit = new[]
        {
            '0',
            '1',
            '2',
            '3',
            '4',
            '5',
            '6',
            '7',
            '8',
            '9'
        };
        private static readonly BigInteger TEN = new BigInteger(10);
        private static readonly BigInteger NINE = new BigInteger(9);

        /// <summary>Reinterpret a float's IEEE-754 bits as an int (net472 lacks BitConverter.SingleToInt32Bits).</summary>
        public static int SingleToInt32Bits(float f) => BitConverter.ToInt32(BitConverter.GetBytes(f), 0);
        public static UnicodeBuilder AppendInt(UnicodeBuilder s, int i)
        {

            // TODO: this elaborate machinery is only being used to output the exponent of a floating point number,
            //  which never has more than 3 digits...
            if (i < 0)
            {
                if (i == int.MinValue)
                {

                    //cannot make this positive due to integer overflow
                    s.Append("-2147483648");
                    return s;
                }

                s.Append('-');
                i = -i;
            }

            int c;
            if (i < 10)
            {

                //one digit
                s.Append(charForDigit[i]);
                return s;
            }
            else if (i < 100)
            {

                //two digits
                s.Append(charForDigit[i / 10]);
                s.Append(charForDigit[i % 10]);
                return s;
            }
            else if (i < 1000)
            {

                //three digits
                s.Append(charForDigit[i / 100]);
                s.Append(charForDigit[(c = i % 100) / 10]);
                s.Append(charForDigit[c % 10]);
                return s;
            }
            else if (i < 10000)
            {

                //four digits
                s.Append(charForDigit[i / 1000]);
                s.Append(charForDigit[(c = i % 1000) / 100]);
                s.Append(charForDigit[(c %= 100) / 10]);
                s.Append(charForDigit[c % 10]);
                return s;
            }
            else if (i < 100000)
            {

                //five digits
                s.Append(charForDigit[i / 10000]);
                s.Append(charForDigit[(c = i % 10000) / 1000]);
                s.Append(charForDigit[(c %= 1000) / 100]);
                s.Append(charForDigit[(c %= 100) / 10]);
                s.Append(charForDigit[c % 10]);
                return s;
            }
            else if (i < 1000000)
            {

                //six digits
                s.Append(charForDigit[i / 100000]);
                s.Append(charForDigit[(c = i % 100000) / 10000]);
                s.Append(charForDigit[(c %= 10000) / 1000]);
                s.Append(charForDigit[(c %= 1000) / 100]);
                s.Append(charForDigit[(c %= 100) / 10]);
                s.Append(charForDigit[c % 10]);
                return s;
            }
            else if (i < 10000000)
            {

                //seven digits
                s.Append(charForDigit[i / 1000000]);
                s.Append(charForDigit[(c = i % 1000000) / 100000]);
                s.Append(charForDigit[(c %= 100000) / 10000]);
                s.Append(charForDigit[(c %= 10000) / 1000]);
                s.Append(charForDigit[(c %= 1000) / 100]);
                s.Append(charForDigit[(c %= 100) / 10]);
                s.Append(charForDigit[c % 10]);
                return s;
            }
            else if (i < 100000000)
            {

                //eight digits
                s.Append(charForDigit[i / 10000000]);
                s.Append(charForDigit[(c = i % 10000000) / 1000000]);
                s.Append(charForDigit[(c %= 1000000) / 100000]);
                s.Append(charForDigit[(c %= 100000) / 10000]);
                s.Append(charForDigit[(c %= 10000) / 1000]);
                s.Append(charForDigit[(c %= 1000) / 100]);
                s.Append(charForDigit[(c %= 100) / 10]);
                s.Append(charForDigit[c % 10]);
                return s;
            }
            else if (i < 1000000000)
            {

                //nine digits
                s.Append(charForDigit[i / 100000000]);
                s.Append(charForDigit[(c = i % 100000000) / 10000000]);
                s.Append(charForDigit[(c %= 10000000) / 1000000]);
                s.Append(charForDigit[(c %= 1000000) / 100000]);
                s.Append(charForDigit[(c %= 100000) / 10000]);
                s.Append(charForDigit[(c %= 10000) / 1000]);
                s.Append(charForDigit[(c %= 1000) / 100]);
                s.Append(charForDigit[(c %= 100) / 10]);
                s.Append(charForDigit[c % 10]);
                return s;
            }
            else
            {

                //ten digits
                s.Append(charForDigit[i / 1000000000]);
                s.Append(charForDigit[(c = i % 1000000000) / 100000000]);
                s.Append(charForDigit[(c %= 100000000) / 10000000]);
                s.Append(charForDigit[(c %= 10000000) / 1000000]);
                s.Append(charForDigit[(c %= 1000000) / 100000]);
                s.Append(charForDigit[(c %= 100000) / 10000]);
                s.Append(charForDigit[(c %= 10000) / 1000]);
                s.Append(charForDigit[(c %= 1000) / 100]);
                s.Append(charForDigit[(c %= 100) / 10]);
                s.Append(charForDigit[c % 10]);
                return s;
            }
        }

        private static void Fppfpp(UnicodeBuilder sb, int e, long f, int p)
        {
            long R = f << Math.Max(e - p, 0);
            long S = 1L << Math.Max(0, -(e - p));
            long Mminus = 1L << Math.Max(e - p, 0);
            long Mplus = Mminus;
            bool initial = true;

            // simpleFixup
            if (f == 1L << (p - 1))
            {
                Mplus = Mplus << 1;
                R = R << 1;
                S = S << 1;
            }

            int k = 0;
            while (R < (S + 9) / 10)
            {

                // (S+9)/10 == ceiling(S/10)
                k--;
                R = R * 10;
                Mminus = Mminus * 10;
                Mplus = Mplus * 10;
            }

            while (2 * R + Mplus >= 2 * S)
            {
                S = S * 10;
                k++;
            }

            for (int z = k; z < 0; z++)
            {
                if (initial)
                {
                    sb.Append("0.");
                }

                initial = false;
                sb.Append('0');
            }


            // end simpleFixup
            //int H = k-1;
            bool low;
            bool high;
            int U;
            while (true)
            {
                k--;
                long R10 = R * 10;
                U = (int)(R10 / S);
                R = R10 - (U * S); // = R*10 % S, but faster - saves a division
                Mminus = Mminus * 10;
                Mplus = Mplus * 10;
                low = 2 * R < Mminus;
                high = 2 * R > 2 * S - Mplus;
                if (low || high)
                    break;
                if (k == -1)
                {
                    if (initial)
                    {
                        sb.Append('0');
                    }

                    sb.Append('.');
                }

                sb.Append(charForDigit[U]);
                initial = false;
            }

            if (high && (!low || 2 * R > S))
            {
                U++;
            }

            if (k == -1)
            {
                if (initial)
                {
                    sb.Append('0');
                }

                sb.Append('.');
            }

            sb.Append(charForDigit[U]);
            for (int z = 0; z < k; z++)
            {
                sb.Append('0');
            }
        }

        private static void FppfppBig(UnicodeBuilder sb, int e, long f, int p)
        {

            //long R = f << Math.max(e-p, 0);
            BigInteger R = new BigInteger(f) << Math.Max(e - p, 0);

            //long S = 1L << Math.max(0, -(e-p));
            BigInteger S = BigInteger.One << Math.Max(0, -(e - p));

            //long Mminus = 1 << Math.max(e-p, 0);
            BigInteger Mminus = BigInteger.One << Math.Max(e - p, 0);

            //long Mplus = Mminus;
            BigInteger Mplus = Mminus;
            bool initial = true;

            // simpleFixup
            if (f == 1L << (p - 1))
            {
                Mplus = Mplus << 1;
                R = R << 1;
                S = S << 1;
            }

            int k = 0;
            while (R.CompareTo((S + NINE) / TEN) < 0)
            {

                // (S+9)/10 == ceiling(S/10)
                k--;
                R = R * TEN;
                Mminus = Mminus * TEN;
                Mplus = Mplus * TEN;
            }

            while (((R << 1) + Mplus).CompareTo(S << 1) >= 0)
            {
                S = S * TEN;
                k++;
            }

            for (int z = k; z < 0; z++)
            {
                if (initial)
                {
                    sb.Append("0.");
                }

                initial = false;
                sb.Append('0');
            }


            bool low;
            bool high;
            int U;
            while (true)
            {
                k--;
                BigInteger R10 = R * TEN;
                U = (R10 / S).IntValue();
                R = R10.Mod(S);
                Mminus = Mminus * TEN;
                Mplus = Mplus * TEN;
                BigInteger R2 = R << 1;
                low = R2.CompareTo(Mminus) < 0;
                high = R2.CompareTo((S << 1) - Mplus) > 0;
                if (low || high)
                    break;
                if (k == -1)
                {
                    if (initial)
                    {
                        sb.Append('0');
                    }

                    sb.Append('.');
                }

                sb.Append(charForDigit[U]);
                initial = false;
            }

            if (high && (!low || (R << 1).CompareTo(S) > 0))
            {
                U++;
            }

            if (k == -1)
            {
                if (initial)
                {
                    sb.Append('0');
                }

                sb.Append('.');
            }

            sb.Append(charForDigit[U]);
            for (int z = 0; z < k; z++)
            {
                sb.Append('0');
            }
        }

        // Long-arithmetic twin of the BigInteger Dragon4 body in FppfppExponential: identical
        // recurrence, so the digits are char-identical (differential-tested in javacompat-tests).
        // Digits accumulate MSB-first into a packed long; any *10 that could overflow bails and
        // the caller reruns the BigInteger form. In practice this covers |value| from ~0.008 to
        // ~1e17 — the entire format-number / string(double) hot zone.
        private static bool TryShortestDigitsCore(int e, long f, int p, out long digits, out int n, out int H)
        {
            digits = 0;
            n = 0;
            H = 0;
            int shiftR = Math.Max(e - p, 0);
            int shiftS = Math.Max(0, -(e - p));
            if (shiftR > 8 || shiftS > 60)
            {
                return false;
            }

            const long SAFE = long.MaxValue / 10;
            long R = f << shiftR;
            long S = 1L << shiftS;
            long Mminus = 1L << shiftR;
            long Mplus = Mminus;

            if (f == 1L << (p - 1))
            {
                Mplus <<= 1;
                R <<= 1;
                S <<= 1;
            }

            int k = 0;
            while (R < (S + 9) / 10)
            {
                if (R > SAFE / 4 || Mplus > SAFE / 4)
                {
                    return false;
                }

                k--;
                R *= 10;
                Mminus *= 10;
                Mplus *= 10;
            }

            while (2 * R + Mplus >= 2 * S)
            {
                if (S > SAFE / 2)
                {
                    return false;
                }

                S *= 10;
                k++;
            }

            if (S > SAFE)
            {
                return false;
            }

            H = k - 1;
            bool low;
            bool high;
            int U;
            while (true)
            {
                if (Mminus > SAFE || Mplus > SAFE || n == 18)
                {
                    return false;
                }

                long R10 = R * 10;
                U = (int)(R10 / S);
                R = R10 - U * S;
                Mminus *= 10;
                Mplus *= 10;
                low = 2 * R < Mminus;
                high = 2 * R > 2 * S - Mplus;
                if (low || high)
                    break;
                digits = digits * 10 + U;
                n++;
            }

            if (high && (!low || 2 * R > S))
            {
                U++;
            }

            digits = digits * 10 + U;
            n++;
            return true;
        }

        // Shortest-form digits of a positive normal double (Dragon4, long path only).
        // The decimal value is digits * 10^(H - n + 1); false when the long twin bails.
        internal static bool TryShortestDigits(double d, out long digits, out int n, out int H)
        {
            digits = 0;
            n = 0;
            H = 0;
            long bits = BitConverter.DoubleToInt64Bits(d);
            long rawExp = (bits & doubleExpMask) >> doubleExpShift;
            if (d <= 0 || rawExp == 0 || rawExp == 0x7ff)
            {
                return false;
            }

            long fraction = (1L << 52) | (bits & doubleFractMask);
            return TryShortestDigitsCore((int)rawExp - doubleExpBias, fraction, 52, out digits, out n, out H);
        }

        private static bool TryFppfppExponentialLong(UnicodeBuilder sb, int e, long f, int p)
        {
            if (!TryShortestDigitsCore(e, f, p, out long digits, out int n, out int H))
            {
                return false;
            }

            if (n == 1)
            {
                sb.Append(charForDigit[(int)digits]);
                sb.Append(".0");
            }
            else
            {
                long div = 1;
                for (int i = 1; i < n; i++)
                {
                    div *= 10;
                }

                sb.Append(charForDigit[(int)(digits / div)]);
                sb.Append('.');
                while (div > 1)
                {
                    digits %= div;
                    div /= 10;
                    sb.Append(charForDigit[(int)(digits / div)]);
                }
            }

            sb.Append('E');
            AppendInt(sb, H);
            return true;
        }

        private static void FppfppExponential(UnicodeBuilder sb, int e, long f, int p)
        {
            if (TryFppfppExponentialLong(sb, e, f, p))
            {
                return;
            }

            //long R = f << Math.max(e-p, 0);
            BigInteger R = new BigInteger(f) << Math.Max(e - p, 0);

            //long S = 1L << Math.max(0, -(e-p));
            BigInteger S = BigInteger.One << Math.Max(0, -(e - p));

            //long Mminus = 1 << Math.max(e-p, 0);
            BigInteger Mminus = BigInteger.One << Math.Max(e - p, 0);

            //long Mplus = Mminus;
            BigInteger Mplus = Mminus;
            bool initial = true;
            bool doneDot = false;

            // simpleFixup
            if (f == 1L << (p - 1))
            {
                Mplus = Mplus << 1;
                R = R << 1;
                S = S << 1;
            }

            int k = 0;
            while (R.CompareTo((S + NINE) / TEN) < 0)
            {

                // (S+9)/10 == ceiling(S/10)
                k--;
                R = R * TEN;
                Mminus = Mminus * TEN;
                Mplus = Mplus * TEN;
            }

            while (((R << 1) + Mplus).CompareTo(S << 1) >= 0)
            {
                S = S * TEN;
                k++;
            }


            // end simpleFixup
            int H = k - 1;
            bool low;
            bool high;
            int U;
            while (true)
            {
                k--;
                BigInteger R10 = R * TEN;
                U = (R10 / S).IntValue();
                R = R10.Mod(S);
                Mminus = Mminus * TEN;
                Mplus = Mplus * TEN;
                BigInteger R2 = R << 1;
                low = R2.CompareTo(Mminus) < 0;
                high = R2.CompareTo((S << 1) - Mplus) > 0;
                if (low || high)
                    break;
                sb.Append(charForDigit[U]);
                if (initial)
                {
                    sb.Append('.');
                    doneDot = true;
                }

                initial = false;
            }

            if (high && (!low || (R << 1).CompareTo(S) > 0))
            {
                U++;
            }

            sb.Append(charForDigit[U]);
            if (!doneDot)
            {
                sb.Append(".0");
            }

            sb.Append('E');
            AppendInt(sb, H);
        }

        //@CSharpReplaceBody(code = "return OutSmart.DAXon.Text.BMPString.of(d.ToString(System.Globalization.CultureInfo.InvariantCulture));") // for now...
        public static UnicodeString ConvertDouble(double d, bool useExponential)
        {
            UnicodeBuilder s = new UnicodeBuilder(32);
            if (d == double.NegativeInfinity)
            {
                s.AppendLatin(NEGATIVE_INFINITY);
            }
            else if (d == double.PositiveInfinity)
            {
                s.AppendLatin(POSITIVE_INFINITY);
            }
            else if (double.IsNaN(d))
            {
                s.AppendLatin(NaN);
            }
            else if (d == 0)
            {
                if ((BitConverter.DoubleToInt64Bits(d) & DOUBLE_SIGN_MASK) != 0)
                {
                    s.Append('-');
                }

                s.Append('0');
                if (useExponential)
                {
                    s.Append(".0E0");
                }
            }
            else if (d == double.MaxValue)
            {
                s.Append("1.7976931348623157E308");
            }
            else if (d == -double.MaxValue)
            {
                s.Append("-1.7976931348623157E308");
            }
            else if (d == double.Epsilon)
            {
                s.Append("4.9E-324");
            }
            else if (d == -double.Epsilon)
            {
                s.Append("-4.9E-324");
            }
            else
            {
                if (d < 0)
                {
                    s.Append('-');
                    d = -d;
                }

                long bits = BitConverter.DoubleToInt64Bits(d);
                long fraction = (1L << 52) | (bits & doubleFractMask);
                long rawExp = (bits & doubleExpMask) >> doubleExpShift;
                int exp = (int)rawExp - doubleExpBias;
                if (rawExp == 0)
                {

                    // subnormal double: fall back to the round-trip shortest representation
                    s.Append(d.ToString("R", CultureInfo.InvariantCulture));
                    return s.ToUnicodeString();
                }

                if (useExponential)
                {
                    FppfppExponential(s, exp, fraction, 52);
                }
                else
                {
                    if (d <= 0.01)
                    {
                        FppfppBig(s, exp, fraction, 52);
                    }
                    else
                    {
                        Fppfpp(s, exp, fraction, 52);
                    }
                }
            }

            return s.ToUnicodeString();
        }

        //@CSharpReplaceBody(code="return s.append(f.ToString(System.Globalization.CultureInfo.InvariantCulture)).toUnicodeString();")  // for now...
        public static UnicodeString AppendFloat(UnicodeBuilder s, float f, bool forceExponential)
        {
            if (f == float.NegativeInfinity)
            {
                s.Append(NEGATIVE_INFINITY);
            }
            else if (f == float.PositiveInfinity)
            {
                s.Append(POSITIVE_INFINITY);
            }
            else if (float.IsNaN(f))
            {
                s.Append(NaN);
            }
            else if (f == 0)
            {
                if ((SingleToInt32Bits(f) & FLOAT_SIGN_MASK) != 0)
                {
                    s.Append('-');
                }

                s.Append('0');
            }
            else if (f == float.MaxValue)
            {
                s.AppendLatin("3.4028235E38");
            }
            else if (f == -float.MaxValue)
            {
                s.AppendLatin("-3.4028235E38");
            }
            else if (f == float.Epsilon)
            {
                s.AppendLatin("1.4E-45");
            }
            else if (f == -float.Epsilon)
            {
                s.AppendLatin("-1.4E-45");
            }
            else
            {
                if (f < 0)
                {
                    s.Append('-');
                    f = -f;
                }

                int bits = SingleToInt32Bits(f);
                int fraction = (1 << 23) | (bits & floatFractMask);
                int rawExp = ((bits & floatExpMask) >> floatExpShift);
                int exp = rawExp - floatExpBias;
                int precision = 23;
                if (rawExp == 0)
                {

                    // subnormal float: fall back to the round-trip shortest representation
                    s.Append(f.ToString("R", CultureInfo.InvariantCulture));
                    return s.ToUnicodeString();
                }

                if (forceExponential || (f >= 1000000 || f < 1E-06F))
                {
                    FppfppExponential(s, exp, fraction, precision);
                }
                else
                {
                    Fppfpp(s, exp, fraction, precision);
                }
            }

            return s.ToUnicodeString();
        } //    public static void main(String[] args) {
        //        if (args.length > 0 && args[0].equals("F")) {
        //                for (int i=1; i<1000; i++) {
        //                    int p=gen.nextInt(999*i*i);
        //                    int q=gen.nextInt(999*i*i);
        //                    String input = (p + "." + q);
        //        } else {
        //                long start = System.currentTimeMillis();
        //                for (int i=1; i<100000; i++) {
        //                    //int p=gen.nextInt(999*i*i);
        //                    int q=gen.nextInt(999*i);
        //                    //String input = (p + "." + q);
        //                    String input = "0.000" + q;
        //                    //System.Console.Error.println("input: " + input + " output: " + sb.toString() + " java: " + f);
    }
}