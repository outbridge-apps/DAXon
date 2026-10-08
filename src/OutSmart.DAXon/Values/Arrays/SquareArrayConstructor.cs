////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Operators;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using System.Collections.Generic;
using System.Linq;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Types;
namespace OutSmart.DAXon.Values.Arrays
{
    internal sealed class SquareArrayConstructor : Expression, IPingable
    {
        private OperandArray operanda;
        private double numberOfCalls;
        private double numberOfConversions;

        public override string ExpressionName => "SquareArrayConstructor";

        public override string StreamerName => "ArrayBlock";

        public override int ImplementationMethod => EVALUATE_METHOD;
        public SquareArrayConstructor(IList<Expression> children)
        {
            Expression[] kids = children.ToArray();
            foreach (Expression e in children)
            {
                AdoptChildExpression(e);
            }

            SetOperanda(new OperandArray(this, kids, OperandRole.NAVIGATE));
        }

        protected void SetOperanda(OperandArray operanda)
        {
            this.operanda = operanda;
        }

        public OperandArray GetOperanda()
        {
            return operanda;
        }

        public override IEnumerable<Operand> Operands()
        {
            return operanda;
        }

        protected override int ComputeSpecialProperties()
        {
            return 0;
        }

        public override bool Equals(object other)
        {
            if (!(other is SquareArrayConstructor))
            {
                return false;
            }
            else
            {
                SquareArrayConstructor ab2 = (SquareArrayConstructor)other;
                if (ab2.GetOperanda().NumberOfOperands != GetOperanda().NumberOfOperands)
                {
                    return false;
                }

                for (int i = 0; i < GetOperanda().NumberOfOperands; i++)
                {
                    if (!GetOperanda().GetOperand(i).Equals(ab2.GetOperanda().GetOperand(i)))
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        protected override int ComputeHashCode()
        {
            int h = 0x778b92a0;
            foreach (Operand o in Operands())
            {
                h ^= o.GetChildExpression().GetHashCode();
            }

            return h;
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            Expression e = base.TypeCheck(visitor, contextInfo);
            if (e != this)
            {
                return e;
            }

            return PreEvaluate(visitor);
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            Expression e = base.Optimize(visitor, contextInfo);
            if (e != this)
            {
                return e;
            }

            return PreEvaluate(visitor);
        }

        private Expression PreEvaluate(ExpressionVisitor visitor)
        {
            foreach (Operand o in Operands())
            {
                if (!(o.GetChildExpression() is Literal))
                {
                    return this;
                }
            }

            try
            {
                return Literal.MakeLiteral(EvaluateItem(visitor.MakeDynamicContext()), this);
            }
            catch (XPathException)
            {
                return this;
            }
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            IList<Expression> m2 = new List<Expression>(GetOperanda().NumberOfOperands);
            foreach (Operand o in Operands())
            {
                m2.Add(o.GetChildExpression().Copy(rebindings));
            }

            SquareArrayConstructor b2 = new SquareArrayConstructor(m2);
            ExpressionTool.CopyLocationInfo(this, b2);
            return b2;
        }

        public override ItemType GetItemType()
        {
            ItemType contentType = null;
            int contentCardinality = StaticProperty.EXACTLY_ONE;
            TypeHierarchy th = GetConfiguration().GetTypeHierarchy();
            foreach (Expression e in GetOperanda().OperandExpressions())
            {
                if (contentType == null)
                {
                    contentType = e.GetItemType();
                    contentCardinality = e.GetCardinality();
                }
                else
                {
                    contentType = Types.Type.GetCommonSuperType(contentType, e.GetItemType(), th);
                    contentCardinality = Cardinality.Union(contentCardinality, e.GetCardinality());
                }
            }

            if (contentType == null)
            {
                contentType = ErrorType.GetInstance();
            }

            return new ArrayItemType(SequenceType.MakeSequenceType(contentType, contentCardinality));
        }

        public override UType GetStaticUType(UType contextItemType)
        {
            return UType.FUNCTION;
        }

        protected override int ComputeCardinality()
        {

            // An array is an item!
            return StaticProperty.EXACTLY_ONE;
        }

        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("arrayBlock", this);
            foreach (Operand o in Operands())
            {
                o.GetChildExpression().Export(@out);
            }

            @out.EndElement();
        }

        public override string ToShortString()
        {
            int n = GetOperanda().NumberOfOperands;
            switch (n)
            {
                case 0:
                    return "[]";
                case 1:
                    return "[" + GetOperanda().GetOperand(0).GetChildExpression().ToShortString() + "]";
                case 2:
                    return "[" + GetOperanda().GetOperand(0).GetChildExpression().ToShortString() + ", " + GetOperanda().GetOperand(1).GetChildExpression().ToShortString() + "]";
                default:
                    return "[" + GetOperanda().GetOperand(0).GetChildExpression().ToShortString() + ", ...]";
            }
        }

        public override string ToString()
        {
            int n = GetOperanda().NumberOfOperands;
            switch (n)
            {
                case 0:
                    return "[]";
                case 1:
                    return "[" + GetOperanda().GetOperand(0).GetChildExpression().ToString() + "]";
                case 2:
                    return "[" + GetOperanda().GetOperand(0).GetChildExpression().ToString() + ", " + GetOperanda().GetOperand(1).GetChildExpression().ToString() + "]";
                default:
                    return "[" + GetOperanda().GetOperand(0).GetChildExpression().ToString() + ", ...]";
            }
        }

        public void Ping()
        {
            numberOfConversions++;
        }

        protected ArrayItem MakeArray(IList<IGroundedValue> members)
        {
            if (numberOfConversions > numberOfCalls * 0.5)
            {

                // More than half the calls result in the array being converted...
                return new ImmutableArrayItem(members);
            }
            else
            {
                numberOfCalls++;
                SimpleArrayItem result = new SimpleArrayItem(members);
                result.RequestNotification(this);
                return result;
            }
        }

        public override IItem EvaluateItem(IXPathContext context)
        {
            IList<IGroundedValue> value = new List<IGroundedValue>(GetOperanda().NumberOfOperands);
            foreach (Operand o in Operands())
            {
                IGroundedValue s = ExpressionTool.EagerEvaluate(o.GetChildExpression(), context);
                value.Add(s);
            }

            return MakeArray(value);
        }

        public override Elaborator GetElaborator()
        {
            return new SquareArrayConstructorElaborator();
        }

        private sealed class SquareArrayConstructorElaborator : ItemElaborator
        {
            public override IItemEvaluator ElaborateForItem()
            {
                SquareArrayConstructor expr = (SquareArrayConstructor)GetExpression();
                IList<ISequenceEvaluator> eagerEvaluators = new List<ISequenceEvaluator>(expr.GetOperanda().NumberOfOperands);
                foreach (Operand o in expr.Operands())
                {
                    eagerEvaluators.Add(o.GetChildExpression().MakeElaborator().Eagerly());
                }

                return (context) =>
                {
                    IList<IGroundedValue> members = new List<IGroundedValue>(eagerEvaluators.Count);
                    foreach (ISequenceEvaluator e in eagerEvaluators)
                    {
                        members.Add(e.Evaluate(context).Materialize());
                    }

                    return expr.MakeArray(members);
                };
            }
        }
    }
}