////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Regex.CharClass;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;

namespace OutSmart.DAXon.Expressions.Sorting
{
    /// <summary>
    /// A StringCollator that sorts lowercase before uppercase, or vice versa.
    /// <para>Case is irrelevant, unless the strings are equal ignoring
    /// case, in which case lowercase comes first.</para>
    /// </summary>
    internal sealed class CaseFirstCollator : IStringCollator
    {
        private readonly IStringCollator baseCollator;
        private readonly bool upperFirst;
        private readonly string uri;

        public string CollationURI => uri;

        // What the substring functions match with under this collation: its strength, case order aside. Null when it has none.
        internal SimpleCollation SubstringCollation { get; set; }

        // The UCA's caseFirst (UTS #35) orders strings the collation itself finds unequal and leaves equality to it; null for
        // Java's case-order, which decides between strings that are equal ignoring case.
        internal IStringCollator Exact { get; set; }

        public CaseFirstCollator(IStringCollator @base, bool upperFirst, string collationURI)
        {
            this.baseCollator = @base;
            this.upperFirst = upperFirst;
            this.uri = collationURI;
        }

        public static IStringCollator MakeCaseOrderedCollator(string uri, IStringCollator stringCollator, string caseOrder)
        {
            switch (caseOrder)
            {
                case "lower-first":
                    stringCollator = new CaseFirstCollator(stringCollator, false, uri);
                    break;
                case "upper-first":
                    stringCollator = new CaseFirstCollator(stringCollator, true, uri);
                    break;
                default:
                    throw new XPathException("case-order must be lower-first, upper-first, or #default");
            }

            return stringCollator;
        }

        public int CompareStrings(UnicodeString a, UnicodeString b)
        {
            if (Exact == null)
            {
                int diff = baseCollator.CompareStrings(a, b);
                return diff != 0 ? diff : ByCase(a, b);
            }

            int exact = Exact.CompareStrings(a, b);
            if (exact == 0)
            {
                return 0;
            }

            int ignoringCase = baseCollator.CompareStrings(a, b);
            if (ignoringCase != 0)
            {
                return ignoringCase;
            }

#if NET
            int byCase = ByCase(a, b);
            return byCase != 0 ? byCase : exact;
#else
            // NLS finds strings equal whose letters differ in number (ß ss, æ ae, ﬁ fi), so the case of each letter cannot
            // decide. Its keys can: their case weights in the case asked for, as ExactKey orders them (its Compare does not
            // follow its own keys here, and orders Aä aÄ apart by language).
            int byAscii = AsciiCaseOnly(a, b);
            if (byAscii != 0)
            {
                return byAscii;
            }

            byte[] keyA;
            byte[] keyB;
            try
            {
                keyA = OwnKey(a);
                keyB = keyA == null ? null : OwnKey(b);
            }
            catch (XPathException e) when (e.HasErrorCode("FOCH0004"))
            {
                // NLS makes no key of tens of millions of characters: its own order, turned as asked, still answers.
                keyB = keyA = null;
            }

            if (keyB == null)
            {
                return Turned ? -exact : exact;
            }

            int byKey = upperFirst ? CompareUpperFirst(keyA, keyB) : CompareBytes(keyA, keyB);
            return byKey != 0 ? byKey : exact;
#endif
        }

#if !NET
        // Whether the collation's own case order is the other one than asked for; it is lower first for most languages.
        private bool Turned
        {
            get
            {
                if (turned == 0)
                {
                    bool lowerFirst = Exact.CompareStrings(StringView.Of("a"), StringView.Of("A")) < 0;
                    turned = lowerFirst == upperFirst ? 1 : -1;
                }

                return turned > 0;
            }
        }

        private int turned;

