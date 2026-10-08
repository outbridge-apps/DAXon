////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Internal.Numerics;
using System;
using System.Collections.Generic;
using System.Text;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// Implementation of format-number() function. Note this has no dependency on number formatting in the JDK.
    /// </summary>
    internal sealed class FormatNumber : SystemFunction, ICallable, IStatefulSystemFunction
    {
        private StructuredQName decimalFormatName; // null for the default format
        private string picture;
        private DecimalSymbols decimalSymbols;
        private SubPicture[] subPictures;

        // Per-call-site memo: the parsed sub-pictures for the last-seen (picture, symbols) pair
        // plus a bounded double-bits -> formatted-string cache. format-number is a pure function
        // of (value, picture, symbols), so replaying a cached result is byte-identical; entries
        // are immutable and the direct-mapped slots tolerate races (a lost write just recomputes).
        // Typical price columns carry few distinct values, so the Dragon4 + BigDecimal + picture
        // pipeline runs once per distinct double instead of once per row.
        private sealed class FmtEntry
        {
            internal readonly long bits;
            internal readonly string result;
            internal FmtEntry(long bits, string result) { this.bits = bits; this.result = result; }
        }

        private sealed class PicsMemo
        {
            internal readonly string picture;
            internal readonly DecimalSymbols dfs;
            internal readonly SubPicture[] pics;
            internal readonly FmtEntry[] cache = new FmtEntry[2048];
            internal PicsMemo(string picture, DecimalSymbols dfs, SubPicture[] pics)
            {
                this.picture = picture;
                this.dfs = dfs;
                this.pics = pics;
            }
        }

        private volatile PicsMemo picsMemo;

        private StringValue FormatCached(NumericValue number, PicsMemo memo)
        {
            if (number is DoubleValue dv)
            {
                long bits = BitConverter.DoubleToInt64Bits(dv.GetDoubleValue());
                int idx = (int)(((ulong)(bits * -7046029254386353131L)) >> 53) & 2047;
                FmtEntry e = memo.cache[idx];
                if (e != null && e.bits == bits)
                {
                    return new StringValue(e.result);
                }

                string r = FormatNumberFn(number, memo.pics, memo.dfs);
                memo.cache[idx] = new FmtEntry(bits, r);
                return new StringValue(r);
            }

            return new StringValue(FormatNumberFn(number, memo.pics, memo.dfs));
        }
        public override Expression FixArguments(params Expression[] arguments)
        {
            if (arguments[1] is Literal && (arguments.Length == 2 || arguments[2] is StringLiteral))
            {
                DecimalFormatManager dfm = GetRetainedStaticContext().GetDecimalFormatManager();
                picture = ((StringLiteral)arguments[1]).Stringify();
                if (arguments.Length == 3 && !Literal.IsEmptySequence(arguments[2]))
                {
                    try
                    {
                        string lexicalName = ((StringLiteral)arguments[2]).Stringify();
                        decimalFormatName = StructuredQName.FromLexicalQName(lexicalName, false, true, GetRetainedStaticContext());
                    }
                    catch (XPathException e)
                    {
                        throw new XPathException("Invalid decimal format name. " + e.Message, "FODF1280");
                    }
                }

                if (decimalFormatName == null)
                {
                    decimalSymbols = dfm.DefaultDecimalFormat;
                }
                else
                {
                    decimalSymbols = dfm.GetNamedDecimalFormat(decimalFormatName);
                    if (decimalSymbols == null)
                    {
                        throw new XPathException("Decimal format " + decimalFormatName.DisplayName + " has not been defined", "FODF1280");
                    }
                }

                subPictures = GetSubPictures(picture, decimalSymbols);
            }

            return null;
        }

        private static SubPicture[] GetSubPictures(string picture, DecimalSymbols dfs)
        {
            // expand by CODEPOINTS — the port's StringView is BMP-only, and an astral separator/digit
            // in the picture (numberformat70/71/123) must be one picture character, not two surrogates
            int[] picture4 = ExpandCodePoints(picture);
            SubPicture[] pics = new SubPicture[2];
            if (picture4.Length == 0)
            {
                throw new XPathException("format-number() picture is zero-length", "FODF1310");
            }

            int sep = -1;
            for (int c = 0; c < picture4.Length; c++)
            {
                if (picture4[c] == dfs.GetPatternSeparator())
                {
                    if (c == 0)
                    {
                        Grumble("first subpicture is zero-length");
                    }
                    else if (sep >= 0)
                    {
                        Grumble("more than one pattern separator");
                    }
                    else if (sep == picture4.Length - 1)
                    {
                        Grumble("second subpicture is zero-length");
                    }

                    sep = c;
                }
            }

            if (sep < 0)
            {
                pics[0] = MakeSubPicture(picture4, dfs);
                pics[1] = null;
            }
            else
            {
                int[] pic0 = new int[sep];
                Array.Copy(picture4, 0, pic0, 0, sep);
                int[] pic1 = new int[picture4.Length - sep - 1];
                Array.Copy(picture4, sep + 1, pic1, 0, picture4.Length - sep - 1);
                pics[0] = MakeSubPicture(pic0, dfs);
                pics[1] = MakeSubPicture(pic1, dfs);
            }

            return pics;
        }

        protected static SubPicture MakeSubPicture(int[] details, DecimalSymbols dfs)
        {
            return new SubPicture(details, dfs);
        }

        private static string FormatNumberFn(NumericValue number, SubPicture[] subPictures, DecimalSymbols dfs)
        {
            NumericValue absN = number;
            SubPicture pic;
            string minusSign = "";
            int signum = number.Signum();
            if (signum == 0 && number.IsNegativeZero())
            {
                signum = -1;
            }

            if (signum < 0)
            {
                absN = number.Negate();
                if (subPictures[1] == null)
                {
                    pic = subPictures[0];
                    minusSign = StringFromCodepoint(dfs.GetMinusSign());
                }
                else
                {
                    pic = subPictures[1];
                }
            }
            else
            {
                pic = subPictures[0];
            }

            return pic.Format(absN, dfs, minusSign);
        }

        private static int[] ExpandCodePoints(string s)
        {
            var list = new List<int>(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                if (char.IsHighSurrogate(s[i]) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
                {
                    list.Add(char.ConvertToUtf32(s[i], s[i + 1]));
                    i++;
                }
                else
                {
                    list.Add(s[i]);
                }
            }

            return list.ToArray();
        }

        private static string StringFromCodepoint(int codepoint)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendCodePoint(codepoint);
            return sb.ToString();
        }

        private static void Grumble(string s)
        {
            throw new XPathException("format-number picture: " + s, "FODF1310");
        }

        public static BigDecimal AdjustToDecimal(double value, int precision)
        {
            // Fast path: a double that is exactly an integer in [-2^53, 2^53] is already its own
            // shortest decimal, so skip Dragon4 (ConvertDouble), the string round-trip and the
            // zeros/nines heuristic — the general path would parse "…E…" back to this same integer
            // (scale 0) and find nothing to simplify. Byte-identical, and (long) is exact in range.
            // The heavy caller is format-number(number(.), …) over columns of integer-valued prices.
            if (value == Math.Floor(value) && Math.Abs(value) < 9007199254740992.0)
            {
                return new BigDecimal((long)value);
            }

            // Fast path 2 (double precision only): build the BigDecimal straight from Dragon4's
            // own shortest digits (long twin), skipping the exponential string, its BigDecimal
            // parse, DecimalToString and the zeros/nines scan. Shape mirrors the string parse
            // exactly: scale = fracDigits - exponent, single significant digit prints as "d.0E…"
            // (unscaled d*10). The 9-char zeros/nines scan is provably a no-op at <= 16
            // significant digits (two distinct decimals that short cannot both round-trip to one
            // double); 17-digit forms and the float path (precision 1, 5-char runs are live on
            // short forms) fall through to the general path. Differential-tested against the
            // pre-fix pipeline in javacompat-tests.
            if (precision != 1 && OutSmart.DAXon.Values.FloatingPointConverter.TryShortestDigits(Math.Abs(value), out long dg, out int nd, out int h))
            {
                int scale = (nd == 1 ? 1 : nd - 1) - h;
                if (nd <= 16 && scale >= 1)
                {
                    long unscaled = nd == 1 ? dg * 10 : dg;
                    return BigDecimal.FromCompact(value < 0 ? -unscaled : unscaled, scale);
                }
            }

            string zeros = precision == 1 ? "00000" : "000000000";
            string nines = precision == 1 ? "99999" : "999999999";
            // Start from the SHORTEST round-trip decimal of the double (Java's BigDecimal.valueOf uses
            // Double.toString). BigDecimal.ValueOf here used "G17" (always 17 sig digits) which injected
            // rounding noise — format-number(1E25,'#') printed 10000000000000001000000000 instead of 10^25.
            BigDecimal initial = new BigDecimal(OutSmart.DAXon.Values.FloatingPointConverter.ConvertDouble(value, true).ToString());
            BigDecimal trial = null;
            StringBuilder fsb = new StringBuilder(16);
            BigDecimalValue.DecimalToString(initial, fsb);
            string s = fsb.ToString();
            int start = s[0] == '-' ? 1 : 0;
            int p = s.IndexOf('.');
            int i = s.LastIndexOf(zeros, StringComparison.Ordinal);
            if (i > 0)
            {
                if (p < 0 || i < p)
                {

                    // we're in the integer part
                    // try replacing all following digits with zeros and seeing if we get the same double back
                    StringBuilder sb = new StringBuilder(s.Length);
                    sb.Append(s.Substring(0, i));
                    for (int n = i; n < s.Length; n++)
                    {
                        sb.Append(s[n] == '.' ? '.' : '0');
                    }

                    trial = new BigDecimal(sb.ToString());
                }
                else
                {

                    // we're in the fractional part
                    // try truncating the number before the zeros and seeing if we get the same double back
                    trial = new BigDecimal(s.Substring(0, i));
                }
            }
            else
            {
                i = s.IndexOf(nines, StringComparison.Ordinal);
                if (i >= 0)
                {
                    if (i == start)
                    {

                        // number starts with 99999... or -99999. Try rounding up to 100000.. or -100000...
                        StringBuilder sb = new StringBuilder(s.Length + 1);
                        if (start == 1)
                        {
                            sb.Append('-');
                        }

                        sb.Append('1');
                        for (int n = start; n < s.Length; n++)
                        {
                            sb.Append(s[n] == '.' ? '.' : '0');
                        }

                        trial = new BigDecimal(sb.ToString());
                    }
                    else
                    {

                        // try rounding up
                        while (i >= 0 && (s[i] == '9' || s[i] == '.'))
                        {
                            i--;
                        }

                        if (i < 0 || s[i] == '-')
                        {
                            return initial; // can't happen: we've already handled numbers starting 99999..
                        }
                        else if (p < 0 || i < p)
                        {

                            // we're in the integer part
                            StringBuilder sb = new StringBuilder(s.Length);
                            sb.Append(s.Substring(0, i));
                            sb.Append((char)((int)s[i] + 1));
                            for (int n = i; n < s.Length; n++)
                            {
                                sb.Append(s[n] == '.' ? '.' : '0');
                            }

                            trial = new BigDecimal(sb.ToString());
                        }
                        else
                        {

                            // we're in the fractional part - can ignore following digits
                            string s2 = s.Substring(0, i) + (char)((int)s[i] + 1);
                            trial = new BigDecimal(s2);
                        }
                    }
                }
            }

            if (trial != null && (precision == 1 ? trial.FloatValue() == value : trial.DoubleValue() == value))
            {
                return trial;
            }
            else
            {
                return initial;
            }
        }

        /* 4.7.4 Rule 3 */
        /* 4.7.4 Rule 8 */
        /* 4.7.4 Rule 9 */
        /* 4.7.4 Rule 10 */
        private static int[] Insert(int[] array, int used, int value, int position)
        {
            if (used + 1 > array.Length)
            {
                Array.Resize(ref array, used + 10);
            }

            Array.Copy(array, position, array, position + 1, used - position);
            array[position] = value;
            return array;
        }

        /* 4.7.4 Rule 3 */
        /* 4.7.4 Rule 8 */
        /* 4.7.4 Rule 9 */
        /* 4.7.4 Rule 10 */
        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            int numArgs = arguments.Length;
            DecimalFormatManager dfm = GetRetainedStaticContext().GetDecimalFormatManager();
            DecimalSymbols dfs;
            AtomicValue av0 = (AtomicValue)arguments[0].Head();
            if (av0 == null)
            {
                av0 = DoubleValue.NaN;
            }

            NumericValue number = (NumericValue)av0;
            if (picture != null)
            {

                // Decimal format and picture known statically
                PicsMemo memo = picsMemo;
                if (memo == null || !ReferenceEquals(memo.pics, subPictures))
                {
                    picsMemo = memo = new PicsMemo(picture, decimalSymbols, subPictures);
                }

                return FormatCached(number, memo);
            }
            else
            {
                if (numArgs == 2)
                {
                    dfs = dfm.DefaultDecimalFormat;
                }
                else
                {

                    // the decimal-format name was given as a run-time expression
                    IItem arg2 = arguments[2].Head();
                    if (arg2 == null)
                    {
                        dfs = dfm.DefaultDecimalFormat;
                    }
                    else
                    {
                        string lexicalName = arg2.UnicodeStringValue.ToString();
                        dfs = GetNamedDecimalFormat(dfm, lexicalName);
                    }
                }

                string format = arguments[1].Head().UnicodeStringValue.ToString();
                PicsMemo memo = picsMemo;
                if (memo == null || memo.dfs != dfs || !memo.picture.Equals(format))
                {
                    picsMemo = memo = new PicsMemo(format, dfs, GetSubPictures(format, dfs));
                }

                return FormatCached(number, memo);
            }
        }

        /* 4.7.4 Rule 3 */
        /* 4.7.4 Rule 8 */
        /* 4.7.4 Rule 9 */
        /* 4.7.4 Rule 10 */
        protected DecimalSymbols GetNamedDecimalFormat(DecimalFormatManager dfm, string lexicalName)
        {
            DecimalSymbols dfs;
            StructuredQName qName;
            try
            {
                qName = StructuredQName.FromLexicalQName(lexicalName, false, true, GetRetainedStaticContext());
            }
            catch (XPathException e)
            {
                throw new XPathException("Invalid decimal format name. " + e.Message, "FODF1280");
            }

            dfs = dfm.GetNamedDecimalFormat(qName);
            if (dfs == null)
            {
                throw new XPathException("format-number function: decimal-format '" + lexicalName + "' is not defined", "FODF1280");
            }

            return dfs;
        }

        /* 4.7.4 Rule 3 */
        /* 4.7.4 Rule 8 */
        /* 4.7.4 Rule 9 */
        /* 4.7.4 Rule 10 */
        private static bool IsInDigitFamily(int ch, int zeroDigit)
        {
            return ch >= zeroDigit && ch < zeroDigit + 10;
        }

        /* 4.7.4 Rule 3 */
        /* 4.7.4 Rule 8 */
        /* 4.7.4 Rule 9 */
        /* 4.7.4 Rule 10 */
        public static string FormatExponential(DoubleValue value)
        {
            try
            {
                DecimalSymbols dfs = new DecimalSymbols(HostLanguage.XSLT, 31);
                dfs.Infinity = "INF";
                SubPicture[] pics = GetSubPictures("0.0##########################e0", dfs);
                return FormatNumberFn(value, pics, dfs);
            }
            catch (XPathException e)
            {
                return value.GetStringValue();
            }
        }

        /* 4.7.4 Rule 3 */
        /* 4.7.4 Rule 8 */
        /* 4.7.4 Rule 9 */
        /* 4.7.4 Rule 10 */
        public FormatNumber Copy()
        {
            FormatNumber copy = (FormatNumber)SystemFunction.MakeFunction(GetFunctionName().GetLocalPart(), GetRetainedStaticContext(), GetArity());
            copy.decimalFormatName = decimalFormatName;
            copy.picture = picture;
            copy.decimalSymbols = decimalSymbols;
            copy.subPictures = subPictures;
            return copy;
        }
        ISequence ICallable.Call(IXPathContext arg0, ISequence[] arg1) => Call(arg0, arg1);
        SystemFunction IStatefulSystemFunction.Copy() => Copy();

        internal sealed class SubPicture
        {
            protected int minWholePartSize = 0;
            protected int maxWholePartSize = 0;
            protected int minFractionPartSize = 0;
            protected int maxFractionPartSize = 0;
            protected int minExponentSize = 0;
            protected int scalingFactor = 0;
            protected bool isPercent = false;
            protected bool isPerMille = false;
            protected string prefix = "";
            protected string suffix = "";
            protected int[] wholePartGroupingPositions = null;
            protected int[] fractionalPartGroupingPositions = null;
            protected bool regular;
            protected bool is31 = false;
            public SubPicture(int[] pic, DecimalSymbols dfs)
            {
                is31 = true;
                int percentSign = dfs.GetPercent();
                int perMilleSign = dfs.GetPerMille();
                int decimalSeparator = dfs.GetDecimalSeparator();
                int groupingSeparator = dfs.GetGroupingSeparator();
                int digitSign = dfs.GetDigit();
                int zeroDigit = dfs.GetZeroDigit();
                int exponentSeparator = dfs.GetExponentSeparator();
                StringBuilder prefixBuilder = new StringBuilder(8);
                StringBuilder suffixBuilder = new StringBuilder(8);
                IList<int> wholePartPositions = null;
                IList<int> fractionalPartPositions = null;
                bool foundDigit = false;
                bool foundDecimalSeparator = false;
                bool foundExponentSeparator = false;
                bool foundExponentSeparator2 = false;
                foreach (int ch in pic)
                {
                    if (ch == digitSign || ch == zeroDigit || IsInDigitFamily(ch, zeroDigit))
                    {
                        foundDigit = true;
                        break;
                    }
                }

                if (!foundDigit)
                {
                    Grumble("subpicture contains no digit or zero-digit sign");
                }

                int phase = 0;

                // phase = 0: passive characters at start
                // phase = 1: digit signs in whole part
                // phase = 2: zero-digit signs in whole part
                // phase = 3: zero-digit signs in fractional part
                // phase = 4: digit signs in fractional part
                // phase = 5: zero-digit signs in exponent part
                // phase = 6: passive characters at end
                foreach (int c in pic)
                {
                    if (c == percentSign || c == perMilleSign)
                    {
                        if (isPercent || isPerMille)
                        {
                            Grumble("Cannot have more than one percent or per-mille character in a sub-picture");
                        }

                        isPercent = c == percentSign;
                        isPerMille = c == perMilleSign;
                        switch (phase)
                        {
                            case 0:
                                prefixBuilder.AppendCodePoint(c);
                                break;
                            case 1:
                            case 2:
                            case 3:
                            case 4:
                            case 5:
                                if (foundExponentSeparator)
                                {
                                    Grumble("Cannot have exponent-separator as well as percent or per-mille character in a sub-picture");
                                }

                                goto case 6;
                            case 6:
                                phase = 6;
                                suffixBuilder.AppendCodePoint(c);
                                break;
                        }
                    }
                    else if (c == digitSign)
                    {
                        switch (phase)
                        {
                            case 0:
                            case 1:
                                phase = 1;
                                maxWholePartSize++;
                                break;
                            case 2:
                                Grumble("Digit sign must not appear after a zero-digit sign in the integer part of a sub-picture");
                                break;
                            case 3:
                            case 4:
                                phase = 4;
                                maxFractionPartSize++;
                                break;
                            case 5:
                                Grumble("Digit sign must not appear in the exponent part of a sub-picture");
                                break;
                            case 6:
                                if (foundExponentSeparator2)
                                {
                                    Grumble("There must only be one exponent separator in a sub-picture");
                                }
                                else
                                {
                                    Grumble("Passive character must not appear between active characters in a sub-picture");
                                }

                                break;
                        }
                    }
                    else if (c == zeroDigit || IsInDigitFamily(c, zeroDigit))
                    {
                        switch (phase)
                        {
                            case 0:
                            case 1:
                            case 2:
                                phase = 2;
                                minWholePartSize++;
                                maxWholePartSize++;
                                break;
                            case 3:
                                minFractionPartSize++;
                                maxFractionPartSize++;
                                break;
                            case 4:
                                Grumble("Zero digit sign must not appear after a digit sign in the fractional part of a sub-picture");
                                break;
                            case 5:
                                minExponentSize++;
                                break;
                            case 6:
                                if (foundExponentSeparator2)
                                {
                                    Grumble("There must only be one exponent separator in a sub-picture");
                                }
                                else
                                {
                                    Grumble("Passive character must not appear between active characters in a sub-picture");
                                }

                                break;
                        }
                    }
                    else if (c == decimalSeparator)
                    {
                        if (foundDecimalSeparator)
                        {
                            Grumble("There must only be one decimal separator in a sub-picture");
                        }

                        switch (phase)
                        {
                            case 0:
                            case 1:
                            case 2:
                                phase = 3;
                                foundDecimalSeparator = true;
                                break;
                            case 3:
                            case 4:
                            case 5:
                                if (foundExponentSeparator)
                                {
                                    Grumble("Decimal separator must not appear in the exponent part of a sub-picture");
                                }

                                break;
                            case 6:
                                Grumble("Decimal separator cannot come after a character in the suffix");
                                break;
                        }
                    }
                    else if (c == groupingSeparator)
                    {
                        switch (phase)
                        {
                            case 0:
                            case 1:
                            case 2:
                                if (wholePartPositions == null)
                                {
                                    wholePartPositions = new List<int>(3);
                                }

                                if (wholePartPositions.Contains(maxWholePartSize))
                                {
                                    Grumble("Sub-picture cannot contain adjacent grouping separators");
                                }

                                wholePartPositions.Add(maxWholePartSize);

                                // note these are positions from a false offset, they will be corrected later
                                break;
                            case 3:
                            case 4:
                                if (maxFractionPartSize == 0)
                                {
                                    Grumble("Grouping separator cannot be adjacent to decimal separator");
                                }

                                if (fractionalPartPositions == null)
                                {
                                    fractionalPartPositions = new List<int>(3);
                                }

                                if (fractionalPartPositions.Contains(maxFractionPartSize))
                                {
                                    Grumble("Sub-picture cannot contain adjacent grouping separators");
                                }

                                fractionalPartPositions.Add(maxFractionPartSize);
                                break;
                            case 5:
                                if (foundExponentSeparator)
                                {
                                    Grumble("Grouping separator must not appear in the exponent part of a sub-picture");
                                }

                                break;
                            case 6:
                                Grumble("Grouping separator found in suffix of sub-picture");
                                break;
                        }
                    }
                    else if (c == exponentSeparator)
                    {
                        switch (phase)
                        {
                            case 0:
                                prefixBuilder.AppendCodePoint(c);
                                break;
                            case 1:
                            case 2:
                            case 3:
                            case 4:
                                phase = 5;
                                foundExponentSeparator = true;
                                break;
                            case 5:
                                if (foundExponentSeparator)
                                {
                                    foundExponentSeparator2 = true;
                                    phase = 6;
                                    suffixBuilder.AppendCodePoint(exponentSeparator);
                                }

                                break;
                            case 6:
                                suffixBuilder.AppendCodePoint(c);
                                break;
                        }
                    } // passive character found
                    else
                    {

                        // passive character found
                        switch (phase)
                        {
                            case 0:
                                prefixBuilder.AppendCodePoint(c);
                                break;
                            case 1:
                            case 2:
                            case 3:
                            case 4:
                            case 5:
                                if (minExponentSize == 0 && foundExponentSeparator)
                                {
                                    phase = 6;
                                    suffixBuilder.AppendCodePoint(exponentSeparator);
                                    suffixBuilder.AppendCodePoint(c);
                                    break;
                                }

                                goto case 6;
                            case 6:
                                phase = 6;
                                suffixBuilder.AppendCodePoint(c);
                                break;
                        }
                    }
                }

                prefix = prefixBuilder.ToString();
                suffix = suffixBuilder.ToString();
                /* 4.7.4 Rule 3 */
                scalingFactor = minWholePartSize;
                if (maxWholePartSize == 0 && maxFractionPartSize == 0)
                {
                    Grumble("Mantissa contains no digit or zero-digit sign");
                }

                /* 4.7.4 Rule 8 */
                if (minWholePartSize == 0 && maxFractionPartSize == 0)
                {

                    //minWholePartSize == 0 && !foundDecimalSeparator
                    if (minExponentSize != 0)
                    {
                        minFractionPartSize = 1;
                        maxFractionPartSize = 1;
                    }
                    else
                    {
                        minWholePartSize = 1;
                    }
                }

                /* 4.7.4 Rule 9 */
                if (minExponentSize != 0 && minWholePartSize == 0 && maxWholePartSize != 0)
                {
                    minWholePartSize = 1;
                }

                /* 4.7.4 Rule 10 */
                if (minWholePartSize == 0 && minFractionPartSize == 0)
                {
                    minFractionPartSize = 1;
                }


                // Sort out the grouping positions
                if (wholePartPositions != null)
                {

                    // convert to positions relative to the decimal separator
                    int n = wholePartPositions.Count;
                    wholePartGroupingPositions = new int[n];
                    for (int i = 0; i < n; i++)
                    {
                        wholePartGroupingPositions[i] = maxWholePartSize - wholePartPositions[n - i - 1];
                    }

                    if (n == 1)
                    {
                        regular = wholePartGroupingPositions[0] * 2 >= maxWholePartSize;
                    }
                    else if (n > 1)
                    {
                        regular = true;
                        int first = wholePartGroupingPositions[0];
                        for (int i = 1; i < n; i++)
                        {
                            if (wholePartGroupingPositions[i] != (i + 1) * first)
                            {
                                regular = false;
                                break;
                            }
                        }

                        if (regular && (maxWholePartSize - wholePartGroupingPositions[n - 1] > first))
                        {
                            regular = false;
                        }

                        if (regular)
                        {
                            wholePartGroupingPositions = new int[1];
                            wholePartGroupingPositions[0] = first;
                        }
                    }

                    if (wholePartGroupingPositions[0] == 0)
                    {

                        Grumble("Cannot have a grouping separator at the end of the integer part");
                    }
                }

                if (fractionalPartPositions != null)
                {
                    int n = fractionalPartPositions.Count;
                    fractionalPartGroupingPositions = new int[n];
                    for (int i = 0; i < n; i++)
                    {
                        fractionalPartGroupingPositions[i] = fractionalPartPositions[i];
                    }
                }
            }

            public string Format(NumericValue value, DecimalSymbols dfs, string minusSign)
            {

                if (value.IsNaN())
                {
                    return dfs.NaN; // changed by W3C Bugzilla 2712
                }

                int multiplier = 1;
                if (isPercent)
                {
                    multiplier = 100;
                }
                else if (isPerMille)
                {
                    multiplier = 1000;
                }

                if (multiplier != 1)
                {
                    try
                    {
                        value = (NumericValue)ArithmeticExpression.Compute(value, Calculator.TIMES, new Int64Value(multiplier), null);
                    }
                    catch (XPathException e)
                    {
                        value = new DoubleValue(double.PositiveInfinity);
                    }
                }

                if ((value is DoubleValue || value is FloatValue) && double.IsInfinity(value.GetDoubleValue()))
                {
                    return minusSign + prefix + dfs.Infinity + suffix;
                }

                StringBuilder sb = new StringBuilder(16);
                if (value is DoubleValue || value is FloatValue)
                {
                    BigDecimal dec = AdjustToDecimal(value.GetDoubleValue(), 2);
                    FormatDecimal(dec, sb);
                }
                else if (value is IntegerValue)
                {
                    if (minExponentSize != 0)
                    {
                        FormatDecimal(((IntegerValue)value).GetDecimalValue(), sb);
                    }
                    else
                    {
                        FormatInteger(value, sb);
                    }
                }
                else if (value is DecimalValue)
                {
                    FormatDecimal(((DecimalValue)value).GetDecimalValue(), sb);
                }


                // Direct assembly for the common picture shape (default '0' digits, BMP separators,
                // no exponent, no fractional grouping, regular whole-part grouping): emits straight
                // from the digit buffer, skipping the codepoint-array expand + per-separator array
                // Insert + rebuild below. Separator positions replicate the legacy Insert loop
                // (before raw index p = point - k*g, 0 < p < point, sign counted as a char).
                if (minExponentSize == 0 && fractionalPartGroupingPositions == null
                    && dfs.GetZeroDigit() == '0'
                    && dfs.GetDecimalSeparator() < 0x10000
                    && (wholePartGroupingPositions == null || (regular && dfs.GetGroupingSeparator() < 0x10000)))
                {
                    int fpoint = -1;
                    for (int i = 0; i < sb.Length; i++)
                    {
                        if (sb[i] == '.')
                        {
                            fpoint = i;
                            break;
                        }
                    }

                    if (fpoint < 0 || maxFractionPartSize != 0 || fpoint == sb.Length - 1)
                    {
                        int end = fpoint >= 0 && maxFractionPartSize == 0 ? sb.Length - 1 : sb.Length;
                        int intLen = fpoint < 0 ? end : fpoint;
                        StringBuilder fast = new StringBuilder(minusSign.Length + prefix.Length + end + 6 + suffix.Length);
                        fast.Append(minusSign);
                        fast.Append(prefix);
                        int gsize = wholePartGroupingPositions == null ? 0 : wholePartGroupingPositions[0];
                        for (int i = 0; i < end; i++)
                        {
                            if (i == fpoint)
                            {
                                fast.Append((char)dfs.GetDecimalSeparator());
                            }
                            else
                            {
                                if (gsize > 0 && i > 0 && i < intLen && (intLen - i) % gsize == 0)
                                {
                                    fast.Append((char)dfs.GetGroupingSeparator());
                                }

                                fast.Append(sb[i]);
                            }
                        }

                        fast.Append(suffix);
                        return fast.ToString();
                    }
                }

                // IMap the digits and decimal point to use the selected characters
                string raw = sb.ToString();
                int[] ib = StringTool.Expand(StringView.Of(raw));
                int ibused = ib.Length;
                int point = raw.IndexOf('.');
                if (point == -1)
                {
                    point = raw.Length;
                }
                else
                {
                    ib[point] = dfs.GetDecimalSeparator();

                    // If there is no fractional part, delete the decimal point
                    if (maxFractionPartSize == 0)
                    {
                        ibused--;
                    }
                }


                // IMap the digits
                if (dfs.GetZeroDigit() != '0')
                {
                    int newZero = dfs.GetZeroDigit();
                    for (int i = 0; i < ibused; i++)
                    {
                        int c = ib[i];
                        if (c >= '0' && c <= '9')
                        {
                            ib[i] = c - '0' + newZero;
                        }
                    }
                }


                // IMap the exponent-separator
                if (dfs.GetExponentSeparator() != 'e')
                {
                    int expS = raw.IndexOf('e');
                    if (expS != -1)
                    {
                        ib[expS] = dfs.GetExponentSeparator();
                    }
                }


                // Add the whole-part grouping separators
                if (wholePartGroupingPositions != null)
                {
                    if (regular)
                    {

                        // grouping separators are at regular positions
                        int g = wholePartGroupingPositions[0];
                        int p = point - g;
                        while (p > 0)
                        {
                            ib = Insert(ib, ibused++, dfs.GetGroupingSeparator(), p);

                            p -= g;
                        }
                    }
                    else
                    {

                        // grouping separators are at irregular positions
                        foreach (int wholePartGroupingPosition in wholePartGroupingPositions)
                        {
                            int p = point - wholePartGroupingPosition;
                            if (p > 0)
                            {
                                ib = Insert(ib, ibused++, dfs.GetGroupingSeparator(), p); //sb.insert(p, unicodeChar(dfs.groupingSeparator));
                            }
                        }
                    }
                }


                // Add the fractional-part grouping separators
                if (fractionalPartGroupingPositions != null)
                {

                    // grouping separators are at irregular positions.
                    for (int i = 0; i < fractionalPartGroupingPositions.Length; i++)
                    {
                        int p = point + 1 + fractionalPartGroupingPositions[i] + i;
                        if (p < ibused)
                        {
                            ib = Insert(ib, ibused++, dfs.GetGroupingSeparator(), p); //sb.insert(p, dfs.groupingSeparator);
                        }
                        else
                        {
                            break;
                        }
                    }
                }

                StringBuilder res = new StringBuilder(prefix.Length + minusSign.Length + suffix.Length + ibused);
                res.Append(minusSign);
                res.Append(prefix);
                for (int i = 0; i < ibused; i++)
                {
                    res.AppendCodePoint(ib[i]);
                }

                res.Append(suffix);
                return res.ToString();
            }

            private void FormatDecimal(BigDecimal dval, StringBuilder fsb)
            {

                //NOTE: C# has its own version of this code in an overriding subclass
                int exponent = 0;
                if (minExponentSize == 0)
                {
                    dval = dval.SetScale(maxFractionPartSize, RoundingMode.HALF_EVEN);
                }
                else if (dval.Sign != 0)
                {
                    exponent = dval.Precision() - dval.Scale() - scalingFactor;
                    dval = dval.MovePointLeft(exponent);
                    dval = dval.SetScale(maxFractionPartSize, RoundingMode.HALF_EVEN);
                }

                BigDecimalValue.DecimalToString(dval, fsb);
                int point = -1;
                for (int i = 0; i < fsb.Length; i++)
                {
                    if (fsb[i] == '.')
                    {
                        point = i;
                        break;
                    }
                }
                int intDigits;
                if (point >= 0)
                {
                    int zz = maxFractionPartSize - minFractionPartSize;
                    while (zz > 0)
                    {
                        if (fsb[fsb.Length - 1] == '0')
                        {
                            fsb.Length = fsb.Length - 1;
                            zz--;
                        }
                        else
                        {
                            break;
                        }
                    }

                    intDigits = point;
                    if (fsb[fsb.Length - 1] == '.')
                    {
                        fsb.Length = fsb.Length - 1;
                    }
                }
                else
                {
                    intDigits = fsb.Length;
                    if (minFractionPartSize > 0)
                    {
                        fsb.Append('.');
                        for (int i = 0; i < minFractionPartSize; i++)
                        {
                            fsb.Append('0');
                        }
                    }
                }

                if (minWholePartSize == 0 && intDigits == 1 && fsb[0] == '0')
                {
                    fsb.Remove(0, 1);
                }
                else if (minWholePartSize > intDigits)
                {
                    StringTool.PrependRepeated(fsb, '0', minWholePartSize - intDigits);
                }

                if (minExponentSize != 0)
                {
                    fsb.Append('e');
                    IntegerValue exp = (IntegerValue)IntegerValue.FromDouble(exponent);
                    string expStr = exp.UnicodeStringValue.ToString();
                    char first = expStr[0];
                    if (first == '-')
                    {
                        fsb.Append('-');
                        expStr = expStr.Substring(1);
                    }

                    int length = expStr.Length;
                    if (length < minExponentSize)
                    {
                        int zz = minExponentSize - length;
                        for (int i = 0; i < zz; i++)
                        {
                            fsb.Append('0');
                        }
                    }

                    fsb.Append(expStr);
                }
            }

            private void FormatInteger(NumericValue value, StringBuilder sb)
            {
                if (!(minWholePartSize == 0 && value.CompareTo(0) == 0))
                {
                    sb.Append(value.UnicodeStringValue);
                    int leadingZeroes = minWholePartSize - sb.Length;
                    if (leadingZeroes > 0)
                    {
                        StringTool.PrependRepeated(sb, '0', leadingZeroes);
                    }
                }

                if (minFractionPartSize != 0)
                {
                    sb.Append('.');
                    for (int i = 0; i < minFractionPartSize; i++)
                    {
                        sb.Append('0');
                    }
                }
            }
        }
    }
}