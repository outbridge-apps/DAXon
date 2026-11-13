////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Regex;
using OutSmart.DAXon.Regex.CharClass;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Values;
using System;
using OutSmart.DAXon.Internal;
using System.IO;
using System.Numerics;
namespace OutSmart.DAXon.Expressions.Sorting
{
    internal sealed class AlphanumericCollator : IStringCollator
    {
        public const string PREFIX = "http://saxon.sf.net/collation/alphaNumeric?base=";
        // Compiled on first use, not in the type initializer: see FormatDate.componentPattern.
        private static ARegularExpression patternLazy;
        private static ARegularExpression pattern => patternLazy ??= ARegularExpression.Compile("\\d+", "");
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
                    // Big-endian, as Java's toByteArray: .NET's little-endian bytes put 256 before 255 in a key comparison.
                    byte[] bin = n.ToByteArray();
                    Array.Reverse(bin);
                    int len = bin.Length;

                    // The length orders the numbers: under 255 bytes one byte, else 255 and four bytes big-endian, so a
                    // longer number still sorts later. (byte)len made 614-617 digits sort before a 1-digit number.
                    baos.WriteByte(0); // separator from previous alpha part
                    if (len < 255)
                    {
                        baos.WriteByte((byte)len);
                    }
                    else
                    {
                        baos.WriteByte(255);
                        baos.WriteByte((byte)(len >> 24));
                        baos.WriteByte((byte)(len >> 16));
                        baos.WriteByte((byte)(len >> 8));
                        baos.WriteByte((byte)len);
                    }

                    baos.Write(bin, 0, bin.Length);
                }
                else
                {
                    Base64BinaryValue b64 = Functions.CollationKeyFn.GetCollationKey(sv.UnicodeStringValue, baseCollator);
                    byte[] bin = b64.BinaryValue;
                    baos.Write(bin, 0, bin.Length);
                }
            }

            return new CollationKeyBytes(baos.ToArray());
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
    }
}