        // Strings that differ only in the case of ASCII letters, in a language that makes no letter of two of them (Czech ch,
        // Danish aa): each such letter is one case weight of the keys, which then differ first at the first letter that does,
        // and order there as the keys of "A" and "a" do (both the turned and the plain order). Two native keys of each pair
        // cost a sort of such ties 2-5x the time; 0 when the strings are not of that shape.
        private int AsciiCaseOnly(UnicodeString a, UnicodeString b)
        {
            long n = a.Length();
            if (n != b.Length() || UpperBeforeLower == 0)
            {
                return 0;
            }

            long first = -1;
            for (long i = 0; i < n; i++)
            {
                int x = a.CodePointAt(i);
                int y = b.CodePointAt(i);
                if (x == y)
                {
                    continue;
                }

                if ((x ^ 0x20) != y || !((x >= 'A' && x <= 'Z') || (x >= 'a' && x <= 'z')))
                {
                    return 0;
                }

                if (first < 0)
                {
                    first = i;
                }
            }

            if (first < 0)
            {
                return 0;
            }

            return a.CodePointAt(first) < 'a' ? UpperBeforeLower : -UpperBeforeLower;
        }

        // The key order of an upper case ASCII letter against its lower case, the same alone and among others; 0 in a
        // language not known to be without letters of two ASCII ones, or when the keys cannot tell, or tell otherwise.
        private int UpperBeforeLower
        {
            get
            {
                if (asciiCase == 0)
                {
                    int alone = KeyOrder("A", "a");
                    int among = KeyOrder("bAb", "bab");
                    asciiCase = NoAsciiContractions() && alone != 0 && alone == among ? alone : 2;
                }

                return asciiCase == 2 ? 0 : asciiCase;
            }
        }

        private int asciiCase;

        private bool NoAsciiContractions()
        {
            if (!(Exact is SimpleCollation simple) || !(simple.Comparator is OutSmart.DAXon.Core.DotNetPlatform.CompareInfoComparer cic))
            {
                return false;
            }

            string name = cic.CompareInfo.Name;
            int dash = name.IndexOf('-');
            switch (dash < 0 ? name : name.Substring(0, dash))
            {
                case "":
                case "en":
                case "de":
                case "fr":
                case "it":
                case "pt":
                    return true;
                default:
                    return false;
            }
        }

        private int KeyOrder(string x, string y)
        {
            byte[] keyX = OwnKey(StringView.Of(x));
            byte[] keyY = keyX == null ? null : OwnKey(StringView.Of(y));
            if (keyY == null)
            {
                return 0;
            }

            return System.Math.Sign(upperFirst ? CompareUpperFirst(keyX, keyY) : CompareBytes(keyX, keyY));
        }
#endif

        // The order of two strings equal ignoring case: by the first letters that differ in case.
        private int ByCase(UnicodeString a, UnicodeString b)
        {
            Categories.Category letters = Letters;
            Categories.Category upperCase = UpperCase;
            Categories.Category lowerCase = LowerCase;

            // This is doing a character-by-character comparison, which isn't really right.
            // There might be a sequence of letters constituting a single collation unit.
            long i = 0;
            long j = 0;
            while (true)
            {
                // Skip characters that are equal in the two strings
                while (i < a.Length() && j < b.Length() && a.CodePointAt(i) == b.CodePointAt(j))
                {
                    i++;
                    j++;
                }

                // Skip non-letters in the first string
                while (i < a.Length() && !letters.Test(a.CodePointAt(i)))
                {
                    i++;
                }

                // Skip non-letters in the second string
                while (j < b.Length() && !letters.Test(b.CodePointAt(j)))
                {
                    j++;
                }

                // If we've got to the end of either string, treat the strings as equal
                if (i >= a.Length())
                {
                    return 0;
                }

                if (j >= b.Length())
                {
                    return 0;
                }

                // If one of the characters is upper/lower case and the other isn't, the issue is decided
                bool aFirst = upperFirst ? upperCase.Test(a.CodePointAt(i++)) : lowerCase.Test(a.CodePointAt(i++));
                bool bFirst = upperFirst ? upperCase.Test(b.CodePointAt(j++)) : lowerCase.Test(b.CodePointAt(j++));
                if (aFirst && !bFirst)
                {
                    return -1;
                }

                if (bFirst && !aFirst)
                {
                    return +1;
                }
            }
        }

