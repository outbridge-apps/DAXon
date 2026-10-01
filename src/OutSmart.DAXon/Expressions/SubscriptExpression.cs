////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Expressions
{
    internal sealed class SubscriptExpression : SingleItemFilter
    {
        private readonly Operand subscriptOp;

        public Expression Subscript
        {
            get => subscriptOp.GetChildExpression(); set
            {
                subscriptOp.SetChildExpression(value);
            }
        }

        public override int ImplementationMethod => EVALUATE_METHOD;

        public override string StreamerName => "SubscriptExpression";

        public override string ExpressionName => "subscript";
        public SubscriptExpression(Expression @base, Expression subscript) : base(@base)
        {
            subscriptOp = new Operand(this, subscript, OperandRole.SINGLE_ATOMIC);
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            GetOperand().TypeCheck(visitor, contextInfo);
            return this;
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            GetOperand().Optimize(visitor, contextInfo);
            if (Literal.IsConstantOne(Subscript))
            {
                return FirstItemExpression.MakeFirstItemExpression(BaseExpression);
            }

            Expression fused = TokenizeFieldExpression.TryFuse(BaseExpression, Subscript);
            if (fused != null)
            {
                ExpressionTool.CopyLocationInfo(this, fused);
                return fused;
            }

            return this;
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            SubscriptExpression exp = new SubscriptExpression(BaseExpression.Copy(rebindings), Subscript.Copy(rebindings));
            ExpressionTool.CopyLocationInfo(this, exp);
            return exp;
        }

        public override IEnumerable<Operand> Operands()
        {
            return OperandList(GetOperand(), subscriptOp);
        }

        public Expression GetSubscriptExpression()
        {
            return Subscript;
        }

        public override bool Equals(object other)
        {
            return other is SubscriptExpression && BaseExpression.IsEqual(((SubscriptExpression)other).BaseExpression) && Subscript.IsEqual(((SubscriptExpression)other).Subscript);
        }

        protected override int ComputeHashCode()
        {
            return BaseExpression.GetHashCode() ^ Subscript.GetHashCode();
        }

        public override IItem EvaluateItem(IXPathContext context)
        {
            return MakeElaborator().ElaborateForItem().Eval(context);
        }

        public static IItem GetItemAt(ISequenceIterator iter, int index)
        {
            IItem item;
            if (index == 1)
            {
                item = iter.Next();
            }
            else if (iter is MemoSequence.ProgressiveIterator)
            {
                MemoSequence mem = ((MemoSequence.ProgressiveIterator)iter).GetMemoSequence();
                item = mem.ItemAt(index - 1);
            }
            else if (iter is IGroundedIterator && ((IGroundedIterator)iter).IsActuallyGrounded())
            {
                try
                {
                    IGroundedValue value = SequenceTool.ToGroundedValue(iter);
                    item = value.ItemAt(index - 1);
                }
                catch (UncheckedXPathException e)
                {
                    throw e.GetXPathException();
                }
            }
            else
            {
                ISequenceIterator tail = TailIterator.Make(iter, index);
                item = tail.Next();
                tail.Dispose();
            }

            return item;
        }

        public override void Export(ExpressionPresenter destination)
        {
            destination.StartElement("subscript", this);
            BaseExpression.Export(destination);
            Subscript.Export(destination);
            destination.EndElement();
        }

        public override string ToString()
        {
            return ExpressionTool.Parenthesize(BaseExpression) + "[" + Subscript + "]";
        }

        public override string ToShortString()
        {
            return ExpressionTool.Parenthesize(BaseExpression) + "[" + Subscript.ToShortString() + "]";
        }

        public override Elaborator GetElaborator()
        {
            return new SubscriptExprElaborator();
        }

        internal sealed class SubscriptExprElaborator : ItemElaborator
        {
            public override IItemEvaluator ElaborateForItem()
            {
                SubscriptExpression expr = (SubscriptExpression)GetExpression();
                IPullEvaluator baseEval = expr.BaseExpression.MakeElaborator().ElaborateForPull();
                IItemEvaluator indexEval = expr.GetSubscriptExpression().MakeElaborator().ElaborateForItem();
                return (context) =>
                {
                    NumericValue index = (NumericValue)indexEval.Eval(context);
                    if (index == null)
                    {
                        return null;
                    }

                    int intIndex = index.AsSubscript();
                    if (intIndex != -1)
                    {
                        ISequenceIterator iter = baseEval.Iterate(context);
                        return GetItemAt(iter, intIndex);
                    }
                    else
                    {

                        // there is no item at the required position
                        return null;
                    }
                };
            }
        }
    }
}