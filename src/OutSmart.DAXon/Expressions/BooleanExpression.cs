////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System.Collections.Generic;
using OutSmart.DAXon.Expressions.Parsing;
namespace OutSmart.DAXon.Expressions
{
    /// <summary>
    /// Boolean expression: two truth values combined using AND or OR.
    /// </summary>
    public abstract class BooleanExpression : BinaryExpression, INegatable
    {

        public override string ExpressionName => Token.tokens[Operator] + "-expression";
        public BooleanExpression(Expression p1, int @operator, Expression p2) : base(p1, @operator, p2)
        {
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            Lhs.TypeCheck(visitor, contextInfo);
            Rhs.TypeCheck(visitor, contextInfo);
            TypeHierarchy th = visitor.GetConfiguration().GetTypeHierarchy();
            XPathException err0 = TypeChecker.EbvError(GetLhsExpression(), th);
            if (err0 != null)
            {
                err0.SetLocator(GetLocation());
                throw err0;
            }

            XPathException err1 = TypeChecker.EbvError(GetRhsExpression(), th);
            if (err1 != null)
            {
                err1.SetLocator(GetLocation());
                throw err1;
            }


            // Precompute the EBV of any constant operand
            if (GetLhsExpression() is Literal && !(((Literal)GetLhsExpression()).GroundedValue is BooleanValue))
            {
                SetLhsExpression(Literal.MakeLiteral(BooleanValue.Get(((Literal)GetLhsExpression()).GroundedValue.EffectiveBooleanValue()), this));
            }

            if (GetRhsExpression() is Literal && !(((Literal)GetRhsExpression()).GroundedValue is BooleanValue))
            {
                SetRhsExpression(Literal.MakeLiteral(BooleanValue.Get(((Literal)GetRhsExpression()).GroundedValue.EffectiveBooleanValue()), this));
            }

            return PreEvaluate();
        }

        protected override int ComputeCardinality()
        {
            return StaticProperty.EXACTLY_ONE;
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {
            OptimizeChildren(visitor, contextItemType);
            bool forStreaming = visitor.IsOptimizeForStreaming();
            SetLhsExpression(ExpressionTool.UnsortedIfHomogeneous(GetLhsExpression(), forStreaming));
            SetRhsExpression(ExpressionTool.UnsortedIfHomogeneous(GetRhsExpression(), forStreaming));
            Expression op0 = BooleanFn.RewriteEffectiveBooleanValue(GetLhsExpression(), visitor, contextItemType);
            if (op0 != null)
            {
                SetLhsExpression(op0);
            }

            Expression op1 = BooleanFn.RewriteEffectiveBooleanValue(GetRhsExpression(), visitor, contextItemType);
            if (op1 != null)
            {
                SetRhsExpression(op1);
            }

            return PreEvaluate();
        }

        protected abstract Expression PreEvaluate();
        protected virtual Expression ForceToBoolean(Expression @in)
        {
            if (@in.GetItemType() == BuiltInAtomicType.BOOLEAN && @in.GetCardinality() == StaticProperty.ALLOWS_ONE)
            {
                return @in;
            }
            else
            {
                return SystemFunction.MakeCall("boolean", GetRetainedStaticContext(), @in);
            }
        }

        public bool IsNegatable(TypeHierarchy th)
        {
            return true;
        }

        public abstract Expression Negate();
        /// <summary>
        /// Evaluate the expression
        /// </summary>
        public override IItem EvaluateItem(IXPathContext context)
        {
            return BooleanValue.Get(EffectiveBooleanValue(context));
        }

        public abstract override bool EffectiveBooleanValue(IXPathContext c);
        public override ItemType GetItemType()
        {
            return BuiltInAtomicType.BOOLEAN;
        }

        public override UType GetStaticUType(UType contextItemType)
        {
            return UType.BOOLEAN;
        }

        public static void ListAndComponents(Expression exp, IList<Expression> list)
        {
            ExpressionTool.ProbeTreeDepth();
            if (exp is BooleanExpression && ((BooleanExpression)exp).Operator == Token.AND)
            {
                foreach (Operand o in exp.Operands())
                {
                    ListAndComponents(o.GetChildExpression(), list);
                }
            }
            else
            {
                list.Add(exp);
            }
        }

        protected override OperandRole GetOperandRole(int arg)
        {
            return OperandRole.INSPECT;
        }
    }
}