        public bool ComparesEqual(UnicodeString s1, UnicodeString s2)
        {
            return CompareStrings(s1, s2) == 0;
        }

        public bool IsEqualToEmpty(UnicodeString s1)
        {
            return baseCollator.IsEqualToEmpty(s1);
        }

        public IAtomicMatchKey GetCollationKey(UnicodeString s)
        {
            IAtomicMatchKey baseKey = baseCollator.GetCollationKey(s);

            // The base collator ignores case (see MakeCollation), so "abc" and "ABC" collide in its key.
            // Append a per-letter case discriminator — 0 for the case that sorts first, 1 for the other —
            // so the composite key reproduces THIS collator's case-first ordering (collation-key-009l).
            // The base key already distinguishes primary/secondary differences; the case bytes only decide
            // among strings that are equal ignoring case, mirroring CompareStrings. Only possible when the
            // base key is a byte sequence (a CompareInfo sort key).
            if (!(baseKey is OutSmart.DAXon.Values.Base64BinaryValue b64))
            {
                return baseKey;
            }

            byte[] baseBytes = b64.BinaryValue;
            byte[] exactBytes = null;
            if (Exact != null)
            {
#if NET
                // Under the UCA's caseFirst the key ends in the collation's own: equal keys are what it finds equal. The case
                // bytes then count letters only, as CompareStrings does, so an ignorable character between them does not shift them.
                if (!(Exact.GetCollationKey(s) is OutSmart.DAXon.Values.Base64BinaryValue exactKey))
                {
                    return baseKey;
                }

                exactBytes = exactKey.BinaryValue;
#else
                return ExactKey(baseBytes, s) ?? baseKey;
#endif
            }

            // One array: the case bytes are counted first.
            long length = s.Length();
            int cased = 0;
            for (long i = 0; i < length; i++)
            {
                if (exactBytes == null || Letters.Test((int)s.CodePointAt(i)))
                {
                    cased++;
                }
            }

            byte[] combined = new byte[baseBytes.Length + cased + (exactBytes?.Length ?? 0)];
            System.Array.Copy(baseBytes, combined, baseBytes.Length);
            int at = baseBytes.Length;
            for (long i = 0; i < length; i++)
            {
                int cp = (int)s.CodePointAt(i);
                if (exactBytes != null && !Letters.Test(cp))
                {
                    continue;
                }

                bool first = upperFirst ? UpperCase.Test(cp) : LowerCase.Test(cp);
                bool other = upperFirst ? LowerCase.Test(cp) : UpperCase.Test(cp);
                // Non-letters and caseless letters (byte 2) never decide against a cased letter here —
                // CompareStrings only ranks a cased letter ahead of its opposite case.
                combined[at++] = (byte)(first ? 0 : (other ? 1 : 2));
            }

            if (exactBytes != null)
            {
                System.Array.Copy(exactBytes, 0, combined, at, exactBytes.Length);
            }

            return new OutSmart.DAXon.Values.CollationKeyBytes(combined);
        }

