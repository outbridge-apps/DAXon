////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using System;
using System.Collections.Generic;
namespace OutSmart.DAXon.Expressions
{
    /// <summary>
    /// Unary Expression: an expression taking a single operand expression
    /// </summary>
    public abstract class UnaryExpression : Expression
    {
        private readonly Operand operand;

        public virtual Expression BaseExpression
        {
            get => operand.GetChildExpression(); set
            {
                operand.SetChildExpression(value);
            }
        }
        public UnaryExpression(Expression p0)
        {
            operand = new Operand(this, p0, GetOperandRole());

            //        }
            ExpressionTool.CopyLocationInfo(p0, this);
        }

        public virtual Operand GetOperand()
        {
            return operand;
        }

        public override IEnumerable<Operand> Operands()
        {
            return operand;
        }

        protected abstract OperandRole GetOperandRole();
        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            operand.TypeCheck(visitor, contextInfo);

            // if the operand value is known, pre-evaluate the expression
            try
            {
                if (BaseExpression is Literal)
                {
                    Expression e2 = Literal.MakeLiteral(SequenceTool.ToGroundedValue(Iterate(visitor.StaticContext.MakeEarlyEvaluationContext())), this);
                    ExpressionTool.CopyLocationInfo(this, e2);
                    return e2;
                } //return (Value)ExpressionTool.eagerEvaluate(this, env.makeEarlyEvaluationContext());
            }
            catch (Exception err)
            {
            }

            return this;
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            operand.Optimize(visitor, contextInfo);

            // if the operand value is known, pre-evaluate the expression
            Expression @base = BaseExpression;
            try
            {
                if (@base is Literal)
                {
                    return Literal.MakeLiteral(SequenceTool.ToGroundedValue(Iterate(visitor.StaticContext.MakeEarlyEvaluationContext())), this);
                }
            }
            catch (XPathException err)
            {
            }
            catch (UncheckedXPathException err)
            {
            }

            return this;
        }

        protected override int ComputeSpecialProperties()
        {
            return BaseExpression.GetSpecialProperties();
        }

        /// <summary>
        /// Determine the static cardinality. Default implementation returns the cardinality of the operand
        /// </summary>
        protected override int ComputeCardinality()
        {
            return BaseExpression.GetCardinality();
        }

        /// <summary>
        /// Determine the static cardinality. Default implementation returns the cardinality of the operand
        /// </summary>
        public override ItemType GetItemType()
        {
            return BaseExpression.GetItemType();
        }

        public override bool Equals(object other)
        {
            return other != null && this.GetType().Equals(other.GetType()) && this.BaseExpression.IsEqual(((UnaryExpression)other).BaseExpression);
        }

        protected override int ComputeHashCode()
        {
            return ("UnaryExpression " + GetType()).GetHashCode() ^ BaseExpression.GetHashCode();
        }

        public override string ToString()
        {
            return ExpressionName + "(" + BaseExpression + ")";
        }

        public override string ToShortString()
        {
            return ExpressionName + "(" + BaseExpression.ToShortString() + ")";
        }

        public override void Export(ExpressionPresenter @out)
        {
            string name = ExpressionName;
            if (name == null)
            {
                @out.StartElement("unaryOperator", this);
                string op = DisplayOperator(@out.GetConfiguration());
                if (op != null)
                {
                    @out.EmitAttribute("op", op);
                }
            }
            else
            {
                @out.StartElement(name, this);
            }

            EmitExtraAttributes(@out);
            BaseExpression.Export(@out);
            @out.EndElement();
        }

        protected virtual void EmitExtraAttributes(ExpressionPresenter @out)
        {
        }

        // default: no action
        protected virtual string DisplayOperator(Configuration config)
        {
            return null;
        }
    }
}
