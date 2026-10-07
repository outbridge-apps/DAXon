////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// fn:upper-case / fn:lower-case.
//
// Case mapping is FULL (per XPath F&O §5.4.7): the result may contain more characters than the
// argument. .NET's string.ToUpper/ToLowerInvariant give only the SIMPLE (1:1) mapping, so the
// Unicode SpecialCasing one-to-many expansions (ß→SS, the ﬀ-ﬆ ligatures, the Armenian ligatures,
// İ→i̇, …) are applied here as a codepoint-level overlay on top of the simple mapping. Only the
// UNCONDITIONAL, language-independent SpecialCasing entries are included; locale-sensitive ones
// (Turkish/Lithuanian) and the context-dependent Greek final-sigma rule are deliberately omitted —
// fn:upper-case/lower-case run in the language-neutral (root) locale. Codepoint-by-codepoint casing
// is context-free, so it matches whole-string ToUpper/ToLowerInvariant for the simple cases.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Values;

namespace OutSmart.DAXon.Functions
{

    // fn:upper-case/lower-case: Call-only impl (String_1 pattern) — the full UpperCase/LowerCase ports drag
    // the StringElaborator cluster.
    internal sealed class UpperLowerCaseFn : ScalarSystemFunction
    {

        // UPPERCASE overlay: unconditional one-to-many SpecialCasing.txt entries PLUS the few simple (1:1)
        // UnicodeData.txt mappings that .NET's ToUpperInvariant fails to apply (MICRO SIGN → GREEK CAPITAL MU,
        // and the Latin title-case digraphs, which .NET treats as already-cased).
        private static readonly Dictionary<int, int[]> UpperSpecial = new Dictionary<int, int[]>
        {
            { 0x00B5, new[] { 0x039C } },                         // µ  MICRO SIGN → Μ (simple, missed by .NET)
            { 0x01C5, new[] { 0x01C4 } },                         // ǅ  → Ǆ  (title-case digraph, simple)
            { 0x01C8, new[] { 0x01C7 } },                         // ǈ  → Ǉ
            { 0x01CB, new[] { 0x01CA } },                         // ǋ  → Ǌ
            { 0x01F2, new[] { 0x01F1 } },                         // ǲ  → Ǳ
            { 0x00DF, new[] { 0x0053, 0x0053 } },                 // ß  LATIN SMALL LETTER SHARP S
            { 0x0149, new[] { 0x02BC, 0x004E } },                 // ŉ  N PRECEDED BY APOSTROPHE
            { 0x01F0, new[] { 0x004A, 0x030C } },                 // ǰ  J WITH CARON
            { 0x0390, new[] { 0x0399, 0x0308, 0x0301 } },         // ΐ  GREEK ι WITH DIALYTIKA AND TONOS
            { 0x03B0, new[] { 0x03A5, 0x0308, 0x0301 } },         // ΰ  GREEK υ WITH DIALYTIKA AND TONOS
            // Greek symbol/final-form variants: simple 1:1 uppercase that .NET's ToUpperInvariant misses.
            { 0x03C2, new[] { 0x03A3 } },                         // ς  FINAL SIGMA → Σ
            { 0x03D0, new[] { 0x0392 } },                         // ϐ  BETA SYMBOL → Β
            { 0x03D1, new[] { 0x0398 } },                         // ϑ  THETA SYMBOL → Θ
            { 0x03D5, new[] { 0x03A6 } },                         // ϕ  PHI SYMBOL → Φ
            { 0x03D6, new[] { 0x03A0 } },                         // ϖ  PI SYMBOL → Π
            { 0x03F0, new[] { 0x039A } },                         // ϰ  KAPPA SYMBOL → Κ
            { 0x03F1, new[] { 0x03A1 } },                         // ϱ  RHO SYMBOL → Ρ
            { 0x03F5, new[] { 0x0395 } },                         // ϵ  LUNATE EPSILON SYMBOL → Ε
            { 0x0587, new[] { 0x0535, 0x0552 } },                 // և  ARMENIAN SMALL LIGATURE ECH YIWN
            { 0x1E96, new[] { 0x0048, 0x0331 } },
            { 0x1E97, new[] { 0x0054, 0x0308 } },
            { 0x1E98, new[] { 0x0057, 0x030A } },
            { 0x1E99, new[] { 0x0059, 0x030A } },
            { 0x1E9A, new[] { 0x0041, 0x02BE } },
            { 0xFB00, new[] { 0x0046, 0x0046 } },                 // ﬀ  FF
            { 0xFB01, new[] { 0x0046, 0x0049 } },                 // ﬁ  FI
            { 0xFB02, new[] { 0x0046, 0x004C } },                 // ﬂ  FL
            { 0xFB03, new[] { 0x0046, 0x0046, 0x0049 } },         // ﬃ  FFI
            { 0xFB04, new[] { 0x0046, 0x0046, 0x004C } },         // ﬄ  FFL
            { 0xFB05, new[] { 0x0053, 0x0054 } },                 // ﬅ  ST
            { 0xFB06, new[] { 0x0053, 0x0054 } },                 // ﬆ  ST
            { 0xFB13, new[] { 0x0544, 0x0546 } },                 // ﬓ  ARMENIAN MEN NOW
            { 0xFB14, new[] { 0x0544, 0x0535 } },                 // ﬔ  ARMENIAN MEN ECH
            { 0xFB15, new[] { 0x0544, 0x053B } },                 // ﬕ  ARMENIAN MEN INI
            { 0xFB16, new[] { 0x054E, 0x0546 } },                 // ﬖ  ARMENIAN VEW NOW
            { 0xFB17, new[] { 0x0544, 0x053D } },                 // ﬗ  ARMENIAN MEN XEH
        };

