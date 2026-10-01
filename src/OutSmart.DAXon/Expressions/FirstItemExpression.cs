////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Ported from upstream net/sf/saxon/expr/FirstItemExpression.java (replaces the stub whose factory
// returned its operand unchanged, so a [1] positional predicate was silently dropped).

using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;

namespace OutSmart.DAXon.Expressions
{
    /// <summary>Returns the first item of a sequence — the rewrite target of the predicate [1] / [last()].</summary>
    internal sealed class FirstItemExpression : SingleItemFilter
    {

        public override int ImplementationMethod => EVALUATE_METHOD;

        public override string ExpressionName => "first";
        private FirstItemExpression(Expression @base) : base(@base)
        {
        }

        public static Expression MakeFirstItemExpression(Expression @base)
        {
            if (@base is FirstItemExpression)
            {
                return @base;
            }

            return new FirstItemExpression(@base);
        }

        protected override int ComputeCardinality()
        {
            return Cardinality.AllowsZero(BaseExpression.GetCardinality())
                ? StaticProperty.ALLOWS_ZERO_OR_ONE
                : StaticProperty.ALLOWS_ONE;
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            Expression e2 = new FirstItemExpression(BaseExpression.Copy(rebindings));
            ExpressionTool.CopyLocationInfo(this, e2);
            return e2;
        }

        public override Patterns.Pattern ToPattern(Configuration config)
        {
            Patterns.Pattern basePattern = BaseExpression.ToPattern(config);
            ItemType type = basePattern.GetItemType();
            if (type is NodeTest)
            {
                Expression baseExpr = BaseExpression;
                if (baseExpr is AxisExpression && ((AxisExpression)baseExpr).Axis == AxisInfo.CHILD && basePattern is NodeTestPattern)
                {
                    return new SimplePositionalPattern((NodeTest)type, 1);
                }
                else
                {
                    return new GeneralNodePattern(this, (NodeTest)type);
                }
            }
            else
            {
                // For a non-node pattern the predicate [1] is always true.
                return basePattern;
            }
        }

        public override IItem EvaluateItem(IXPathContext context)
        {
            ISequenceIterator iter = BaseExpression.Iterate(context);
            IItem result = iter.Next();
            iter.Dispose();
            return result;
        }

        // The port executes via Elaborators; without an item-elaborator the inherited UnaryExpression
        // pull path streams the operand straight through, so E[1] returned the whole sequence. An
        // ItemElaborator makes both the item and (via SingletonIterator) the pull path deliver just the
        // first item.
        public override Elaborator GetElaborator()
        {
            return new FirstItemElaborator();
        }

        internal sealed class FirstItemElaborator : ItemElaborator
        {
            public override IItemEvaluator ElaborateForItem()
            {
                FirstItemExpression exp = (FirstItemExpression)GetExpression();
                IPullEvaluator baseEval = exp.BaseExpression.MakeElaborator().ElaborateForPull();
                return (context) =>
                {
                    ISequenceIterator iter = baseEval.Iterate(context);
                    IItem result = iter.Next();
                    iter.Dispose();
                    return result;
                };
            }
        }
    }
}