        // Asked for once: Categories.GetCategory takes a lock that every key and comparison went through.
        private static readonly Categories.Category Letters = Categories.GetCategory("L");
        private static readonly Categories.Category UpperCase = Categories.GetCategory("Lu");
        private static readonly Categories.Category LowerCase = Categories.GetCategory("Ll");

#if !NET
        // The UCA's caseFirst: the key ignoring case, then the collation's own in the case order asked for, as CompareStrings
        // orders. Equal keys are what the collation finds equal; null when it has no byte key.
        private IAtomicMatchKey ExactKey(byte[] baseBytes, UnicodeString s)
        {
            byte[] own = OwnKey(s);
            if (own == null)
            {
                return null;
            }

            byte[] exactBytes = upperFirst ? UpperFirst(own) : own;
            // A zero after each part: no part is the start of another (the empty string's is empty), so the first decides.
            byte[] combined = new byte[baseBytes.Length + exactBytes.Length + 2];
            System.Array.Copy(baseBytes, combined, baseBytes.Length);
            System.Array.Copy(exactBytes, 0, combined, baseBytes.Length + 1, exactBytes.Length);
            if (combined.Length >= OutSmart.DAXon.Core.MemoryLedger.Big)
            {
                OutSmart.DAXon.Core.MemoryLedger.HoldText(combined);
                OutSmart.DAXon.Core.Controller.CheckActiveTimeoutNow();
            }

            return new OutSmart.DAXon.Values.CollationKeyBytes(combined);
        }

        // The collation's own key as NLS makes it; null when it has no byte key.
        private byte[] OwnKey(UnicodeString s)
        {
            return (Exact.GetCollationKey(s) as OutSmart.DAXon.Values.Base64BinaryValue)?.BinaryValue;
        }

        private const byte Separator = 1;
        private const byte LowerCaseWeight = 0x02;
        private const byte UpperCaseBit = 0x10;

        // An NLS key is primary 01 diacritics 01 case 01 the rest, a case weight an element, compared left to right; lower
        // case (02) is the least weight, so the ones at the end are left out. Upper first turns the upper case bit, and then
        // lower case is no longer the least: each other weight is written as a token of a class, the count of lower case ones
        // before it (so that count orders as lower case would) and the weight; the end is a mark between the classes below
        // lower case and above it. CompareUpperFirst orders two keys as these do. A key of another shape is left as it is.
        internal static byte[] UpperFirst(byte[] key)
        {
            if (!Sections(key, out int cases, out int rest))
            {
                return key;
            }

            int size = cases + 1 + 1 + (key.Length - rest);
            var reader = new CaseTokens(key, cases + 1, rest);
            while (reader.Next(out _, out uint lower, out _))
            {
                size += 2 + CountBytes(lower);
            }

            byte[] turned = new byte[size];
            System.Array.Copy(key, turned, cases + 1);
            int at = cases + 1;
            reader = new CaseTokens(key, cases + 1, rest);
            while (reader.Next(out byte kind, out uint lower, out byte weight))
            {
                turned[at++] = kind;
                at = WriteCount(turned, at, lower, kind == AboveLowerCase);
                turned[at++] = weight;
            }

            turned[at++] = EndOfCases;
            System.Array.Copy(key, rest, turned, at, key.Length - rest);
            return turned;
        }

        // The byte order of UpperFirst(a) against UpperFirst(b), with neither made.
        internal static int CompareUpperFirst(byte[] a, byte[] b)
        {
            if (!Sections(a, out int casesA, out int restA) || !Sections(b, out int casesB, out int restB))
            {
                return CompareBytes(UpperFirst(a), UpperFirst(b));
            }

            // Up to the case section the keys are as NLS made them; equal that far, both have it at the same place.
            int head = System.Math.Min(casesA, casesB);
            for (int i = 0; i <= head; i++)
            {
                if (a[i] != b[i])
                {
                    return a[i] < b[i] ? -1 : 1;
                }
            }

            var tokensA = new CaseTokens(a, casesA + 1, restA);
            var tokensB = new CaseTokens(b, casesB + 1, restB);
            while (true)
            {
                byte kindA = tokensA.Next(out byte k, out uint lowerA, out byte weightA) ? k : EndOfCases;
                byte kindB = tokensB.Next(out k, out uint lowerB, out byte weightB) ? k : EndOfCases;
                if (kindA != kindB)
                {
                    return kindA < kindB ? -1 : 1;
                }

                if (kindA == EndOfCases)
                {
                    break;
                }

                if (lowerA != lowerB)
                {
                    // Below lower case fewer lower case ones before it is less; above it more is less.
                    return (kindA == BelowLowerCase) == (lowerA < lowerB) ? -1 : 1;
                }

                if (weightA != weightB)
                {
                    return weightA < weightB ? -1 : 1;
                }
            }

            int n = System.Math.Min(a.Length - restA, b.Length - restB);
            for (int i = 0; i < n; i++)
            {
                if (a[restA + i] != b[restB + i])
                {
                    return a[restA + i] < b[restB + i] ? -1 : 1;
                }
            }

            return (a.Length - restA).CompareTo(b.Length - restB);
        }

