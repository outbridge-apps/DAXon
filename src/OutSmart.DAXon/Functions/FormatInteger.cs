////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Regex;
using OutSmart.DAXon.Regex.CharClass;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Collections;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Globalization;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Numbering;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Caching;
namespace OutSmart.DAXon.Functions
{
    internal sealed class FormatInteger : SystemFunction, IStatefulSystemFunction
    {
        public const string preface = "In the picture string for format-integer, ";
        // The grouping-separator analysis is a pure function of (picture, radix) and the resulting
        // NumericGroupFormatter is immutable; the same numeric picture recurs for every value of a
        // format-integer / format-date column, so memoize it (mirrors FormatDate.ComponentSpecifier
        // Cache). Cache successes only; invalid pictures (rare) fall through and throw as before.
        private static readonly ClockCache<string, NumericGroupFormatter> PicSeparatorCache
            = new ClockCache<string, NumericGroupFormatter>(128);
        private static readonly IRegularExpression badDecimalHashPattern = ARegularExpression.Compile("(([\\dXx]+|\\w+)#+.*)|(#+[^\\dXx]+)", "");
        private static readonly IRegularExpression modifierPattern = ARegularExpression.Compile("([co](\\(.*\\))?)?[at]?", "");
        private static readonly IRegularExpression decimalDigitPattern = ARegularExpression.Compile("^((\\p{Nd}|#|[^\\p{N}\\p{L}])+?)$", "");
        private static readonly IRegularExpression nonDecimalDigitPattern = ARegularExpression.Compile("^(([Xx#]|[^\\p{N}\\p{L}])+?)$", "");
        private Func<IntegerValue, string> formatter = null;

        // Per-call-site memo: the resolved formatter for the last-seen (picture, language) pair
        // plus a bounded long -> formatted-string cache. format-integer is a pure function of
        // (value, picture, language), so replaying a cached result is byte-identical; entries are
        // immutable and the direct-mapped slots tolerate races. Word/ordinal pictures rebuild the
        // string per value otherwise, and typical columns carry few distinct integers.
        private sealed class IntFmtEntry
        {
            internal readonly long value;
            internal readonly string result;
            internal IntFmtEntry(long value, string result) { this.value = value; this.result = result; }
        }

        private sealed class IntFmtMemo
        {
            internal readonly string picture;
            internal readonly string language;
            internal readonly Func<IntegerValue, string> formatter;
            internal readonly IntFmtEntry[] cache = new IntFmtEntry[1024];
            internal IntFmtMemo(string picture, string language, Func<IntegerValue, string> formatter)
            {
                this.picture = picture;
                this.language = language;
                this.formatter = formatter;
            }
        }

        private volatile IntFmtMemo intMemo;
        // Unicode general category of a code point (BMP or supplementary), matching the
        // classification the format-integer picture parser relies on.
        private static UnicodeCategory CategoryOf(int codePoint) =>
            codePoint <= 0xFFFF
                ? CharUnicodeInfo.GetUnicodeCategory((char)codePoint)
                : CharUnicodeInfo.GetUnicodeCategory(char.ConvertFromUtf32(codePoint), 0);
        public override Expression MakeOptimizedFunctionCall(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo, params Expression[] arguments)
        {
            bool opt = true;
            if (!(arguments[1] is Literal))
            {
                opt = false;
            }

            if (arguments.Length == 3 && !(arguments[2] is Literal))
            {
                opt = false;
            }

            if (!opt)
            {
                return base.MakeOptimizedFunctionCall(visitor, contextInfo, arguments);
            }

            Configuration config = visitor.GetConfiguration();
            string language = arguments.Length == 3 ? ((StringLiteral)arguments[2]).GroundedValue.GetStringValue() : config.GetDefaultLanguage();
            INumberer numb = config.MakeNumberer(language, null);
            bool allow40 = visitor.StaticContext.GetPackageData().HostLanguageVersion >= 40;
            formatter = MakeFormatter(numb, ((StringLiteral)arguments[1]).GroundedValue.GetStringValue(), allow40);
            return base.MakeOptimizedFunctionCall(visitor, contextInfo, arguments);
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            return FormatIntegerFn((IntegerValue)arguments[0].Head(), (StringValue)arguments[1].Head(), arguments.Length == 2 ? null : (StringValue)arguments[2].Head(), context);
        }

