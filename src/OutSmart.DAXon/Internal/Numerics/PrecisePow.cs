////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Runtime.CompilerServices;

namespace OutSmart.DAXon.Internal.Numerics
{
    // pow as IEEE 754-2008 defines it, correctly rounded, the same on every runtime. The platform's pow misses by one ulp
    // now and then (10^23), differently per runtime. Double-double arithmetic, some 100 bits: a result rounds wrongly only
    // if its bits past the 53rd run the same for 45 more. Exact powers go through exact steps, so a tie is a real tie.
    internal static class PrecisePow
    {
        private const double Split = 134217729.0;   // 2^27 + 1
        private const int LargestIntegerPower = 65536;   // squaring doubles an error: 2^16 steps leave 2^-88
        private const double OverflowLog = 710;      // above ln(MaxValue)
        private const double UnderflowLog = -746;    // below ln(2^-1075)

        private static readonly Dd Ln2 = new Dd(6.931471805599452862e-01, 2.319046813846299558e-17);
        private const double Ln2Third = 5.707708438416212066e-34;

        private static readonly Dd[] InverseOdd = MakeInverseOdd(32);
        private static readonly Dd[] InverseFactorial = MakeInverseFactorial(16);

        // Tables that leave the series a few short terms: ln of the multiples of 1/256 a mantissa folds to, and 2^(j/64).
        // Built once by the long series, to the same some 100 bits.
        private const int LogGrid = 256;
        private const int ExpSteps = 64;
        private static readonly Dd[] LnGrid = MakeLnGrid();
        private static readonly Dd[] TwoToStep = MakeTwoToStep();
        private static readonly Dd Ln2Step = new Dd(Ln2.Hi / ExpSteps, Ln2.Lo / ExpSteps);

        private readonly struct Dd
        {
            public readonly double Hi;
            public readonly double Lo;

            public Dd(double hi, double lo)
            {
                Hi = hi;
                Lo = lo;
            }
        }

#if NET
        // Optimized from the first call: at the first tier of .NET 10 the struct arithmetic ran 5x slower, and for how long varied.
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
        public static double Pow(double x, double y)
        {
            return Compute(x, y, true);
        }

        // The 100-bit path alone, for a differential of the quick one.
        internal static double PowWithoutQuickPath(double x, double y)
        {
            return Compute(x, y, false);
        }

#if NET
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
        private static double Compute(double x, double y, bool quickPath)
        {
            if (double.IsNaN(x) || double.IsInfinity(x) || double.IsNaN(y) || double.IsInfinity(y) || x == 0 || y == 0)
            {
                return Math.Pow(x, y);
            }

            bool integer = Math.Floor(y) == y;
            if (x < 0 && !integer)
            {
                return double.NaN;
            }

            double sign = x < 0 && Math.Abs(y) < 9007199254740992.0 && ((long)y & 1) != 0 ? -1 : 1;
            double a = Math.Abs(x);
            if (a == 1)
            {
                return sign;
            }

            // Past the range of doubles whatever the digits. The margin is far above the error of the platform's log.
            double estimate = y * Math.Log(a);
            if (estimate > OverflowLog)
            {
                return sign * double.PositiveInfinity;
            }

            if (estimate < UnderflowLog)
            {
                return sign * 0.0;
            }

            int exponent = Normalize(ref a);
            for (int roots = 0; roots <= 3; roots++)
            {
                double power = y * (1 << roots);
                if (Math.Floor(power) == power && Math.Abs(power) <= LargestIntegerPower)
                {
                    return sign * Power(a, exponent, roots, (int)power);
                }
            }

            double quick = quickPath ? QuickPow(a, exponent, y) : double.NaN;
            return sign * (double.IsNaN(quick) ? ExpNear(Multiply(y, LogNear(a, exponent))) : quick);
        }

