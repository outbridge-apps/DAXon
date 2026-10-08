////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Expressions.Parsing;
namespace OutSmart.DAXon.Expressions
{
    public class OrExpression : BooleanExpression
    {

        public override double Cost => GetLhsExpression().Cost + GetRhsExpression().Cost / 2;
        public OrExpression(Expression p1, Expression p2) : base(p1, Token.OR, p2)
        {
        }

        protected override Expression PreEvaluate()
        {
            if (Literal.HasEffectiveBooleanValue(GetLhsExpression(), true) || Literal.HasEffectiveBooleanValue(GetRhsExpression(), true))
            {

                // A or true() => true()
                // true() or B => true()
                return Literal.MakeLiteral(BooleanValue.TRUE, this);
            }
            else if (Literal.HasEffectiveBooleanValue(GetLhsExpression(), false))
            {

                // false() or B => B
                return ForceToBoolean(GetRhsExpression());
            }
            else if (Literal.HasEffectiveBooleanValue(GetRhsExpression(), false))
            {

                // A or false() => A
                return ForceToBoolean(GetLhsExpression());
            }
            else
            {
                return this;
            }
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {
            Expression e = base.Optimize(visitor, contextItemType);
            if (e != this)
            {
                return e;
            }


            // If this is a top-level or-expression then try to replace multiple branches with a general comparison
            if (!(ParentExpression is OrExpression))
            {
                Expression e2 = visitor.ObtainOptimizer().TryGeneralComparison(visitor, contextItemType, this);
                if (e2 != null && e2 != this)
                {
                    return e2;
                }
            }

            return this;
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            OrExpression exp = new OrExpression(GetLhsExpression().Copy(rebindings), GetRhsExpression().Copy(rebindings));
            ExpressionTool.CopyLocationInfo(this, exp);
            return exp;
        }

        public override Expression Negate()
        {

            // Apply de Morgan's laws
            // not(A or B) => not(A) and not(B)
            Expression not0 = SystemFunction.MakeCall("not", GetRetainedStaticContext(), GetLhsExpression());
            Expression not1 = SystemFunction.MakeCall("not", GetRetainedStaticContext(), GetRhsExpression());
            AndExpression result = new AndExpression(not0, not1);
            ExpressionTool.CopyLocationInfo(this, result);
            return result;
        }

        protected override string Tag()
        {
            return "or";
        }

        /// <summary>
        /// Evaluate as a boolean.
        /// </summary>
        public override bool EffectiveBooleanValue(IXPathContext c)
        {
            return GetLhsExpression().EffectiveBooleanValue(c) || GetRhsExpression().EffectiveBooleanValue(c);
        }

        /// <summary>
        /// Evaluate as a boolean.
        /// </summary>
        public override Elaborator GetElaborator()
        {
            return new OrElaborator();
        }

        /// <summary>
        /// Elaborator for an "or" expression ({@code A or B})
        /// </summary>
        internal sealed class OrElaborator : BooleanElaborator
        {
            public override IBooleanEvaluator ElaborateForBoolean()
            {
                OrExpression expr = (OrExpression)GetExpression();
                IBooleanEvaluator eval0 = expr.GetLhsExpression().MakeElaborator().ElaborateForBoolean();
                IBooleanEvaluator eval1 = expr.GetRhsExpression().MakeElaborator().ElaborateForBoolean();

                // Don't throw an error if either branch returns true.
                // See bug 5721. Conforms with the 4.0 rules for guarded expressions, even if the
                // operands are reordered
                return (context) =>
                {
                    XPathException saved = null;
                    try
                    {
                        bool b0 = eval0.Eval(context);
                        if (b0)
                        {
                            return true;
                        }
                    }
                    catch (UncheckedXPathException err)
                    {
                        saved = err.GetXPathException();
                    }
                    catch (XPathException err)
                    {
                        saved = err;
                    }

                    bool b1 = eval1.Eval(context);
                    if (b1)
                    {
                        return true;
                    }

                    if (saved != null)
                    {
                        throw saved;
                    }

                    return false;
                };
            }
        }
    }
}
