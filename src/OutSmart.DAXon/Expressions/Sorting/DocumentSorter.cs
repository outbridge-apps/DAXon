////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Expressions.Sorting
{
    public class DocumentSorter : UnaryExpression
    {
        private readonly IComparer<NodeInfo> comparer;

        public override string ExpressionName => "docOrder";

        public override int NetCost => 30;

        public override int ImplementationMethod => ITERATE_METHOD;

        public override string StreamerName => "DocumentSorterAdjunct";
        public DocumentSorter(Expression @base) : base(@base)
        {
            int props = base.GetSpecialProperties();
            if (((props & StaticProperty.CONTEXT_DOCUMENT_NODESET) != 0) || (props & StaticProperty.SINGLE_DOCUMENT_NODESET) != 0)
            {
                comparer = LocalOrderComparer.GetInstance();
            }
            else
            {
                comparer = GlobalOrderComparer.GetInstance();
            }
        }

        public DocumentSorter(Expression @base, bool intraDocument) : base(@base)
        {
            if (intraDocument)
            {
                comparer = LocalOrderComparer.GetInstance();
            }
            else
            {
                comparer = GlobalOrderComparer.GetInstance();
            }
        }

        protected override OperandRole GetOperandRole()
        {
            return OperandRole.SAME_FOCUS_ACTION;
        }

        public virtual IComparer<NodeInfo> GetComparer()
        {
            return comparer;
        }

        public override Expression Simplify()
        {
            Expression operand = BaseExpression.Simplify();
            if (operand.HasSpecialProperty(StaticProperty.ORDERED_NODESET))
            {

                // this can happen as a result of further simplification
                return operand;
            }

            return this;
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            Expression e2 = base.TypeCheck(visitor, contextInfo);
            if (e2 != this)
            {
                return e2;
            }

            TypeHierarchy th = visitor.GetConfiguration().GetTypeHierarchy();
            if (th.Relationship(BaseExpression.GetItemType(), AnyNodeTest.GetInstance()) == Affinity.DISJOINT)
            {
                return BaseExpression;
            }

            Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.MISC, "document-order sorter", 0);
            Expression operand = visitor.GetConfiguration().GetTypeChecker(false).StaticTypeCheck(BaseExpression, SequenceType.NODE_SEQUENCE, role, visitor);
            BaseExpression = operand;
            return this;
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            GetOperand().Optimize(visitor, contextInfo);
            Expression sortable = BaseExpression;
            bool tryHarder = sortable.IsStaticPropertiesKnown();
            while (true)
            {
                if (sortable.HasSpecialProperty(StaticProperty.ORDERED_NODESET))
                {

                    // this can happen as a result of further simplification
                    return sortable;
                }

                if (!Cardinality.AllowsMany(sortable.GetCardinality()))
                {
                    return sortable;
                }

                if (sortable is SlashExpression)
                {
                    SlashExpression slash = (SlashExpression)sortable;

                    // Bug 3389: try to rewrite sort(conditionalSort($var/child.x) / child.y)
                    // as conditionalSort($var, (child.x/child.y))
                    Expression lhs = slash.GetLhsExpression();
                    Expression rhs = slash.GetRhsExpression();
                    if (lhs is ConditionalSorter && slash.GetRhsExpression().HasSpecialProperty(StaticProperty.PEER_NODESET))
                    {
                        ConditionalSorter c = (ConditionalSorter)lhs;
                        DocumentSorter d = c.DocumentSorter;
                        Expression condition = c.Condition;
                        Expression s = new SlashExpression(d.BaseExpression, rhs);
                        s = s.Optimize(visitor, contextInfo);
                        return new ConditionalSorter(condition, new DocumentSorter(s));
                    }


                    // docOrder(docOrder(A)/B) can be rewritten as docOrder(A/B). However, this may not always
                    // be wise, because the inner docOrder might eliminate many duplicates, reducing the cost
                    // of the /B operation. We therefore do it only if B is a low-cost operation.
                    if (lhs is DocumentSorter && rhs is AxisExpression && ((AxisExpression)rhs).Axis == AxisInfo.CHILD)
                    {
                        SlashExpression s1 = new SlashExpression(((DocumentSorter)lhs).BaseExpression, rhs);
                        ExpressionTool.CopyLocationInfo(this, s1);
                        return new DocumentSorter(s1).Optimize(visitor, contextInfo);
                    }


                    // docOrder(A/B) can be rewritten as head(A)!docOrder(B) in the case where B returns nodes
                    // and is independent of the focus. We already know it returns nodes otherwise we wouldn't be here.
                    // SEE BUG 4640
                    if (!ExpressionTool.DependsOnFocus(rhs) && !rhs.HasSpecialProperty(StaticProperty.HAS_SIDE_EFFECTS) && rhs.HasSpecialProperty(StaticProperty.NO_NODES_NEWLY_CREATED))
                    {
                        Expression e1 = FirstItemExpression.MakeFirstItemExpression(slash.GetLhsExpression());
                        Expression e2 = new DocumentSorter(slash.GetRhsExpression());
                        SlashExpression e3 = new SlashExpression(e1, e2);
                        ExpressionTool.CopyLocationInfo(this, e3);
                        return e3.Optimize(visitor, contextInfo);
                    }
                }


                // Try once more after recomputing the static properties of the expression
                if (tryHarder)
                {
                    sortable.ResetLocalStaticProperties();
                    tryHarder = false;
                }
                else
                {
                    break;
                }
            }

            if (sortable is SlashExpression && !visitor.IsOptimizeForStreaming() && !(ParentExpression is ConditionalSorter))
            {
                return visitor.ObtainOptimizer().MakeConditionalDocumentSorter(this, (SlashExpression)sortable);
            }

            return this;
        }

        public override Expression Unordered(bool retainAllNodes, bool forStreaming)
        {
            Expression operand = BaseExpression.Unordered(retainAllNodes, forStreaming);
            if (operand.HasSpecialProperty(StaticProperty.ORDERED_NODESET))
            {
                return operand;
            }

            if (!retainAllNodes)
            {
                return operand;
            }
            else if (operand is SlashExpression)
            {

                // handle the common case of //section/head where it is safe to remove sorting, because
                // no duplicates need to be removed
                SlashExpression exp = (SlashExpression)operand;
                Expression a = exp.GetSelectExpression();
                Expression b = exp.GetActionExpression();
                a = ExpressionTool.UnfilteredExpression(a, false);
                b = ExpressionTool.UnfilteredExpression(b, false);
                if (a is AxisExpression && (((AxisExpression)a).Axis == AxisInfo.DESCENDANT || ((AxisExpression)a).Axis == AxisInfo.DESCENDANT_OR_SELF) && b is AxisExpression && ((AxisExpression)b).Axis == AxisInfo.CHILD)
                {
                    return operand.Unordered(retainAllNodes, false);
                }
            }

            BaseExpression = operand;
            return this;
        }

        protected override int ComputeSpecialProperties()
        {
            return BaseExpression.GetSpecialProperties() | StaticProperty.ORDERED_NODESET;
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            DocumentSorter ds = new DocumentSorter(BaseExpression.Copy(rebindings));
            ExpressionTool.CopyLocationInfo(this, ds);
            return ds;
        }

        public override Patterns.Pattern ToPattern(Configuration config)
        {
            return BaseExpression.ToPattern(config);
        }

        public override ISequenceIterator Iterate(IXPathContext context)
        {
            return DocumentOrderIterator.Of(BaseExpression.Iterate(context), comparer);
        }

        public override bool EffectiveBooleanValue(IXPathContext context)
        {
            return BaseExpression.EffectiveBooleanValue(context);
        }

        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("docOrder", this);
            @out.EmitAttribute("intra", comparer is LocalOrderComparer ? "1" : "0");
            BaseExpression.Export(@out);
            @out.EndElement();
        }

        public override Elaborator GetElaborator()
        {
            return new DocumentSorterElaborator();
        }

        /// <summary>
        /// Elaborator for a docOrder expression - sorts nodes into document order and eliminates duplicates
        /// </summary>
        internal sealed class DocumentSorterElaborator : PullElaborator
        {
            public override IPullEvaluator ElaborateForPull()
            {
                DocumentSorter expr = (DocumentSorter)GetExpression();
                IPullEvaluator baseEval = expr.BaseExpression.MakeElaborator().ElaborateForPull();
                IComparer<NodeInfo> comparer = expr.GetComparer();
                return (context) => DocumentOrderIterator.Of(baseEval.Iterate(context), comparer);
            }

            public override IBooleanEvaluator ElaborateForBoolean()
            {
                DocumentSorter expr = (DocumentSorter)GetExpression();
                return expr.BaseExpression.MakeElaborator().ElaborateForBoolean();
            }
        }
    }
}
