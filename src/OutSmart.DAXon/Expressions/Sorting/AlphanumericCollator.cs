////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Regex;
using OutSmart.DAXon.Regex.CharClass;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Internal.Numerics;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
using System.IO;
using System.Numerics;
namespace OutSmart.DAXon.Expressions.Sorting
{
    internal sealed class AlphanumericCollator : IStringCollator
    {
        public const string PREFIX = "http://saxon.sf.net/collation/alphaNumeric?base=";
        private static readonly ARegularExpression pattern = ARegularExpression.Compile("\\d+", "");
        private readonly IStringCollator baseCollator;

        public string CollationURI => PREFIX + baseCollator.CollationURI;
        public AlphanumericCollator(IStringCollator @base)
        {
            baseCollator = @base;
        }

        public int CompareStrings(UnicodeString cs1, UnicodeString cs2)
        {
            IRegexIterator iter1 = pattern.Analyze(cs1);
            IRegexIterator iter2 = pattern.Analyze(cs2);
            while (true)
            {

                // find the next numeric or non-numeric substring in each string
                StringValue sv1 = iter1.Next();
                StringValue sv2 = iter2.Next();
                if (sv1 == null)
                {
                    return sv2 == null ? 0 : -1;
                }

                if (sv2 == null)
                {
                    return +1;
                }

                bool numeric1 = iter1.IsMatching();
                bool numeric2 = iter2.IsMatching();
                if (numeric1 && numeric2)
                {
                    BigInteger n1 = ParseDigits(sv1.GetStringValue());
                    BigInteger n2 = ParseDigits(sv2.GetStringValue());
                    int c = n1.CompareTo(n2);
                    if (c != 0)
                    {
                        return c;
                    }
                }
                else
                {
                    UnicodeString u1 = numeric1 ? EmptyUnicodeString.GetInstance() : sv1.UnicodeStringValue;
                    UnicodeString u2 = numeric2 ? EmptyUnicodeString.GetInstance() : sv2.UnicodeStringValue;
                    int c = baseCollator.CompareStrings(u1, u2);
                    if (c != 0)
                    {
                        return c;
                    }
                } // otherwise, the substrings are equal: move on to the next part of the string
            }
        }

        // Java IStringCollator.isEqualToEmpty default method (no DIM on net472 -> emitted per-impl).
        public bool IsEqualToEmpty(UnicodeString s1)
        {
            return ComparesEqual(s1, EmptyUnicodeString.GetInstance());
        }
        public bool ComparesEqual(UnicodeString s1, UnicodeString s2)
        {
            return CompareStrings(s1, s2) == 0;
        }

        public IAtomicMatchKey GetCollationKey(UnicodeString cs)
        {

            // See bug 5049
            MemoryStream baos = new MemoryStream();
            IRegexIterator iter = pattern.Analyze(cs);
            for (StringValue sv; (sv = iter.Next()) != null;)
            {
                if (iter.IsMatching())
                {

                    // numeric part
                    BigInteger n = ParseDigits(sv.GetStringValue());
                    byte[] bin = n.ToByteArray();
                    int len = bin.Length;

                    // Assume max length of numeric part 255
                    WriteByte(baos, (byte)0); // separator from previous alpha part
                    WriteByte(baos, (byte)len);
                    baos.Write(bin, 0, bin.Length); // written this way to avoid checked exceptions
                }
                else
                {
                    Base64BinaryValue b64 = AlphanumericCollationKey(sv.UnicodeStringValue, baseCollator);
                    byte[] bin = b64.BinaryValue;
                    baos.Write(bin, 0, bin.Length);
                }
            }

            return new Base64BinaryValue(baos.ToArray());
        }

        // The pattern takes every Unicode decimal digit, as Java's BigInteger reads them; the ASCII-only parse threw on the rest.
        private static BigInteger ParseDigits(string digits)
        {
            int i = 0;
            while (i < digits.Length && digits[i] <= '9')
            {
                i++;
            }

            if (i == digits.Length)
            {
                return BigIntegers.FromString(digits);
            }

            BigInteger value = BigInteger.Zero;
            for (i = 0; i < digits.Length; i++)
            {
                int codePoint = digits[i];
                if (char.IsHighSurrogate(digits[i]) && i + 1 < digits.Length)
                {
                    codePoint = char.ConvertToUtf32(digits[i], digits[++i]);
                }

                value = value * 10 + DigitValue(codePoint);
            }

            return value;
        }

        // Decimal digits are encoded in runs of ten, zero first: the value is the offset into the run.
        internal static int DigitValue(int codePoint)
        {
            if (codePoint <= '9')
            {
                return codePoint - '0';
            }

            int start = codePoint;
            while (Categories.ESCAPE_d.Test(start - 1))
            {
                start--;
            }

            return (codePoint - start) % 10;
        }

        // Inlined faithful copy of functions/CollationKeyFn.GetCollationKey (that class is <Compile Remove>'d).
        private static Base64BinaryValue AlphanumericCollationKey(UnicodeString s, IStringCollator collator)
        {
            AtomicValue val = collator.GetCollationKey(s).AsAtomic();
            if (val is Base64BinaryValue)
            {
                return (Base64BinaryValue)val;
            }
            if (val is StringValue)
            {
                return ((StringValue)val).CodepointCollationKey;
            }
            throw new InvalidOperationException("Collation key must be Base64Binary");
        }
        private static void WriteByte(MemoryStream baos, byte val)
        {
            baos.WriteByte(val);
        }
    }
}