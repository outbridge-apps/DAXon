////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Text;
namespace OutSmart.DAXon.Expressions.Sorting
{
    /// <summary>
    /// A collating sequence that uses Unicode codepoint ordering
    /// </summary>
    internal sealed class CodepointCollator : IStringCollator, ISubstringMatcher
    {
        private static readonly CodepointCollator theInstance = new CodepointCollator();

        public string CollationURI => NamespaceConstant.CODEPOINT_COLLATION_URI;
        public static CodepointCollator GetInstance()
        {
            return theInstance;
        }

        public int CompareStrings(UnicodeString a, UnicodeString b)
        {
            return a.CompareTo(b);
        }

        public bool ComparesEqual(UnicodeString s1, UnicodeString s2)
        {
            return s1.Equals(s2);
        }

        public bool Contains(UnicodeString s1, UnicodeString s2)
        {
            return s1.IndexOf(s2, 0) >= 0;
        }

        public bool EndsWith(UnicodeString s1, UnicodeString s2)
        {
            if (s2.Length() > s1.Length())
            {
                return false;
            }

            return s1.HasSubstring(s2, s1.Length() - s2.Length());
        }

        public bool StartsWith(UnicodeString s1, UnicodeString s2)
        {
            return s1.HasSubstring(s2, 0);
        }

        public UnicodeString SubstringAfter(UnicodeString s1, UnicodeString s2)
        {
            long i = s1.IndexOf(s2, 0);
            if (i < 0)
            {
                return EmptyUnicodeString.GetInstance();
            }

            return s1.Substring(i + s2.Length());
        }

        public UnicodeString SubstringBefore(UnicodeString s1, UnicodeString s2)
        {
            long j = s1.IndexOf(s2, 0);
            if (j < 0)
            {
                return EmptyUnicodeString.GetInstance();
            }

            return s1.Prefix(j);
        }

        public IAtomicMatchKey GetCollationKey(UnicodeString s)
        {
            return s;
        }

        public bool IsEqualToEmpty(UnicodeString s1)
        {
            return s1.IsEmpty();
        }
    }
}