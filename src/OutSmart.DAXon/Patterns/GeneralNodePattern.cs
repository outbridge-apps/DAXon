////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using System.Collections.Generic;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Types;
namespace OutSmart.DAXon.Patterns
{
    internal sealed class GeneralNodePattern : Pattern
    {
        private Expression equivalentExpr;
        private readonly NodeTest itemType;
        private Expression topNodeEquivalent = null;
        private volatile IPullEvaluator equivalentExprEvaluator;   // volatile: published once, read hot (round 11)
        private volatile IPullEvaluator equivalentTopNodeEvaluator;
        private Pattern precondition = null;

        public override int Dependencies => equivalentExpr.Dependencies & (StaticProperty.DEPENDS_ON_LOCAL_VARIABLES | StaticProperty.DEPENDS_ON_USER_FUNCTIONS);

        public override int Fingerprint => itemType.Fingerprint;
        public GeneralNodePattern(Expression expr, NodeTest itemType)
        {
            equivalentExpr = expr;
            this.itemType = itemType;
        }

        public void MakeTopNodeEquivalent()
        {
            if (equivalentExpr is SlashExpression)
            {
                Expression head = ((SlashExpression)equivalentExpr).FirstStep;
                if (ExpressionTool.GetAxisNavigation(head) == AxisInfo.CHILD)
                {
                    SlashExpression copy = (SlashExpression)equivalentExpr.Copy(new RebindingMap());
                    Expression copyHead = copy.FirstStep;
                    while (true)
                    {
                        if (copyHead is FilterExpression)
                        {
                            copyHead = ((FilterExpression)copyHead).Base;
                        }
                        else if (copyHead is SingleItemFilter)
                        {
                            copyHead = ((SingleItemFilter)copyHead).BaseExpression;
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (copyHead is AxisExpression)
                    {
                        ((AxisExpression)copyHead).Axis = AxisInfo.SELF;
                        topNodeEquivalent = copy;
                    }
                }
            }
        }

        public override IEnumerable<Operand> Operands()
        {
            return new Operand(this, equivalentExpr, OperandRole.SAME_FOCUS_ACTION);
        }

        public override bool IsMotionless()
        {
            return false;
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {
            ContextItemStaticInfo cit = new ContextItemStaticInfo(AnyNodeTest.GetInstance(), false);
            equivalentExpr = equivalentExpr.TypeCheck(visitor, cit);
            MakeTopNodeEquivalent();
            return this;
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            Configuration config = visitor.GetConfiguration();
            ContextItemStaticInfo defaultInfo = config.DefaultContextItemStaticInfo;
            equivalentExpr = equivalentExpr.Optimize(visitor, defaultInfo);

            // See if the expression is now known to be non-positional
            if (equivalentExpr is FilterExpression && !((FilterExpression)equivalentExpr).IsFilterIsPositional())
            {
                try
                {
                    return ((Pattern)PatternMaker.FromExpression(equivalentExpr, config, true)).TypeCheck(visitor, defaultInfo);
                }
                catch (XPathException err)
                {
                }
            }


            // See if there are any predicates we can promote, to avoid a complex search
            if (equivalentExpr is FirstItemExpression || equivalentExpr is LastItemExpression)
            {
                UnaryExpression unaryExpr = ((UnaryExpression)equivalentExpr);
                Expression baseExpr = unaryExpr.BaseExpression;
                if (baseExpr is FilterExpression && !((FilterExpression)baseExpr).IsFilterIsPositional())
                {
                    precondition = new BasePatternWithPredicate(new UniversalPattern(), ((FilterExpression)baseExpr).Filter.Copy(new RebindingMap()));
                    precondition = (Pattern)precondition.TypeCheck(visitor, contextInfo).Optimize(visitor, contextInfo);
                }
            }

            return this;
        }

        public override void BindCurrent(ILocalBinding binding)
        {
            if (ExpressionTool.CallsFunction(equivalentExpr, Current.FN_CURRENT, false))
            {
                if (equivalentExpr.IsCallOn(typeof(Current)))
                {
                    equivalentExpr = new LocalVariableReference(binding);
                }
                else
                {
                    ReplaceCurrent(equivalentExpr, binding);
                }
            }
        }

        public override int AllocateSlots(SlotManager slotManager, int nextFree)
        {
            return ExpressionTool.AllocateSlots(equivalentExpr, nextFree, slotManager);
        }

        public override bool Matches(IItem item, IXPathContext context)
        {
            TypeHierarchy th = context.GetConfiguration().GetTypeHierarchy();
            if (!itemType.Matches(item, th))
            {
                return false;
            }

            if (precondition != null && !precondition.Matches(item, context))
            {
                return false;
            }

            IAxisIterator anc = ((NodeInfo)item).IterateAxis(AxisInfo.ANCESTOR_OR_SELF);
            NodeInfo top = (NodeInfo)item;
            while (true)
            {
                NodeInfo a = anc.Next();
                if (a == null)
                {

                    // The first step in a pattern, if it uses the child axis, is interpreted as "child-or-top" (test case match-274)
                    if (topNodeEquivalent != null && UType.CHILD_NODE_KINDS.Matches(top))
                    {
                        if (equivalentTopNodeEvaluator == null)
                        {
                            equivalentTopNodeEvaluator = topNodeEquivalent.MakeElaborator().ElaborateForPull();
                        }

                        return IsSelected(((NodeInfo)item), top, equivalentTopNodeEvaluator, context);
                    }

                    return false;
                }

                if (MatchesBeneathAnchor((NodeInfo)item, a, context))
                {
                    return true;
                }

                top = a;
            }
        }

        public override bool MatchesBeneathAnchor(NodeInfo node, NodeInfo anchor, IXPathContext context)
        {
            if (!itemType.Test(node))
            {
                return false;
            }


            // for a positional pattern, we do it the hard way: test whether the
            // node is a member of the nodeset obtained by evaluating the
            // equivalent expression
            if (anchor == null)
            {
                IAxisIterator ancestors = node.IterateAxis(AxisInfo.ANCESTOR_OR_SELF);
                while (true)
                {
                    NodeInfo ancestor = ancestors.Next();
                    if (ancestor == null)
                    {
                        return false;
                    }

                    if (MatchesBeneathAnchor(node, ancestor, context))
                    {
                        return true;
                    }
                }
            }

            if (equivalentExprEvaluator == null)
            {
                equivalentExprEvaluator = equivalentExpr.MakeElaborator().ElaborateForPull();
            }

            return IsSelected(node, anchor, equivalentExprEvaluator, context);
        }

        private bool IsSelected(NodeInfo node, NodeInfo anchor, IPullEvaluator selector, IXPathContext context)
        {

            IXPathContext c2 = context.NewMinorContext();
            ManualIterator iter = new ManualIterator(anchor);
            c2.SetCurrentIterator(iter);
            try
            {
                ISequenceIterator nsv = selector.Iterate(c2);
                while (true)
                {
                    NodeInfo n = (NodeInfo)nsv.Next();
                    if (n == null)
                    {
                        return false;
                    }

                    if (n.Equals(node))
                    {
                        return true;
                    }
                }
            }
            catch (XPathException.Circularity e)
            {
                throw;
            }
            // No StackOverflow catch: post-round-BC a recursion overflow raises RecursionDepthError,
            // a foreign type that passes through the XPathException catch below untouched. The old
            // XPathException.StackOverflow is constructed only at the API boundary, never thrown here.
            catch (XPathException e)
            {
                HandleDynamicError(e, c2);
                return false;
            }
        }

        public override UType GetUType()
        {
            return itemType.GetUType();
        }

        public override ItemType GetItemType()
        {
            return itemType;
        }

        public override bool Equals(object other)
        {
            if (other is GeneralNodePattern)
            {
                GeneralNodePattern lpp = (GeneralNodePattern)other;
                return equivalentExpr.IsEqual(lpp.equivalentExpr);
            }
            else
            {
                return false;
            }
        }

        protected override int ComputeHashCode()
        {
            return 83641 ^ equivalentExpr.GetHashCode();
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            GeneralNodePattern n = new GeneralNodePattern(equivalentExpr.Copy(rebindings), itemType);
            ExpressionTool.CopyLocationInfo(this, n);
            n.OriginalText = OriginalText;
            n.topNodeEquivalent = topNodeEquivalent == null ? null : topNodeEquivalent.Copy(rebindings);
            n.precondition = precondition;
            return n;
        }

        public override void Export(ExpressionPresenter presenter)
        {
            presenter.StartElement("p.genNode");
            presenter.EmitAttribute("test", AlphaCode.FromItemType(itemType));
            equivalentExpr.Export(presenter);
            presenter.EndElement();
        }
    }
}
