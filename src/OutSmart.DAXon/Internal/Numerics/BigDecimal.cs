////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Globalization;
using SysBigInt = System.Numerics.BigInteger;

namespace OutSmart.DAXon.Internal.Numerics
{

    /// <summary>
    /// Internal arbitrary-precision signed decimal: a <see cref="System.Numerics.BigInteger"/>
    /// unscaled mantissa with a power-of-10 scale exponent. All arithmetic (including division)
    /// is pure BigInteger math — no double intermediates anywhere. Semantics follow the
    /// java.math.BigDecimal spec and are etalon-pinned against a real JDK 21 run
    /// (acceptance\javacompat-tests\BigDecimalDivideTests.cs).
    ///
    /// Scale() and UnscaledValue() are METHODS (the engine calls value.Scale() and
    /// value.Abs().UnscaledValue()); keep them as methods, not properties.
    ///
    /// Invariant: Scale() &gt;= 0 on every publicly returned instance (ToString, Align and the
    /// consumers assume it). Java permits negative scales (e.g. stripTrailingZeros(1600) ==
    /// 1.6E+3 with scale -2); where Java would produce one, this type normalizes
    /// value-identically to scale 0 (mantissa multiplied out) — the only observable difference
    /// is Scale()/UnscaledValue(), toPlainString output is identical, and no call site reaches
    /// such a case. Exception convention: arithmetic failures throw System.ArithmeticException
    /// (the value converter maps java.lang.ArithmeticException to it) with the Java message texts.
    /// </summary>
    public sealed class BigDecimal : IComparable<BigDecimal>, IEquatable<BigDecimal>
    {
        // Java BigDecimal ROUND_* rounding-mode ints — internal switch tags only; the public
        // rounding surface is the RoundingMode enum.
        private const int ROUND_UP = 0;
        private const int ROUND_DOWN = 1;
        private const int ROUND_CEILING = 2;
        private const int ROUND_FLOOR = 3;
        private const int ROUND_HALF_UP = 4;
        private const int ROUND_HALF_DOWN = 5;
        private const int ROUND_HALF_EVEN = 6;
        private const int ROUND_UNNECESSARY = 7;
        public static readonly BigDecimal Zero = new BigDecimal(0);
        public static readonly BigDecimal One = new BigDecimal(1);
        public static readonly BigDecimal Ten = new BigDecimal(10);

        private readonly int _scale;

        // ---- long-compact representation (mirrors java.math.BigDecimal's intCompact/intVal) -----
        // Small-magnitude decimals (the overwhelmingly common case: prices, counts, most literals)
        // fit their unscaled mantissa in a long, held in _compact; INFLATED marks the big path.
        // The BigInteger form lives in _big (a boxed SysBigInt): always set on the big path,
        // materialized lazily for compact values the first time Unscaled is read (benign race —
        // concurrent readers box the same immutable value). Keeping the compact form in a FIELD
        // (rather than re-deriving it per operation from a BigInteger) is what makes
        // +/-/*/compareTo/stripTrailingZeros run in pure long arithmetic; every branch still
        // falls back to BigInteger on overflow, so all returned (unscaled, scale) pairs — and
        // therefore all results — are byte-identical to the BigInteger path.
        private readonly long _compact;
        private object _big;
        public int Sign => _compact != INFLATED ? Math.Sign(_compact) : ((SysBigInt)_big).Sign;

        private SysBigInt Unscaled
        {
            get
            {
                object b = _big;
                if (b != null)
                    return (SysBigInt)b;
                SysBigInt v = _compact;
                _big = v;
                return v;
            }
        }

        // Compact mantissa for a fresh instance: INFLATED when out of long range (or the
        // sentinel value itself, which stays on the big path exactly as before).
        private static long ComputeCompact(SysBigInt value)
        {
            if (value >= BI_LONG_MIN && value <= BI_LONG_MAX)
            {
                long v = (long)value;
                if (v != INFLATED)
                    return v;
            }
            return INFLATED;
        }

        private const long INFLATED = long.MinValue;
        private static readonly SysBigInt BI_LONG_MIN = long.MinValue;
        private static readonly SysBigInt BI_LONG_MAX = long.MaxValue;
        private static readonly long[] LONG_POW10 =
        {
            1L, 10L, 100L, 1000L, 10000L, 100000L, 1000000L, 10000000L, 100000000L,
            1000000000L, 10000000000L, 100000000000L, 1000000000000L, 10000000000000L,
            100000000000000L, 1000000000000000L, 10000000000000000L, 100000000000000000L,
            1000000000000000000L,   // 10^18 (10^19 would overflow long)
        };

