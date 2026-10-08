////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Ported from upstream net/sf/saxon/expr/NegateExpression.java (replaces the Phase 4.8c throwing stub).

using OutSmart.DAXon.Core;
using System;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;

namespace OutSmart.DAXon.Expressions
{
    /// <summary>
    /// Negate Expression: implements the unary minus operator. Created during type-checking of an
    /// ArithmeticExpression, so operand conversion has already been arranged there.
    /// </summary>
    internal sealed class NegateExpression : UnaryExpression
    {
        private bool backwardsCompatible;

        public override int ImplementationMethod => EVALUATE_METHOD;

        public override string ExpressionName => "minus";

        public NegateExpression(Expression @base) : base(@base)
        {
        }

        public void SetBackwardsCompatible(bool compatible)
        {
            backwardsCompatible = compatible;
        }

        public bool IsBackwardsCompatible()
        {
            return backwardsCompatible;
        }

        protected override OperandRole GetOperandRole()
        {
            return OperandRole.SINGLE_ATOMIC;
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            GetOperand().TypeCheck(visitor, contextInfo);
            Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.UNARY_EXPR, "-", 0);
            Expression operand = visitor.GetConfiguration().GetTypeChecker(backwardsCompatible).StaticTypeCheck(
                BaseExpression,
                SequenceType.MakeSequenceType(NumericType.GetInstance(), StaticProperty.ALLOWS_ZERO_OR_ONE),
                role, visitor);
            BaseExpression = operand;
            if (operand is Literal && ((Literal)operand).GroundedValue is NumericValue nv)
            {
                return Literal.MakeLiteral(nv.Negate(), this);
            }

            return this;
        }

        public override ItemType GetItemType()
        {
            return BaseExpression.GetItemType().GetPrimitiveItemType();
        }

        /// <summary>
        /// Evaluate the expression to yield a single item (the negated numeric value). Delegates to the
        /// elaborator, matching upstream NegateExpression.evaluateItem — required so that direct
        /// evaluateItem/iterate calls (e.g. early-evaluation of a literal operand during type-checking)
        /// do not fall through to the base Iterate/EvaluateItem mutual-recursion.
        /// </summary>
        public override IItem EvaluateItem(IXPathContext context)
        {
            return MakeElaborator().ElaborateForItem().Eval(context);
        }

        protected override int ComputeCardinality()
        {
            return BaseExpression.GetCardinality() & ~StaticProperty.ALLOWS_MANY;
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            NegateExpression exp = new NegateExpression(BaseExpression.Copy(rebindings));
            ExpressionTool.CopyLocationInfo(this, exp);
            return exp;
        }

        protected override string DisplayOperator(Configuration config)
        {
            return "-";
        }

        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("minus", this);
            if (backwardsCompatible)
            {
                @out.EmitAttribute("vn", "1");
            }

            BaseExpression.Export(@out);
            @out.EndElement();
        }

        public override Elaborator GetElaborator()
        {
            return new NegateElaborator();
        }

        /// <summary>Elaborator for a negate expression (unary minus).</summary>
        internal sealed class NegateElaborator : ItemElaborator
        {
            public override IItemEvaluator ElaborateForItem()
            {
                NegateExpression exp = (NegateExpression)GetExpression();
                IItemEvaluator argEval = exp.BaseExpression.MakeElaborator().ElaborateForItem();
                bool maybeEmpty = Cardinality.AllowsZero(exp.BaseExpression.GetCardinality());
                bool backwardsCompatible = exp.IsBackwardsCompatible();
                if (maybeEmpty)
                {
                    if (backwardsCompatible)
                    {
                        return (context) =>
                        {
                            NumericValue v1 = (NumericValue)argEval.Eval(context);
                            return v1 == null ? DoubleValue.NaN : v1.Negate();
                        };
                    }
                    else
                    {
                        return (context) =>
                        {
                            NumericValue v1 = (NumericValue)argEval.Eval(context);
                            return v1 == null ? null : (IItem)v1.Negate();
                        };
                    }
                }
                else
                {
                    return (context) => ((NumericValue)argEval.Eval(context)).Negate();
                }
            }
        }
    }
}