        private StringValue FormatIntegerFn(IntegerValue num, StringValue picture, StringValue language, IXPathContext context)
        {
            Configuration config = context.GetConfiguration();
            bool allow40 = GetRetainedStaticContext().GetPackageData().HostLanguageVersion >= 40;
            if (num == null)
            {
                return StringValue.EMPTY_STRING;
            }

            string languageVal;
            if (language != null)
            {
                languageVal = language.GetStringValue();
            }
            else
            {

                //default language
                languageVal = config.GetDefaultLanguage();
            }

            string pictureVal = picture.GetStringValue();
            IntFmtMemo memo = intMemo;
            if (memo == null || !memo.picture.Equals(pictureVal) || !memo.language.Equals(languageVal))
            {
                Func<IntegerValue, string> localFormatter = formatter;
                if (localFormatter == null)
                {
                    INumberer numb = config.MakeNumberer(languageVal, null);
                    localFormatter = MakeFormatter(numb, pictureVal, allow40);
                }

                intMemo = memo = new IntFmtMemo(pictureVal, languageVal, localFormatter);
            }

            try
            {
                if (num is Int64Value i64)
                {
                    long v = i64.LongValue();
                    int idx = (int)(((ulong)(v * -7046029254386353131L)) >> 54) & 1023;
                    IntFmtEntry e = memo.cache[idx];
                    if (e != null && e.value == v)
                    {
                        return new StringValue(e.result);
                    }

                    string r = memo.formatter(num);
                    memo.cache[idx] = new IntFmtEntry(v, r);
                    return new StringValue(r);
                }

                return new StringValue(memo.formatter(num));
            }
            catch (UncheckedXPathException e)
            {
                throw e.GetXPathException();
            }
        }

        private Func<IntegerValue, string> MakeFormatter(INumberer numb, string pic, bool allow40)
        {
            if ((pic.Length == 0))
            {
                throw new XPathException(preface + "the picture cannot be empty", "FODF1310");
            }

            bool hasExplicitRadix = false;
            int radix = 10;
            if (allow40 && pic.MatchesRegex("^([2-9]|[12][0-9]|3[0-6])\\^.*[xX].*$"))
            {
                int hat = pic.IndexOf('^');
                radix = int.Parse(pic.Substring(0, hat), CultureInfo.InvariantCulture);
                hasExplicitRadix = true;
                pic = pic.Substring(hat + 1);
            }

            string primaryToken;
            string modifier;
            string parenthetical;
            int lastSemicolon = pic.LastIndexOf(';');
            if (lastSemicolon >= 0)
            {
                primaryToken = pic.Substring(0, lastSemicolon);
                if ((primaryToken.Length == 0))
                {
                    throw new XPathException(preface + "the primary format token cannot be empty", "FODF1310");
                }

                modifier = lastSemicolon < pic.Length - 1 ? pic.Substring(lastSemicolon + 1) : "";
                if (!modifierPattern.Matches(StringView.Tidy(modifier)))
                {
                    throw new XPathException(preface + "the modifier is invalid", "FODF1310");
                }
            }
            else
            {
                primaryToken = pic;
                modifier = "";
            }

            bool cardinal = modifier.StartsWith("c", StringComparison.Ordinal);
            bool ordinal = modifier.StartsWith("o", StringComparison.Ordinal);

            //boolean traditional = modifier.endsWith("t");
            bool alphabetic = modifier.EndsWith("a", StringComparison.Ordinal);
            int leftParen = modifier.IndexOf('(');
            int rightParen = modifier.LastIndexOf(')');
            parenthetical = leftParen < 0 ? "" : modifier.Substring(leftParen + 1, rightParen - leftParen - 1);
            string letterValue = alphabetic ? "alphabetic" : "traditional";
            string ordinalValue = ordinal ? parenthetical == "" ? "yes" : parenthetical : "";
            string cardinalValue = cardinal ? parenthetical == "" ? "yes" : parenthetical : "";
            UnicodeString primary = StringView.Tidy(primaryToken);
            Categories.Category isDecimalDigit = Categories.GetCategory("Nd");
            bool isDecimalDigitPattern = false;
            if (hasExplicitRadix)
            {
                if (!nonDecimalDigitPattern.Matches(primary))
                {
                    throw new XPathException(preface + "the primary format token with radix " + radix + " does not " + "meet the rules for a non-decimal digit pattern", "FODF1310");
                }

                letterValue = (primary.IndexOf('X') >= 0 ? "X" : "x") + radix;
            }
            else
            {
                IIntIterator iter = primary.CodePoints();
                while (iter.MoveNext())
                {
                    if (isDecimalDigit.Test(iter.Current))
                    {
                        isDecimalDigitPattern = true;
                        break;
                    }
                }

                if (isDecimalDigitPattern && !decimalDigitPattern.Matches(primary))
                {
                    throw new XPathException(preface + "the primary format token contains a decimal digit but does not " + "meet the rules for a decimal digit pattern", "FODF1310");
                }
            }

            if (isDecimalDigitPattern || hasExplicitRadix)
            {
                NumericGroupFormatter picGroupFormat = GetPicSeparators(primary, hasExplicitRadix);
                UnicodeString adjustedPicture = picGroupFormat.AdjustedPicture;
                string finalLetterValue = letterValue;
                return (num) =>
                {
                    try
                    {
                        string s = numb.Format(num.Abs().LongValue(), adjustedPicture, picGroupFormat, finalLetterValue, "", ordinalValue);
                        return num.Signum() < 0 ? ("-" + s) : s;
                    }
                    catch (XPathException e)
                    {
                        throw new UncheckedXPathException(e);
                    }
                };
            }
            else
            {
                UnicodeString token = StringView.Tidy(primaryToken);
                string finalLetterValue = letterValue;
                return (num) =>
                {
                    try
                    {
                        string s = numb.Format(num.Abs().LongValue(), token, null, finalLetterValue, cardinalValue, ordinalValue);
                        return num.Signum() < 0 ? ("-" + s) : s;
                    }
                    catch (XPathException e)
                    {
                        throw new UncheckedXPathException(e);
                    }
                };
            }
        }

