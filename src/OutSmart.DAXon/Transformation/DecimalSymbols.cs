////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Collections;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Transformation
{
    public class DecimalSymbols
    {
        // Property slots, not a host contract: internal so INFINITY/NAN stop colliding by case
        // with the Infinity/NaN properties below, which is what a name binder trips over.
        internal const int DECIMAL_SEPARATOR = 0;
        internal const int GROUPING_SEPARATOR = 1;
        internal const int DIGIT = 2;
        internal const int MINUS_SIGN = 3;
        internal const int PERCENT = 4;
        internal const int PER_MILLE = 5;
        internal const int ZERO_DIGIT = 6;
        internal const int EXPONENT_SEPARATOR = 7;
        internal const int PATTERN_SEPARATOR = 8;
        internal const int INFINITY = 9;
        internal const int NAN = 10;
        private const int ERR_NOT_SINGLE_CHAR = 0;
        private const int ERR_NOT_UNICODE_DIGIT = 1;
        private const int ERR_SAME_CHAR_IN_TWO_ROLES = 2;
        private const int ERR_TWO_VALUES_FOR_SAME_PROPERTY = 3;
        private static readonly string[] XSLT_CODES = new[]
        {
            "XTSE0020",
            "XTSE1295",
            "XTSE1300",
            "XTSE1290"
        };
        private static readonly string[] XQUERY_CODES = new[]
        {
            "XQST0097",
            "XQST0097",
            "XQST0098",
            "XQST0114"
        };
        public static readonly string[] propertyNames = new[]
        {
            "decimal-separator",
            "grouping-separator",
            "digit",
            "minus-sign",
            "percent",
            "per-mille",
            "zero-digit",
            "exponent-separator",
            "pattern-separator",
            "infinity",
            "NaN"
        };

        static int[] zeroDigits = new[]
        {
            0x0030,
            0x0660,
            0x06f0,
            0x0966,
            0x09e6,
            0x0a66,
            0x0ae6,
            0x0b66,
            0x0be6,
            0x0c66,
            0x0ce6,
            0x0d66,
            0x0e50,
            0x0ed0,
            0x0f20,
            0x1040,
            0x17e0,
            0x1810,
            0x1946,
            0x19d0,
            0xff10,
            0x104a0,
            0x1d7ce,
            0x1d7d8,
            0x1d7e2,
            0x1d7ec,
            0x1d7f6
        };
        private string[] errorCodes = XSLT_CODES;
        private string infinityValue;
        private string NaNValue;
        private readonly int[] intValues = new int[propertyNames.Length - 2];
        private readonly int[] precedences = new int[propertyNames.Length];
        private readonly bool[] inconsistent = new bool[propertyNames.Length];

        public virtual string Infinity
        {
            get => infinityValue; set
            {
                SetProperty(INFINITY, value, 0);
            }
        }

        public virtual string NaN
        {
            get => NaNValue; set
            {
                SetProperty(NAN, value, 0);
            }
        }
        /// <summary>
        /// Create a DecimalSymbols object with default values for all properties
        /// </summary>
        public DecimalSymbols(HostLanguage language, int languageLevel)
        {
            intValues[DECIMAL_SEPARATOR] = '.';
            intValues[GROUPING_SEPARATOR] = ',';
            intValues[DIGIT] = '#';
            intValues[MINUS_SIGN] = '-';
            intValues[PERCENT] = '%';
            intValues[PER_MILLE] = '‰';
            intValues[ZERO_DIGIT] = '0';
            intValues[EXPONENT_SEPARATOR] = 'e';
            intValues[PATTERN_SEPARATOR] = ';';
            infinityValue = "Infinity";
            NaNValue = "NaN";
            ArrayTools.Fill(precedences, int.MinValue);
            SetHostLanguage(language, languageLevel);
        }

        public virtual void SetHostLanguage(HostLanguage language, int languageLevel)
        {
            if (language == HostLanguage.XQUERY)
            {
                errorCodes = XQUERY_CODES;
            }
            else
            {
                errorCodes = XSLT_CODES;
            }
        }

        public virtual int GetDecimalSeparator()
        {
            return intValues[DECIMAL_SEPARATOR];
        }

        public virtual int GetGroupingSeparator()
        {
            return intValues[GROUPING_SEPARATOR];
        }

        public virtual int GetDigit()
        {
            return intValues[DIGIT];
        }

        public virtual int GetMinusSign()
        {
            return intValues[MINUS_SIGN];
        }

        public virtual int GetPercent()
        {
            return intValues[PERCENT];
        }

        public virtual int GetPerMille()
        {
            return intValues[PER_MILLE];
        }

        public virtual int GetZeroDigit()
        {
            return intValues[ZERO_DIGIT];
        }

        public virtual int GetExponentSeparator()
        {
            return intValues[EXPONENT_SEPARATOR];
        }

        public virtual int GetPatternSeparator()
        {
            return intValues[PATTERN_SEPARATOR];
        }

        public virtual void SetDecimalSeparator(string value)
        {
            SetProperty(DECIMAL_SEPARATOR, value, 0);
        }

        public virtual void SetGroupingSeparator(string value)
        {
            SetProperty(GROUPING_SEPARATOR, value, 0);
        }

        public virtual void SetDigit(string value)
        {
            SetProperty(DIGIT, value, 0);
        }

        public virtual void SetMinusSign(string value)
        {
            SetProperty(MINUS_SIGN, value, 0);
        }

        public virtual void SetPercent(string value)
        {
            SetProperty(PERCENT, value, 0);
        }

        public virtual void SetPerMille(string value)
        {
            SetProperty(PER_MILLE, value, 0);
        }

        public virtual void SetZeroDigit(string value)
        {
            SetProperty(ZERO_DIGIT, value, 0);
        }

        public virtual void SetExponentSeparator(string value)
        {
            SetProperty(EXPONENT_SEPARATOR, value, 0);
        }

        public virtual void SetPatternSeparator(string value)
        {
            SetProperty(PATTERN_SEPARATOR, value, 0);
        }

        public virtual void SetProperty(int key, string value, int precedence)
        {
            string name = propertyNames[key];
            if (key <= PATTERN_SEPARATOR)
            {
                int intValue = SingleChar(name, value);
                if (precedence > precedences[key])
                {
                    intValues[key] = intValue;
                    precedences[key] = precedence;
                    inconsistent[key] = false;
                }
                else if (precedence == precedences[key])
                {
                    if (intValue != intValues[key])
                    {
                        inconsistent[key] = true;
                    }
                }
                else
                {
                }

                if (key == ZERO_DIGIT && !IsValidZeroDigit(intValue))
                {
                    throw new XPathException("The value of the zero-digit attribute must be a Unicode digit with value zero", errorCodes[ERR_NOT_UNICODE_DIGIT]);
                }
            }
            else if (key == INFINITY)
            {
                if (precedence > precedences[key])
                {
                    infinityValue = value;
                    precedences[key] = precedence;
                    inconsistent[key] = false;
                }
                else if (precedence == precedences[key])
                {
                    if (!infinityValue.Equals(value))
                    {
                        inconsistent[key] = true;
                    }
                }
            }
            else if (key == NAN)
            {
                if (precedence > precedences[key])
                {
                    NaNValue = value;
                    precedences[key] = precedence;
                    inconsistent[key] = false;
                }
                else if (precedence == precedences[key])
                {
                    if (!NaNValue.Equals(value))
                    {
                        inconsistent[key] = false;
                    }
                }
            }
            else
            {
                throw new ArgumentException();
            }
        }

        public virtual void SetIntProperty(string name, int value)
        {
            for (int i = 0; i < propertyNames.Length; i++)
            {
                if (propertyNames[i].Equals(name))
                {
                    intValues[i] = value;
                }
            }
        }

        public virtual void Export(StructuredQName name, ExpressionPresenter @out)
        {
            DecimalSymbols defaultSymbols = new DecimalSymbols(HostLanguage.XSLT, 31);
            @out.StartElement("decimalFormat");
            if (name != null)
            {
                @out.EmitAttribute("name", name);
            }

            for (int i = 0; i < intValues.Length; i++)
            {
                int propValue = intValues[i];
                if (propValue != defaultSymbols.intValues[i])
                {
                    @out.EmitAttribute(propertyNames[i], propValue + "");
                }
            }

            if (Infinity != "Infinity")
            {
                @out.EmitAttribute("infinity", Infinity);
            }

            if (NaN != "NaN")
            {
                @out.EmitAttribute("NaN", NaN);
            }

            @out.EndElement();
        }

        private int SingleChar(string name, string value)
        {
            // one CODEPOINT, not one UTF-16 unit — an astral separator is a single character (numberformat70)
            bool single = value != null &&
                (value.Length == 1 ? !char.IsSurrogate(value[0])
                                   : value.Length == 2 && char.IsSurrogatePair(value[0], value[1]));
            if (!single)
            {
                XPathException err = new XPathException("Attribute " + name + " should be a single character", errorCodes[ERR_NOT_SINGLE_CHAR]);
                err.SetIsStaticError(true);
                throw err;
            }

            return char.ConvertToUtf32(value, 0);
        }

        public virtual void CheckConsistency(StructuredQName name)
        {
            for (int i = 0; i < 10; i++)
            {
                if (inconsistent[i])
                {
                    throw new XPathException("Inconsistency in " + (name == null ? "unnamed decimal format. " : "decimal format " + name.DisplayName + ". ") + "There are two inconsistent values for decimal-format property " + propertyNames[i] + " at the same import precedence").WithErrorCode(errorCodes[ERR_TWO_VALUES_FOR_SAME_PROPERTY]).AsStaticError();
                }
            }

            IntHashMap<string> map = new IntHashMap<string>(20);
            map.Put(GetDecimalSeparator(), "decimal-separator");
            if (map[GetGroupingSeparator()] != null)
            {
                Duplicate("grouping-separator", map[GetGroupingSeparator()], name);
            }

            map.Put(GetGroupingSeparator(), "grouping-separator");
            if (map[GetPercent()] != null)
            {
                Duplicate("percent", map[GetPercent()], name);
            }

            map.Put(GetPercent(), "percent");
            if (map[GetPerMille()] != null)
            {
                Duplicate("per-mille", map[GetPerMille()], name);
            }

            map.Put(GetPerMille(), "per-mille");
            if (map[GetDigit()] != null)
            {
                Duplicate("digit", map[GetDigit()], name);
            }

            map.Put(GetDigit(), "digit");
            if (map[GetPatternSeparator()] != null)
            {
                Duplicate("pattern-separator", map[GetPatternSeparator()], name);
            }

            map.Put(GetPatternSeparator(), "pattern-separator");
            if (map[GetExponentSeparator()] != null)
            {
                Duplicate("exponent-separator", map[GetExponentSeparator()], name);
            }

            map.Put(GetExponentSeparator(), "exponent-separator");
            int zero = GetZeroDigit();
            for (int i = zero; i < zero + 10; i++)
            {
                if (map[i] != null)
                {
                    throw new XPathException("Inconsistent properties in " + (name == null ? "unnamed decimal format. " : "decimal format " + name.DisplayName + ". ") + "The same character is used as digit " + (i - zero) + " in the chosen digit family, and as the " + map[i]).WithErrorCode(errorCodes[ERR_SAME_CHAR_IN_TWO_ROLES]);
                }
            }
        }

        private void Duplicate(string role1, string role2, StructuredQName name)
        {
            throw new XPathException("Inconsistent properties in " + (name == null ? "unnamed decimal format. " : "decimal format " + name.DisplayName + ". ") + "The same character is used as the " + role1 + " and as the " + role2).WithErrorCode(errorCodes[ERR_SAME_CHAR_IN_TWO_ROLES]);
        }

        public static bool IsValidZeroDigit(int zeroDigit)
        {
            return Array.BinarySearch(zeroDigits, zeroDigit) >= 0;
        }
        public override bool Equals(object obj)
        {
            if (!(obj is DecimalSymbols))
            {
                return false;
            }

            DecimalSymbols o = (DecimalSymbols)obj;
            return GetDecimalSeparator() == o.GetDecimalSeparator() && GetGroupingSeparator() == o.GetGroupingSeparator() && GetDigit() == o.GetDigit() && GetMinusSign() == o.GetMinusSign() && GetPercent() == o.GetPercent() && GetPerMille() == o.GetPerMille() && GetZeroDigit() == o.GetZeroDigit() && GetPatternSeparator() == o.GetPatternSeparator() && Infinity.Equals(o.Infinity) && NaN.Equals(o.NaN);
        }

        public override int GetHashCode()
        {
            return GetDecimalSeparator() + (37 * GetGroupingSeparator()) + (41 * GetDigit());
        }
    }
}