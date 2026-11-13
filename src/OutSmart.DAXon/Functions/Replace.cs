////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Regex;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
namespace OutSmart.DAXon.Functions
{
    internal sealed class Replace : RegexFunction
    {
        private bool replacementChecked;
        public static Replace Make20()
        {
            return new Replace();
        }

        protected override bool AllowRegexMatchingEmptyString()
        {
            return false;
        }

        public override Expression MakeFunctionCall(params Expression[] arguments)
        {
            bool doEarlyReplacementCheck = true;
            if (arguments.Length >= 4)
            {
                if (arguments[3] is StringLiteral)
                {
                    string flags = ((StringLiteral)arguments[3]).Stringify();
                    if (flags.Contains("q"))
                    {
                        doEarlyReplacementCheck = false;
                    }
                }
                else
                {
                    doEarlyReplacementCheck = false;
                }
            }

            if (arguments[2] is StringLiteral && doEarlyReplacementCheck)
            {

                // Do early checking of the replacement expression if known statically
                UnicodeString rep = ((StringLiteral)arguments[2]).GetString();
                if (CheckReplacement(rep) == null)
                {
                    replacementChecked = true;
                }
            }

            return base.MakeFunctionCall(arguments);
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            StringValue arg0 = (StringValue)arguments[0].Head();
            UnicodeString input = arg0 == null ? EmptyUnicodeString.GetInstance() : arg0.UnicodeStringValue;
            IRegularExpression re = GetRegularExpression(arguments, 1, 3);
            IItem replacementArg = arguments[2].Head();
            if (replacementArg == null)
            {
                throw new XPathException("Third argument of fn:replace() must not be empty").WithErrorCode("XPTY0004").AsTypeError();
            }

            UnicodeString replacement = replacementArg.UnicodeStringValue;
            if (!replacementChecked && !re.Flags.Contains("q") && !re.IsPlatformNative())
            {

                // if it is a string literal, the check was done at compile time
                string msg = CheckReplacement(replacement);
                if (msg != null)
                {
                    throw new XPathException(msg, "FORX0004", context);
                }
            }

            return new StringValue(re.Replace(input, replacement));
        }

        public static string CheckReplacement(UnicodeString rep)
        {
            for (int i = 0; i < rep.Length(); i++)
            {
                int c = rep.CodePointAt(i);
                if (c == '$')
                {
                    if (i + 1 < rep.Length())
                    {
                        int index = ++i;
                        int next = rep.CodePointAt(index);
                        if (next < '0' || next > '9')
                        {
                            return "Invalid replacement string in replace(): $ sign must be followed by digit 0-9";
                        }
                    }
                    else
                    {
                        return "Invalid replacement string in replace(): $ sign at end of string";
                    }
                }
                else if (c == '\\')
                {
                    if (i + 1 < rep.Length())
                    {
                        int index = ++i;
                        int next = rep.CodePointAt(index);
                        if (next != '\\' && next != '$')
                        {
                            return "Invalid replacement string in replace(): \\ character must be followed by \\ or $";
                        }
                    }
                    else
                    {
                        return "Invalid replacement string in replace(): \\ character at end of string";
                    }
                }
            }

            return null;
        }
    }
}