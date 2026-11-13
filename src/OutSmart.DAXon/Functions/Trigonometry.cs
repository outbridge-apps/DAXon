////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Globalization;
using System.Numerics;

namespace OutSmart.DAXon.Functions
{
    // math:sin, math:cos and math:tan. On .NET Framework x64 Math.Sin/Cos/Tan use the x87 instructions: nearly always
    // correctly rounded close to zero, but they reduce the argument with a 66-bit pi, which loses about |x| * 2^-66, and
    // from 2^63 on they return the argument itself. So: Math.* where that loss is at most 1/25 ulp of the answer (|x| below
    // 512, the answer not small against |x|), else fdlibm (Java's StrictMath), about 3.5% of answers 1 ulp off at any size:
    // x = n * pi/2 + y + tail with |y| <= pi/4, the kernels taking the tail in before their one rounding.
    // .NET Core's own functions reduce exactly and are used as they are.
    internal static class Trigonometry
    {
        public static double Sin(double x)
        {
#if NETFRAMEWORK
            if (double.IsNaN(x) || double.IsInfinity(x))
            {
                return double.NaN;
            }

            double platform = Math.Sin(x);
            if (PlatformHolds(x, platform))
            {
                return platform;
            }

            int n = Reduce(x, out double y, out double tail);
            switch (n & 3)
            {
                case 0:
                    return KernelSin(y, tail);
                case 1:
                    return KernelCos(y, tail);
                case 2:
                    return -KernelSin(y, tail);
                default:
                    return -KernelCos(y, tail);
            }
#else
            return Math.Sin(x);
#endif
        }

        public static double Cos(double x)
        {
#if NETFRAMEWORK
            if (double.IsNaN(x) || double.IsInfinity(x))
            {
                return double.NaN;
            }

            double platform = Math.Cos(x);
            if (PlatformHolds(x, platform))
            {
                return platform;
            }

            int n = Reduce(x, out double y, out double tail);
            switch (n & 3)
            {
                case 0:
                    return KernelCos(y, tail);
                case 1:
                    return -KernelSin(y, tail);
                case 2:
                    return -KernelCos(y, tail);
                default:
                    return KernelSin(y, tail);
            }
#else
            return Math.Cos(x);
#endif
        }

        public static double Tan(double x)
        {
#if NETFRAMEWORK
            if (double.IsNaN(x) || double.IsInfinity(x))
            {
                return double.NaN;
            }

            // Close to a pole the answer is large, its reciprocal small: the same loss, the other way up.
            double platform = Math.Tan(x);
            if (PlatformHolds(x, platform) && Math.Abs(x * platform) <= PlatformBelow)
            {
                return platform;
            }

            int n = Reduce(x, out double y, out double tail);
            return KernelTan(y, tail, 1 - ((n & 1) << 1));
#else
            return Math.Tan(x);
#endif
        }

#if NETFRAMEWORK
        private const double PlatformBelow = 512;

        // Whether the platform's answer can stand: no reduction at all, or a small one against the answer's size.
        private static bool PlatformHolds(double x, double answer)
        {
            double t = Math.Abs(x);
            return (HighWord(t) <= 0x3FE921FB) || (t < PlatformBelow && Math.Abs(answer) * PlatformBelow >= t);
        }

        private static int HighWord(double d)
        {
            return (int)(BitConverter.DoubleToInt64Bits(d) >> 32);
        }

        private static double ClearLowWord(double d)
        {
            return BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(d) & unchecked((long)0xFFFFFFFF00000000L));
        }

        // sin(x + y), |x| <= pi/4, y the tail of x: the tail enters before the one rounding (fdlibm's __kernel_sin).
        private static double KernelSin(double x, double y)
        {
            if ((HighWord(x) & 0x7FFFFFFF) < 0x3E400000 && (int)x == 0)
            {
                return x;
            }

            double z = x * x;
            double v = z * x;
            double r = 8.33333333332248946124e-03 + z * (-1.98412698298579493134e-04 + z * (2.75573137070700676789e-06 + z * (-2.50507602534068634195e-08 + z * 1.58969099521155010221e-10)));
            return x - ((z * (0.5 * y - v * r) - y) - v * -1.66666666666666324348e-01);
        }

