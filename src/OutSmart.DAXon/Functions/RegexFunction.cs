////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Regex;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using System.Collections.Generic;
namespace OutSmart.DAXon.Functions
{
    internal abstract class RegexFunction : SystemFunction, IStatefulSystemFunction
    {
        private IRegularExpression staticRegex;
        public virtual IRegularExpression StaticRegex => staticRegex;

        private void TryToBindRegularExpression(Expression[] arguments)
        {

            // For all these functions, the regular expression is the second argument, and the flags
            // argument is the last argument.
            if (arguments[1] is StringLiteral && arguments[arguments.Length - 1] is StringLiteral)
            {
                try
                {
                    Configuration config = GetRetainedStaticContext().GetConfiguration();
                    UnicodeString re = ((StringLiteral)arguments[1]).GroundedValue.UnicodeStringValue;
                    string flags = ((StringLiteral)arguments[arguments.Length - 1]).Stringify();
                    string hostLang = "XP30";
                    if (config.XsdVersion == Configuration.XSD11)
                    {
                        hostLang += "/XSD11";
                    }

                    IList<string> warnings = new List<string>(1);
                    staticRegex = config.CompileRegularExpression(re, flags, hostLang, warnings);
                    if (!AllowRegexMatchingEmptyString() && staticRegex.Matches(EmptyUnicodeString.GetInstance()))
                    {
                        staticRegex = null; // will cause a dynamic error
                    }
                }
                catch (XPathException err)
                {
                }
            }
        }

        // If the regex is invalid, we leave it to be evaluated again at execution time
        public RegexFunction Copy()
        {
            RegexFunction copy = (RegexFunction)SystemFunction.MakeFunction(GetFunctionName().GetLocalPart(), GetRetainedStaticContext(), GetArity());
            copy.staticRegex = staticRegex;
            return copy;
        }

        protected abstract bool AllowRegexMatchingEmptyString();
        public override Expression MakeFunctionCall(params Expression[] arguments)
        {
            TryToBindRegularExpression(arguments);
            return base.MakeFunctionCall(arguments);
        }

        public override Expression MakeOptimizedFunctionCall(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo, params Expression[] arguments)
        {
            TryToBindRegularExpression(arguments);
            return base.MakeOptimizedFunctionCall(visitor, contextInfo, arguments);
        }

        protected virtual IRegularExpression GetRegularExpression(ISequence[] args, int regexPos, int flagsPos)
        {
            if (staticRegex != null)
            {
                return staticRegex;
            }

            Configuration config = GetRetainedStaticContext().GetConfiguration();
            IItem regexItem = args[regexPos].Head();
            if (regexItem == null)
            {

                // for XPath 4.0 tokenize() only
                if (GetRetainedStaticContext().GetPackageData().HostLanguageVersion >= 40)
                {
                    return null;
                }
                else
                {
                    XPathException err = new XPathException("Regular expression argument must not be an empty sequence (unless 4.0 is enabled)", "XPTY0004");
                    err.SetIsTypeError(true);
                    throw err;
                }
            }

            UnicodeString regexArg = regexItem.UnicodeStringValue;
            string flags = "";
            if (flagsPos < args.Length)
            {
                IItem flagsItem = args[flagsPos].Head(); // May generally be empty in XPath 4.0
                if (flagsItem == null && GetRetainedStaticContext().GetPackageData().HostLanguageVersion < 40)
                {
                    XPathException err = new XPathException("Flags argument must not be an empty sequence (unless 4.0 is enabled)", "XPTY0004");
                    err.SetIsTypeError(true);
                    throw err;
                }

                flags = flagsItem == null ? "" : flagsItem.GetStringValue();
            }

            string hostLang = "XP30";
            if (config.XsdVersion == Configuration.XSD11)
            {
                hostLang += "/XSD11";
            }

            IList<string> warnings = new List<string>(1);
            IRegularExpression regex = config.CompileRegularExpression(regexArg, flags, hostLang, warnings);
            if (!AllowRegexMatchingEmptyString() && regex.Matches(EmptyUnicodeString.GetInstance()))
            {
                throw new XPathException("The regular expression must not be one that matches a zero-length string", "FORX0003");
            }

            return regex;
        }
        // Delegate the interface method to the real (covariant) Copy() above. net472 has no covariant-return
        // override, so this explicit impl is required; it was `=> default` (null), which made
        // SystemFunctionCall.Copy (used by LetExpression.InlineReferences) NRE for any regex fn — analyze-string,
        // matches, replace, tokenize — when copied during optimisation.
        SystemFunction IStatefulSystemFunction.Copy() => Copy();
    }
}