        // The same steps to some 70 bits, most of them in plain doubles; NaN when the result lies too near a rounding
        // boundary for that to settle it (about one call in 10^4), or beyond the normal range. Then the 100-bit path decides.
#if NET
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
        private static double QuickPow(double m, int exponent, double y)
        {
            if (m > 1.4142135623730951)
            {
                m /= 2;
                exponent++;
            }

            double c = Math.Round(m * LogGrid) / LogGrid;
            double u = m - c;   // exact
            Dd v = TwoSum(m, c);
            double sh = u / v.Hi;
            Dd sv = TwoProduct(sh, v.Hi);
            double sl = ((u - sv.Hi) - sv.Lo - sh * v.Lo) / v.Hi;

            // 2 atanh(s) = 2s + 2s^3/3 + ...: past 2s the terms are under 1e-6 of it, |s| < 0.0014, so a double holds them.
            double q = sh * sh;
            double odd = sh * q * (2.0 / 3 + q * (2.0 / 5 + q * (2.0 / 7 + q * (2.0 / 9))));
            Dd log = Add(LnGrid[(int)(c * LogGrid)], FastTwoSum(2 * sh, 2 * sl + odd));
            if (exponent != 0)
            {
                log = Add(Multiply(exponent, Ln2), log);
            }

            Dd z = Multiply(y, log);
            if (z.Hi > 709 || z.Hi < -708)
            {
                return double.NaN;
            }

            double j = Math.Round(z.Hi / Ln2Step.Hi);
            Dd r = Add(z, Multiply(-j, Ln2Step));

            // e^r - 1 = r + r^2/2 + r^3/6 + ..., |r| <= ln2/128: r^2 exactly, the rest and the cross term r_hi r_lo as doubles.
            double rh = r.Hi;
            Dd r2 = TwoProduct(rh, rh);
            double rest = rh * r2.Hi * (1.0 / 6 + rh * (1.0 / 24 + rh * (1.0 / 120 + rh * (1.0 / 720 + rh / 5040)))) + rh * r.Lo;
            Dd s = Add(new Dd(rh, r.Lo), new Dd(r2.Hi / 2, r2.Lo / 2 + rest));
            long steps = (long)j;
            Dd t = TwoToStep[(int)(steps & (ExpSteps - 1))];
            Dd e = Add(t, Multiply(t, s));

            // The error: 2^-70 of z from the log, 2^-74 from the series and the doubles; twice that, to be sure.
            double eps = (Math.Abs(z.Hi) + 0.0625) * 1.7e-21;   // 2^-69 per unit of z
            long bits = BitConverter.DoubleToInt64Bits(e.Hi);
            double half = BitConverter.Int64BitsToDouble(((bits >> 52) - 53) << 52);   // half an ulp of e.Hi (in [0.7, 3))
            if ((bits & 0xFFFFFFFFFFFFFL) == 0)
            {
                half /= 2;   // just below a power of two the steps are half as big
            }

            if (half - Math.Abs(e.Lo) <= eps * e.Hi)
            {
                return double.NaN;
            }

            return ScaleBy(e.Hi, steps >> 6);
        }

        // ln(m * 2^exponent) as Log has it, from the nearest grid point c: ln c + 2 atanh((m-c)/(m+c)), |s| < 0.0014.
#if NET
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
        private static Dd LogNear(double m, int exponent)
        {
            if (m > 1.4142135623730951)
            {
                m /= 2;
                exponent++;
            }

            double c = Math.Round(m * LogGrid) / LogGrid;
            Dd log = LnGrid[(int)(c * LogGrid)];
            if (m != c)
            {
                Dd s = Divide(new Dd(m - c, 0), TwoSum(m, c));   // m - c is exact: they are within a factor of two
                Dd q = Multiply(s, s);
                double h = q.Hi;   // below 2e-6: past q^2/5 the terms are under 1e-18, and a double holds their digits
                double tail = h * (1.0 / 7 + h * (1.0 / 9 + h * (1.0 / 11 + h * (1.0 / 13))));
                Dd sum = Add(new Dd(1, 0), Multiply(q, Add(InverseOdd[1], Multiply(q, Add(InverseOdd[2], new Dd(tail, 0))))));

                Dd atanh = Multiply(s, sum);
                log = Add(log, new Dd(atanh.Hi * 2, atanh.Lo * 2));
            }

            return exponent == 0 ? log : Add(Multiply(exponent, Ln2), log);
        }

