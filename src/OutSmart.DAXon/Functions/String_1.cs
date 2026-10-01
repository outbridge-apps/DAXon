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
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;

namespace OutSmart.DAXon.Functions
{
    // String_1 (fn:string#1) — REAL elaborator-free impl ported from the excluded String_1.cs.
    // ScalarSystemFunction.Call delegates to Evaluate (interpreter path). The real file's GetElaborator()
    // override (StringFnElaborator : StringElaborator) is intentionally omitted: it would require the
    // StringElaborator compile cluster (the one that explodes the pipeline). The base SystemFunction
    // elaborator default applies; correctness comes from Evaluate, optimization comes later.
    internal sealed class String_1 : ScalarSystemFunction
    {
        public String_1() { }
        public static Func<String_1> New() => () => new String_1();
        public override AtomicValue Evaluate(IItem arg, IXPathContext context)
        {
            try
            {
                return new StringValue(arg.UnicodeStringValue);
            }
            catch (UncheckedXPathException err)
            {
                throw err.GetXPathException();
            }
        }
        public override ISequence ResultWhenEmpty()
        {
            return StringValue.EMPTY_STRING;
        }

        public override Elaborator GetElaborator()
        {
            return new StringFnElaborator();
        }

        // string(child::NAME) on an untyped Tiny tree reads the single child's string directly
        // (no axis iterator, no node wrapper). Off the fast path — foreign/typed tree, or a second
        // matching child (the 0..1 cardinality error) — the generic evaluator runs instead.
        private sealed class StringFnElaborator : ScalarFunctionElaborator
        {
            public override IItemEvaluator ElaborateForItem()
            {
                IItemEvaluator generic = base.ElaborateForItem();
                SystemFunctionCall fnc = (SystemFunctionCall)GetExpression();
                if (fnc.GetArg(0) is CardinalityChecker cc
                    && Cardinality.AllowsZero(cc.RequiredCardinality)
                    && !Cardinality.AllowsMany(cc.RequiredCardinality)
                    && FusedChildAtomizer.MatchAxis(cc.BaseExpression, out int fp))
                {
                    return (context) =>
                    {
                        StringValue fast = FusedChildAtomizer.ReadSingleChildString(context.GetContextItem(), fp, out bool offPath);
                        if (!offPath)
                        {
                            return fast ?? StringValue.EMPTY_STRING;
                        }

                        return generic.Eval(context);
                    };
                }

                return generic;
            }
        }
    }
}
