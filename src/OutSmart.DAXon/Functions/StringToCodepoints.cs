////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
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
    /// This class supports the function string-to-codepoints()
    /// </summary>
    internal sealed class StringToCodepoints : SystemFunction
    {
        public override IntegerValue[] IntegerBounds => new IntegerValue[]
            {
                Int64Value.PLUS_ONE,
                Int64Value.MakeIntegerValue(1114111)
            };

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            StringValue val = (StringValue)arguments[0].Head();
            if (val == null)
            {
                return EmptySequence.GetInstance();
            }

            return SequenceTool.ToLazySequence(val.IterateCharacters());
        }

        public override Expressions.Elaboration.Elaborator GetElaborator()
        {
            return new StringToCodepointsElaborator();
        }

        // Direct pull: the one argument is consumed unconditionally, so it is evaluated as a
        // single item and the codepoint iterator is returned itself — no per-call ToLazySequence
        // wrap and no LazySequence re-unwrap on the consumer side (this also hands Sum's
        // CodepointIterator raw-int fold its iterator directly). Same head-cast semantics as
        // Call; a many-valued or error-bearing argument falls back to the generic elaborator.
        private sealed class StringToCodepointsElaborator : Expressions.Elaboration.PullElaborator
        {
            public override Expressions.Elaboration.IPullEvaluator ElaborateForPull()
            {
                SystemFunctionCall expr = (SystemFunctionCall)GetExpression();
                if (Cardinality.AllowsMany(expr.GetArg(0).GetCardinality()) || ErrorExpression.IsContainedIn(expr.GetArg(0)))
                {
                    SystemFunctionCall.SystemFunctionCallElaborator generic = new SystemFunctionCall.SystemFunctionCallElaborator();
                    generic.SetExpression(expr);
                    return generic.ElaborateForPull();
                }

                Expressions.Elaboration.IItemEvaluator arg0 = expr.GetArg(0).MakeElaborator().ElaborateForItem();
                return (context) =>
                {
                    try
                    {
                        StringValue val = (StringValue)arg0(context);
                        return val == null ? (ISequenceIterator)Trees.Iterators.EmptyIterator.GetInstance() : val.IterateCharacters();
                    }
                    catch (XPathException e)
                    {
                        throw e.MaybeWithLocation(expr.GetLocation()).MaybeWithContext(context);
                    }
                };
            }
        }
    }
}
