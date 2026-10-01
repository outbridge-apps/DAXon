////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;

namespace OutSmart.DAXon.Functions
{

    // SubstringBefore (fn:substring-before#2) — REAL impl ported from the excluded SubstringBefore.cs:37-43,
    // elaborator-free. The real file is excluded ONLY because its inner SubstringBeforeFnElaborator : StringElaborator
    // drags the StringElaborator compile cluster; Call + the (compiled) CollatingFunctionFixed base + the default
    // codepoint collator's ISubstringMatcher.SubstringBefore are all available. DCOLL in the Register wires the
    // default collation via CollatingFunctionFixed.SetRetainedStaticContext -> AllocateCollator, which also
    // validates/upgrades it to an ISubstringMatcher because IsSubstringMatchingFunction()=>true. The 3-arg form
    // (CollatingFunctionFree, excluded) is intentionally NOT provided; Invoice only needs substring-before#2.
    internal sealed class SubstringBefore : CollatingFunctionFixed
    {
        public SubstringBefore() { }
        public static Func<SubstringBefore> New() => () => new SubstringBefore();
        public override bool IsSubstringMatchingFunction() => true;
        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            UnicodeString s0 = GetUniStringArg(arguments[0]);
            UnicodeString s1 = GetUniStringArg(arguments[1]);
            IStringCollator collator = StringCollator;
            return new StringValue(((ISubstringMatcher)collator).SubstringBefore(s0, s1));
        }

        public override Elaborator GetElaborator()
        {
            return new SubstringMatchElaborator();
        }
    }

    // Eager-item elaborator shared by fn:substring-before/-after: both arguments are consumed
    // unconditionally, so they are evaluated as single items — without the per-call
    // LazyPullEvaluator + CardinalityCheckingIterator + LazySequence wrapping of the generic
    // argument pipeline. Same GetUniStringArg semantics (head cast to StringValue, empty when
    // absent) and the same ISubstringMatcher primitive. Many-valued or error-bearing arguments
    // fall back to the generic function-call elaborator.
    internal sealed class SubstringMatchElaborator : ItemElaborator
    {
        public override IItemEvaluator ElaborateForItem()
        {
            SystemFunctionCall expr = (SystemFunctionCall)GetExpression();
            CollatingFunctionFixed fn = (CollatingFunctionFixed)expr.TargetFunction;
            bool before = fn is SubstringBefore;
            for (int a = 0; a < 2; a++)
            {
                if (Cardinality.AllowsMany(expr.GetArg(a).GetCardinality()) || ErrorExpression.IsContainedIn(expr.GetArg(a)))
                {
                    SystemFunctionCall.SystemFunctionCallElaborator generic = new SystemFunctionCall.SystemFunctionCallElaborator();
                    generic.SetExpression(expr);
                    return generic.ElaborateForItem();
                }
            }

            IItemEvaluator arg0 = expr.GetArg(0).MakeElaborator().ElaborateForItem();
            IItemEvaluator arg1 = expr.GetArg(1).MakeElaborator().ElaborateForItem();
            return (context) =>
            {
                try
                {
                    IItem i0 = arg0.Eval(context);
                    IItem i1 = arg1.Eval(context);
                    UnicodeString s0 = i0 == null ? EmptyUnicodeString.GetInstance() : ((StringValue)i0).UnicodeStringValue;
                    UnicodeString s1 = i1 == null ? EmptyUnicodeString.GetInstance() : ((StringValue)i1).UnicodeStringValue;
                    ISubstringMatcher m = (ISubstringMatcher)fn.StringCollator;
                    return new StringValue(before ? m.SubstringBefore(s0, s1) : m.SubstringAfter(s0, s1));
                }
                catch (XPathException e)
                {
                    throw e.MaybeWithLocation(expr.GetLocation()).MaybeWithContext(context);
                }
            };
        }
    }
}