        // LOWERCASE overlay: the İ one-to-many expansion plus the title-case digraphs (simple, missed by .NET).
        private static readonly Dictionary<int, int[]> LowerSpecial = new Dictionary<int, int[]>
        {
            { 0x0130, new[] { 0x0069, 0x0307 } },                 // İ  LATIN CAPITAL LETTER I WITH DOT ABOVE
            { 0x03F4, new[] { 0x03B8 } },                         // ϴ  GREEK CAPITAL THETA SYMBOL → θ (simple, missed by .NET)
            { 0x01C5, new[] { 0x01C6 } },                         // ǅ  → ǆ  (title-case digraph, simple)
            { 0x01C8, new[] { 0x01C9 } },                         // ǈ  → ǉ
            { 0x01CB, new[] { 0x01CC } },                         // ǋ  → ǌ
            { 0x01F2, new[] { 0x01F3 } },                         // ǲ  → ǳ
        };
        private readonly bool _upper;
        public UpperLowerCaseFn(bool upper) { _upper = upper; }
        public static Func<UpperLowerCaseFn> NewUpper() => () => new UpperLowerCaseFn(true);
        public static Func<UpperLowerCaseFn> NewLower() => () => new UpperLowerCaseFn(false);

        public override AtomicValue Evaluate(IItem arg, IXPathContext context)
        {
            // ASCII 8-bit content maps byte-for-byte, skipping the String materialization +
            // per-char MapCase + UnicodeString rewrap. Latin1 0x80-0xFF stays on the generic
            // route (µ/ß/ÿ case outside Latin1 or expand).
            AtomicValue fast = TryAsciiCase(arg.UnicodeStringValue, _upper);
            if (fast != null)
            {
                return fast;
            }

            return new StringValue(MapCase(arg.GetStringValue(), _upper));
        }

        private static AtomicValue TryAsciiCase(UnicodeString us, bool upper)
        {
            byte[] b;
            int off, len;
            if (us is Slice8 s8)
            {
                b = s8.ByteArray;
                off = s8.Start;
                len = s8.End - s8.Start;
            }
            else if (us is Twine8 t8)
            {
                b = t8.ByteArray;
                off = 0;
                len = b.Length;
            }
            else
            {
                return null;
            }

            int firstChange = -1;
            for (int k = 0; k < len; k++)
            {
                byte c = b[off + k];
                if (c >= 0x80)
                {
                    return null;
                }

                if (upper ? c >= (byte)'a' && c <= (byte)'z' : c >= (byte)'A' && c <= (byte)'Z')
                {
                    if (firstChange < 0)
                    {
                        firstChange = k;
                    }
                }
            }

            if (firstChange < 0)
            {
                return new StringValue(us);
            }

            byte[] mapped = new byte[len];
            Array.Copy(b, off, mapped, 0, len);
            for (int k = firstChange; k < len; k++)
            {
                byte c = mapped[k];
                if (upper ? c >= (byte)'a' && c <= (byte)'z' : c >= (byte)'A' && c <= (byte)'Z')
                {
                    mapped[k] = (byte)(c ^ 0x20);
                }
            }

            return new StringValue(new Twine8(mapped));
        }

        private static string MapCase(string s, bool upper)
        {
            var special = upper ? UpperSpecial : LowerSpecial;

            // Identity fast path: no char changes case, none is special (all special keys are
            // > 0x7F), no surrogates - return the argument itself with no allocation at all.
            int start = 0;
            while (start < s.Length)
            {
                char c = s[start];
                if (char.IsSurrogate(c) || (c > 0x7F && special.ContainsKey(c)))
                {
                    break;
                }

                if ((upper ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c)) != c)
                {
                    break;
                }

                Core.Controller.CheckActiveTimeoutEvery64K(start++);   // both loops look at the limits every 64K characters
            }

            if (start == s.Length)
            {
                return s;
            }

            var sb = new StringBuilder(s.Length + 2);
            sb.Append(s, 0, start);
            int i = start;
            int look = i + 65536;
            while (i < s.Length)
            {
                if (i >= look)
                {
                    Core.Controller.CheckActiveTimeoutNow();
                    look = i + 65536;
                }

                char c = s[i];
                if (!char.IsSurrogate(c))
                {
                    // BMP fast path: char-level invariant mapping, no per-char string allocations;
                    // the special overlay only holds codepoints > 0x7F, so ASCII skips the lookup
                    if (c > 0x7F && special.TryGetValue(c, out int[] expansion))
                    {
                        foreach (int e in expansion)
                            sb.Append(char.ConvertFromUtf32(e));
                    }
                    else
                    {
                        sb.Append(upper ? char.ToUpperInvariant(c) : char.ToLowerInvariant(c));
                    }

                    i++;
                    continue;
                }

                int cp = char.ConvertToUtf32(s, i);
                if (special.TryGetValue(cp, out int[] astralExpansion))
                {
                    foreach (int e in astralExpansion)
                        sb.Append(char.ConvertFromUtf32(e));
                }
                else
                {
                    // Simple (1:1) case mapping, context-free; ConvertFromUtf32 handles astral planes.
                    string one = char.ConvertFromUtf32(cp);
                    sb.Append(upper ? one.ToUpperInvariant() : one.ToLowerInvariant());
                }

                i += 2;
            }

            return sb.ToString();
        }

        // upper-case(()) / lower-case(()) return "" (upstream UpperCase/LowerCase resultWhenEmpty);
        // returning () would violate the declared ONE cardinality and NRE downstream comparisons.
        public override ISequence ResultWhenEmpty()
        {
            return StringValue.EMPTY_STRING;
        }
    }
}
