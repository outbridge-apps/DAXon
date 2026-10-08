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
        private int version = 20;

        private bool replacementChecked;
        public static Replace Make20()
        {
            Replace rep = new Replace();
            rep.version = 20;
            return rep;
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
                    if (flags.Contains("q") || flags.Contains(";"))
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
            UnicodeString replacement = replacementArg == null ? null : replacementArg.UnicodeStringValue;
            if (replacement == null && version == 20)
            {
                throw new XPathException("Third argument of fn:replace() must not be empty").WithErrorCode("XPTY0004").AsTypeError();
            }

            Func<UnicodeString, UnicodeString[], UnicodeString> action = null;
            if (arguments.Length == 5)
            {
                IFunctionItem actionFn = (IFunctionItem)arguments[4].Head();
                if (actionFn != null)
                {
                    action = (@in, groups) =>
                    {
                        try
                        {

                            // cast to UnicodeString[] is needed for the transpiler
                            IList<IItem> groupItems = new List<IItem>(((UnicodeString[])groups).Length);
                            foreach (UnicodeString group in groups)
                            {
                                groupItems.Add(new StringValue(group, BuiltInAtomicType.UNTYPED_ATOMIC));
                            }

                            ISequence result = actionFn.Call(context, new ISequence[] { new StringValue(@in, BuiltInAtomicType.UNTYPED_ATOMIC), SequenceExtent.MakeSequenceExtent(groupItems) });
                            IItem resultItem = result.Head();
                            if (resultItem == null)
                            {
                                return EmptyUnicodeString.GetInstance();
                            }
                            else
                            {
                                return resultItem.UnicodeStringValue;
                            }
                        }
                        catch (XPathException e)
                        {
                            throw new InvalidOperationException(e.Message, e);
                        }
                    };
                } //            IRegularExpression re = getRegularExpression(arguments, 1, 3);
            }

            if (replacement != null && action != null)
            {
                throw new XPathException("Cannot supply both a replacement string and a replacement action", "FORX0005");
            }

            if (replacement != null && !replacementChecked && !re.Flags.Contains("q") && !re.IsPlatformNative())
            {

                // if it is a string literal, the check was done at compile time
                string msg = CheckReplacement(replacement);
                if (msg != null)
                {
                    throw new XPathException(msg, "FORX0004", context);
                }
            }

            if (replacement != null)
            {
                return new StringValue(re.Replace(input, replacement));
            }
            else if (action != null)
            {
                return new StringValue(re.ReplaceWith(input, action));
            }
            else
            {
                return new StringValue(re.Replace(input, EmptyUnicodeString.GetInstance()));
            }
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