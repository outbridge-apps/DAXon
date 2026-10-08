////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System.Collections.Generic;
namespace OutSmart.DAXon.Expressions.Sorting
{
    internal sealed class ConditionalSorter : Expression
    {

        private static readonly OperandRole DOC_SORTER_ROLE = new OperandRole(OperandRole.CONSTRAINED_CLASS, OperandUsage.TRANSMISSION, SequenceType.ANY_SEQUENCE, (expr) => expr is DocumentSorter);
        private readonly Operand conditionOp;
        private readonly Operand sorterOp;

        public Expression Condition
        {
            get => conditionOp.GetChildExpression(); set
            {
                conditionOp.SetChildExpression(value);
            }
        }

        public DocumentSorter DocumentSorter
        {
            get => (DocumentSorter)sorterOp.GetChildExpression(); set
            {
                sorterOp.SetChildExpression(value);
            }
        }

        public override int ImplementationMethod => ITERATE_METHOD;

        public override string ExpressionName => "conditionalSort";
        public ConditionalSorter(Expression condition, DocumentSorter sorter)
        {
            conditionOp = new Operand(this, condition, OperandRole.SINGLE_ATOMIC);
            sorterOp = new Operand(this, sorter, DOC_SORTER_ROLE);
            AdoptChildExpression(condition);
            AdoptChildExpression(sorter);
        }

        public override IEnumerable<Operand> Operands()
        {
            return OperandList(conditionOp, sorterOp);
        }

        public override Expression Simplify()
        {
            return Rewrite((e) => e.Simplify());
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            return Rewrite((exp) => exp.TypeCheck(visitor, contextInfo));
        }

        public override int GetCardinality()
        {
            return DocumentSorter.GetCardinality();
        }

        protected override int ComputeSpecialProperties()
        {
            return Condition.GetSpecialProperties() | StaticProperty.ORDERED_NODESET & ~StaticProperty.REVERSE_DOCUMENT_ORDER;
        }

        public override
        Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            return Rewrite((exp) => exp.Optimize(visitor, contextInfo));
        }

        private Expression Rewrite(IRewriteAction rewriter)
        {
            Expression @base = rewriter(DocumentSorter);
            if (@base is DocumentSorter)
            {
                sorterOp.SetChildExpression(@base);
            }
            else
            {
                return @base;
            }

            Expression cond = rewriter(Condition);
            if (cond is Literal)
            {
                bool b = ((Literal)cond).GroundedValue.EffectiveBooleanValue();
                if (b)
                {
                    return @base;
                }
                else
                {
                    return ((DocumentSorter)@base).BaseExpression;
                }
            }
            else
            {
                conditionOp.SetChildExpression(cond);
                return this;
            }
        }

        public override Expression Unordered(bool retainAllNodes, bool forStreaming)
        {
            Expression @base = DocumentSorter.Unordered(retainAllNodes, forStreaming);
            if (@base is DocumentSorter)
            {
                return this;
            }
            else
            {
                return @base;
            }
        }

        protected override int ComputeCardinality()
        {
            return StaticProperty.ALLOWS_ZERO_OR_MORE;
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            ConditionalSorter cs = new ConditionalSorter(Condition.Copy(rebindings), (DocumentSorter)DocumentSorter.Copy(rebindings));
            ExpressionTool.CopyLocationInfo(this, cs);
            return cs;
        }

        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("conditionalSort", this);
            Condition.Export(@out);
            DocumentSorter.Export(@out);
            @out.EndElement();
        }

        public override ItemType GetItemType()
        {
            return DocumentSorter.GetItemType();
        }

        public override ISequenceIterator Iterate(IXPathContext context)
        {
            return MakeElaborator().ElaborateForPull().Iterate(context);
        }

        public override Elaborator GetElaborator()
        {
            return new ConditionalSorterElaborator();
        }
        // IRewriteAction interface->delegate.
        private delegate Expression IRewriteAction(Expression e);

        internal sealed class ConditionalSorterElaborator : PullElaborator
        {
            public override IPullEvaluator ElaborateForPull()
            {
                ConditionalSorter expr = (ConditionalSorter)GetExpression();
                IBooleanEvaluator condition = expr.Condition.MakeElaborator().ElaborateForBoolean();
                IPullEvaluator sorter = expr.DocumentSorter.MakeElaborator().ElaborateForPull();
                IPullEvaluator nonSorter = expr.DocumentSorter.BaseExpression.MakeElaborator().ElaborateForPull();
                return (context) =>
                {
                    bool b = condition.Eval(context);
                    if (b)
                    {
                        return sorter.Iterate(context);
                    }
                    else
                    {
                        return nonSorter.Iterate(context);
                    }
                };
            }
        }
    }
}
