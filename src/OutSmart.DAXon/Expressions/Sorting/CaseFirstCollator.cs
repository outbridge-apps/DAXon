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
            a = a.Tidy();
            b = b.Tidy();
            Categories.Category letters = Categories.GetCategory("L");
            Categories.Category upperCase = Categories.GetCategory("Lu");
            Categories.Category lowerCase = Categories.GetCategory("Ll");
            int diff = baseCollator.CompareStrings(a, b);
            if (diff != 0)
            {
                return diff;
            }

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
            if (baseKey is OutSmart.DAXon.Values.Base64BinaryValue b64)
            {
                UnicodeString t = s.Tidy();
                Categories.Category upperCase = Categories.GetCategory("Lu");
                Categories.Category lowerCase = Categories.GetCategory("Ll");
                byte[] baseBytes = b64.BinaryValue;
                byte[] combined = new byte[baseBytes.Length + (int)t.Length()];
                System.Array.Copy(baseBytes, combined, baseBytes.Length);
                int pos = baseBytes.Length;
                for (long i = 0; i < t.Length(); i++)
                {
                    int cp = (int)t.CodePointAt(i);
                    bool first = upperFirst ? upperCase.Test(cp) : lowerCase.Test(cp);
                    bool other = upperFirst ? lowerCase.Test(cp) : upperCase.Test(cp);
                    // Non-letters and caseless letters (byte 2) never decide against a cased letter here —
                    // CompareStrings only ranks a cased letter ahead of its opposite case.
                    combined[pos++] = (byte)(first ? 0 : (other ? 1 : 2));
                }

                return new OutSmart.DAXon.Values.Base64BinaryValue(combined);
            }

            return baseKey;
        }
    }
}