        // Cached BigInteger powers of ten: the divide/rescale family used SysBigInt.Pow(10, k) per
        // call, allocating the same mid-size constants (10^18..10^40) on every decimal division.
        private static readonly SysBigInt[] POW10_BI = BuildPow10();
        private static SysBigInt[] BuildPow10()
        {
            var p = new SysBigInt[64];
            p[0] = SysBigInt.One;
            for (int i = 1; i < p.Length; i++)
            {
                p[i] = p[i - 1] * 10;
            }
            return p;
        }

        private static SysBigInt Pow10(int k) => k < POW10_BI.Length ? POW10_BI[k] : SysBigInt.Pow(10, k);

        // True (+ the long mantissa) when the value is on the compact path.
        private bool TryGetCompact(out long v)
        {
            v = _compact;
            return v != INFLATED;
        }

        // Unboxed (mantissa, scale) access for external accumulators (fn:sum's decimal path).
        internal bool TryGetCompactParts(out long u, out int s)
        {
            u = _compact;
            s = _scale;
            return _compact != INFLATED;
        }

        internal static bool TryAddCompactParts(long xu, int xs, long yu, int ys, out long ru, out int rs)
            => TryAddCompact(xu, xs, yu, ys, false, out ru, out rs);

        // a*b in long with overflow detection (returns false when it would overflow).
        private static bool TryMulLong(long a, long b, out long r)
        {
            if (a >= int.MinValue && a <= int.MaxValue && b >= int.MinValue && b <= int.MaxValue)
            {
                // Both within int range → the 64-bit product cannot overflow; skips the
                // division-based check below (an integer divide per multiply/rescale).
                r = a * b;
                return true;
            }
            r = unchecked(a * b);
            if (a == 0)
            {
                return true;
            }
            if (a == -1 && b == INFLATED)   // -1 * long.MinValue overflows
            {
                return false;
            }
            return r / a == b;
        }

        // a+b in long with overflow detection.
        private static bool TryAddLong(long a, long b, out long r)
        {
            r = unchecked(a + b);
            return ((a ^ r) & (b ^ r)) >= 0;
        }

        // Rescale xu@xs and yu@ys to a common scale in long, then add (subtract=false) or subtract.
        // Returns false on any overflow (scale gap > 18, 10^gap * mantissa, or the final add).
        private static bool TryAddCompact(long xu, int xs, long yu, int ys, bool subtract, out long ru, out int rscale)
        {
            ru = 0;
            long a, b;
            int diff = xs - ys;
            if (diff == 0)
            {
                a = xu;
                b = yu;
                rscale = xs;
            }
            else if (diff > 0)
            {
                if (diff > 18 || !TryMulLong(yu, LONG_POW10[diff], out b))
                {
                    rscale = 0;
                    return false;
                }
                a = xu; rscale = xs;
            }
            else
            {
                int d = -diff;
                if (d > 18 || !TryMulLong(xu, LONG_POW10[d], out a))
                {
                    rscale = 0;
                    return false;
                }
                b = yu; rscale = ys;
            }
            if (subtract)
            {
                if (b == INFLATED)   // -(long.MinValue) overflows
                {
                    return false;
                }
                b = -b;
            }
            return TryAddLong(a, b, out ru);
        }

        public BigDecimal(SysBigInt unscaled, int scale) { _compact = ComputeCompact(unscaled); _big = unscaled; _scale = scale; }
        // Java BigDecimal(BigInteger): the value with scale 0. Added with the biginteger
        // idiomatization family - before it, `new BigDecimal(javaBigIntegerWrapper)` could
        // only bind BigDecimal(long) through the wrapper's implicit->long conversion, which
        // THROWS OverflowException for values beyond 64 bits (e.g. BigIntegerValue.
        // GetDecimalValue of a >long integer). This is the faithful, lossless ctor.
        public BigDecimal(SysBigInt value) : this(value, 0) { }
        public BigDecimal(long value)
        {
            if (value != INFLATED)
            {
                _compact = value;
                _scale = 0;
            }
            else
            {
                _compact = INFLATED;
                _big = new SysBigInt(value);
                _scale = 0;
            }
        }

