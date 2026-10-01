////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Expressions
{
    internal sealed class AndExpression : BooleanExpression
    {

        public override double Cost => GetLhsExpression().Cost + GetRhsExpression().Cost / 2;
        public AndExpression(Expression p1, Expression p2) : base(p1, Token.AND, p2)
        {
        }

        protected override Expression PreEvaluate()
        {

            // If the value can be determined from knowledge of one operand, precompute the result
            if (Literal.IsConstantBoolean(GetLhsExpression(), false) || Literal.IsConstantBoolean(GetRhsExpression(), false))
            {

                // A and false() => false()
                // false() and B => false()
                return Literal.MakeLiteral(BooleanValue.FALSE, this);
            }
            else if (Literal.HasEffectiveBooleanValue(GetLhsExpression(), true))
            {

                // true() and B => B
                return ForceToBoolean(GetRhsExpression());
            }
            else if (Literal.HasEffectiveBooleanValue(GetRhsExpression(), true))
            {

                // A and true() => A
                return ForceToBoolean(GetLhsExpression());
            }
            else
            {
                return this;
            }
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            Expression t = base.Optimize(visitor, contextInfo);
            if (t != this)
            {
                return t;
            }


            // Rewrite (A and B) as (if (A) then B else false()). The benefit of this is that when B is a recursive
            // function call, it is treated as a tail call (test qxmp290). To avoid disrupting other optimizations
            // of "and" expressions (specifically, where clauses in FLWOR expressions), do this ONLY if B is a user
            // function call (we can't tell if it's recursive), and it's not in a loop.
            TypeHierarchy th = visitor.GetConfiguration().GetTypeHierarchy();
            if (GetRhsExpression() is UserFunctionCall && th.IsSubType(GetRhsExpression().GetItemType(), BuiltInAtomicType.BOOLEAN) && !ExpressionTool.IsLoopingSubexpression(this, null))
            {
                Expression cond = Choose.MakeConditional(GetLhsExpression(), GetRhsExpression(), Literal.MakeLiteral(BooleanValue.FALSE, this));
                ExpressionTool.CopyLocationInfo(this, cond);
                return cond;
            }

            return this;
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            AndExpression a2 = new AndExpression(GetLhsExpression().Copy(rebindings), GetRhsExpression().Copy(rebindings));
            ExpressionTool.CopyLocationInfo(this, a2);
            return a2;
        }

        public override Expression Negate()
        {

            // Apply de Morgan's laws
            // not(A and B) ==> not(A) or not(B)
            Expression not0 = SystemFunction.MakeCall("not", GetRetainedStaticContext(), GetLhsExpression());
            Expression not1 = SystemFunction.MakeCall("not", GetRetainedStaticContext(), GetRhsExpression());
            return new OrExpression(not0, not1);
        }

        protected override string Tag()
        {
            return "and";
        }

        public override bool EffectiveBooleanValue(IXPathContext c)
        {
            return GetLhsExpression().EffectiveBooleanValue(c) && GetRhsExpression().EffectiveBooleanValue(c);
        }

        public override Elaborator GetElaborator()
        {
            return new AndElaborator();
        }

        /// <summary>
        /// Elaborator for an AndExpression (P and Q)
        /// </summary>
        internal sealed class AndElaborator : BooleanElaborator
        {
            public override IBooleanEvaluator ElaborateForBoolean()
            {
                AndExpression expr = (AndExpression)GetExpression();
                IBooleanEvaluator eval0 = expr.GetLhsExpression().MakeElaborator().ElaborateForBoolean();
                IBooleanEvaluator eval1 = expr.GetRhsExpression().MakeElaborator().ElaborateForBoolean();

                // Don't throw an error if either branch returns false.
                // See bug 5721. This allows reordering of predicates without generating
                // spurious errors.
                return (context) =>
                {
                    XPathException saved = null;
                    try
                    {
                        bool b0 = eval0.Eval(context);
                        if (!b0)
                        {
                            return false;
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
                    if (!b1)
                    {
                        return false;
                    }

                    if (saved != null)
                    {
                        throw saved;
                    }

                    return true;
                };
            }
        }
    }
}
