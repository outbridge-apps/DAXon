////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// Implement the XPath normalize-unicode() function (both the 1-argument and 2-argument versions)
    /// </summary>
    internal sealed class NormalizeUnicode : SystemFunction
    {
        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            StringValue sv = (StringValue)arguments[0].Head();
            if (sv == null)
            {
                return StringValue.EMPTY_STRING;
            }

            string nf = arguments.Length == 1 ? "NFC" : Whitespace.Trim(arguments[1].Head().GetStringValue());
            return new StringValue(Normalize(sv.GetStringValue(), nf));
        }

        public static string Normalize(string sv, string form)
        {
            NormalizationForm fb;
            if (form.Equals("NFC", StringComparison.OrdinalIgnoreCase))
            {
                fb = NormalizationForm.FormC;
            }
            else if (form.Equals("NFD", StringComparison.OrdinalIgnoreCase))
            {
                fb = NormalizationForm.FormD;
            }
            else if (form.Equals("NFKC", StringComparison.OrdinalIgnoreCase))
            {
                fb = NormalizationForm.FormKC;
            }
            else if (form.Equals("NFKD", StringComparison.OrdinalIgnoreCase))
            {
                fb = NormalizationForm.FormKD;
            }
            else if ((form.Length == 0))
            {
                return sv;
            }
            else
            {
                string msg = "Normalization form " + form + " is not supported";
                throw new XPathException(msg, "FOCH0003");
            }

            RequireTables(sv, "FOCH0003");
            return Normalize(sv, fb);
        }

        // In .NET's InvariantGlobalization mode string.Normalize returns text past ASCII as it is: a function or a
        // serializer asked to normalize says it cannot, rather than answer wrong. (ASCII is its own normal form.)
        private static readonly bool HasTables = "é".Normalize(NormalizationForm.FormD).Length == 2;

        internal static void RequireTables(string text, string errorCode)
        {
            if (HasTables)
            {
                return;
            }

            foreach (char c in text)
            {
                if (c > 127)
                {
                    throw new XPathException("Unicode normalization needs the globalization data that InvariantGlobalization mode leaves out", errorCode);
                }
            }
        }

        // Every normalization the engine does goes through here - the function, the normalization-form of a
        // serializer, the escaping of a URI attribute.
        public static string Normalize(string sv, NormalizationForm fb)
        {
            return sv.Length < LongString ? PlatformNormalize(sv, fb) : NormalizeLong(sv, fb);
        }

        // From this length String.Normalize is given a stretch at a time, the time limit looked at between them. In one call
        // it sorts a run of n combining marks in O(n^2) (200 000 marks: 40 s), and .NET Framework's expands compatibility
        // forms superlinearly: a call the time limit cannot stop.
        private const int LongString = 4096;

        // A longer run of combining marks is sorted here (NormalizeStretch): 6 000 of them cost the platform 36 ms.
        private const int LongMarkRun = 32;

        // A stretch ends before a character that StartsStretch or CutsBefore, so stretches normalize apart as they do
        // together. ASCII is its own normal form, and text that comes back as it went is not copied.
        private static string NormalizeLong(string sv, NormalizationForm fb)
        {
            StringBuilder changed = null;
            PlatformAnswers platform = null;
            bool compatibility = fb == NormalizationForm.FormKC || fb == NormalizationForm.FormKD;
            for (int start = 0; start < sv.Length;)
            {
                Core.Controller.CheckActiveTimeoutNow();
                // One past 4 096: .NET 8 and 10 rent the answer a buffer of the next power of two, so a stretch that grows fits
                // in it at the first pass; at 4 096 ICU normalizes it twice.
                int end = Math.Min(start + LongString + 1, sv.Length);
                bool ascii = IsAscii(sv, start, end);
                while (end < sv.Length && !StartsStretch(sv[end]) && !CutsBefore(sv[end], compatibility))
                {
                    end++;
                    ascii = false;
                }

                if (ascii)
                {
                    changed?.Append(sv, start, end - start);
                }
                else
                {
                    string stretch = end - start == sv.Length ? sv : sv.Substring(start, end - start);
                    string normal;
                    if (stretch.Length <= 2 * LongString && !HasLongMarkRun(sv, start, end, compatibility))
                    {
                        normal = IsNormal(stretch, fb) ? stretch : PlatformNormalize(stretch, fb);
                    }
                    else
                    {
                        platform = platform ?? new PlatformAnswers(compatibility ? NormalizationForm.FormKD : NormalizationForm.FormD);
                        normal = NormalizeStretch(stretch, fb, platform);
                    }

                    if (changed == null && !string.Equals(normal, stretch, StringComparison.Ordinal))
                    {
                        changed = Held(new StringBuilder(sv.Length)).Append(sv, 0, start);
                    }

                    changed?.Append(normal);
                }

                start = end;
            }

            return changed == null ? sv : changed.ToString();
        }

        // A starter that composes with nothing before it, in any form: ASCII, and what is written without it for thousands
        // of characters on end - CJK ideographs (U+3400 to U+9FFF), kana letters and Hangul syllables.
        private static bool StartsStretch(char c)
        {
            return c < 0x80 || (c >= 0x3400 && c <= 0x9FFF) || (c >= 0xAC00 && c <= 0xD7A3)
                || (c >= 0x3041 && c <= 0x3096) || (c >= 0x30A1 && c <= 0x30FA);
        }

        // Past a stretch's length, a starter of the plane whose decomposition in the form begins with a starter: the only
        // starters that compose with what goes before them are conjoining jamo, on every runtime the engine runs on.
        // Asked of the platform once a character, so text with no spaces (Cyrillic, Thai) still has places to be cut.
        private static bool CutsBefore(char c, bool compatibility)
        {
            byte[] known = compatibility ? Cuts.Compatibility : Cuts.Canonical;
            if (known[c] == 0)
            {
                known[c] = Cuts.Ask(c, compatibility) ? (byte)1 : (byte)2;
            }

            return known[c] == 1;
        }

        // A byte a code unit: 0 not asked yet, 1 a stretch may end before it, 2 not.
        private static class Cuts
        {
            internal static readonly byte[] Canonical = new byte[0x10000];
            internal static readonly byte[] Compatibility = new byte[0x10000];

            internal static bool Ask(char c, bool compatibility)
            {
                bool cut = IsStarter(c);
                if (cut)
                {
                    try
                    {
                        string d = c.ToString().Normalize(compatibility ? NormalizationForm.FormKD : NormalizationForm.FormD);
                        cut = d.Length > 0 && IsStarter(d[0]);
                    }
                    catch (ArgumentException)
                    {
                        // A noncharacter.
                        cut = false;
                    }
                }

                return cut;
            }

            // No mark, nor a code point the platform's tables do not know, no half of a pair and no conjoining jamo.
            private static bool IsStarter(char c)
            {
                return c < 0x300 || (!char.IsSurrogate(c) && (MarkCategories.OfCodeUnits[c >> 6] & (1UL << (c & 63))) == 0
                    && !(c >= 0x1100 && c <= 0x11FF) && !(c >= 0xA960 && c <= 0xA97F) && !(c >= 0xD7B0 && c <= 0xD7FF));
            }
        }

        // Whether s[start..end) is ASCII.
        private static bool IsAscii(string s, int start, int end)
        {
#if NET
            return Ascii.IsValid(s.AsSpan(start, end - start));
#else
            bool ascii = true;
            unsafe
            {
                fixed (char* text = s)
                {
                    char* c = text + start;
                    char* last = text + end;
                    for (; ascii && last - c >= 4; c += 4)
                    {
                        ascii = (*(ulong*)c & 0xFF80FF80FF80FF80UL) == 0;
                    }

                    for (; ascii && c < last; c++)
                    {
                        ascii = *c < 0x80;
                    }
                }
            }

            return ascii;
#endif
        }

        // .NET Framework's String.Normalize copies text that is in the form already, twice; its IsNormalized copies nothing.
        private static bool IsNormal(string stretch, NormalizationForm fb)
        {
#if NETFRAMEWORK
            try
            {
                return stretch.IsNormalized(fb);
            }
            catch (System.ArgumentException)
            {
                return false;
            }
#else
            return false;
#endif
        }

        // Whether more than LongMarkRun characters in a row of s[start..end) may be combining marks: by general category,
        // as the classes are not exposed. One the platform's tables do not know may be a mark of a later Unicode. Such a
        // run covers LongMarkRun + 1 code units in a row, so one unit in that many is looked at. In NFKC and NFKD the
        // half-width sound marks U+FF9E and U+FF9F are marks too: their compatibility decompositions are.
        internal static bool HasLongMarkRun(string s, int start, int end, bool compatibility)
        {
            ulong[] marks = compatibility ? MarkCategories.InCompatibilityForms : MarkCategories.OfCodeUnits;
            bool found = false;
            for (int at = start + LongMarkRun; at < end && !found; at += LongMarkRun + 1)
            {
                found = InMark(s, at, start, end, marks) && HasLongMarkRunAbout(s, at, start, end, marks);
            }

            return found;
        }

        // Whether the code unit is a combining mark or half of one.
        private static bool InMark(string s, int at, int start, int end, ulong[] marks)
        {
            char c = s[at];
            if (c < 0x300)
            {
                return false;
            }

            if (!char.IsSurrogate(c))
            {
                return (marks[c >> 6] & (1UL << (c & 63))) != 0;
            }

            int high = char.IsHighSurrogate(c) ? at : at - 1;
            return high >= start && high + 1 < end && char.IsSurrogatePair(s[high], s[high + 1])
                && MarkCategories.Has(CharUnicodeInfo.GetUnicodeCategory(s, high));
        }

        // As far back as the run about a mark is counted from: LongMarkRun + 1 paired marks and half a pair fit.
        private const int MarkRunReach = 2 * (LongMarkRun + 3);

        // Whether the marks in a row about the one at this code unit are more than LongMarkRun.
        private static bool HasLongMarkRunAbout(string s, int at, int start, int end, ulong[] marks)
        {
            int from = at;
            while (from > start && at - from < MarkRunReach
                && (char.IsSurrogate(s[from - 1]) || (s[from - 1] >= 0x300 && (marks[s[from - 1] >> 6] & (1UL << (s[from - 1] & 63))) != 0)))
            {
                from--;
            }

            int run = 0;
            bool ended = false;
            for (int i = from; i < end && !ended && run <= LongMarkRun; i++)
            {
                char c = s[i];
                bool mark = c >= 0x300 && (marks[c >> 6] & (1UL << (c & 63))) != 0;
                if (!mark && char.IsHighSurrogate(c) && i + 1 < end && char.IsLowSurrogate(s[i + 1]))
                {
                    mark = MarkCategories.Has(CharUnicodeInfo.GetUnicodeCategory(s, i));
                    i++;
                }

                ended = !mark && i >= at;
                run = mark ? run + 1 : 0;
            }

            return run > LongMarkRun;
        }

        // Built at the first long string past ASCII: a category looked up a character cost as much as normalizing it.
        private static class MarkCategories
        {
            // A bit a UTF-16 code unit; half a pair has none.
            internal static readonly ulong[] OfCodeUnits = Build();

            // The same, and the two characters that are no mark but decompose to one in NFKD.
            internal static readonly ulong[] InCompatibilityForms = WithSoundMarks(OfCodeUnits);

            internal static bool Has(UnicodeCategory category)
            {
                return category == UnicodeCategory.NonSpacingMark || category == UnicodeCategory.SpacingCombiningMark
                    || category == UnicodeCategory.EnclosingMark || category == UnicodeCategory.OtherNotAssigned;
            }

            private static ulong[] Build()
            {
                var bits = new ulong[1024];
                for (int c = 0x300; c < 0x10000; c++)
                {
                    if (Has(CharUnicodeInfo.GetUnicodeCategory((char)c)))
                    {
                        bits[c >> 6] |= 1UL << (c & 63);
                    }
                }

                return bits;
            }

            private static ulong[] WithSoundMarks(ulong[] marks)
            {
                var bits = (ulong[])marks.Clone();
                bits[0xFF9E >> 6] |= (1UL << (0xFF9E & 63)) | (1UL << (0xFF9F & 63));
                return bits;
            }
        }

        // A stretch with no character to cut it at, or with such a run: decomposed here, a code point at a time, and String.Normalize
        // given only what is linear for it.
        private static string NormalizeStretch(string sv, NormalizationForm fb, PlatformAnswers platform)
        {
            var decomposed = Held(new StringBuilder(sv.Length));
            bool runs = false;       // two non-starters in a row: their order may need putting right
            bool lastMark = false;
            long steps = 0;
            for (int i = 0; i < sv.Length;)
            {
                Core.Controller.CheckActiveTimeoutEvery64K(steps++);
                char c = sv[i];
                if (c < 0x80)
                {
                    decomposed.Append(c);
                    lastMark = false;
                    i++;
                    continue;
                }

                int length = char.IsHighSurrogate(c) && i + 1 < sv.Length && char.IsLowSurrogate(sv[i + 1]) ? 2 : 1;
                string parts = platform.Decomposition(sv, i, length);
                for (int k = 0; k < parts.Length; k += char.IsSurrogatePair(parts, k) ? 2 : 1)
                {
                    bool mark = platform.IsNonStarter(parts, k);
                    runs |= mark && lastMark;
                    lastMark = mark;
                }

                decomposed.Append(parts);
                i += length;
            }

            string d = runs ? platform.InCanonicalOrder(decomposed.ToString()) : decomposed.ToString();
            Core.MemoryLedger.Hold(d, 22 + 2L * d.Length);
            if (!(fb == NormalizationForm.FormC || fb == NormalizationForm.FormKC))
            {
                return d;
            }

            // Composed a stretch at a time, each cut before an ASCII character: a starter that composes with nothing
            // before it, so the stretches compose apart as they do together.
            var composed = Held(new StringBuilder(d.Length));
            for (int start = 0; start < d.Length;)
            {
                Core.Controller.CheckActiveTimeout();
                int end = Math.Min(start + LongString, d.Length);
                while (end < d.Length && d[end] >= 0x80)
                {
                    end++;
                }

                composed.Append(PlatformNormalize(d.Substring(start, end - start), fb));
                start = end;
            }

            return composed.ToString();
        }

        // What String.Normalize says of single code points, asked once a call: their decompositions, and the order of
        // the combining classes of marks, which .NET does not expose, as it reorders them in pairs.
        private sealed class PlatformAnswers
        {
            private readonly NormalizationForm decomposition;
            private readonly Dictionary<int, string> decompositions = new Dictionary<int, string>();
            private readonly Dictionary<int, bool> nonStarters = new Dictionary<int, bool>();

            internal PlatformAnswers(NormalizationForm decomposition)
            {
                this.decomposition = decomposition;
            }

            internal string Decomposition(string s, int at, int length)
            {
                int cp = length == 2 ? char.ConvertToUtf32(s[at], s[at + 1]) : s[at];
                if (!decompositions.TryGetValue(cp, out string parts))
                {
                    parts = s.Substring(at, length);
                    try
                    {
                        parts = parts.Normalize(decomposition);
                    }
                    catch (ArgumentException)
                    {
                        // A noncharacter or half a pair stays as it is (see NormalizeAroundNonChars).
                    }

                    decompositions.Add(cp, parts);
                }

                return parts;
            }

            // Combining class above zero. U+0345 has the highest class (240), so another mark goes before it.
            internal bool IsNonStarter(string s, int at)
            {
                int cp = char.IsSurrogatePair(s, at) ? char.ConvertToUtf32(s[at], s[at + 1]) : s[at];
                if (cp < 0x300)
                {
                    return false;
                }

                if (!nonStarters.TryGetValue(cp, out bool mark))
                {
                    switch (CharUnicodeInfo.GetUnicodeCategory(s, at))
                    {
                        case UnicodeCategory.NonSpacingMark:
                        case UnicodeCategory.SpacingCombiningMark:
                        case UnicodeCategory.EnclosingMark:
                        case UnicodeCategory.OtherNotAssigned:
                            string one = char.ConvertFromUtf32(cp);
                            mark = cp == 0x345 || Swapped("ͅ", one);
                            break;
                        default:
                            mark = false;
                            break;
                    }

                    nonStarters.Add(cp, mark);
                }

                return mark;
            }

            // Whether the class of a is above that of b, both marks: the platform puts the pair the other way round.
            private static bool Swapped(string a, string b)
            {
                try
                {
                    return string.Equals((a + b).Normalize(NormalizationForm.FormD), b + a, StringComparison.Ordinal);
                }
                catch (ArgumentException)
                {
                    return false;
                }
            }

            // Each run of marks sorted by combining class, stably (the canonical ordering algorithm). A run that is in
            // order is copied as it is; one that is not is sorted by a key a mark - class, then place - not a string a
            // mark, so a long run stays within the memory limit.
            internal string InCanonicalOrder(string d)
            {
                var marks = new List<string>();
                foreach (KeyValuePair<int, bool> e in nonStarters)
                {
                    if (e.Value)
                    {
                        marks.Add(char.ConvertFromUtf32(e.Key));
                    }
                }

                marks.Sort((x, y) => Swapped(x, y) ? 1 : Swapped(y, x) ? -1 : 0);
                var rank = new Dictionary<int, int>(marks.Count);
                int last = 0;
                for (int k = 0; k < marks.Count; k++)
                {
                    last = k == 0 ? 0 : last + (Swapped(marks[k], marks[k - 1]) ? 1 : 0);
                    rank.Add(char.ConvertToUtf32(marks[k], 0), last);
                }

                var ordered = Held(new StringBuilder(d.Length));
                long steps = 0;
                int runStart = -1;
                bool inOrder = true;
                int lastRank = 0;
                for (int i = 0; i <= d.Length;)
                {
                    Core.Controller.CheckActiveTimeoutEvery64K(steps++);
                    int length = i < d.Length && char.IsSurrogatePair(d, i) ? 2 : 1;
                    if (i < d.Length && IsNonStarter(d, i))
                    {
                        int r = rank[length == 2 ? char.ConvertToUtf32(d[i], d[i + 1]) : d[i]];
                        inOrder = runStart < 0 || (inOrder && r >= lastRank);
                        runStart = runStart < 0 ? i : runStart;
                        lastRank = r;
                    }
                    else
                    {
                        if (runStart >= 0)
                        {
                            AppendRun(ordered, d, runStart, i, inOrder, rank);
                            runStart = -1;
                        }

                        if (i < d.Length)
                        {
                            ordered.Append(d, i, length);
                        }
                    }

                    i += length;
                }

                return ordered.ToString();
            }

            // The marks of d[start..end) in canonical order onto the builder.
            private static void AppendRun(StringBuilder ordered, string d, int start, int end, bool inOrder, Dictionary<int, int> rank)
            {
                if (inOrder)
                {
                    ordered.Append(d, start, end - start);
                }
                else
                {
                    var keys = new List<long>();
                    Core.MemoryLedger.Hold(keys, KeyBytes);
                    for (int i = start; i < end; i += char.IsSurrogatePair(d, i) ? 2 : 1)
                    {
                        int cp = char.IsSurrogatePair(d, i) ? char.ConvertToUtf32(d[i], d[i + 1]) : d[i];
                        keys.Add(((long)rank[cp] << 32) | (uint)i);
                    }

                    // Class, then place: a stable sort.
                    keys.Sort();
                    long steps = 0;
                    foreach (long key in keys)
                    {
                        Core.Controller.CheckActiveTimeoutEvery64K(steps++);
                        int at = (int)(key & 0xFFFFFFFF);
                        ordered.Append(d, at, char.IsSurrogatePair(d, at) ? 2 : 1);
                    }
                }
            }

            private static readonly Func<object, long> KeyBytes = o => 8L * ((List<long>)o).Capacity;
        }

        // A builder of the call, in its memory limit as it grows.
        private static StringBuilder Held(StringBuilder builder)
        {
            Core.MemoryLedger.Hold(builder, BuilderBytes);
            return builder;
        }

        private static readonly Func<object, long> BuilderBytes = o => 2L * ((StringBuilder)o).Capacity;

        private static string PlatformNormalize(string sv, NormalizationForm fb)
        {
            try
            {
                return sv.Normalize(fb);
            }
            catch (System.ArgumentException)
            {
                // .NET's String.Normalize rejects a whole string that contains any noncharacter code point
                // (U+FDD0–U+FDEF, U+xFFFE/U+xFFFF) with ArgumentException; Java's ICU normalizer passes each
                // noncharacter through unchanged (they have no decomposition/composition) while still
                // normalizing the surrounding text. Match Java by normalizing each maximal noncharacter-free
                // run and re-inserting the noncharacters verbatim. Half a surrogate pair goes the same way.
                return NormalizeAroundNonChars(sv, fb);
            }
        }

        private static bool IsNonChar(int cp)
        {
            return (cp >= 0xFDD0 && cp <= 0xFDEF) || (cp & 0xFFFE) == 0xFFFE || (cp >= 0xD800 && cp <= 0xDFFF);
        }

        private static string NormalizeAroundNonChars(string sv, NormalizationForm fb)
        {
            StringBuilder outb = new StringBuilder(sv.Length);
            StringBuilder run = new StringBuilder();
            int i = 0;
            while (i < sv.Length)
            {
                char c = sv[i];
                bool pair = char.IsHighSurrogate(c) && i + 1 < sv.Length && char.IsLowSurrogate(sv[i + 1]);
                int cp = pair ? char.ConvertToUtf32(c, sv[i + 1]) : c;
                int adv = pair ? 2 : 1;
                if (IsNonChar(cp))
                {
                    if (run.Length > 0)
                    {
                        outb.Append(run.ToString().Normalize(fb));
                        run.Clear();
                    }
                    outb.Append(sv, i, adv);
                }
                else
                {
                    run.Append(sv, i, adv);
                }

                i += adv;
            }

            if (run.Length > 0)
            {
                outb.Append(run.ToString().Normalize(fb));
            }
            return outb.ToString();
        }
    }
}