        public static NumericGroupFormatter GetPicSeparators(UnicodeString picExpanded, bool hasExplicitRadix)
        {
            // "x" prefix distinguishes the radix-true key space; the factory throws for an invalid
            // picture, in which case GetOrAdd caches nothing and re-throws it (rare path).
            string key = (hasExplicitRadix ? "x" : "") + picExpanded.ToString();
            return PicSeparatorCache.GetOrAdd(key, _ => ComputePicSeparators(picExpanded, hasExplicitRadix));
        }

        private static NumericGroupFormatter ComputePicSeparators(UnicodeString picExpanded, bool hasExplicitRadix)
        {
            IntSet groupingPositions = new IntHashSet(5);
            IList<int> separatorList = new List<int>();
            int groupingPosition = 0; // number of digits to the right of a grouping separator
            int firstGroupingPos = 0; // number of digits to the right of the first grouping separator
            int lastGroupingPos = 0;
            bool regularCheck = true;
            int zeroDigit = -1;
            if (badDecimalHashPattern.Matches(picExpanded))
            {
                throw new XPathException(preface + "the picture is not valid (it uses '#' where disallowed)", "FODF1310");
            }

            for (long i = picExpanded.Length() - 1; i >= 0; i--)
            {
                int codePoint = picExpanded.CodePointAt(i);
                switch (CategoryOf(codePoint))
                {
                    case UnicodeCategory.DecimalDigitNumber:
                        if (zeroDigit == -1)
                        {
                            zeroDigit = Alphanumeric.GetDigitFamily(codePoint);
                        }
                        else
                        {
                            if (zeroDigit != Alphanumeric.GetDigitFamily(codePoint))
                            {
                                throw new XPathException(preface + "the picture mixes digits from different digit families", "FODF1310");
                            }
                        }

                        groupingPosition++;
                        break;
                    case UnicodeCategory.UppercaseLetter:
                    case UnicodeCategory.LowercaseLetter:
                        if (!hasExplicitRadix)
                        {
                            break;
                        }

                        if (codePoint == 'x' || codePoint == 'X')
                        {
                            if (zeroDigit == -1)
                            {
                                zeroDigit = codePoint;
                            }
                            else if (zeroDigit != codePoint)
                            {
                                throw new XPathException(preface + "the picture mixes upper-case and lower-case non-decimal digits", "FODF1310");
                            }
                        }
                        else
                        {
                            throw new XPathException(preface + "non-decimal digits must be indicated by 'x' or 'X'", "FODF1310");
                        }

                        groupingPosition++;
                        break;
                    case UnicodeCategory.LetterNumber:
                    case UnicodeCategory.OtherNumber:
                    case UnicodeCategory.ModifierLetter:
                    case UnicodeCategory.OtherLetter:
                        break;
                    default:
                        if (i == picExpanded.Length() - 1)
                        {
                            throw new XPathException(preface + "the picture cannot end with a separator", "FODF1310");
                        }

                        if (codePoint == '#')
                        {
                            groupingPosition++;
                            if (i != 0)
                            {
                                switch (CategoryOf(picExpanded.CodePointAt(i - 1)))
                                {
                                    case UnicodeCategory.DecimalDigitNumber:
                                    case UnicodeCategory.LetterNumber:
                                    case UnicodeCategory.OtherNumber:
                                    case UnicodeCategory.UppercaseLetter:
                                    case UnicodeCategory.LowercaseLetter:
                                    case UnicodeCategory.ModifierLetter:
                                    case UnicodeCategory.OtherLetter:
                                        throw new XPathException(preface + "the picture cannot contain alphanumeric character(s) before character '#'", "FODF1310");
                                }
                            }


                            //
                            break;
                        }
                        else
                        {
                            bool added = groupingPositions.Add(groupingPosition);
                            if (!added)
                            {
                                throw new XPathException(preface + "the picture contains consecutive separators", "FODF1310");
                            }

                            separatorList.Add(codePoint);
                            if (groupingPositions.Count == 1)
                            {
                                firstGroupingPos = groupingPosition;
                            }
                            else
                            {
                                if (groupingPosition != firstGroupingPos * groupingPositions.Count)
                                {
                                    regularCheck = false;
                                }

                                if (separatorList[0] != codePoint)
                                {
                                    regularCheck = false;
                                }
                            }

                            if (i == 0)
                            {
                                throw new XPathException(preface + "the picture cannot begin with a separator", "FODF1310");
                            }

                            lastGroupingPos = groupingPosition;
                        }

                        break;
                }
            }

            if (regularCheck && groupingPositions.Count >= 1)
            {
                if (picExpanded.Length() - lastGroupingPos - groupingPositions.Count > firstGroupingPos)
                {
                    regularCheck = false;
                }
            }

            UnicodeString adjustedPic = ExtractSeparators(picExpanded, groupingPositions);
            if (groupingPositions.IsEmpty())
            {
                return new RegularGroupFormatter(0, "", adjustedPic);
            }

            if (regularCheck)
            {
                if (separatorList.Count == 0)
                {
                    return new RegularGroupFormatter(0, "", adjustedPic);
                }
                else
                {
                    StringBuilder sb = new StringBuilder(4);
                    sb.AppendCodePoint(separatorList[0]);
                    return new RegularGroupFormatter(firstGroupingPos, sb.ToString(), adjustedPic);
                }
            }
            else
            {
                return new IrregularGroupFormatter(groupingPositions, separatorList, adjustedPic);
            }
        }

        //
        private static UnicodeString ExtractSeparators(UnicodeString arr, IntSet excludePositions)
        {

            // TODO: this doesn't do what the documentation says: it ignores the supplied positions entirely
            UnicodeBuilder ub = new UnicodeBuilder(arr.Length32());
            IIntIterator iter = arr.CodePoints();
            while (iter.MoveNext())
            {
                int c = iter.Current;
                if (NumberFormatter.IsLetterOrDigit(c))
                {
                    ub.Append(c);
                }
            }

            return ub.ToUnicodeString();
        }

        //
        public SystemFunction Copy()
        {
            FormatInteger fi2 = (FormatInteger)SystemFunction.MakeFunction(GetFunctionName().GetLocalPart(), GetRetainedStaticContext(), GetArity());
            fi2.formatter = formatter;
            return fi2;
        }
    }
}
