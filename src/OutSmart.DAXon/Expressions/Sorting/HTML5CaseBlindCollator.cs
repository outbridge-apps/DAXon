////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Expressions.Sorting
{
    internal sealed class HTML5CaseBlindCollator : IStringCollator, ISubstringMatcher
    {
        private static readonly HTML5CaseBlindCollator theInstance = new HTML5CaseBlindCollator();

        public string CollationURI => NamespaceConstant.HTML5_CASE_BLIND_COLLATION_URI;
        public static HTML5CaseBlindCollator GetInstance()
        {
            return theInstance;
        }

        public int CompareStrings(UnicodeString a, UnicodeString b)
        {

            // Note that Java does UTF-16 code unit comparison, which is not the same as Unicode codepoint comparison
            // except in the "equals" case. So we have to do a character-by-character comparison
            return CompareCS(a, b);
        }

        private int CompareCS(UnicodeString a, UnicodeString b)
        {
            long alen = a.Length();
            long blen = b.Length();
            long i = 0;
            long j = 0;
            while (true)
            {
                if (i == alen)
                {
                    if (j == blen)
                    {
                        return 0;
                    }
                    else
                    {
                        return -1;
                    }
                }

                if (j == blen)
                {
                    return +1;
                }

                int nexta = a.CodePointAt(i++);
                int nextb = b.CodePointAt(j++);
                if (nexta >= 'a' && nexta <= 'z')
                {
                    nexta += 'A' - 'a';
                }

                if (nextb >= 'a' && nextb <= 'z')
                {
                    nextb += 'A' - 'a';
                }

                int c = nexta - nextb;
                if (c != 0)
                {
                    return c;
                }
            }
        }

        // Java IStringCollator.isEqualToEmpty default method (no DIM on net472 -> emitted per-impl).
        public bool IsEqualToEmpty(UnicodeString s1)
        {
            return ComparesEqual(s1, EmptyUnicodeString.GetInstance());
        }
        public bool ComparesEqual(UnicodeString s1, UnicodeString s2)
        {
            return CompareCS(s1, s2) == 0;
        }

        public bool Contains(UnicodeString s1, UnicodeString s2)
        {
            return Normalize(s1).IndexOf(Normalize(s2), 0) >= 0;
        }

        public bool EndsWith(UnicodeString s1, UnicodeString s2)
        {
            return Normalize(s1).HasSubstring(Normalize(s2), s1.Length() - s2.Length());
        }

        public bool StartsWith(UnicodeString s1, UnicodeString s2)
        {
            return Normalize(s1).HasSubstring(Normalize(s2), 0);
        }

        public UnicodeString SubstringAfter(UnicodeString s1, UnicodeString s2)
        {
            long i = Normalize(s1).IndexOf(Normalize(s2), 0);
            if (i < 0)
            {
                return EmptyUnicodeString.GetInstance();
            }

            return s1.Substring(i + s2.Length(), s1.Length());
        }

        public UnicodeString SubstringBefore(UnicodeString s1, UnicodeString s2)
        {
            long j = Normalize(s1).IndexOf(Normalize(s2), 0);
            if (j < 0)
            {
                return EmptyUnicodeString.GetInstance();
            }

            return s1.Prefix(j);
        }

        public IAtomicMatchKey GetCollationKey(UnicodeString s)
        {
            return Normalize(s);
        }

        /// <summary>
        /// Normalize the strings prior to comparison for substring-comparison operations
        /// </summary>
        private UnicodeString Normalize(UnicodeString cs)
        {
            UnicodeBuilder sb = new UnicodeBuilder(cs.Length32());
            IIntIterator iter = cs.CodePoints();
            while (iter.MoveNext())
            {
                int c = iter.Current;
                if ('a' <= c && c <= 'z')
                {
                    sb.Append((char)(c + 'A' - 'a'));
                }
                else
                {
                    sb.Append(c);
                }
            }

            return sb.ToUnicodeString();
        }
    }
}