        // e^z as Exp has it: 2^(j/64) from the table times e^r, |r| <= ln2/128.
#if NET
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
        private static double ExpNear(Dd z)
        {
            double j = Math.Round(z.Hi / Ln2Step.Hi);
            Dd r = Add(z, Multiply(-j, Ln2Step));
            r = TwoSum(r.Hi, r.Lo - j * (Ln2Third / ExpSteps));
            // e^r - 1 = r + r^2 (1/2 + r (1/6 + ... r (1/720 + tail))): past r^6/720 the terms are under 3e-20, a double's digits
            double h = r.Hi;
            double tail = h * (1.0 / 5040 + h * (1.0 / 40320 + h * (1.0 / 362880 + h * (1.0 / 3628800 + h * (1.0 / 39916800 + h / 479001600)))));
            Dd p = Add(InverseFactorial[6], new Dd(tail, 0));
            for (int n = 5; n >= 2; n--)
            {
                p = Add(InverseFactorial[n], Multiply(r, p));
            }

            Dd s = Add(r, Multiply(Multiply(r, r), p));

            long steps = (long)j;
            Dd t = TwoToStep[(int)(steps & (ExpSteps - 1))];
            return Scale(Add(t, Multiply(t, s)), steps >> 6);   // a floor division, for negative steps too
        }

        private static Dd[] MakeLnGrid()
        {
            var table = new Dd[LogGrid * 3 / 2];   // the folded mantissa lies in [0.7071, 1.4143)
            for (int k = LogGrid * 7 / 10; k < table.Length; k++)
            {
                double c = (double)k / LogGrid;
                table[k] = c < 1 ? Log(c * 2, -1) : Log(c, 0);
            }

            return table;
        }

        private static Dd[] MakeTwoToStep()
        {
            var table = new Dd[ExpSteps];
            for (int j = 0; j < ExpSteps; j++)
            {
                double f = (double)j / ExpSteps;
                table[j] = ExpOfSmall(Add(Multiply(f, Ln2), new Dd(f * Ln2Third, 0)));
            }

            return table;
        }

        // The value is m * 2^exponent with m in [1, 2): m comes back through the reference.
#if NET
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
        private static int Normalize(ref double m)
        {
            int exponent = 0;
            if (m < 2.2250738585072014E-308)
            {
                m *= 18014398509481984.0;   // 2^54: out of the subnormals
                exponent = -54;
            }

            long bits = BitConverter.DoubleToInt64Bits(m);
            exponent += (int)((bits >> 52) & 0x7FF) - 1023;
            m = BitConverter.Int64BitsToDouble((bits & 0xFFFFFFFFFFFFFL) | 0x3FF0000000000000L);
            return exponent;
        }

        // (m * 2^exponent)^(power / 2^roots).
#if NET
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
        private static double Power(double m, int exponent, int roots, int power)
        {
            long whole = exponent;
            int extra = (int)(whole & ((1 << roots) - 1));   // what keeps the exponent a multiple of 2^roots
            whole -= extra;
            Dd b = new Dd(m * (1 << extra), 0);
            for (int i = 0; i < roots; i++)
            {
                b = SquareRoot(b);
            }

            whole >>= roots;
            long bExponent = whole;
            long rExponent = 0;
            Dd r = new Dd(1, 0);
            int n = Math.Abs(power);
            while (true)
            {
                if ((n & 1) != 0)
                {
                    r = Multiply(r, b);
                    rExponent += bExponent;
                    r = Halved(r, ref rExponent);
                }

                n >>= 1;
                if (n == 0)
                {
                    break;
                }

                b = Multiply(b, b);
                bExponent *= 2;
                b = Halved(b, ref bExponent);
            }

            if (power < 0)
            {
                r = Divide(new Dd(1, 0), r);
                rExponent = -rExponent;
            }

            return Scale(r, rExponent);
        }