        // cos(x + y), |x| <= pi/4 (fdlibm's __kernel_cos).
        private static double KernelCos(double x, double y)
        {
            int ix = HighWord(x) & 0x7FFFFFFF;
            if (ix < 0x3E400000 && (int)x == 0)
            {
                return 1;
            }

            double z = x * x;
            double r = z * (4.16666666666666019037e-02 + z * (-1.38888888888741095749e-03 + z * (2.48015872894767294178e-05 + z * (-2.75573143513906633035e-07 + z * (2.08757232129817482790e-09 + z * -1.13596475577881948265e-11)))));
            if (ix < 0x3FD33333)
            {
                return 1 - (0.5 * z - (z * r - x * y));
            }

            double qx = ix > 0x3FE90000 ? 0.28125 : BitConverter.Int64BitsToDouble((long)(ix - 0x00200000) << 32);
            double hz = 0.5 * z - qx;
            double a = 1 - qx;
            return a - (hz - (z * r - x * y));
        }

        // tan(x + y) for odd = 1, -1/tan(x + y) for odd = -1, |x| <= pi/4 (fdlibm's __kernel_tan).
        private static double KernelTan(double x, double y, int odd)
        {
            int hx = HighWord(x);
            int ix = hx & 0x7FFFFFFF;
            double z;
            double w;
            if (ix < 0x3E300000 && (int)x == 0)
            {
                if (x == 0 && odd == -1)
                {
                    return 1 / Math.Abs(x);
                }

                return odd == 1 ? x : MinusOneOver(x + y, y, x);
            }

            if (ix >= 0x3FE59428)
            {
                if (hx < 0)
                {
                    x = -x;
                    y = -y;
                }

                z = 7.85398163397448278999e-01 - x;
                w = 3.06161699786838301793e-17 - y;
                x = z + w;
                y = 0;
            }

            z = x * x;
            w = z * z;
            double r = 1.33333333333201242699e-01 + w * (2.18694882948595424599e-02 + w * (3.59207910759131235356e-03 + w * (5.88041240820264096874e-04 + w * (7.81794442939557092300e-05 + w * -1.85586374855275456654e-05))));
            double v = z * (5.39682539762260521377e-02 + w * (8.86323982359930005737e-03 + w * (1.45620945432529025516e-03 + w * (2.46463134818469906812e-04 + w * (7.14072491382608190305e-05 + w * 2.59073051863633712884e-05)))));
            double s = z * x;
            r = y + z * (s * (r + v) + y);
            r += 3.33333333333334091986e-01 * s;
            w = x + r;
            if (ix >= 0x3FE59428)
            {
                v = odd;
                return (1 - ((hx >> 30) & 2)) * (v - 2.0 * (x - (w * w / (w + v) - r)));
            }

            return odd == 1 ? w : MinusOneOver(w, r, x);
        }

        // -1 / w, w = x + r, to the last bit.
        private static double MinusOneOver(double w, double r, double x)
        {
            double z = ClearLowWord(w);
            double v = r - (z - x);
            double a = -1.0 / w;
            double t = ClearLowWord(a);
            double s = 1.0 + t * z;
            return t + a * (s + t * v);
        }

