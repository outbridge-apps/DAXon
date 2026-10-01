////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Regex;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// This class implements the 3-argument matches() function for regular expression matching
    /// </summary>
    internal sealed class Matches : RegexFunction
    {
        protected override bool AllowRegexMatchingEmptyString()
        {
            return true;
        }

        public bool EvalMatches(UnicodeString input, UnicodeString regex, UnicodeString flags, IXPathContext context)
        {
            IRegularExpression re;
            if (regex == null)
            {
                return false;
            }

            try
            {
                string lang = "XP30";
                if (context.GetConfiguration().XsdVersion == Configuration.XSD11)
                {
                    lang += "/XSD11";
                }

                re = context.GetConfiguration().CompileRegularExpression(regex, flags.ToString(), lang, null);
            }
            catch (XPathException err)
            {
                err.MaybeSetErrorCode("FORX0002");
                err.MaybeSetContext(context);
                throw;
            }

            return re.ContainsMatch(input);
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            IRegularExpression re = GetRegularExpression(arguments, 1, 2);
            StringValue arg = (StringValue)arguments[0].Head();
            UnicodeString @in = arg == null ? EmptyUnicodeString.GetInstance() : arg.UnicodeStringValue;
            bool result = re.ContainsMatch(@in);
            return BooleanValue.Get(result);
        }

        public override Elaborator GetElaborator()
        {
            return new MatchesFnElaborator();
        }

        internal sealed class MatchesFnElaborator : BooleanElaborator
        {
            public override IBooleanEvaluator ElaborateForBoolean()
            {
                SystemFunctionCall fnc = (SystemFunctionCall)GetExpression();
                Matches fn = (Matches)fnc.TargetFunction;
                IUnicodeStringEvaluator arg0eval = fnc.GetArg(0).MakeElaborator().ElaborateForUnicodeString(true);
                IRegularExpression staticRegex = fn.StaticRegex;
                if (staticRegex == null)
                {
                    IUnicodeStringEvaluator arg1eval = fnc.GetArg(1).MakeElaborator().ElaborateForUnicodeString(true);
                    IUnicodeStringEvaluator arg2eval = fn.GetArity() == 3 ? fnc.GetArg(2).MakeElaborator().ElaborateForUnicodeString(true) : (cxt) => EmptyUnicodeString.GetInstance();
                    return (context) =>
                    {
                        try
                        {
                            return fn.EvalMatches(arg0eval.Eval(context), arg1eval.Eval(context), arg2eval.Eval(context), context);
                        }
                        catch (XPathException err)
                        {
                            throw err.MaybeWithLocation(fnc.GetLocation()).MaybeWithContext(context);
                        }
                    };
                }
                else
                {
                    return (context) => staticRegex.ContainsMatch(arg0eval.Eval(context));
                }
            }
        }
    }
}