        // Brings a value in [1, 256) below 2.
#if NET
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
        private static Dd Halved(Dd v, ref long exponent)
        {
            while (v.Hi >= 2)
            {
                v = new Dd(v.Hi / 2, v.Lo / 2);
                exponent++;
            }

            return v;
        }

        // ln(m * 2^exponent): 2 atanh((m-1)/(m+1)) with m around 1, so no digit is lost when the value itself is near 1.
#if NET
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
        private static Dd Log(double m, int exponent)
        {
            if (m > 1.4142135623730951)
            {
                m /= 2;
                exponent++;
            }

            Dd s = Divide(new Dd(m - 1, 0), TwoSum(m, 1));
            Dd s2 = Multiply(s, s);
            Dd sum = new Dd(1, 0);
            Dd term = s2;
            for (int k = 1; k < InverseOdd.Length && Math.Abs(term.Hi) > 1e-36; k++)
            {
                sum = Add(sum, Multiply(term, InverseOdd[k]));
                term = Multiply(term, s2);
            }

            Dd log = Multiply(s, sum);
            log = new Dd(log.Hi * 2, log.Lo * 2);
            return exponent == 0 ? log : Add(Multiply(exponent, Ln2), log);
        }

        // e^z for 0 <= z < ln 2, the table's entries: the long series, as pow had it before the tables.
#if NET
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
        private static Dd ExpOfSmall(Dd z)
        {
            double k = Math.Round(z.Hi / Ln2.Hi);
            Dd r = Add(z, Multiply(-k, Ln2));
            r = TwoSum(r.Hi, r.Lo - k * Ln2Third);

            // e^r - 1 on r / 2^9, doubled back nine times: s -> 2s + s^2 keeps the digits a form with the 1 would lose.
            const int Doublings = 9;
            const double Shrink = 1.0 / (1 << Doublings);
            r = new Dd(r.Hi * Shrink, r.Lo * Shrink);
            Dd s = r;
            Dd term = r;
            for (int n = 2; n < InverseFactorial.Length; n++)
            {
                term = Multiply(term, r);
                if (Math.Abs(term.Hi) < 1e-40)
                {
                    break;
                }

                s = Add(s, Multiply(term, InverseFactorial[n]));
            }

            for (int i = 0; i < Doublings; i++)
            {
                s = Add(new Dd(s.Hi * 2, s.Lo * 2), Multiply(s, s));
            }

            Dd e = Add(new Dd(1, 0), s);
            return k == 0 ? e : new Dd(e.Hi * 2, e.Lo * 2);   // k is 0 or 1 here
        }

