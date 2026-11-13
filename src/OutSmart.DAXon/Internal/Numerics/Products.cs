////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Numerics;

namespace OutSmart.DAXon.Internal.Numerics
{
    // The product of two integers with the run's clock read as it goes. .NET Framework multiplies schoolbook in one call,
    // minutes on megadigit operands that repeated squaring reaches in a line (79 s under a 2 s limit). Past a few thousand
    // words each, Karatsuba splits the work, which is faster as well, and every piece reads the clock. Later runtimes are
    // subquadratic themselves, so a split only costs: there it starts where one call passes about half a second.
    internal static class Products
    {
        // below this either way: the platform's multiplication
#if NETFRAMEWORK
        private const double SplitBits = 32 * 2048;
#else
        private const double SplitBits = 1 << 22;
#endif

        internal static BigInteger Multiply(BigInteger a, BigInteger b)
        {
            if (a.IsZero || b.IsZero || Bits(a) < SplitBits || Bits(b) < SplitBits)
            {
                return a * b;
            }

            BigInteger product = Karatsuba(BigInteger.Abs(a), BigInteger.Abs(b));
            return a.Sign * b.Sign < 0 ? -product : product;
        }

        private static BigInteger Karatsuba(BigInteger x, BigInteger y)
        {
            double xBits = Bits(x);
            double yBits = Bits(y);
            if (xBits < SplitBits || yBits < SplitBits)
            {
                Core.Controller.CheckActiveTimeoutNow();
                return x * y;
            }

            int k = (int)(Math.Max(xBits, yBits) / 2);
            BigInteger x1 = x >> k;
            BigInteger x0 = x - (x1 << k);
            BigInteger y1 = y >> k;
            BigInteger y0 = y - (y1 << k);
            BigInteger z2 = Karatsuba(x1, y1);
            BigInteger z0 = Karatsuba(x0, y0);
            BigInteger z1 = Karatsuba(x1 + x0, y1 + y0) - z2 - z0;
            return (z2 << (2 * k)) + (z1 << k) + z0;
        }

        // About the bit length of a non-negative value (exact enough to choose a split); 0 for zero.
        private static double Bits(BigInteger v)
        {
            return v.IsZero ? 0 : BigInteger.Log(BigInteger.Abs(v), 2) + 1;
        }
    }
}
