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
            // Java's mappings that .NET's invariant tables lack, on .NET Framework or on both (every codepoint
            // compared against Java 17; the ones only .NET 10 maps, from a later Unicode, are left to it).
            { 0x0131, new[] { 0x0049 } },
            { 0x017F, new[] { 0x0053 } },
            { 0x023F, new[] { 0x2C7E } },
            { 0x0240, new[] { 0x2C7F } },
            { 0x0252, new[] { 0x2C70 } },
            { 0x0265, new[] { 0xA78D } },
            { 0x0266, new[] { 0xA7AA } },
            { 0x0345, new[] { 0x0399 } },
            { 0x0525, new[] { 0x0524 } },
            { 0x0527, new[] { 0x0526 } },
            { 0x1E9B, new[] { 0x1E60 } },
            { 0x1F50, new[] { 0x03A5, 0x0313 } },
            { 0x1F52, new[] { 0x03A5, 0x0313, 0x0300 } },
            { 0x1F54, new[] { 0x03A5, 0x0313, 0x0301 } },
            { 0x1F56, new[] { 0x03A5, 0x0313, 0x0342 } },
            { 0x1F80, new[] { 0x1F08, 0x0399 } },
            { 0x1F81, new[] { 0x1F09, 0x0399 } },
            { 0x1F82, new[] { 0x1F0A, 0x0399 } },
            { 0x1F83, new[] { 0x1F0B, 0x0399 } },
            { 0x1F84, new[] { 0x1F0C, 0x0399 } },
            { 0x1F85, new[] { 0x1F0D, 0x0399 } },
            { 0x1F86, new[] { 0x1F0E, 0x0399 } },
            { 0x1F87, new[] { 0x1F0F, 0x0399 } },
            { 0x1F88, new[] { 0x1F08, 0x0399 } },
            { 0x1F89, new[] { 0x1F09, 0x0399 } },
            { 0x1F8A, new[] { 0x1F0A, 0x0399 } },
            { 0x1F8B, new[] { 0x1F0B, 0x0399 } },
            { 0x1F8C, new[] { 0x1F0C, 0x0399 } },
            { 0x1F8D, new[] { 0x1F0D, 0x0399 } },
            { 0x1F8E, new[] { 0x1F0E, 0x0399 } },
            { 0x1F8F, new[] { 0x1F0F, 0x0399 } },
            { 0x1F90, new[] { 0x1F28, 0x0399 } },
            { 0x1F91, new[] { 0x1F29, 0x0399 } },
            { 0x1F92, new[] { 0x1F2A, 0x0399 } },
            { 0x1F93, new[] { 0x1F2B, 0x0399 } },
            { 0x1F94, new[] { 0x1F2C, 0x0399 } },
            { 0x1F95, new[] { 0x1F2D, 0x0399 } },
            { 0x1F96, new[] { 0x1F2E, 0x0399 } },
            { 0x1F97, new[] { 0x1F2F, 0x0399 } },
            { 0x1F98, new[] { 0x1F28, 0x0399 } },
            { 0x1F99, new[] { 0x1F29, 0x0399 } },
            { 0x1F9A, new[] { 0x1F2A, 0x0399 } },
            { 0x1F9B, new[] { 0x1F2B, 0x0399 } },
            { 0x1F9C, new[] { 0x1F2C, 0x0399 } },
            { 0x1F9D, new[] { 0x1F2D, 0x0399 } },
            { 0x1F9E, new[] { 0x1F2E, 0x0399 } },
            { 0x1F9F, new[] { 0x1F2F, 0x0399 } },
            { 0x1FA0, new[] { 0x1F68, 0x0399 } },
            { 0x1FA1, new[] { 0x1F69, 0x0399 } },
            { 0x1FA2, new[] { 0x1F6A, 0x0399 } },
            { 0x1FA3, new[] { 0x1F6B, 0x0399 } },
            { 0x1FA4, new[] { 0x1F6C, 0x0399 } },
            { 0x1FA5, new[] { 0x1F6D, 0x0399 } },
            { 0x1FA6, new[] { 0x1F6E, 0x0399 } },
            { 0x1FA7, new[] { 0x1F6F, 0x0399 } },
            { 0x1FA8, new[] { 0x1F68, 0x0399 } },
            { 0x1FA9, new[] { 0x1F69, 0x0399 } },
            { 0x1FAA, new[] { 0x1F6A, 0x0399 } },
            { 0x1FAB, new[] { 0x1F6B, 0x0399 } },
            { 0x1FAC, new[] { 0x1F6C, 0x0399 } },
            { 0x1FAD, new[] { 0x1F6D, 0x0399 } },
            { 0x1FAE, new[] { 0x1F6E, 0x0399 } },
            { 0x1FAF, new[] { 0x1F6F, 0x0399 } },
            { 0x1FB2, new[] { 0x1FBA, 0x0399 } },
            { 0x1FB3, new[] { 0x0391, 0x0399 } },
            { 0x1FB4, new[] { 0x0386, 0x0399 } },
            { 0x1FB6, new[] { 0x0391, 0x0342 } },
            { 0x1FB7, new[] { 0x0391, 0x0342, 0x0399 } },
            { 0x1FBC, new[] { 0x0391, 0x0399 } },
            { 0x1FBE, new[] { 0x0399 } },
            { 0x1FC2, new[] { 0x1FCA, 0x0399 } },
            { 0x1FC3, new[] { 0x0397, 0x0399 } },
            { 0x1FC4, new[] { 0x0389, 0x0399 } },
            { 0x1FC6, new[] { 0x0397, 0x0342 } },
            { 0x1FC7, new[] { 0x0397, 0x0342, 0x0399 } },
            { 0x1FCC, new[] { 0x0397, 0x0399 } },
            { 0x1FD2, new[] { 0x0399, 0x0308, 0x0300 } },
            { 0x1FD3, new[] { 0x0399, 0x0308, 0x0301 } },
            { 0x1FD6, new[] { 0x0399, 0x0342 } },
            { 0x1FD7, new[] { 0x0399, 0x0308, 0x0342 } },
            { 0x1FE2, new[] { 0x03A5, 0x0308, 0x0300 } },
            { 0x1FE3, new[] { 0x03A5, 0x0308, 0x0301 } },
            { 0x1FE4, new[] { 0x03A1, 0x0313 } },
            { 0x1FE6, new[] { 0x03A5, 0x0342 } },
            { 0x1FE7, new[] { 0x03A5, 0x0308, 0x0342 } },
            { 0x1FF2, new[] { 0x1FFA, 0x0399 } },
            { 0x1FF3, new[] { 0x03A9, 0x0399 } },
            { 0x1FF4, new[] { 0x038F, 0x0399 } },
            { 0x1FF6, new[] { 0x03A9, 0x0342 } },
            { 0x1FF7, new[] { 0x03A9, 0x0342, 0x0399 } },
            { 0x1FFC, new[] { 0x03A9, 0x0399 } },
            { 0x2CEC, new[] { 0x2CEB } },
            { 0x2CEE, new[] { 0x2CED } },
            { 0x2CF3, new[] { 0x2CF2 } },
            { 0x2D27, new[] { 0x10C7 } },
            { 0x2D2D, new[] { 0x10CD } },
            { 0xA661, new[] { 0xA660 } },
            { 0xA791, new[] { 0xA790 } },
            { 0xA793, new[] { 0xA792 } },
            { 0xA7A1, new[] { 0xA7A0 } },
            { 0xA7A3, new[] { 0xA7A2 } },
            { 0xA7A5, new[] { 0xA7A4 } },
            { 0xA7A7, new[] { 0xA7A6 } },
            { 0xA7A9, new[] { 0xA7A8 } },
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
            // Java's mappings that .NET's invariant tables lack, on .NET Framework or on both (every codepoint
            // compared against Java 17; the ones only .NET 10 maps, from a later Unicode, are left to it).
            { 0x0524, new[] { 0x0525 } },
            { 0x0526, new[] { 0x0527 } },
            { 0x10C7, new[] { 0x2D27 } },
            { 0x10CD, new[] { 0x2D2D } },
            { 0x1E9E, new[] { 0x00DF } },
            { 0x2126, new[] { 0x03C9 } },
            { 0x212A, new[] { 0x006B } },
            { 0x212B, new[] { 0x00E5 } },
            { 0x2C70, new[] { 0x0252 } },
            { 0x2C7E, new[] { 0x023F } },
            { 0x2C7F, new[] { 0x0240 } },
            { 0x2CEB, new[] { 0x2CEC } },
            { 0x2CED, new[] { 0x2CEE } },
            { 0x2CF2, new[] { 0x2CF3 } },
            { 0xA660, new[] { 0xA661 } },
            { 0xA78D, new[] { 0x0265 } },
            { 0xA790, new[] { 0xA791 } },
            { 0xA792, new[] { 0xA793 } },
            { 0xA7A0, new[] { 0xA7A1 } },
            { 0xA7A2, new[] { 0xA7A3 } },
            { 0xA7A4, new[] { 0xA7A5 } },
            { 0xA7A6, new[] { 0xA7A7 } },
            { 0xA7A8, new[] { 0xA7A9 } },
            { 0xA7AA, new[] { 0x0266 } },
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
            if (!Slice8.Latin1Span(us, out byte[] b, out int off, out int end))
            {
                return null;
            }

            int len = end - off;

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
