////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// Implements the fn:contains() function, with the collation already known
    /// </summary>
    internal sealed class Contains : CollatingFunctionFixed
    {
        public override bool IsSubstringMatchingFunction()
        {
            return true;
        }

        private static bool ContainsFn(StringValue arg0, StringValue arg1, ISubstringMatcher collator)
        {
            if (arg1 == null || arg1.IsEmpty() || collator.IsEqualToEmpty(arg1.UnicodeStringValue))
            {
                return true;
            }

            if (arg0 == null || arg0.IsEmpty())
            {
                return false;
            }

            return collator.Contains(arg0.UnicodeStringValue, arg1.UnicodeStringValue);
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            StringValue s0 = (StringValue)arguments[0].Head();
            StringValue s1 = (StringValue)arguments[1].Head();
            return BooleanValue.Get(ContainsFn(s0, s1, (ISubstringMatcher)StringCollator));
        }

        public override Elaborator GetElaborator()
        {
            return new ContainsFnElaborator();
        }

        /// <summary>
        /// Expression elaborator for a call to contains(), starts-with(), or ends-with()
        /// </summary>
        internal sealed class ContainsFnElaborator : BooleanElaborator
        {
            public override IBooleanEvaluator ElaborateForBoolean()
            {
                SystemFunctionCall fnc = (SystemFunctionCall)GetExpression();
                CollatingFunctionFixed fn = (CollatingFunctionFixed)fnc.TargetFunction;
                ISubstringMatcher collation = (ISubstringMatcher)fn.StringCollator;
                string name = fnc.GetFunctionName().GetLocalPart();
                if (collation == CodepointCollator.GetInstance())
                {
                    // Stay on UnicodeString: tree text is a byte rep (Slice8/Twine8) whose
                    // IndexOf/HasSubstring overrides scan bytes directly; the old path via
                    // ElaborateForString converted every value to a System.String first (a full
                    // char[] copy per call). For well-formed strings ordinal UTF-16 search and
                    // codepoint search agree, so results are unchanged. Empty-needle screens
                    // reproduce the String semantics (contains/starts/ends-with "" are true).
                    IUnicodeStringEvaluator arg0Eval = fnc.GetArg(0).MakeElaborator().ElaborateForUnicodeString(true);
                    IUnicodeStringEvaluator arg1Eval = fnc.GetArg(1).MakeElaborator().ElaborateForUnicodeString(true);
                    switch (name)
                    {
                        case "contains":
                            return (context) =>
                            {
                                UnicodeString s0 = arg0Eval.Eval(context);
                                UnicodeString s1 = arg1Eval.Eval(context);
                                return s1.IsEmpty() || s0.IndexOf(s1, 0) >= 0;
                            };
                        case "starts-with":
                            return (context) =>
                            {
                                UnicodeString s0 = arg0Eval.Eval(context);
                                UnicodeString s1 = arg1Eval.Eval(context);
                                return s0.HasSubstring(s1, 0);
                            };
                        case "ends-with":
                            return (context) =>
                            {
                                UnicodeString s0 = arg0Eval.Eval(context);
                                UnicodeString s1 = arg1Eval.Eval(context);
                                long l0 = s0.Length();
                                long l1 = s1.Length();
                                return l1 <= l0 && s0.HasSubstring(s1, l0 - l1);
                            };
                        default:
                            throw new NotSupportedException();
                    }
                }
                else
                {
                    IUnicodeStringEvaluator arg0Eval = fnc.GetArg(0).MakeElaborator().ElaborateForUnicodeString(true);
                    IUnicodeStringEvaluator arg1Eval = fnc.GetArg(1).MakeElaborator().ElaborateForUnicodeString(true);
                    switch (name)
                    {
                        case "contains":
                            return (context) =>
                            {
                                UnicodeString s0 = arg0Eval.Eval(context);
                                UnicodeString s1 = arg1Eval.Eval(context);
                                return collation.Contains(s0, s1);
                            };
                        case "starts-with":
                            return (context) =>
                            {
                                UnicodeString s0 = arg0Eval.Eval(context);
                                UnicodeString s1 = arg1Eval.Eval(context);
                                return collation.StartsWith(s0, s1);
                            };
                        case "ends-with":
                            return (context) =>
                            {
                                UnicodeString s0 = arg0Eval.Eval(context);
                                UnicodeString s1 = arg1Eval.Eval(context);
                                return collation.EndsWith(s0, s1);
                            };
                        default:
                            throw new NotSupportedException();
                    }
                }
            }
        }
    }
}
