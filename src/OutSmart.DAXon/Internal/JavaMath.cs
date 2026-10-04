////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;

namespace OutSmart.DAXon.Internal
{
    /// <summary>
    /// Durable home for java.lang.Math members whose semantics have NO provably-identical
    /// System.Math equivalent on net472. The idiomatization pass (CollectionsModernizer
    /// --families math) rewrites the 1:1 shim forwards (Sin/Floor/Min/Ceil->Ceiling/...)
    /// to System.Math and points Round/Rint here instead. New code goes to final
    /// namespaces, hence OutSmart.DAXon.Internal (the OutSmart.DAXon.Internal.Math shim forwards here
    /// so both surfaces share one implementation).
    /// </summary>
    internal static class JavaMath
    {
        // Java Math.round(double) is floor(x + 0.5): the midpoint goes TOWARD POSITIVE
        // INFINITY, so round(-2.5) == -2 and round(2.5) == 3. This is NOT equivalent to
        // .NET Math.Round(x, MidpointRounding.AwayFromZero) (gives -3 for -2.5) and NOT
        // the .NET default MidpointRounding.ToEven (gives 2 for 2.5).
        //
        // Documented Java edge cases implemented explicitly:
        //   NaN                                  -> 0
        //   +Infinity or >= Long.MAX_VALUE       -> long.MaxValue
        //   -Infinity or <= Long.MIN_VALUE       -> long.MinValue
        // (Java's (long) cast of a double SATURATES; the C# unchecked cast of an
        // out-of-range double is undefined, so the clamps must be explicit.)
        //
        // Implemented in the floor/diff form, NOT the literal a + 0.5 addition: the addition
        // double-rounds near-midpoint values (floor(0.49999999999999994 + 0.5) == 1), which is
        // exactly the defect JDK 7+ fixed with bit-twiddling (JDK-8010430). floor/diff is
        // addition-free, so it matches JDK 7+ on that family too: floor(x) stays exact and the
        // diff >= 0.5 comparison only fires on TRUE midpoints (JDK 21-verified: round(0.5)=1,
        // round(-2.5)=-2, round(0.49999999999999994)=0).
        public static long Round(double a)
        {
            if (double.IsNaN(a)) // Java: round(NaN) == 0
            {
                return 0L;
            }
            double f = Math.Floor(a);
            double d = a - f; // exact for finite doubles (Sterbenz-adjacent: both same scale)
            f = d >= 0.5d ? f + 1.0d : f;
            if (f >= 9223372036854775808.0d)   // >= 2^63 (incl. +Infinity)
            {
                return long.MaxValue;
            }
            if (f <= -9223372036854775808.0d)  // <= -2^63 (incl. -Infinity; -2^63 IS long.MinValue)
            {
                return long.MinValue;
            }
            return (long)f;
        }

        // Java's (long) cast of a double: toward zero, saturating, NaN -> 0. The C# cast of an out-of-range
        // double depends on the runtime (long.MinValue on .NET Framework and .NET 8, saturated since .NET 9).
        public static long ToLong(double a)
        {
            if (double.IsNaN(a))
            {
                return 0L;
            }
            if (a >= 9223372036854775808.0d)   // >= 2^63 (incl. +Infinity)
            {
                return long.MaxValue;
            }
            if (a <= -9223372036854775808.0d)  // <= -2^63 (incl. -Infinity)
            {
                return long.MinValue;
            }
            return (long)a;
        }

        // Java Math.round(float) -> int, same floor/diff form (JDK 7+ fixed the +0.5f
        // addition defect for float too: round(0.49999997f) == 0).
        public static int Round(float a)
        {
            if (float.IsNaN(a)) // Java: round(NaN) == 0
            {
                return 0;
            }
            double x = a; // float -> double widening is exact
            double f = Math.Floor(x);
            double d = x - f;
            f = d >= 0.5d ? f + 1.0d : f;
            if (f >= 2147483648.0d)    // >= 2^31 (incl. +Infinity)
            {
                return int.MaxValue;
            }
            if (f <= -2147483648.0d)   // <= -2^31 (incl. -Infinity; -2^31 IS int.MinValue)
            {
                return int.MinValue;
            }
            return (int)f;
        }

        // Java Math.rint: round half to EVEN, returning double. System.Math.Round(double)
        // (banker's rounding, zero digits) implements the identical contract on net472,
        // including NaN -> NaN, +/-Infinity -> unchanged, already-integral -> unchanged,
        // |x| >= 2^52 -> unchanged. Kept as a named utility (paired with Round) so the
        // Java rounding call sites stay greppable and the forward is documented once.
        public static double Rint(double a) => Math.Round(a);
    }
}
