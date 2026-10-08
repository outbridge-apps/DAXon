////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Core;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Expressions;

// PatternMaker, MultipleNodeKindTest, AtomicSortComparer, LocalOrderComparer,
// BooleanFn, FunctionAvailable, BuiltInType, ParamKeywords, UnparsedEntity,
// DummyNamespaceResolver, LoopLifter Ã¢â‚¬â€ frequently-referenced helper classes.
namespace OutSmart.DAXon.Patterns
{
    internal static class PatternMaker
    {
        // Runtime: real FromExpression (the prior `=> null` hollow stub made every expression->Pattern
        // conversion return null -> NRE at PatternParser.ParsePattern pat.SetOriginalText, the moment any
        // xsl:template match pattern compiled). Faithful to PatternMaker.java: ToPattern + location copy.
        public static object FromExpression(object expr, object config, bool isFinal)
        {
            Pattern result = ((Expression)expr).ToPattern((Configuration)config);
            OutSmart.DAXon.Expressions.Parsing.ExpressionTool.CopyLocationInfo((Expression)expr, result);
            return result;
        }
        // SystemFunctionCall callers reference GetAxisForPathStep.
        // Faithful: the upwards axis qualifying a path step is the INVERSE of the step's axis
        // (child -> parent), recursing into the first step of a sub-path. The prior `=> 0` returned
        // AxisInfo.ANCESTOR for every step, which made A/B match like A//B (too loose).
        // XR8: full upstream dispatch — the previous cut fell to PARENT for FilterExpression &c.,
        // so a predicated step like //a[@id='x'] pattern-matched as /a[@id='x'] (silent no-match).
        public static int GetAxisForPathStep(object step)
        {
            OutSmart.DAXon.Expressions.Parsing.ExpressionTool.ProbeTreeDepth();
            if (step is AxisExpression)
                return AxisInfo.inverseAxis[((AxisExpression)step).Axis];
            if (step is FilterExpression)
                return GetAxisForPathStep(((FilterExpression)step).GetSelectExpression());
            if (step is FirstItemExpression)
                return GetAxisForPathStep(((FirstItemExpression)step).BaseExpression);
            if (step is LastItemExpression)
                return GetAxisForPathStep(((LastItemExpression)step).BaseExpression);
            if (step is TailExpression)
                return GetAxisForPathStep(((TailExpression)step).BaseExpression);
            if (step is SubscriptExpression)
                return GetAxisForPathStep(((SubscriptExpression)step).BaseExpression);
            if (step is SlashExpression)
                return GetAxisForPathStep(((SlashExpression)step).FirstStep);
            if (step is ContextItemExpression)
                return AxisInfo.SELF;
            throw new OutSmart.DAXon.Transformation.XPathException("The path in a pattern must contain simple steps");
        }
    }
}
