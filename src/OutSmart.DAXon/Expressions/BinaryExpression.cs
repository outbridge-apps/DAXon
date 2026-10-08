////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Expressions.Parsing;
namespace OutSmart.DAXon.Expressions
{
    public abstract class BinaryExpression : Expression
    {
        private readonly Operand lhs;
        private readonly Operand rhs;
        // internal, not public: Operator below is the CLS-visible spelling, and a public pair
        // differing only by case makes the whole type unusable from a case-insensitive binder.
        internal int @operator; // represented by the token number from class Tokenizer

        public virtual Operand Lhs => lhs;

        public virtual Operand Rhs => rhs;

        public virtual int Operator => @operator;

        public override int ImplementationMethod => EVALUATE_METHOD | ITERATE_METHOD;
        public BinaryExpression(Expression p0, int op, Expression p1)
        {
            @operator = op;

            lhs = new Operand(this, p0, GetOperandRole(0));
            rhs = new Operand(this, p1, GetOperandRole(1));
            AdoptChildExpression(p0);
            AdoptChildExpression(p1);
        }

        public override IEnumerable<Operand> Operands()
        {
            return OperandList(lhs, rhs);
        }

        protected virtual OperandRole GetOperandRole(int arg)
        {
            return OperandRole.SINGLE_ATOMIC;
        }

        public Expression GetLhsExpression()
        {
            return lhs.GetChildExpression();
        }

        public virtual void SetLhsExpression(Expression child)
        {
            lhs.SetChildExpression(child);
        }

        public Expression GetRhsExpression()
        {
            return rhs.GetChildExpression();
        }