        // Compact-path result constructor: the mantissa is a known long, so no BigInteger is
        // touched (Unscaled materializes lazily if ever read). long.MinValue collides with the
        // INFLATED sentinel and takes the big form — value-identical.
        private BigDecimal(long compact, int scale, bool marker)
        {
            if (compact != INFLATED)
            {
                _compact = compact;
                _scale = scale;
            }
            else
            {
                _compact = INFLATED;
                _big = new SysBigInt(compact);
                _scale = scale;
            }
        }
        internal static BigDecimal FromCompact(long v, int scale) => new BigDecimal(v, scale, true);
        public BigDecimal(decimal value)
        {
            var bits = decimal.GetBits(value);
            var scale = (bits[3] >> 16) & 0x7F;
            var mantissa = (new SysBigInt((uint)bits[0]) | (new SysBigInt((uint)bits[1]) << 32) | (new SysBigInt((uint)bits[2]) << 64));
            if (value < 0)
                mantissa = -mantissa;
            _compact = ComputeCompact(mantissa);
            _big = mantissa;
            _scale = scale;
        }
        // Java BigDecimal(double): the EXACT decimal value of the double's binary
        // expansion (javadoc: "exactly equal to the value of the double"). Deliberately
        // different from ValueOf(double) (shortest round-trip decimal) — the transpiled
        // BigDecimalValue(double) ctor relies on the extra precision and then
        // canonicalizes via StripTrailingZeros. Decompose value = m * 2^e from the
        // IEEE-754 bits; Java normalizes (m even, e < 0 -> shift right), then for
        // e < 0: m / 2^-e == m * 5^-e at scale -e — exact, pure BigInteger math.
        // NaN/Infinity throws NumberFormatException like Java; the poc call site's
        // catch(Exception) maps it to ValidationFailure FOCA0002.
        public BigDecimal(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new FormatException("Infinite or NaN");
            long bits = BitConverter.DoubleToInt64Bits(value);
            bool negative = bits < 0;
            int exp = (int)((bits >> 52) & 0x7FFL);
            long mantissa = bits & 0xFFFFFFFFFFFFFL;
            if (exp == 0)                    // subnormal: no implicit bit
            {
                exp = -1074;
            }
            else
            {
                mantissa |= 1L << 52;
                exp -= 1075;
            }
            if (mantissa == 0)
            {
                _compact = 0;
                _scale = 0;
                return;
            }
            while ((mantissa & 1) == 0 && exp < 0) // Java's normalize step
            {
                mantissa >>= 1;
                exp++;
            }
            var m = new SysBigInt(mantissa);
            if (negative)
                m = -m;
            SysBigInt u;
            if (exp >= 0)
            {
                u = m * SysBigInt.Pow(2, exp);
                _scale = 0;
            }
            else
            {
                u = m * SysBigInt.Pow(5, -exp);
                _scale = -exp;
            }
            _compact = ComputeCompact(u);
            _big = u;
        }
        public BigDecimal(string text)
        {
            // Java-faithful BigDecimal(String): [sign] digits [. digits] [eE [sign] digits].
            // The exponent shifts the scale (scale = fracDigits - exp). A negative resulting
            // scale is normalized to 0 by scaling the mantissa — value-identical, and the
            // rest of this shim (Align/ToBigInteger/DecimalToString consumers) assumes Scale() >= 0.
            var s = text.Trim();
            int exp = 0;
            int e = s.IndexOfAny(new[] { 'e', 'E' });
            if (e >= 0)
            {
                exp = int.Parse(s.Substring(e + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
                s = s.Substring(0, e);
            }
            if (s.StartsWith("+", StringComparison.Ordinal))
                s = s.Substring(1);
            var dot = s.IndexOf('.');
            SysBigInt unscaled;
            int scale;
            if (dot < 0)
            {
                unscaled = SysBigInt.Parse(s, CultureInfo.InvariantCulture);
                scale = 0;
            }
            else
            {
                var combined = s.Substring(0, dot) + s.Substring(dot + 1);
                unscaled = SysBigInt.Parse(combined, CultureInfo.InvariantCulture);
                scale = s.Length - dot - 1;
            }
            scale -= exp;
            if (scale < 0)
            {
                unscaled *= Pow10(-scale);
                scale = 0;
            }
            _compact = ComputeCompact(unscaled);
            _big = unscaled;
            _scale = scale;
        }

        // Java-shaped accessors (java.math.BigDecimal.unscaledValue()/scale() are methods;
        // the transpiled tree calls these method forms — see class doc).
        public SysBigInt UnscaledValue() => Unscaled;
        public int Scale() => _scale;

        public static BigDecimal ValueOf(long value) => new BigDecimal(value);
        // Java-faithful: valueOf(double) == new BigDecimal(Double.toString(d)) — full double
        // range. The old (decimal) cast overflowed beyond ~7.9e28; IntegerValue.FromDouble(±1e100)
        // reaches this via FormatNumber.AdjustToDecimal during Expression static init.
        // G17 round-trips on net472 ("R" has documented round-trip bugs there); longer-than-shortest
        // digit tails are exactly what AdjustToDecimal's zeros/nines heuristic cleans up.
        public static BigDecimal ValueOf(double value) => new BigDecimal(value.ToString("G17", CultureInfo.InvariantCulture));

        public static BigDecimal operator +(BigDecimal x, BigDecimal y)
        {
            if (x.TryGetCompact(out long xu) && y.TryGetCompact(out long yu)
                && TryAddCompact(xu, x._scale, yu, y._scale, false, out long ru, out int rs))
                return FromCompact(ru, rs);
            var (a, b, s) = Align(x, y);
            return new BigDecimal(a + b, s);
        }
        public static BigDecimal operator -(BigDecimal x, BigDecimal y)
        {
            if (x.TryGetCompact(out long xu) && y.TryGetCompact(out long yu)
                && TryAddCompact(xu, x._scale, yu, y._scale, true, out long ru, out int rs))
                return FromCompact(ru, rs);
            var (a, b, s) = Align(x, y);
            return new BigDecimal(a - b, s);
        }
        public static BigDecimal operator *(BigDecimal x, BigDecimal y)
        {
            if (x.TryGetCompact(out long xu) && y.TryGetCompact(out long yu) && TryMulLong(xu, yu, out long ru))
                return FromCompact(ru, x._scale + y._scale);
            return new BigDecimal(x.Unscaled * y.Unscaled, x._scale + y._scale);
        }
        public static BigDecimal operator -(BigDecimal x) =>
            x._compact != INFLATED ? FromCompact(-x._compact, x._scale) : new BigDecimal(-x.Unscaled, x._scale);
        public BigDecimal Abs() =>
            _compact != INFLATED
                ? (_compact < 0 ? FromCompact(-_compact, _scale) : this)
                : new BigDecimal(SysBigInt.Abs(Unscaled), _scale);

        public int CompareTo(BigDecimal other)
        {
            if (TryGetCompact(out long xu) && other.TryGetCompact(out long yu))
            {
                if (_scale == other._scale)
                    return xu.CompareTo(yu);
                // sign(this - other) == compareTo, when the aligned subtraction doesn't overflow.
                if (TryAddCompact(xu, _scale, yu, other._scale, true, out long diff, out _))
                    return diff.CompareTo(0L);
            }
            var (a, b, _2) = Align(this, other);
            return a.CompareTo(b);
        }

        // NOTE: deliberately value-based (compareTo == 0), i.e. 1.0 equals 1. Java's
        // BigDecimal.equals is scale-sensitive (2.0 != 2.00); the port's call sites rely
        // on value equality, so this divergence is kept. GetHashCode is made consistent
        // with this Equals by hashing the trailing-zero-stripped canonical form
        // (previously it hashed ToString(), so 1.0 and 1 were "equal" with different
        // hashes — broken for dictionary use).
        public bool Equals(BigDecimal other) => other != null && CompareTo(other) == 0;
        public override bool Equals(object obj) => obj is BigDecimal bd && Equals(bd);
        public override int GetHashCode()
        {
            var c = StripTrailingZeros();
            return unchecked((c.Unscaled.GetHashCode() * 397) ^ c._scale);
        }

        public override string ToString()
        {
            if (_compact != INFLATED)
            {
                if (_scale == 0)
                    return _compact.ToString(CultureInfo.InvariantCulture);
                var cs = Math.Abs(_compact).ToString(CultureInfo.InvariantCulture);
                var csign = _compact < 0 ? "-" : "";
                if (_scale < 0)
                    return csign + cs + new string('0', -_scale);
                if (cs.Length <= _scale)
                    cs = cs.PadLeft(_scale + 1, '0');
                return csign + cs.Substring(0, cs.Length - _scale) + "." + cs.Substring(cs.Length - _scale);
            }
            if (_scale == 0)
                return Unscaled.ToString(CultureInfo.InvariantCulture);
            var s = SysBigInt.Abs(Unscaled).ToString(CultureInfo.InvariantCulture);
            var sign = Unscaled.Sign < 0 ? "-" : "";
            // Negative scale means an integer: unscaled x 10^(-scale), i.e. |scale| trailing zeros. The
            // positive-scale branch below would compute Substring(0, len-scale) with len-scale > len and throw.
            if (_scale < 0)
                return sign + s + new string('0', -_scale);
            if (s.Length <= _scale)
                s = s.PadLeft(_scale + 1, '0');
            return sign + s.Substring(0, s.Length - _scale) + "." + s.Substring(s.Length - _scale);
        }

        // Java longValue(): discard the fraction (truncate toward zero), then return the
        // low-order 64 bits — wrapping, never throwing. The previous implementation cast
        // the raw mantissa to long BEFORE dividing ((long)Unscaled overflows-throws
        // for >64-bit mantissas, and (long)Pow(10,_scale) overflows for Scale() > 18).
        public long LongValue()
        {
            if (_compact != INFLATED && _scale == 0)
                return _compact;
            if (_compact != INFLATED && _scale > 0 && _scale <= 18)
                return _compact / LONG_POW10[_scale];
            var integerPart = _scale == 0 ? Unscaled : Unscaled / Pow10(_scale);
            return unchecked((long)(ulong)(integerPart & ulong.MaxValue));
        }
        public int IntValue() => unchecked((int)LongValue());
        public double DoubleValue() => double.Parse(ToString(), CultureInfo.InvariantCulture);
        // Java BigDecimal.floatValue() — narrowing decimal->float conversion.
        // FormatNumber.AdjustToDecimal compares trial.FloatValue() == value at precision 1.
        public float FloatValue() => (float)DoubleValue();

        // ==================== Division — pure BigInteger math ====================
        // Written from the javadoc spec; etalon-pinned against JDK 21 in acceptance\javacompat-tests\BigDecimalDivideTests.cs.

        /// <summary>
        /// Java divide(divisor): exact quotient. Result scale is the preferred scale
        /// (this.scale - divisor.scale) when the exact quotient is representable at it,
        /// otherwise the larger scale actually needed. Throws ArithmeticException
        /// "Non-terminating decimal expansion..." when the exact quotient does not
        /// terminate (reduced denominator has prime factors other than 2 and 5).
        /// </summary>
        public BigDecimal Divide(BigDecimal divisor)
        {
            if (divisor.Sign == 0)
                throw new ArithmeticException("Division by zero");
            int preferredScale = _scale - divisor._scale;
            if (Sign == 0)
                return new BigDecimal(SysBigInt.Zero, Math.Max(0, preferredScale));

            var n = Unscaled;
            var d = divisor.Unscaled;
            if (d.Sign < 0)
            {
                n = -n;
                d = -d;
            }
            var g = SysBigInt.GreatestCommonDivisor(n, d);
            if (!g.IsOne)
            {
                n /= g;
                d /= g;
            }

            // Terminating-expansion test: after full reduction the denominator may only
            // contain factors 2 and 5.
            int twos = 0, fives = 0;
            while (d.IsEven)
            {
                d /= 2;
                twos++;
            }
            while (true)
            {
                var q5 = SysBigInt.DivRem(d, 5, out var r5);
                if (!r5.IsZero)
                    break;
                d = q5; fives++;
            }
            if (!d.IsOne)
                throw new ArithmeticException("Non-terminating decimal expansion; no exact representable decimal result.");

            int extra = Math.Max(twos, fives);
            if (extra > twos)
                n *= SysBigInt.Pow(2, extra - twos);
            if (extra > fives)
                n *= SysBigInt.Pow(5, extra - fives);
            int resultScale = preferredScale + extra;
            // Java can return a negative scale here (e.g. 100/0.5 == 2E+2); normalize
            // value-identically to keep the shim's Scale() >= 0 invariant.
            return resultScale >= 0
                ? new BigDecimal(n, resultScale)
                : new BigDecimal(n * Pow10(-resultScale), 0);
        }

        /// <summary>Java divide(divisor, roundingMode): quotient at this.scale.</summary>
        public BigDecimal Divide(BigDecimal divisor, int roundingMode) => Divide(divisor, _scale, roundingMode);
        public BigDecimal Divide(BigDecimal divisor, RoundingMode roundingMode) => Divide(divisor, _scale, (int)roundingMode);

        /// <summary>
        /// Java divide(divisor, scale, roundingMode): exact scaled integer division.
        /// The operands are rescaled so the integer quotient lands at the requested scale;
        /// the integer remainder decides rounding per mode.
        /// </summary>
        public BigDecimal Divide(BigDecimal divisor, int scale, int roundingMode)
        {
            if (roundingMode < ROUND_UP || roundingMode > ROUND_UNNECESSARY)
                throw new ArgumentException("Invalid rounding mode: " + roundingMode);
            if (divisor.Sign == 0)
                throw new ArithmeticException("/ by zero");

            // this/divisor = (u1/u2) * 10^(s2-s1); at target scale: q = u1*10^(scale+s2-s1) / u2.
            int shift = scale + divisor._scale - _scale;

            // Compact fast path: schoolbook block division in longs. Computes exactly
            // floor(|a|*10^shift / |b|) and its remainder — the same integers the generic path gets
            // from BigInteger — by emitting the quotient in ≤18-digit blocks (r < |b|, so
            // r*10^step stays within a long). Rounding decisions run on the long remainder with the
            // same comparisons as RoundQuotient; parity for HALF_EVEN via acc.IsEven. The |b| bound
            // keeps 2*r overflow-free; the shift bound keeps the loop trivially finite.
            if (shift >= 0 && shift <= 57
                && TryGetCompact(out long ca) && divisor.TryGetCompact(out long cb)
                && ca != long.MinValue && cb != long.MinValue)
            {
                long ab = Math.Abs(ca), bb = Math.Abs(cb);
                int bd = 1;
                while (bd < 19 && bb >= LONG_POW10[bd])
                {
                    bd++;
                }
                int chunk = 18 - bd;
                if (bb <= long.MaxValue / 4 && chunk >= 1)
                {
                    long q0 = ab / bb, r = ab % bb;
                    SysBigInt acc = q0;
                    int rem = shift;
                    while (rem > 0)
                    {
                        int step = rem < chunk ? rem : chunk;
                        long p = LONG_POW10[step];
                        r *= p;
                        long qi = r / bb;
                        r -= qi * bb;
                        acc = acc * p + qi;
                        rem -= step;
                    }

                    int csign = (ca < 0) == (cb < 0) ? 1 : -1;
                    if (r != 0)
                    {
                        long twice = r * 2;
                        bool inc;
                        switch (roundingMode)
                        {
                            case ROUND_UP: inc = true; break;
                            case ROUND_DOWN: inc = false; break;
                            case ROUND_CEILING: inc = csign > 0; break;
                            case ROUND_FLOOR: inc = csign < 0; break;
                            case ROUND_HALF_UP: inc = twice >= bb; break;
                            case ROUND_HALF_DOWN: inc = twice > bb; break;
                            case ROUND_HALF_EVEN: inc = twice > bb || (twice == bb && !acc.IsEven); break;
                            case ROUND_UNNECESSARY: throw new ArithmeticException("Rounding necessary");
                            default: throw new ArgumentException("Invalid rounding mode: " + roundingMode);
                        }
                        if (inc)
                            acc += SysBigInt.One;
                    }

                    var cq = csign < 0 ? -acc : acc;
                    return scale >= 0
                        ? new BigDecimal(cq, scale)
                        : new BigDecimal(cq * Pow10(-scale), 0);
                }
            }

            var num = Unscaled;
            var den = divisor.Unscaled;
            if (shift >= 0)
                num *= Pow10(shift);
            else
                den *= Pow10(-shift);
            var q = RoundQuotient(num, den, roundingMode);
            // Java allows a negative target scale (no included call site uses one);
            // normalize value-identically per the shim invariant.
            return scale >= 0
                ? new BigDecimal(q, scale)
                : new BigDecimal(q * Pow10(-scale), 0);
        }
        public BigDecimal Divide(BigDecimal divisor, int scale, RoundingMode roundingMode) => Divide(divisor, scale, (int)roundingMode);

        // No MathContext model in the shim, and the included poc tree has zero MathContext
        // references. Approximated as a generous-scale HALF_EVEN division (DECIMAL128 is
        // 34 significant digits HALF_EVEN) instead of the old double round-trip.
        public BigDecimal Divide(BigDecimal divisor, object mathContext) =>
            Divide(divisor, Math.Max(_scale - divisor._scale, 0) + 36, ROUND_HALF_EVEN);

        /// <summary>
        /// Java divideToIntegralValue(divisor): integer part of the exact quotient,
        /// preferred scale (this.scale - divisor.scale). A negative preferred scale is
        /// normalized to 0 (value-identical; Java keeps e.g. 3E+1 when the quotient has
        /// trailing zeros — Remainder() reproduces that case exactly via the raw form).
        /// </summary>
        public BigDecimal DivideToIntegralValue(BigDecimal divisor)
        {
            var raw = DivideToIntegralRaw(divisor);
            return raw._scale >= 0 ? raw : new BigDecimal(raw.Unscaled * Pow10(-raw._scale), 0);
        }

        /// <summary>
        /// Java remainder(divisor) == this.subtract(divideToIntegralValue(divisor)
        /// .multiply(divisor)) — NOT the modulo operation; the result has the sign of
        /// the dividend. Uses the raw (possibly negative-scale) integral quotient
        /// internally so result scales match Java exactly; the final scale is always
        /// max(this.scale, ...) >= 0, so the public invariant holds.
        /// </summary>
        public BigDecimal Remainder(BigDecimal divisor) => this - DivideToIntegralRaw(divisor) * divisor;

        // Integral quotient with Java's preferred-scale semantics. The returned scale may
        // be negative (internal use by Remainder only; public callers go through
        // DivideToIntegralValue which normalizes).
        private BigDecimal DivideToIntegralRaw(BigDecimal divisor)
        {
            if (divisor.Sign == 0)
                throw new ArithmeticException("Division by zero");
            int preferredScale = _scale - divisor._scale;
            int shift = divisor._scale - _scale; // = -preferredScale
            var num = Unscaled;
            var den = divisor.Unscaled;
            if (shift >= 0)
                num *= Pow10(shift);
            else
                den *= Pow10(-shift);
            var q = num / den; // BigInteger division truncates toward zero == integer part

            if (q.IsZero)
                return new BigDecimal(SysBigInt.Zero, preferredScale);
            if (preferredScale >= 0)
                return new BigDecimal(q * Pow10(preferredScale), preferredScale);
            // preferredScale < 0: Java strips up to -preferredScale trailing zeros from
            // the integral quotient (e.g. 7.5 dtiv 0.25 -> 3E+1, scale -1).
            int stripped = 0;
            while (stripped < -preferredScale)
            {
                var q10 = SysBigInt.DivRem(q, 10, out var r10);
                if (!r10.IsZero)
                    break;
                q = q10; stripped++;
            }
            return new BigDecimal(q, -stripped);
        }

        // Shared rounded integer division: round(num/den) per Java rounding mode.
        private static SysBigInt RoundQuotient(SysBigInt num, SysBigInt den, int roundingMode)
        {
            int sign = num.Sign * den.Sign;
            var n = SysBigInt.Abs(num);
            var d = SysBigInt.Abs(den);
            var q = SysBigInt.DivRem(n, d, out var r);
            if (!r.IsZero)
            {
                var twiceRem = r * 2;
                bool increment;
                switch (roundingMode)
                {
                    case ROUND_UP: increment = true; break;
                    case ROUND_DOWN: increment = false; break;
                    case ROUND_CEILING: increment = sign > 0; break;
                    case ROUND_FLOOR: increment = sign < 0; break;
                    case ROUND_HALF_UP: increment = twiceRem >= d; break;
                    case ROUND_HALF_DOWN: increment = twiceRem > d; break;
                    case ROUND_HALF_EVEN: increment = twiceRem > d || (twiceRem == d && !q.IsEven); break;
                    case ROUND_UNNECESSARY: throw new ArithmeticException("Rounding necessary");
                    default: throw new ArgumentException("Invalid rounding mode: " + roundingMode);
                }
                if (increment)
                    q += SysBigInt.One;
            }
            return sign < 0 ? -q : q;
        }

        // Java semantics: re-scale the mantissa and round the dropped digits per the requested mode.
        public BigDecimal SetScale(int newScale) => SetScale(newScale, (int)RoundingMode.UNNECESSARY); // Java: setScale(int) throws on precision loss
        public BigDecimal SetScale(int newScale, RoundingMode roundingMode) => SetScale(newScale, (int)roundingMode);
        public BigDecimal SetScale(int newScale, int roundingMode)
        {
            if (newScale == _scale)
                return this;
            if (newScale > _scale)
                return new BigDecimal(Unscaled * Pow10(newScale - _scale), newScale);
            var divisor = Pow10(_scale - newScale);
            var q = SysBigInt.DivRem(Unscaled, divisor, out var r); // truncates toward zero (= DOWN)
            if (r == SysBigInt.Zero)
                return new BigDecimal(q, newScale);
            var sign = Unscaled.Sign;
            var twiceRem = SysBigInt.Abs(r) * 2;
            bool increment;
            switch (roundingMode)
            {
                case ROUND_UP: increment = true; break;
                case ROUND_DOWN: increment = false; break;
                case ROUND_CEILING: increment = sign > 0; break;
                case ROUND_FLOOR: increment = sign < 0; break;
                case ROUND_HALF_UP: increment = twiceRem >= divisor; break;
                case ROUND_HALF_DOWN: increment = twiceRem > divisor; break;
                case ROUND_HALF_EVEN: increment = twiceRem > divisor || (twiceRem == divisor && !(q % 2).IsZero); break;
                // Must be System.ArithmeticException: transpiled callers catch that type
                // (the converter's mapping of java.lang.ArithmeticException).
                case ROUND_UNNECESSARY: throw new ArithmeticException("Rounding necessary");
                default: increment = false; break;
            }
            return new BigDecimal(increment ? q + sign : q, newScale);
        }

        // Java semantics, except a result that would need a negative scale (integers with trailing
        // zeros, e.g. 1600 -> 1.6E+3) stops at scale 0 — value-identical.
        public BigDecimal StripTrailingZeros()
        {
            // Scale 0 has nothing strippable (this class never goes below scale 0); returning
            // early also keeps compact integers off the big path below, which would otherwise
            // box a BigInteger via Unscaled just to conclude no-op.
            if (_scale == 0)
            {
                return this;
            }
            if (Sign == 0) // Java 8+: 0.000 strips to 0
            {
                return new BigDecimal(SysBigInt.Zero, 0);
            }
            if (_scale > 0 && TryGetCompact(out long lu))
            {
                if (lu % 10L != 0)   // dominant case (fresh arithmetic results): one modulo, nothing to strip
                {
                    return this;
                }
                // Chunked trailing-zero strip (8/4/2/1): same result as the by-1 loop in ~log steps.
                // Hot case: canonicalizing long-decimal tree values ("4497.1000000000" = 9 zeros).
                int ls = _scale;
                while (ls >= 8 && lu % 100000000L == 0)
                {
                    lu /= 100000000L;
                    ls -= 8;
                }
                if (ls >= 4 && lu % 10000L == 0)
                {
                    lu /= 10000L;
                    ls -= 4;
                }
                if (ls >= 2 && lu % 100L == 0)
                {
                    lu /= 100L;
                    ls -= 2;
                }
                if (ls >= 1 && lu % 10L == 0)
                {
                    lu /= 10L;
                    ls -= 1;
                }
                return ls == _scale ? this : FromCompact(lu, ls);
            }
            // An odd mantissa cannot end in a decimal 0: nothing to strip, no BigInteger DivRem.
            // (Typical hot case: a fresh division result ending in a repeating odd digit.)
            if (!Unscaled.IsEven)
                return this;

            // Chunked strip (8/4/2/1, cached powers): clean-division quotients carry the whole shift
            // as trailing zeros (e.g. 1200 div 3 at scale 18 → 4*10^20), which the by-1 loop paid as
            // ~20 BigInteger DivRems. Identical result in ~log steps; once the value shrinks into the
            // compact range the long loop above would have taken over anyway.
            var u = Unscaled;
            int s = _scale;
            while (s >= 8)
            {
                var q8 = SysBigInt.DivRem(u, POW10_BI[8], out var r8);
                if (!r8.IsZero)
                    break;
                u = q8; s -= 8;
            }
            if (s >= 4)
            {
                var q4 = SysBigInt.DivRem(u, POW10_BI[4], out var r4);
                if (r4.IsZero)
                {
                    u = q4;
                    s -= 4;
                }
            }
            if (s >= 2)
            {
                var q2 = SysBigInt.DivRem(u, POW10_BI[2], out var r2);
                if (r2.IsZero)
                {
                    u = q2;
                    s -= 2;
                }
            }
            if (s >= 1)
            {
                var q1 = SysBigInt.DivRem(u, 10, out var r1);
                if (r1.IsZero)
                {
                    u = q1;
                    s -= 1;
                }
            }
            return s == _scale ? this : new BigDecimal(u, s);
        }

        // Java movePointLeft(n): scale becomes max(this.scale + n, 0) — never negative;
        // the mantissa absorbs the difference. (Previously produced a negative scale for
        // n < -Scale(), breaking the invariant.)
        public BigDecimal MovePointLeft(int n)
        {
            int ns = _scale + n;
            return ns >= 0 ? new BigDecimal(Unscaled, ns) : new BigDecimal(Unscaled * Pow10(-ns), 0);
        }
        // No Math.Max(0, ...) clamp here — it would silently drop a factor of 10^k when n > Scale().
        public BigDecimal MovePointRight(int n)
        {
            int ns = _scale - n;
            return ns >= 0 ? new BigDecimal(Unscaled, ns) : new BigDecimal(Unscaled * Pow10(-ns), 0);
        }
        public int Precision() => Unscaled == SysBigInt.Zero ? 1 : SysBigInt.Abs(Unscaled).ToString().Length;
        // Java toBigInteger(): truncates toward zero. Returns System.Numerics.BigInteger directly (the compat
        // wrapper struct OutSmart.DAXon.Internal.Numerics.BigInteger was retired; the engine is idiomatic
        // System.Numerics.BigInteger, with Java-semantics helpers in BigIntegers).
        public SysBigInt ToBigInteger() => Unscaled / Pow10(Math.Max(0, _scale));
        // Java toBigIntegerExact(): throws on a nonzero fractional part (was: silent truncation).
        public SysBigInt ToBigIntegerExact()
        {
            if (_scale <= 0)
                return ToBigInteger();
            var q = SysBigInt.DivRem(Unscaled, Pow10(_scale), out var r);
            if (!r.IsZero)
                throw new ArithmeticException("Rounding necessary");
            return q;
        }
        // Java pow(int n): exact — unscaled^n at scale*n; n must be in [0, 999999999]
        // (negative n throws "Invalid operation"); x.pow(0) == ONE even for x == 0.
        // (Previously went through double, garbage beyond ~15 significant digits.)
        public BigDecimal Pow(int n)
        {
            if (n < 0 || n > 999999999)
                throw new ArithmeticException("Invalid operation");
            if (n == 0)
                return One;
            return new BigDecimal(SysBigInt.Pow(Unscaled, n), _scale * n);
        }

        private static (SysBigInt a, SysBigInt b, int scale) Align(BigDecimal x, BigDecimal y)
        {
            if (x._scale == y._scale)
                return (x.Unscaled, y.Unscaled, x._scale);
            if (x._scale > y._scale)
            {
                var shift = x._scale - y._scale;
                return (x.Unscaled, y.Unscaled * Pow10(shift), x._scale);
            }
            else
            {
                var shift = y._scale - x._scale;
                return (x.Unscaled * Pow10(shift), y.Unscaled, y._scale);
            }
        }
    }
}
