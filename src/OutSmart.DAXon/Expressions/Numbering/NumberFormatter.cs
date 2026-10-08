////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Regex.CharClass;
using OutSmart.DAXon.Collections;
using System.Collections.Generic;
using System.Text;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Internal;
using System.Numerics;
namespace OutSmart.DAXon.Expressions.Numbering
{
    internal sealed class NumberFormatter
    {

        private static readonly IIntPredicateProxy alphanumeric = IntUnionPredicate.MakeUnion(Categories.GetCategory("N"), (Categories.GetCategory("L")));
        private List<UnicodeString> formatTokens;
        private List<UnicodeString> punctuationTokens;
        private bool startsWithPunctuation;
        public void Prepare(string format)
        {

            // Tokenize the format string into alternating alphanumeric and non-alphanumeric tokens
            if ((format.Length == 0))
            {
                format = "1";
            }

            formatTokens = new List<UnicodeString>(10);
            punctuationTokens = new List<UnicodeString>(10);
            UnicodeString uFormat = StringView.Tidy(format);
            int len = uFormat.Length32();
            int i = 0;
            int t;
            bool first = true;
            startsWithPunctuation = true;
            while (i < len)
            {
                int c = uFormat.CodePointAt(i);
                t = i;
                while (IsLetterOrDigit(c))
                {
                    i++;
                    if (i == len)
                        break;
                    c = uFormat.CodePointAt(i);
                }

                if (i > t)
                {
                    UnicodeString tok = uFormat.Substring(t, i);
                    formatTokens.Add(tok);
                    if (first)
                    {
                        punctuationTokens.Add(BMPString.Of("."));
                        startsWithPunctuation = false;
                        first = false;
                    }
                }

                if (i == len)
                    break;
                t = i;
                c = uFormat.CodePointAt(i);
                while (!IsLetterOrDigit(c))
                {
                    first = false;
                    i++;
                    if (i == len)
                        break;
                    c = uFormat.CodePointAt(i);
                }

                if (i > t)
                {
                    UnicodeString sep = uFormat.Substring(t, i);
                    punctuationTokens.Add(sep);
                }
            }

            if (formatTokens.Count == 0)
            {
                formatTokens.Add(BMPString.Of("1"));
                if (punctuationTokens.Count == 1)
                {
                    punctuationTokens.Add(punctuationTokens[0]);
                }
            }
        }

        public static bool IsLetterOrDigit(int c)
        {
            if (c <= 0x7F)
            {

                // Fast path for ASCII characters
                return (c >= 0x30 && c <= 0x39) || (c >= 0x41 && c <= 0x5A) || (c >= 0x61 && c <= 0x7A);
            }
            else
            {
                return alphanumeric.Test(c);
            }
        }
        public UnicodeString Format(IList<object> numbers, int groupSize, string groupSeparator, string letterValue, string ordinal, INumberer numberer)
        {
            UnicodeBuilder sb = new UnicodeBuilder(32);
            int num = 0;
            int tok = 0;

            // output first punctuation token
            if (startsWithPunctuation)
            {
                sb.Accept(punctuationTokens[tok]);
            }


            // output the list of numbers
            RegularGroupFormatter rgf = new RegularGroupFormatter(groupSize, groupSeparator, EmptyUnicodeString.GetInstance());
            while (num < numbers.Count)
            {
                if (num > 0)
                {
                    if (tok == 0 && startsWithPunctuation)
                    {

                        // The first punctuation token isn't a separator if it appears before the first
                        // formatting token. Such a punctuation token is used only once, at the start.
                        sb.Append('.');
                    }
                    else
                    {
                        sb.Accept(punctuationTokens[tok]);
                    }
                }

                object o = numbers[num++];
                string s;
                if (o is long)
                {
                    long nr = (long)o;
                    s = numberer.Format(nr, formatTokens[tok], rgf, letterValue, "", ordinal);
                }
                else if (o is BigInteger)
                {

                    // Saxon bug 2071; test case number-0111
                    s = rgf.Format(o.ToString());
                    s = TranslateDigits(s, formatTokens[tok]);
                }
                else
                {

                    // Not sure this can happen
                    s = o.ToString();
                }

                sb.Append(s);
                tok++;
                if (tok == formatTokens.Count)
                {
                    tok--;
                }
            }


            // output the final punctuation token
            if (punctuationTokens.Count > formatTokens.Count)
            {
                sb.Accept(punctuationTokens[punctuationTokens.Count - 1]);
            }

            return sb.ToUnicodeString();
        }

        private string TranslateDigits(string @in, UnicodeString picture)
        {
            if (picture.Length() == 0)
            {
                return @in;
            }

            int formchar = picture.CodePointAt(0);
            int digitValue = Alphanumeric.GetDigitValue(formchar);
            if (digitValue >= 0)
            {
                int zero = formchar - digitValue;
                if (zero == (int)'0')
                {
                    return @in;
                }

                int[] digits = new int[10];
                for (int z = 0; z <= 9; z++)
                {
                    digits[z] = zero + z;
                }

                StringBuilder sb = new StringBuilder(128);
                for (int i = 0; i < @in.Length; i++)
                {
                    char c = @in[i];
                    if (c >= '0' && c <= '9')
                    {
                        sb.AppendCodePoint(digits[c - '0']);
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }

                return sb.ToString();
            }
            else
            {
                return @in;
            }
        }
    }
}