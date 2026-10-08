////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Operators;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System.Collections.Generic;
namespace OutSmart.DAXon.Expressions.Flwor
{
    internal sealed class TupleExpression : Expression
    {
        private OperandArray operanda;

        protected OperandArray Operanda
        {
            get => operanda; set
            {
                this.operanda = value;
            }
        }

        public int Size => Operanda.NumberOfOperands;

        public override int ImplementationMethod => EVALUATE_METHOD;

        public override string ExpressionName => "tuple";

        public override int IntrinsicDependencies => 0;
        public TupleExpression()
        {
        }

        public override IEnumerable<Operand> Operands()
        {
            return operanda;
        }

        public void SetVariables(IList<LocalVariableReference> refs)
        {
            Expression[] e = new Expression[refs.Count];
            for (int i = 0; i < refs.Count; i++)
            {
                e[i] = refs[i];
            }

            Operanda = new OperandArray(this, e, OperandRole.SAME_FOCUS_ACTION);
        }

        public LocalVariableReference GetSlot(int i)
        {
            return (LocalVariableReference)Operanda.GetOperandExpression(i);
        }

        public bool IncludesBinding(IBinding binding)
        {
            foreach (Operand o in Operands())
            {
                if (((LocalVariableReference)o.GetChildExpression()).GetBinding() == binding)
                {
                    return true;
                }
            }

            return false;
        }

        public override ItemType GetItemType()
        {
            return JavaExternalObjectType.Of(typeof(Tuple));
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            for (int i = 0; i < Size; i++)
            {
                operanda.GetOperand(i).TypeCheck(visitor, contextInfo);
            }

            return this;
        }

        public override bool Equals(object other)
        {
            if (!(other is TupleExpression))
            {
                return false;
            }
            else
            {
                TupleExpression t2 = (TupleExpression)other;
                if (Operanda.NumberOfOperands != t2.Operanda.NumberOfOperands)
                {
                    return false;
                }

                for (int i = 0; i < Size; i++)
                {
                    if (!GetSlot(i).IsEqual(t2.GetSlot(i)))
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        protected override int ComputeHashCode()
        {
            int h = 77;
            foreach (Operand o in Operands())
            {
                h ^= o.GetChildExpression().GetHashCode();
            }

            return h;
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            int n = Operanda.NumberOfOperands;
            IList<LocalVariableReference> refs2 = new List<LocalVariableReference>(n);
            for (int i = 0; i < n; i++)
            {
                refs2.Add((LocalVariableReference)GetSlot(i).Copy(rebindings));
            }

            TupleExpression t2 = new TupleExpression();
            ExpressionTool.CopyLocationInfo(this, t2);
            t2.SetVariables(refs2);
            return t2;
        }

        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("tuple", this);
            foreach (Operand o in Operands())
            {
                o.GetChildExpression().Export(@out);
            }

            @out.EndElement();
        }

        public override IItem EvaluateItem(IXPathContext context)
        {
            int n = Size;
            ISequence[] tuple = new ISequence[n];
            for (int i = 0; i < n; i++)
            {
                IGroundedValue v = GetSlot(i).EvaluateVariable(context).Materialize();
                if (v is StringValue)
                {
                    v = ((StringValue)v).Economize();
                }

                tuple[i] = v;
            }

            return new Tuple(tuple);
        }

        public void SetCurrentTuple(IXPathContext context, Tuple tuple)
        {
            ISequence[] members = tuple.GetMembers();
            int n = Size;
            for (int i = 0; i < n; i++)
            {
                context.SetLocalVariable(GetSlot(i).GetBinding().LocalSlotNumber, members[i]);
            }
        }

        protected override int ComputeCardinality()
        {
            return StaticProperty.EXACTLY_ONE;
        }
    }
}
