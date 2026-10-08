////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Globalization;
using System.Text;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Expressions.Numbering
{
    internal abstract class AbstractNumberer : INumberer
    {
        public const int UPPER_CASE = 0;
        public const int LOWER_CASE = 1;
        public const int TITLE_CASE = 2;
        public static int[] lowerCaseAlphabet = StringTool.Expand(new Twine8("0123456789abcdefghijklmnopqrstuvwxyz"));
        public static int[] upperCaseAlphabet = StringTool.Expand(new Twine8("0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ"));

        /*if (formchar == 'w')*/
        protected static readonly int[] westernDigits = new int[]
        {
            '0',
            '1',
            '2',
            '3',
            '4',
            '5',
            '6',
            '7',
            '8',
            '9'
        };
        /*if (formchar == 'w')*/
        protected static readonly string latinUpper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        /*if (formchar == 'w')*/
        protected static readonly string latinLower = "abcdefghijklmnopqrstuvwxyz";
        /*if (formchar == 'w')*/
        protected static readonly string greekUpper = "ΑΒΓΔΕΖΗΘΙΚ" + "ΛΜΝΞΟΠΡ\u03a2ΣΤ" + "ΥΦΧΨΩ";
        /*if (formchar == 'w')*/
        protected static readonly string greekLower = "αβγδεζηθικ" + "λμνξοπρςστ" + "υφχψω";
        /*if (formchar == 'w')*/
        protected static readonly string cyrillicUpper = "АБВГДЕЖЗИ" + "КЛМНОПРССУ" + "ФХЦЧШЩЫЭЮЯ";
        /*if (formchar == 'w')*/
        protected static readonly string cyrillicLower = "абвгдежзи" + "клмнопрссу" + "фхцчшщыэюя";
        /*if (formchar == 'w')*/
        protected static readonly string hebrew = "אבגדהוזחטיכל" + "מנסעפצקרשת";
        /*if (formchar == 'w')*/
        protected static readonly string hiraganaA = "あいうえおかきくけこ" + "さしすせそたちつてと" + "なにぬねのはひふへほ" + "まみむめもやゆよらり" + "るれろわをん";
        /*if (formchar == 'w')*/
        protected static readonly string katakanaA = "アイウエオカキクケコ" + "サシスセソタチツテト" + "ナニヌネノハヒフヘホ" + "マミムメモヤユヨラリ" + "ルレロワヲン";
        /*if (formchar == 'w')*/
        protected static readonly string hiraganaI = "いろはにほへとちりぬ" + "るをわかよたれそつね" + "ならむうゐのおくやま" + "けふこえてあさきゆめ" + "みしゑひもせす";
        /*if (formchar == 'w')*/
        protected static readonly string katakanaI = "イロハニホヘトチリヌ" + "ルヲワカヨタレソツネ" + "ナラムウヰノオクヤマ" + "ケフコエテアサキユメ" + "ミシヱヒモセス";

        /*if (formchar == 'w')*/
        private static readonly string[] romanThousands = new[]
        {
            "",
            "m",
            "mm",
            "mmm",
            "mmmm",
            "mmmmm",
            "mmmmmm",
            "mmmmmmm",
            "mmmmmmmm",
            "mmmmmmmmm"
        };
        /*if (formchar == 'w')*/
        private static readonly string[] romanHundreds = new[]
        {
            "",
            "c",
            "cc",
            "ccc",
            "cd",
            "d",
            "dc",
            "dcc",
            "dccc",
            "cm"
        };
        /*if (formchar == 'w')*/
        private static readonly string[] romanTens = new[]
        {
            "",
            "x",
            "xx",
            "xxx",
            "xl",
            "l",
            "lx",
            "lxx",
            "lxxx",
            "xc"
        };
        /*if (formchar == 'w')*/
        private static readonly string[] romanUnits = new[]
        {
            "",
            "i",
            "ii",
            "iii",
            "iv",
            "v",
            "vi",
            "vii",
            "viii",
            "ix"
        };

        /*if (formchar == 'w')*/
        // no action (not used at top level)
        private static readonly int[] kanjiDigits = new[]
        {
            0x3007,
            0x4e00,
            0x4e8c,
            0x4e09,
            0x56db,
            0x4e94,
            0x516d,
            0x4e03,
            0x516b,
            0x4e5d
        };
        private string country;

        public virtual string Country
        {
            get => country; set
            {
                this.country = value;
            }
        }
        public virtual System.Globalization.CultureInfo DefaultedLocale()
        {
            return null;
        }

        public virtual void SetLanguage(string language)
        {
        }

        public string Format(long number, UnicodeString picture, int groupSize, string groupSeparator, string letterValue, string cardinal, string ordinal)
        {
            return Format(number, picture, new RegularGroupFormatter(groupSize, groupSeparator, EmptyUnicodeString.GetInstance()), letterValue, cardinal, ordinal);
        }

        public virtual string Format(long number, UnicodeString picture, NumericGroupFormatter numGroupFormatter, string letterValue, string cardinal, string ordinal)
        {
            int[] digits = westernDigits;
            if (letterValue != null && letterValue.StartsWith("x", StringComparison.Ordinal))
            {
                int radix = int.Parse(letterValue.Substring(1), CultureInfo.InvariantCulture);
                digits = ArrayTools.CopyOf(lowerCaseAlphabet, radix);
            }
            else if (letterValue != null && letterValue.StartsWith("X", StringComparison.Ordinal))
            {
                int radix = int.Parse(letterValue.Substring(1), CultureInfo.InvariantCulture);
                digits = ArrayTools.CopyOf(upperCaseAlphabet, radix);
            }

            if (number < 0)
            {
                return number.ToString(CultureInfo.InvariantCulture);
            }

            if (picture == null || picture.Length() == 0)
            {
                return number.ToString(CultureInfo.InvariantCulture);
            }

            int pictureLength = picture.Length32();
            StringBuilder sb = new StringBuilder(16);
            int formchar = picture.CodePointAt(0);
            if (formchar == 'X' || formchar == 'x')
            {
                formchar = '0';
            }

            StringBuilder fsb = new StringBuilder(2);
            switch (formchar)
            {
                case '0':
                case '1':
                    string radical = ToRadical(number, digits, pictureLength, numGroupFormatter);
                    if (ordinal == null || ordinal.Length == 0)
                    {
                        return radical;
                    }

                    sb.Append(radical);
                    sb.Append(OrdinalSuffix(ordinal, number));
                    break;
                case 'A':
                    if (number == 0)
                    {
                        return "0";
                    }

                    return ToAlphaSequence(number, latinUpper);
                case 'a':
                    if (number == 0)
                    {
                        return "0";
                    }

                    return ToAlphaSequence(number, latinLower);
                case 'w':
                case 'W':
                    int wordCase;
                    if (pictureLength == 1)
                    {
                        if (formchar == 'W')
                        {
                            wordCase = UPPER_CASE;
                        } /*if (formchar == 'w')*/
                        else
                        {
                            wordCase = LOWER_CASE;
                        }
                    }
                    else
                    {

                        // includes cases like "ww" or "Wz". The action here is conformant, but it's not clear what's best
                        wordCase = TITLE_CASE;
                    }

                    if (ordinal != null && !(ordinal.Length == 0))
                    {
                        return ToOrdinalWords(ordinal, number, wordCase);
                    }
                    else
                    {
                        return ToWords(cardinal, number, wordCase);
                    }

                case 'i':
                    if (number == 0)
                    {
                        return "0";
                    }

                    if (letterValue == null || (letterValue.Length == 0) || letterValue.Equals("traditional"))
                    {
                        return ToRoman(number);
                    }
                    else
                    {
                        AlphaDefault(number, 'i', sb);
                    }

                    break;
                case 'I':
                    if (number == 0)
                    {
                        return "0";
                    }

                    if (letterValue == null || (letterValue.Length == 0) || letterValue.Equals("traditional"))
                    {
                        return ToRoman(number).ToUpperInvariant();
                    }
                    else
                    {
                        AlphaDefault(number, 'I', sb);
                    }

                    break;
                case '①':

                    // circled digits
                    if (number == 0)
                    {
                        return "" + (char)0x24EA;
                    }

                    if (number > 20 && number <= 35)
                    {
                        return "" + (char)(0x3251 + number - 21);
                    }

                    if (number > 35 && number <= 50)
                    {
                        return "" + (char)(0x32B1 + number - 36);
                    }

                    if (number > 50)
                    {
                        return number.ToString(CultureInfo.InvariantCulture);
                    }

                    return "" + (char)(0x2460 + number - 1);
                case '⑴':

                    // parenthesized digits
                    if (number == 0 || number > 20)
                    {
                        return number.ToString(CultureInfo.InvariantCulture);
                    }

                    return "" + (char)(0x2474 + number - 1);
                case '⒈':

                    // digit full stop
                    if (number == 0)
                    {
                        return "" + (char)0xD83C + (char)0xDD00;
                    }

                    if (number > 20)
                    {
                        return number.ToString(CultureInfo.InvariantCulture);
                    }

                    return "" + (char)(0x2488 + number - 1);
                case '❶':

                    // dingbat negative circled digits
                    if (number == 0)
                    {
                        return "" + (char)0x24FF;
                    }

                    if (number > 10 && number <= 20)
                    {
                        return "" + (char)(0x24EB + number - 11);
                    }

                    if (number > 20)
                    {
                        return number.ToString(CultureInfo.InvariantCulture);
                    }

                    return "" + (char)(0x2776 + number - 1);
                case '➀':

                    // double circled sans-serif digits
                    if (number == 0)
                    {
                        return "" + (char)0xD83C + (char)0xDD0B;
                    }

                    if (number > 10)
                    {
                        return number.ToString(CultureInfo.InvariantCulture);
                    }

                    return "" + (char)(0x2780 + number - 1);
                case '⓵':

                    // double circled digits
                    if (number == 0 || number > 10)
                    {
                        return number.ToString(CultureInfo.InvariantCulture);
                    }

                    return "" + (char)(0x24F5 + number - 1);
                case '➊':

                    // dingbat negative circled sans-serif digits
                    if (number == 0)
                    {
                        return "" + (char)0xD83C + (char)0xDD0C;
                    }

                    if (number > 10)
                    {
                        return number.ToString(CultureInfo.InvariantCulture);
                    }

                    return "" + (char)(0x278A + number - 1);
                case '㈠':

                    // parenthesized ideograph
                    if (number == 0 || number > 10)
                    {
                        return number.ToString(CultureInfo.InvariantCulture);
                    }

                    return "" + (char)(0x3220 + number - 1);
                case '㊀':

                    // circled ideograph
                    if (number == 0 || number > 10)
                    {
                        return number.ToString(CultureInfo.InvariantCulture);
                    }

                    return "" + (char)(0x3280 + number - 1);
                case 65799:

                    // aegean number
                    if (number == 0 || number > 10)
                    {
                        return number.ToString(CultureInfo.InvariantCulture);
                    }

                    fsb.AppendCodePoint(65799 + (int)number - 1);
                    return fsb.ToString();
                case 69216:

                    // rumi digit
                    if (number == 0 || number > 10)
                    {
                        return number.ToString(CultureInfo.InvariantCulture);
                    }

                    fsb.AppendCodePoint(69216 + (int)number - 1);
                    return fsb.ToString();
                case 69714:

                    // brahmi digit
                    if (number == 0 || number > 10)
                    {
                        return number.ToString(CultureInfo.InvariantCulture);
                    }

                    fsb.AppendCodePoint(69714 + (int)number - 1);
                    return fsb.ToString();
                case 119648:

                    // counting rod unit digit
                    if (number == 0 || number >= 10)
                    {
                        return number.ToString(CultureInfo.InvariantCulture);
                    }

                    fsb.AppendCodePoint(119648 + (int)number - 1);
                    return fsb.ToString();
                case 127234:

                    // digit one comma
                    if (number == 0)
                    {
                        fsb.AppendCodePoint(127233);
                        return fsb.ToString();
                    }

                    if (number >= 10)
                    {
                        return number.ToString(CultureInfo.InvariantCulture);
                    }

                    fsb.AppendCodePoint(127234 + (int)number - 1);
                    return fsb.ToString();
                case 'Α':
                    if (number == 0)
                    {
                        return "0";
                    }

                    return ToAlphaSequence(number, greekUpper);
                case 'α':
                    if (number == 0)
                    {
                        return "0";
                    }

                    return ToAlphaSequence(number, greekLower);
                case 'А':
                    if (number == 0)
                    {
                        return "0";
                    }

                    return ToAlphaSequence(number, cyrillicUpper);
                case 'а':
                    if (number == 0)
                    {
                        return "0";
                    }

                    return ToAlphaSequence(number, cyrillicLower);
                case 'א':
                    if (number == 0)
                    {
                        return "0";
                    }

                    return ToAlphaSequence(number, hebrew);
                case 'あ':
                    if (number == 0)
                    {
                        return "0";
                    }

                    return ToAlphaSequence(number, hiraganaA);
                case 'ア':
                    if (number == 0)
                    {
                        return "0";
                    }

                    return ToAlphaSequence(number, katakanaA);
                case 'い':
                    if (number == 0)
                    {
                        return "0";
                    }

                    return ToAlphaSequence(number, hiraganaI);
                case 'イ':
                    if (number == 0)
                    {
                        return "0";
                    }

                    return ToAlphaSequence(number, katakanaI);
                case '一':
                    return ToJapanese(number);
                default:
                    int digitValue = Alphanumeric.GetDigitValue(formchar);
                    if (digitValue >= 0)
                    {
                        int zero = formchar - digitValue;
                        digits = new int[10];
                        for (int z = 0; z <= 9; z++)
                        {
                            digits[z] = zero + z;
                        }

                        return ToRadical(number, digits, pictureLength, numGroupFormatter);
                    }
                    else
                    {
                        if (formchar < 'ᄀ' && char.IsLetter((char)formchar) && number > 0)
                        {
                            AlphaDefault(number, (char)formchar, sb);
                        }
                        else
                        {

                            // fallback to western numbering
                            sb.Append(ToRadical(number, westernDigits, pictureLength, numGroupFormatter));
                            if (ordinal != null && !(ordinal.Length == 0))
                            {
                                sb.Append(OrdinalSuffix(ordinal, number));
                            } //return toRadical(number, westernDigits, pictureLength, numGroupFormatter);
                        }

                        break;
                    }
            }

            return sb.ToString();
        }

        /*if (formchar == 'w')*/
        protected virtual string OrdinalSuffix(string ordinalParam, long number)
        {
            return "";
        }
        /*if (formchar == 'w')*/
        protected virtual void AlphaDefault(long number, char formchar, StringBuilder sb)
        {
            int min = formchar;
            int max = formchar;

            // use the contiguous range of letters starting with the specified one
            while (char.IsLetterOrDigit((char)(max + 1)))
            {
                max++;
            }

            sb.Append(ToAlpha(number, min, max));
        }

        /*if (formchar == 'w')*/
        protected virtual string ToAlpha(long number, int min, int max)
        {
            if (number <= 0)
            {
                return number.ToString(CultureInfo.InvariantCulture);
            }

            int range = max - min + 1;
            // Iterative: the recursion was linear in `number` when range == 1 (format token 'z',
            // whose contiguous letter range is just {z}), so format-integer(1000000,'z') overflowed
            // the stack with an uncatchable SOE. Digits emerge least-significant first; reverse once.
            StringBuilder sb = new StringBuilder();
            while (number > 0)
            {
                sb.Append((char)(((number - 1) % range) + min));
                number = (number - 1) / range;
                // Only the range==1 case reaches four digits; the deadline then bounds an output
                // that would otherwise run to a billion characters. Common runs (log depth) never
                // trip this, so it costs the ordinary format-integer nothing.
                if ((sb.Length & 0xfff) == 0)
                {
                    OutSmart.DAXon.Core.Controller.CheckActiveTimeout();
                }
            }

            return Reversed(sb);
        }

        /*if (formchar == 'w')*/
        protected virtual string ToAlphaSequence(long number, string alphabet)
        {
            if (number <= 0)
            {
                return number.ToString(CultureInfo.InvariantCulture);
            }

            // Iterative, for the same stack-overflow reason as ToAlpha: a one-character alphabet
            // makes the recursion linear in `number`.
            int range = alphabet.Length;
            StringBuilder sb = new StringBuilder();
            while (number > 0)
            {
                sb.Append(alphabet[(int)((number - 1) % range)]);
                number = (number - 1) / range;
            }

            return Reversed(sb);
        }

        private static string Reversed(StringBuilder sb)
        {
            char[] a = new char[sb.Length];
            for (int i = 0; i < a.Length; i++)
            {
                a[i] = sb[a.Length - 1 - i];
            }

            return new string(a);
        }

        /*if (formchar == 'w')*/
        private string ToRadical(long number, int[] digits, int pictureLength, NumericGroupFormatter numGroupFormatter)
        {
            string temp = ConvertDigitSystem(number, digits, pictureLength);
            if (numGroupFormatter == null)
            {
                return temp;
            }

            return numGroupFormatter.Format(temp);
        }

        /*if (formchar == 'w')*/
        public static string ConvertDigitSystem(long number, int[] digits, int requiredLength)
        {
            if (ReferenceEquals(digits, westernDigits))
            {
                // ASCII decimal pictures (format="1"/"000001"): long.ToString + pad replaces the
                // per-digit PrependWideChar loop. number == 0 stays the empty radical (all-pad),
                // matching the generic loop below.
                string w = number == 0 ? "" : number.ToString(System.Globalization.CultureInfo.InvariantCulture);
                return w.Length >= requiredLength ? w : w.PadLeft(requiredLength, '0');
            }

            StringBuilder temp = new StringBuilder(16);
            int @base = digits.Length;
            StringBuilder s = new StringBuilder(16);
            long n = number;
            int count = 0;
            while (n > 0)
            {
                int digit = digits[(int)(n % @base)];
                StringTool.PrependWideChar(s, digit);
                count++;
                n = n / @base;
            }

            for (int i = 0; i < (requiredLength - count); i++)
            {
                temp.AppendCodePoint(digits[0]);
            }

            temp.Append(s);
            return temp.ToString();
        }

        /*if (formchar == 'w')*/
        public static string ToRoman(long n)
        {
            if (n <= 0 || n > 9999)
            {
                return n.ToString(CultureInfo.InvariantCulture);
            }

            return romanThousands[(int)n / 1000] + romanHundreds[((int)n / 100) % 10] + romanTens[((int)n / 10) % 10] + romanUnits[(int)n % 10];
        }
        /*if (formchar == 'w')*/
        public virtual string ToJapanese(long number)
        {
            StringBuilder fsb = new StringBuilder(16);
            if (number == 0)
            {
                fsb.AppendCodePoint(0x3007);
            }
            else if (number <= 9999)
            {
                ToJapanese((int)number, fsb, false);
            }
            else
            {
                fsb.Append(number.ToString(CultureInfo.InvariantCulture));
            }

            return fsb.ToString();
        }

        /*if (formchar == 'w')*/
        private static void ToJapanese(int nr, StringBuilder fsb, bool isInitial)
        {
            if (nr == 0)
            {
            }
            else if (nr <= 9)
            {
                if (!(nr == 1 && isInitial))
                {
                    fsb.AppendCodePoint(kanjiDigits[nr]);
                }
            }
            else if (nr == 10)
            {
                fsb.AppendCodePoint(0x5341);
            }
            else if (nr <= 99)
            {
                ToJapanese(nr / 10, fsb, true);
                fsb.AppendCodePoint(0x5341);
                ToJapanese(nr % 10, fsb, false);
            }
            else if (nr <= 999)
            {
                ToJapanese(nr / 100, fsb, true);
                fsb.AppendCodePoint(0x767e);
                ToJapanese(nr % 100, fsb, false);
            }
            else if (nr <= 9999)
            {
                ToJapanese(nr / 1000, fsb, true);
                fsb.AppendCodePoint(0x5343);
                ToJapanese(nr % 1000, fsb, false);
            }
        }
        /*if (formchar == 'w')*/
        public abstract string ToWords(string cardinal, long number);
        /*if (formchar == 'w')*/
        public virtual string ToWords(string cardinal, long number, int wordCase)
        {
            string s;
            if (number == 0)
            {
                s = Zero();
            }
            else
            {
                s = ToWords(cardinal, number);
            }

            switch (wordCase)
            {
                case UPPER_CASE:
                    return s.ToUpperInvariant();
                case LOWER_CASE:
                    return s.ToLowerInvariant();
                default:
                    return s;
            }
        }

        /*if (formchar == 'w')*/
        public virtual string Zero()
        {
            return "Zero";
        }

        /*if (formchar == 'w')*/
        public abstract string ToOrdinalWords(string ordinalParam, long number, int wordCase);
        /*if (formchar == 'w')*/
        public abstract string MonthName(int month, int minWidth, int maxWidth);
        /*if (formchar == 'w')*/
        public abstract string DayName(int day, int minWidth, int maxWidth);
        /*if (formchar == 'w')*/
        public virtual string HalfDayName(int minutes, int minWidth, int maxWidth)
        {
            string s;
            if (minutes == 0 && maxWidth >= 8 && country == "gb")
            {
                s = "Midnight";
            }
            else if (minutes < 12 * 60)
            {
                switch (maxWidth)
                {
                    case 1:
                        s = "A";
                        break;
                    case 2:
                    case 3:
                        s = "Am";
                        break;
                    default:
                        s = "A.M.";
                        break;
                }
            }
            else if (minutes == 12 * 60 && maxWidth >= 8 && country == "gb")
            {
                s = "Noon";
            }
            else
            {
                switch (maxWidth)
                {
                    case 1:
                        s = "P";
                        break;
                    case 2:
                    case 3:
                        s = "Pm";
                        break;
                    default:
                        s = "P.M.";
                        break;
                }
            }

            return s;
        }

        /*if (formchar == 'w')*/
        public virtual string GetOrdinalSuffixForDateTime(string component)
        {
            return "yes";
        }

        /*if (formchar == 'w')*/
        public virtual string GetEraName(int year)
        {
            return year > 0 ? "AD" : "BC";
        }

        /*if (formchar == 'w')*/
        public virtual string GetCalendarName(string code)
        {
            if (code.Equals("AD"))
            {
                return "Gregorian";
            }
            else
            {
                return code;
            }
        }
    }
}