        // fdlibm's constants: 2/pi, and pi/2 in three pieces of 33 bits each with what is left after each (the pieces
        // times an n below 2^20 are exact).
        private static readonly double InvPio2 = BitConverter.Int64BitsToDouble(0x3FE45F306DC9C883L);
        private static readonly double Pio2_1 = BitConverter.Int64BitsToDouble(0x3FF921FB54400000L);
        private static readonly double Pio2_1t = BitConverter.Int64BitsToDouble(0x3DD0B4611A626331L);
        private static readonly double Pio2_2 = BitConverter.Int64BitsToDouble(0x3DD0B4611A600000L);
        private static readonly double Pio2_2t = BitConverter.Int64BitsToDouble(0x3BA3198A2E037073L);
        private static readonly double Pio2_3 = BitConverter.Int64BitsToDouble(0x3BA3198A2E000000L);
        private static readonly double Pio2_3t = BitConverter.Int64BitsToDouble(0x397B839A252049C1L);

        // The high word of n * pi/2: only an x that shares it can lose enough bits to want a second piece.
        private static readonly int[] NearMultiple =
        {
            0x3FF921FB, 0x400921FB, 0x4012D97C, 0x401921FB, 0x401F6A7A, 0x4022D97C, 0x4025FDBB, 0x402921FB,
            0x402C463A, 0x402F6A7A, 0x4031475C, 0x4032D97C, 0x40346B9C, 0x4035FDBB, 0x40378FDB, 0x403921FB,
            0x403AB41B, 0x403C463A, 0x403DD85A, 0x403F6A7A, 0x40407E4C, 0x4041475C, 0x4042106C, 0x4042D97C,
            0x4043A28C, 0x40446B9C, 0x404534AC, 0x4045FDBB, 0x4046C6CB, 0x40478FDB, 0x404858EB, 0x404921FB,
        };

        // x = n * pi/2 + y + tail, |y| <= pi/4 (about), for a finite |x| > pi/4. Returns n (only n mod 4 is meaningful).
        private static int Reduce(double x, out double y, out double tail)
        {
            double t = Math.Abs(x);
            int n;
            int ix = HighWord(t);
            if (ix < 0x4002D97C)   // below 3 pi / 4: n is 1, and pi/2 is subtracted as it stands
            {
                n = 1;
                bool close = ix == 0x3FF921FB;   // near pi/2: the first piece leaves too few bits
                double z = t - Pio2_1;
                double piece = close ? Pio2_2t : Pio2_1t;
                if (close)
                {
                    z -= Pio2_2;
                }

                y = z - piece;
                tail = (z - y) - piece;
            }
            else if (ix <= 0x413921FB)
            {
                // Cody-Waite as fdlibm: a second and a third piece when the first leaves too few bits (x near n * pi/2)
                n = (int)(t * InvPio2 + 0.5);
                double fn = n;
                double r = t - fn * Pio2_1;
                double w = fn * Pio2_1t;
                y = r - w;
                int j = BiasedExponent(t);
                if ((n >= 32 || ix == NearMultiple[n - 1]) && j - BiasedExponent(y) > 16)
                {
                    double u = r;
                    w = fn * Pio2_2;
                    r = u - w;
                    w = fn * Pio2_2t - ((u - r) - w);
                    y = r - w;
                    if (j - BiasedExponent(y) > 49)
                    {
                        u = r;
                        w = fn * Pio2_3;
                        r = u - w;
                        w = fn * Pio2_3t - ((u - r) - w);
                        y = r - w;
                    }
                }

                tail = (r - y) - w;
            }
            else
            {
                n = ReduceLarge(t, out y, out tail);
            }

            if (x < 0)
            {
                y = -y;
                tail = -tail;
                return -n;
            }

            return n;
        }

        private static int BiasedExponent(double d)
        {
            return (int)((BitConverter.DoubleToInt64Bits(d) >> 52) & 0x7FF);
        }

        // 2/pi to 1600 bits after the point, and pi/2 to 256: for |x| up to 2^1024, 240 bits of x * 2/pi's fraction are
        // kept, enough for the closest a double comes to a multiple of pi/2 (about 2^-61) and a double-double after it.
        private const int TwoOverPiBits = 1600;
        private const int PiOver2Bits = 256;
        private const int FractionBits = 240;