        // v * 2^exponent, rounded once. Below the normal range the bits to drop are compared as they stand: rounding the
        // high part alone would round twice, and a tie goes to even.
#if NET
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
        private static double Scale(Dd v, long exponent)
        {
            double m = v.Hi;
            int e = Normalize(ref m);
            long whole = exponent + e;
            if (whole > 1023)
            {
                return double.PositiveInfinity;
            }

            if (whole >= -1022)
            {
                return ScaleBy(v.Hi, exponent);
            }

            if (whole < -1080)
            {
                return 0.0;
            }

            // In units of the least subnormal the value is below 2^52, and the high part has an exact integer part.
            double unit = ScaleBy(1.0, exponent + 1074);
            double units = v.Hi * unit;
            double low = v.Lo * unit;
            double floor = Math.Floor(units);
            double rest = units - floor;
            double half = rest - 0.5;
            double rounded = half + low > 0 ? floor + 1
                : half + low < 0 ? floor
                : Math.IEEERemainder(floor, 2) == 0 ? floor : floor + 1;
            return rounded * double.Epsilon;
        }

#if NET
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
        private static double ScaleBy(double v, long exponent)
        {
            while (exponent > 1000)
            {
                v *= 1.0715086071862673E+301;   // 2^1000
                exponent -= 1000;
            }

            while (exponent < -1000)
            {
                v *= 9.3326361850321888E-302;   // 2^-1000
                exponent += 1000;
            }

            return v * BitConverter.Int64BitsToDouble((exponent + 1023) << 52);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Dd TwoSum(double a, double b)
        {
            double s = a + b;
            double bb = s - a;
            return new Dd(s, (a - (s - bb)) + (b - bb));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Dd FastTwoSum(double a, double b)
        {
            double s = a + b;
            return new Dd(s, b - (s - a));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Dd TwoProduct(double a, double b)
        {
            double p = a * b;
#if NET
            // The same exact error as the split below, in one step; without the instruction the runtime emulates it, 30x slower.
            if (System.Runtime.Intrinsics.X86.Fma.IsSupported || System.Runtime.Intrinsics.Arm.AdvSimd.IsSupported)
            {
                return new Dd(p, Math.FusedMultiplyAdd(a, b, -p));
            }

#endif
            double t = Split * a;
            double aHi = t - (t - a);
            double aLo = a - aHi;
            t = Split * b;
            double bHi = t - (t - b);
            double bLo = b - bHi;
            return new Dd(p, ((aHi * bHi - p) + aHi * bLo + aLo * bHi) + aLo * bLo);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Dd Add(Dd a, Dd b)
        {
            Dd s = TwoSum(a.Hi, b.Hi);
            Dd t = TwoSum(a.Lo, b.Lo);
            Dd v = FastTwoSum(s.Hi, s.Lo + t.Hi);
            return FastTwoSum(v.Hi, v.Lo + t.Lo);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Dd Multiply(Dd a, Dd b)
        {
            Dd p = TwoProduct(a.Hi, b.Hi);
            return FastTwoSum(p.Hi, p.Lo + (a.Hi * b.Lo + a.Lo * b.Hi));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Dd Multiply(double a, Dd b)
        {
            Dd p = TwoProduct(a, b.Hi);
            return FastTwoSum(p.Hi, p.Lo + a * b.Lo);
        }

#if NET
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
        private static Dd Divide(Dd a, Dd b)
        {
            double q1 = a.Hi / b.Hi;
            Dd r = Add(a, Multiply(-q1, b));
            double q2 = r.Hi / b.Hi;
            r = Add(r, Multiply(-q2, b));
            double q3 = r.Hi / b.Hi;
            Dd q = FastTwoSum(q1, q2);
            return Add(q, new Dd(q3, 0));
        }

#if NET
        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
        private static Dd SquareRoot(Dd a)
        {
            double h = Math.Sqrt(a.Hi);
            Dd rest = Add(a, Negated(TwoProduct(h, h)));
            return FastTwoSum(h, rest.Hi / (2 * h));
        }

        private static Dd Negated(Dd a)
        {
            return new Dd(-a.Hi, -a.Lo);
        }

        private static Dd[] MakeInverseOdd(int count)
        {
            var table = new Dd[count];
            for (int i = 0; i < count; i++)
            {
                table[i] = Divide(new Dd(1, 0), new Dd(2 * i + 1, 0));
            }

            return table;
        }

        private static Dd[] MakeInverseFactorial(int count)
        {
            var table = new Dd[count];
            table[0] = new Dd(1, 0);
            for (int i = 1; i < count; i++)
            {
                table[i] = Divide(table[i - 1], new Dd(i, 0));
            }

            return table;
        }
    }
}
