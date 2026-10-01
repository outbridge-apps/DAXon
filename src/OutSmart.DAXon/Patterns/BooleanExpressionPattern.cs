////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Types;
using System.Collections.Generic;

namespace OutSmart.DAXon.Patterns
{
    // Faithful port of net.sf.saxon.pattern.BooleanExpressionPattern (Saxon 12.9). Was a hollow stub whose
    // implicit conversion to Pattern returned NULL, so every XSLT 3.0 selection pattern `.[ Expr ]`
    // silently yielded a null Pattern -> NRE in Pattern.Make.
    // Matches an item if the predicate expression has EBV true() with that item as the singleton focus.
    internal sealed class BooleanExpressionPattern : Pattern, IPatternWithPredicate
    {
        private readonly Operand expressionOp;
        private volatile IBooleanEvaluator predicateEvaluator;   // volatile: published once, read per node match (round 11)

        public Expression Predicate => expressionOp.GetChildExpression();

        public override int Fingerprint => -1;

        public BooleanExpressionPattern(Expression expression)
        {
            this.expressionOp = new Operand(this, expression, OperandRole.SINGLE_ATOMIC);
            SetPriority(1);
        }

        public override IEnumerable<Operand> Operands()
        {
            return expressionOp;
        }

        public override UType GetUType()
        {
            if (Predicate is InstanceOfExpression)
            {
                return ((InstanceOfExpression)Predicate).RequiredItemType.GetUType();
            }
            else
            {
                return UType.ANY;
            }
        }

        public override int AllocateSlots(SlotManager slotManager, int nextFree)
        {
            return ExpressionTool.AllocateSlots(Predicate, nextFree, slotManager);
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {
            ContextItemStaticInfo cit = visitor.GetConfiguration().DefaultContextItemStaticInfo;
            expressionOp.SetChildExpression(Predicate.TypeCheck(visitor, cit));
            return this;
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            ContextItemStaticInfo cit = visitor.GetConfiguration().DefaultContextItemStaticInfo;
            expressionOp.SetChildExpression(Predicate.Optimize(visitor, cit));
            return this;
        }

        public override bool Matches(IItem item, IXPathContext context)
        {
            if (predicateEvaluator == null)
            {
                predicateEvaluator = Predicate.MakeElaborator().ElaborateForBoolean();
            }

            IXPathContext c2 = context.NewMinorContext();
            ManualIterator iter = new ManualIterator(item);
            c2.SetCurrentIterator(iter);
            c2.CurrentOutputUri = null;
            try
            {
                return predicateEvaluator(c2);
            }
            catch (XPathException)
            {
                return false;
            }
        }

        public override ItemType GetItemType()
        {
            if (Predicate is InstanceOfExpression)
            {
                InstanceOfExpression ioe = (InstanceOfExpression)Predicate;
                if (ioe.BaseExpression is ContextItemExpression)
                {
                    return ioe.RequiredItemType;
                }
            }

            return AnyItemType.GetInstance();
        }

        public override string Reconstruct()
        {
            return ".[" + Predicate + "]";
        }

        public override bool Equals(object other)
        {
            return (other is BooleanExpressionPattern) && ((BooleanExpressionPattern)other).Predicate.IsEqual(Predicate);
        }

        protected override int ComputeHashCode()
        {
            return 0x7aeffea9 ^ Predicate.GetHashCode();
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            BooleanExpressionPattern n = new BooleanExpressionPattern(Predicate.Copy(rebindings));
            ExpressionTool.CopyLocationInfo(this, n);
            n.OriginalText = OriginalText;
            return n;
        }

        public override void Export(ExpressionPresenter presenter)
        {
            presenter.StartElement("p.booleanExp");
            Predicate.Export(presenter);
            presenter.EndElement();
        }
    }
}
