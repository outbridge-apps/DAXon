////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Collections;
namespace OutSmart.DAXon.Serialization.CharCodes
{
    /// <summary>
    /// A class to hold some static constants and methods associated with processing UTF16 and surrogate pairs
    /// </summary>
    internal sealed class UTF16CharacterSet : ICharacterSet
    {

        public const int NONBMP_MIN = 0x10000;
        public const int NONBMP_MAX = 0x10FFFF;
        private static readonly UTF16CharacterSet theInstance = new UTF16CharacterSet();
        public static readonly char SURROGATE1_MIN = (char)0xD800;
        public static readonly char SURROGATE1_MAX = (char)0xDBFF;
        public static readonly char SURROGATE2_MIN = (char)0xDC00;
        public static readonly char SURROGATE2_MAX = (char)0xDFFF;

        public string CanonicalName => "UTF-16";
        private UTF16CharacterSet()
        {
        }

        public static UTF16CharacterSet GetInstance()
        {
            return theInstance;
        }

        public bool InCharset(int c)
        {
            return true;
        }
        public static int CombinePair(char high, char low)
        {
            return (high - SURROGATE1_MIN) * 0x400 + (low - SURROGATE2_MIN) + NONBMP_MIN;
        }

        public static char HighSurrogate(int ch)
        {
            return (char)(((ch - NONBMP_MIN) >> 10) + SURROGATE1_MIN);
        }

        public static char LowSurrogate(int ch)
        {
            return (char)(((ch - NONBMP_MIN) & 0x3FF) + SURROGATE2_MIN);
        }

        public static bool IsSurrogate(int c)
        {
            return (c & 0xF800) == 0xD800;
        }

        public static bool IsHighSurrogate(int ch)
        {
            return (SURROGATE1_MIN <= ch && ch <= SURROGATE1_MAX);
        }

        public static bool IsLowSurrogate(int ch)
        {
            return (SURROGATE2_MIN <= ch && ch <= SURROGATE2_MAX);
        }

        public static int FirstInvalidChar(IIntIterator iter, IIntPredicateProxy predicate)
        {
            while (iter.MoveNext())
            {
                int ch32 = iter.Current;
                if (!predicate.Test(ch32))
                {
                    return ch32;
                }
            }

            return -1;
        }
    }
}