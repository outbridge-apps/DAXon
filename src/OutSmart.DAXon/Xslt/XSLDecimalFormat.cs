////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// Handler for xsl:decimal-format elements in stylesheet. <br>
    /// </summary>
    internal sealed class XSLDecimalFormat : StyleElement
    {
        bool prepared = false;
        string name;
        string decimalSeparator;
        string groupingSeparator;
        string exponentSeparator;
        string infinity;
        string minusSign;
        string NaN;
        string percent;
        string perMille;
        string zeroDigit;
        string digit;
        string patternSeparator;
        DecimalSymbols symbols;
        public override bool IsDeclaration()
        {
            return true;
        }

        public override void PrepareAttributes()
        {
            if (prepared)
            {
                return;
            }

            prepared = true;
            foreach (AttributeInfo att in Attributes())
            {
                INodeName attName = att.GetNodeName();
                string f = attName.DisplayName;
                string value = att.Value;
                switch (f)
                {
                    case "name":
                        name = Whitespace.Trim(value);
                        break;
                    case "decimal-separator":
                        decimalSeparator = value;
                        break;
                    case "grouping-separator":
                        groupingSeparator = value;
                        break;
                    case "infinity":
                        infinity = value;
                        break;
                    case "minus-sign":
                        minusSign = value;
                        break;
                    case "NaN":
                        NaN = value;
                        break;
                    case "percent":
                        percent = value;
                        break;
                    case "per-mille":
                        perMille = value;
                        break;
                    case "zero-digit":
                        zeroDigit = value;
                        break;
                    case "digit":
                        digit = value;
                        break;
                    case "exponent-separator":
                        exponentSeparator = value;
                        break;
                    case "pattern-separator":
                        patternSeparator = value;
                        break;
                    default:
                        CheckUnknownAttribute(attName);
                        break;
                }
            }
        }

        public override void Validate(ComponentDeclaration decl)
        {
            CheckTopLevel("XTSE0010", false);
            CheckEmpty();
            int precedence = decl.Precedence;
            if (symbols == null)
            {
                return; // error already reported
            }

            if (decimalSeparator != null)
            {
                SetProp(DecimalSymbols.DECIMAL_SEPARATOR, decimalSeparator, precedence);
            }

            if (groupingSeparator != null)
            {
                SetProp(DecimalSymbols.GROUPING_SEPARATOR, groupingSeparator, precedence);
            }

            if (infinity != null)
            {
                SetProp(DecimalSymbols.INFINITY, infinity, precedence);
            }

            if (minusSign != null)
            {
                SetProp(DecimalSymbols.MINUS_SIGN, minusSign, precedence);
            }

            if (NaN != null)
            {
                SetProp(DecimalSymbols.NAN, NaN, precedence);
            }

            if (percent != null)
            {
                SetProp(DecimalSymbols.PERCENT, percent, precedence);
            }

            if (perMille != null)
            {
                SetProp(DecimalSymbols.PER_MILLE, perMille, precedence);
            }

            if (zeroDigit != null)
            {
                SetProp(DecimalSymbols.ZERO_DIGIT, zeroDigit, precedence);
            }

            if (digit != null)
            {
                SetProp(DecimalSymbols.DIGIT, digit, precedence);
            }

            if (exponentSeparator != null)
            {
                SetProp(DecimalSymbols.EXPONENT_SEPARATOR, exponentSeparator, precedence);
            }

            if (patternSeparator != null)
            {
                SetProp(DecimalSymbols.PATTERN_SEPARATOR, patternSeparator, precedence);
            }
        }

        private void SetProp(int propertyCode, string value, int precedence)
        {
            try
            {
                symbols.SetProperty(propertyCode, value, precedence);
            }
            catch (XPathException err)
            {
                throw err.WithLocation(new AttributeLocation(this, StructuredQName.FromClarkName(DecimalSymbols.propertyNames[propertyCode])));
            }
        }

        public override void Index(ComponentDeclaration decl, PrincipalStylesheetModule top)
        {
            PrepareAttributes();
            DecimalFormatManager dfm = GetCompilation().GetPrincipalStylesheetModule().GetDecimalFormatManager();
            if (name == null)
            {
                symbols = dfm.DefaultDecimalFormat;
            }
            else
            {
                StructuredQName formatName = MakeQName(name, null, "name");
                symbols = dfm.ObtainNamedDecimalFormat(formatName);
                symbols.SetHostLanguage(HostLanguage.XSLT, 30);
            }
        }

        public override void CompileDeclaration(Compilation compilation, ComponentDeclaration decl)
        {
        }
    }
}