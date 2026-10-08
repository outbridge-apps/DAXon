////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Lib;
namespace OutSmart.DAXon.Expressions
{
    internal abstract class CompareToConstant : UnaryExpression, IComparisonExpression
    {
        protected int @operator;

        public Operand Lhs => GetOperand();
        public Operand Rhs => new Operand(this, GetRhsExpression(), OperandRole.SINGLE_ATOMIC);

        public virtual int ComparisonOperator => @operator;

        public override int ImplementationMethod => EVALUATE_METHOD;

        public int SingletonOperator => @operator;
        public abstract IStringCollator StringCollator { get; }
        public CompareToConstant(Expression p0) : base(p0)
        {
        }

        protected override OperandRole GetOperandRole()
        {
            return OperandRole.SINGLE_ATOMIC;
        }

        public Expression GetLhsExpression()
        {
            return BaseExpression;
        }

        public abstract Expression GetRhsExpression();

        protected override int ComputeSpecialProperties()
        {
            return StaticProperty.NO_NODES_NEWLY_CREATED;
        }

        public override IItem EvaluateItem(IXPathContext context)
        {
            return BooleanValue.Get(EffectiveBooleanValue(context));
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            GetOperand().Optimize(visitor, contextInfo);
            if (GetLhsExpression() is Literal)
            {
                return Literal.MakeLiteral(BooleanValue.Get(EffectiveBooleanValue(null)), this);
            }

            return this;
        }

        public override ItemType GetItemType()
        {
            return BuiltInAtomicType.BOOLEAN;
        }

        public bool ConvertsUntypedToOther()
        {
            return true;
        }

        public static bool InterpretComparisonResult(int @operator, int c)
        {
            switch (@operator)
            {
                case Token.FEQ:
                    return c == 0;
                case Token.FNE:
                    return c != 0;
                case Token.FGT:
                    return c > 0;
                case Token.FLT:
                    return c < 0;
                case Token.FGE:
                    return c >= 0;
                case Token.FLE:
                    return c <= 0;
                default:
                    throw new NotSupportedException("Unknown operator " + @operator);
            }
        }
        public abstract IAtomicComparer GetAtomicComparer();
    }
}

