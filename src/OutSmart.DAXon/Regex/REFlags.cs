////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
namespace OutSmart.DAXon.Regex
{
    internal sealed class REFlags
    {
        private bool caseIndependent;
        private bool multiLine;
        private bool singleLine;
        private bool allowWhitespace;
        private bool literal;
        private bool xpath20;
        private bool xpath30;
        private bool xsd11;
        private bool debug; // flags = ";g"
        private bool allowUnknownBlockNames = false; //flags = ";k"
        public REFlags(string flags, string language)
        {
            if (language.Equals("XSD10"))
            {
            }
            else if (language.Contains("XSD11"))
            {
                allowUnknownBlockNames = !language.Contains("XP");
                xsd11 = true;
            }

            if (language.Contains("XP20"))
            {
                xpath20 = true;
            }
            else if (language.Contains("XP30") || language.Contains("XP31"))
            {
                xpath20 = true;
                xpath30 = true;
            }

            int semi = flags.IndexOf(';');
            int endStd = semi >= 0 ? semi : flags.Length;
            for (int i = 0; i < endStd; i++)
            {
                char c = flags[i];
                switch (c)
                {
                    case 'i':
                        caseIndependent = true;
                        break;
                    case 'm':
                        multiLine = true;
                        break;
                    case 's':
                        singleLine = true;
                        break;
                    case 'q':
                        literal = true;
                        if (!xpath30)
                        {
                            throw new RESyntaxException("'q' flag requires XPath 3.0 to be enabled");
                        }

                        break;
                    case 'x':
                        allowWhitespace = true;
                        break;
                    default:
                        throw new RESyntaxException("Unrecognized flag '" + c + "'");
                }
            }

            for (int i = semi + 1; i < flags.Length; i++)
            {
                char c = flags[i];
                switch (c)
                {
                    case 'g':
                        debug = true;
                        break;
                    case 'k':
                        allowUnknownBlockNames = true;
                        break;
                    case 'K':
                        allowUnknownBlockNames = false;
                        break;
                }
            }
        }

        public bool IsCaseIndependent()
        {
            return caseIndependent;
        }

        public bool IsMultiLine()
        {
            return multiLine;
        }

        public bool IsSingleLine()
        {
            return singleLine;
        }

        public bool IsAllowWhitespace()
        {
            return allowWhitespace;
        }

        public bool IsLiteral()
        {
            return literal;
        }

        public bool IsAllowsXPath20Extensions()
        {
            return xpath20;
        }

        public bool IsAllowsXPath30Extensions()
        {
            return xpath30;
        }

        public bool IsAllowsXSD11Syntax()
        {
            return xsd11;
        }

        public bool IsAllowUnknownBlockNames()
        {
            return allowUnknownBlockNames;
        }
    }
}