        // Where the case section of an NLS key begins (its separator) and ends (the next one); false for another shape.
        private static bool Sections(byte[] key, out int cases, out int rest)
        {
            int diacritics = System.Array.IndexOf(key, Separator);
            cases = diacritics < 0 ? -1 : System.Array.IndexOf(key, Separator, diacritics + 1);
            rest = cases < 0 ? -1 : System.Array.IndexOf(key, Separator, cases + 1);
            return rest >= 0;
        }

        // The weights of a case section other than lower case, upper case bit turned, each with its class and the count of
        // lower case weights before it.
        private struct CaseTokens
        {
            private readonly byte[] key;
            private readonly int end;
            private int at;

            internal CaseTokens(byte[] key, int start, int end)
            {
                this.key = key;
                this.at = start;
                this.end = end;
            }

            internal bool Next(out byte kind, out uint lower, out byte weight)
            {
                lower = 0;
                while (at < end)
                {
                    byte w = key[at++];
                    if (w == LowerCaseWeight)
                    {
                        lower++;
                        continue;
                    }

                    weight = (byte)(w ^ UpperCaseBit);
                    kind = weight < (LowerCaseWeight ^ UpperCaseBit) ? BelowLowerCase : AboveLowerCase;
                    return true;
                }

                kind = EndOfCases;
                weight = 0;
                return false;
            }
        }

        // A count as bytes whose order is the count's, none the start of another: one byte below 0xC0, two below 0x30C0, five
        // above. Turned (each byte complemented) the order is the other way round, as the class above lower case wants.
        private static int CountBytes(uint count)
        {
            return count < OneByte ? 1 : count < TwoBytes ? 2 : 5;
        }

        private static int WriteCount(byte[] into, int at, uint count, bool turned)
        {
            byte flip = turned ? (byte)0xFF : (byte)0;
            if (count < OneByte)
            {
                into[at++] = (byte)(count ^ flip);
            }
            else if (count < TwoBytes)
            {
                uint d = count - OneByte;
                into[at++] = (byte)((OneByte + (d >> 8)) ^ flip);
                into[at++] = (byte)((d & 0xFF) ^ flip);
            }
            else
            {
                uint d = count - TwoBytes;
                into[at++] = (byte)(0xF0 ^ flip);
                into[at++] = (byte)((d >> 24) ^ flip);
                into[at++] = (byte)((d >> 16) ^ flip);
                into[at++] = (byte)((d >> 8) ^ flip);
                into[at++] = (byte)(d ^ flip);
            }

            return at;
        }

        private const uint OneByte = 0xC0;
        private const uint TwoBytes = OneByte + 0x30 * 256;

        private const byte BelowLowerCase = 2;
        private const byte EndOfCases = 3;
        private const byte AboveLowerCase = 4;

        internal static int CompareBytes(byte[] a, byte[] b)
        {
            int n = System.Math.Min(a.Length, b.Length);
            for (int i = 0; i < n; i++)
            {
                if (a[i] != b[i])
                {
                    return a[i] < b[i] ? -1 : 1;
                }
            }

            return a.Length.CompareTo(b.Length);
        }
#endif
    }
}