        // Made when a large argument first needs them (a nested class: not before).
        private static class Tables
        {
            internal static readonly BigInteger TwoOverPi = BigInteger.Parse("0" + "0A2F9836E4E441529FC2757D1F534DDC0DB6295993C439041FE5163ABDEBBC561B7246E3A424DD2E006492EEA09D1921CFE1DEB1CB129A73EE88235F52EBB4484E99C7026B45F7E413991D639835339F49C845F8BBDF9283B1FF897FFDE05980FEF2F118B5A0A6D1F6D367ECF27CB09B74F463F669E5FEA2D7527BAC7EBE5F17B3D0739F78A5292EA6BFB5FB11F8D5D0856033046FC7B6BABF0CFBC209AF4361DA9E391615EE61B086599855F14A068408DFFD8804D73273106061556CA73A8C960E27BC08C6B47C4", NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            internal static readonly BigInteger PiOver2 = BigInteger.Parse("0" + "1921FB54442D18469898CC51701B839A252049C1114CF98E804177D4C76273644", NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        // Payne-Hanek: |x| = m * 2^e, so x * 2/pi = m * T * 2^(e - P) with T the first P bits of 2/pi. Bits of T worth a
        // multiple of 4 and those below the kept fraction are left out.
        private static int ReduceLarge(double t, out double y, out double tail)
        {
            BigInteger twoOverPi = Tables.TwoOverPi;
            BigInteger piOver2 = Tables.PiOver2;
            long bits = BitConverter.DoubleToInt64Bits(t);
            int e = (int)((bits >> 52) & 0x7FF) - 1075;
            long m = (bits & 0xFFFFFFFFFFFFFL) | (1L << 52);
            int p = e + FractionBits;   // T's bits after the point
            BigInteger mask = (BigInteger.One << (FractionBits + 2)) - 1;
            BigInteger product = m * ((twoOverPi >> (TwoOverPiBits - p)) & mask);
            int n = (int)((product >> FractionBits) & 3);
            BigInteger fraction = product & ((BigInteger.One << FractionBits) - 1);
            if (fraction >= BigInteger.One << (FractionBits - 1))
            {
                fraction -= BigInteger.One << FractionBits;   // the nearer multiple: |y| <= pi/4
                n++;
            }

            // y + tail = fraction * pi/2 / 2^(FractionBits + PiOver2Bits)
            BigInteger exact = fraction * piOver2;
            int scale = -(FractionBits + PiOver2Bits);
            y = Leading(exact, scale, out BigInteger rest);
            tail = Leading(rest, scale, out _);
            return n;
        }

        // The double nearest the leading bits of v * 2^scale, and v less the part it stands for.
        private static double Leading(BigInteger v, int scale, out BigInteger rest)
        {
            if (v.IsZero)
            {
                rest = BigInteger.Zero;
                return 0;
            }

            BigInteger magnitude = BigInteger.Abs(v);
            int shift = Math.Max(0, BitLength(magnitude) - 62);
            double d = (double)(long)(magnitude >> shift);   // long to double rounds correctly; at most 2^62
            BigInteger left = magnitude - (new BigInteger((long)d) << shift);   // negative when d rounded up
            rest = v.Sign < 0 ? -left : left;
            return (v.Sign < 0 ? -d : d) * PowerOfTwo(shift + scale);
        }

        private static int BitLength(BigInteger v)
        {
            byte[] bytes = v.ToByteArray();
            int top = bytes.Length - 1;
            while (top > 0 && bytes[top] == 0)
            {
                top--;
            }

            int length = top * 8;
            for (int b = bytes[top]; b != 0; b >>= 1)
            {
                length++;
            }

            return length;
        }

        private static double PowerOfTwo(int k)
        {
            return BitConverter.Int64BitsToDouble((long)(k + 1023) << 52);
        }
#endif
    }
}