        public virtual void SetRhsExpression(Expression child)
        {
            rhs.SetChildExpression(child);
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            ResetLocalStaticProperties();
            lhs.TypeCheck(visitor, contextInfo);
            rhs.TypeCheck(visitor, contextInfo);

            // if both operands are known, pre-evaluate the expression
            try
            {
                if ((GetLhsExpression() is Literal) && (GetRhsExpression() is Literal))
                {
                    IGroundedValue v = EvaluateItem(visitor.StaticContext.MakeEarlyEvaluationContext()).Materialize();
                    return Literal.MakeLiteral(v, this);
                }
            }
            catch (XPathException err)
            {
            }

            return this;
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {
            lhs.Optimize(visitor, contextItemType);
            rhs.Optimize(visitor, contextItemType);

            // if both operands are known, pre-evaluate the expression
            try
            {
                Optimizer opt = visitor.ObtainOptimizer();
                if (opt.IsOptionSet(OptimizerOptions.CONSTANT_FOLDING) && (GetLhsExpression() is Literal) && (GetRhsExpression() is Literal))
                {
                    IItem item = EvaluateItem(visitor.StaticContext.MakeEarlyEvaluationContext());
                    if (item != null)
                    {
                        IGroundedValue v = item.Materialize();
                        return Literal.MakeLiteral(v, this);
                    }
                }
            }
            catch (XPathException err)
            {
            }

            return this;
        }

        public override void SetFlattened(bool flattened)
        {
            if (GetOperandRole(0).Usage == OperandUsage.ABSORPTION)
            {
                GetLhsExpression().SetFlattened(flattened);
            }

            if (GetOperandRole(1).Usage == OperandUsage.ABSORPTION)
            {
                GetRhsExpression().SetFlattened(flattened);
            }
        }

        protected override int ComputeCardinality()
        {
            Expression lhs = GetLhsExpression();
            Expression rhs = GetRhsExpression();
            if (!Cardinality.AllowsZero(lhs.GetCardinality()) && lhs.GetItemType() is IAtomicType && !Cardinality.AllowsZero(rhs.GetCardinality()) && rhs.GetItemType() is IAtomicType)
            {
                return StaticProperty.EXACTLY_ONE;
            }
            else
            {
                return StaticProperty.ALLOWS_ZERO_OR_ONE;
            }
        }

        protected override int ComputeSpecialProperties()
        {
            int p = base.ComputeSpecialProperties();
            return p | StaticProperty.NO_NODES_NEWLY_CREATED;
        }

        protected static bool IsCommutative(int @operator)
        {
            return @operator == Token.AND || @operator == Token.OR || @operator == Token.UNION || @operator == Token.INTERSECT || @operator == Token.PLUS || @operator == Token.MULT || @operator == Token.EQUALS || @operator == Token.FEQ || @operator == Token.NE || @operator == Token.FNE;
        }

        protected static bool IsAssociative(int @operator)
        {
            return @operator == Token.AND || @operator == Token.OR || @operator == Token.UNION || @operator == Token.INTERSECT || @operator == Token.PLUS || @operator == Token.MULT;
        }

        protected static bool IsInverse(int op1, int op2)
        {
            return op1 != op2 && op1 == Token.Inverse(op2);
        }

        public override bool Equals(object other)
        {
            if (other is BinaryExpression && HasCompatibleStaticContext((Expression)other))
            {
                BinaryExpression b = (BinaryExpression)other;
                Expression lhs1 = GetLhsExpression();
                Expression rhs1 = GetRhsExpression();
                Expression lhs2 = b.GetLhsExpression();
                Expression rhs2 = b.GetRhsExpression();
                if (@operator == b.@operator)
                {
                    if (lhs1.IsEqual(lhs2) && rhs1.IsEqual(rhs2))
                    {
                        return true;
                    }

                    if (IsCommutative(@operator) && lhs1.IsEqual(rhs2) && rhs1.IsEqual(lhs2))
                    {
                        return true;
                    }

                    if (IsAssociative(@operator) && PairwiseEqual(FlattenExpression(), b.FlattenExpression()))
                    {
                        return true;
                    }
                }

                return IsInverse(@operator, b.@operator) && lhs1.IsEqual(rhs2) && rhs1.IsEqual(lhs2);
            }

            return false;
        }

        private IList<Expression> FlattenExpression()
        {
            IList<Expression> list = new List<Expression>();
            return FlattenExpression(list);
        }

        private IList<Expression> FlattenExpression(IList<Expression> list)
        {
            if (GetLhsExpression() is BinaryExpression && ((BinaryExpression)GetLhsExpression()).@operator == @operator)
            {
                ((BinaryExpression)GetLhsExpression()).FlattenExpression(list);
            }
            else
            {
                int h = GetLhsExpression().GetHashCode();
                list.Add(GetLhsExpression());
                int i = list.Count - 1;
                while (i > 0 && h > list[i - 1].GetHashCode())
                {
                    list[i] = list[i - 1];
                    list[i - 1] = GetLhsExpression();
                    i--;
                }
            }

            if (GetRhsExpression() is BinaryExpression && ((BinaryExpression)GetRhsExpression()).@operator == @operator)
            {
                ((BinaryExpression)GetRhsExpression()).FlattenExpression(list);
            }
            else
            {
                int h = GetRhsExpression().GetHashCode();
                list.Add(GetRhsExpression());
                int i = list.Count - 1;
                while (i > 0 && h > list[i - 1].GetHashCode())
                {
                    list[i] = list[i - 1];
                    list[i - 1] = GetRhsExpression();
                    i--;
                }
            }

            return list;
        }

        private bool PairwiseEqual<T>(IList<T> a, IList<T> b)
        {
            if (a.Count != b.Count)
            {
                return false;
            }

            for (int i = 0; i < a.Count; i++)
            {
                if (!a[i].Equals(b[i]))
                {
                    return false;
                }
            }

            return true;
        }

        protected override int ComputeHashCode()
        {

            // Ensure that an operator and its inverse get the same hash code,
            // so that (A lt B) has the same hash code as (B gt A)
            int op = Math.Min(@operator, Token.Inverse(@operator));
            return ("BinaryExpression " + op).GetHashCode() ^ GetLhsExpression().GetHashCode() ^ GetRhsExpression().GetHashCode();
        }

        public override string ToString()
        {
            return ExpressionTool.Parenthesize(GetLhsExpression()) + " " + DisplayOperator() + " " + ExpressionTool.Parenthesize(GetRhsExpression());
        }

        public override string ToShortString()
        {
            return Parenthesize(GetLhsExpression()) + " " + DisplayOperator() + " " + Parenthesize(GetRhsExpression());
        }

        private string Parenthesize(Expression operand)
        {
            string operandStr = operand.ToShortString();
            if (operand is BinaryExpression && XPathParser.OperatorPrecedence(((BinaryExpression)operand).@operator) < XPathParser.OperatorPrecedence(@operator))
            {
                operandStr = "(" + operandStr + ")";
            }

            return operandStr;
        }

        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement(Tag(), this);
            @out.EmitAttribute("op", DisplayOperator());
            ExplainExtraAttributes(@out);
            GetLhsExpression().Export(@out);
            GetRhsExpression().Export(@out);
            @out.EndElement();
        }

        protected virtual string Tag()
        {
            return "operator";
        }

        protected virtual void ExplainExtraAttributes(ExpressionPresenter @out)
        {
        }

        protected virtual string DisplayOperator()
        {
            return Token.tokens[@operator];
        }
    }
}