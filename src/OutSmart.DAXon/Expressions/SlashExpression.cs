////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Values.Arrays;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Types;
namespace OutSmart.DAXon.Expressions
{
    public class SlashExpression : BinaryExpression, IContextSwitchingExpression
    {
        private bool contextFree;
        private bool indexingDisabled;

        public virtual Expression Start
        {
            get => GetLhsExpression(); set
            {
                SetLhsExpression(value);
            }
        }

        public override string ExpressionName => "pathExpression";

        public override IntegerValue[] IntegerBounds => GetStep().IntegerBounds;

        public override double Cost
        {
            get
            {
                int factor = Cardinality.AllowsMany(GetLhsExpression().GetCardinality()) ? 5 : 1;
                double lh = GetLhsExpression().Cost + 1;
                double rh = GetRhsExpression().Cost;
                double product = lh + factor * rh;
                return Math.Max(product, MAX_COST);
            }
        }

        public override int ImplementationMethod => ITERATE_METHOD;

        //}
        //
        //
        public virtual Expression FirstStep
        {
            get
            {
                if (Start is SlashExpression)
                {
                    return ((SlashExpression)Start).FirstStep;
                }
                else
                {
                    return Start;
                }
            }
        }

        //}
        //
        //
        public virtual Expression RemainingSteps
        {
            get
            {
                if (Start is SlashExpression)
                {
                    IList<Expression> list = new List<Expression>(4);
                    GatherSteps(list);
                    Expression rem = RebuildSteps(list.GetRange(1, (list.Count) - (1)));
                    ExpressionTool.CopyLocationInfo(this, rem);
                    return rem;
                }
                else
                {
                    return GetStep();
                }
            }
        }

        //}
        //
        //
        public virtual Expression LastStep
        {
            get
            {
                if (GetStep() is SlashExpression)
                {
                    return ((SlashExpression)GetStep()).LastStep;
                }
                else
                {
                    return GetStep();
                }
            }
        }

        //}
        //
        //
        public virtual Expression LeadingSteps
        {
            get
            {
                if (GetStep() is SlashExpression)
                {
                    IList<Expression> list = new List<Expression>(4);
                    GatherSteps(list);
                    Expression rem = RebuildSteps(list.GetRange(0, (list.Count - 1) - (0)));
                    ExpressionTool.CopyLocationInfo(this, rem);
                    return rem;
                }
                else
                {
                    return Start;
                }
            }
        }

        //}
        //
        //
        public override string StreamerName => "ForEach";
        public SlashExpression(Expression start, Expression step) : base(start, Token.SLASH, step)
        {
        }

        protected override OperandRole GetOperandRole(int arg)
        {
            return arg == 0 ? OperandRole.FOCUS_CONTROLLING_SELECT : OperandRole.FOCUS_CONTROLLED_ACTION;
        }

        public virtual Expression GetStep()
        {
            return GetRhsExpression();
        }

        public virtual void SetStep(Expression step)
        {
            SetRhsExpression(step);
        }

        public Expression GetSelectExpression()
        {
            return Start;
        }

        public Expression GetActionExpression()
        {
            return GetStep();
        }

        public virtual void DisableIndexing()
        {
            indexingDisabled = true;
        }

        public override ItemType GetItemType()
        {
            return GetStep().GetItemType();
        }

        public override UType GetStaticUType(UType contextItemType)
        {
            return GetStep().GetStaticUType(Start.GetStaticUType(contextItemType));
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            Lhs.TypeCheck(visitor, contextInfo);

            // If the first expression is known to be empty, just return empty without checking the step expression.
            // (Checking the step expression can cause spurious errors, such as "the context item is absent")
            if (Literal.IsEmptySequence(Start))
            {
                return Start;
            }


            // The first operand must be of type node()*
            Configuration config = visitor.GetConfiguration();
            TypeChecker tc = config.GetTypeChecker(false);
            Func<RoleDiagnostic> roleSupplier = () => new RoleDiagnostic(RoleDiagnostic.BINARY_EXPR, "/", 0, "XPTY0019");
            Start = tc.StaticTypeCheck(Start, SequenceType.NODE_SEQUENCE, roleSupplier, visitor);

            // Now check the second operand
            ItemType startType = Start.GetItemType();
            if (startType == ErrorType.GetInstance())
            {

                // implies the start expression will return an empty sequence, so the whole expression is void
                return Literal.MakeEmptySequence();
            }

            // The first operand of '/' must yield nodes. If it is statically known to be
            // non-empty and non-node (e.g. atomic values, as in (1 to 3)/x), that is a type
            // error XPTY0019. Normally the StaticTypeCheck above catches this, but when the
            // operand has been constant-folded to a Literal it may slip through; detect it
            // here so we raise XPTY0019 rather than the step's context-item error XPTY0020.
            if ((Genre)startType.GetGenre() != Genre.NODE && !Cardinality.AllowsZero(Start.GetCardinality()))
            {
                throw new XPathException("The first operand of '/' must be a sequence of nodes, but the supplied value contains an atomic value").AsTypeError().WithErrorCode("XPTY0019").WithLocation(GetLocation());
            }

            ContextItemStaticInfo cit = config.MakeContextItemStaticInfo(startType, false);
            cit.ContextSettingExpression = Start;
            Rhs.TypeCheck(visitor, cit);

            // Give a warning if people write a/[x = 3]
            if (GetRhsExpression() is SquareArrayConstructor)
            {
                SquareArrayConstructor sq = (SquareArrayConstructor)GetRhsExpression();
                if (sq.GetOperanda().NumberOfOperands == 1)
                {
                    visitor.StaticContext.IssueWarning("An array constructor appears immediately after '/' or '//'. Perhaps " + "'/*[predicate]' was intended? If not, consider using '!' rather than '/' to remove this warning.", DAXonErrorCode.SXWN9028, sq.GetLocation());
                }
            }


            // If the expression has the form (a//descendant-or-self.node())/b, try to simplify it to
            // use the descendant axis
            Expression e2 = SimplifyDescendantPath(visitor.StaticContext);
            if (e2 != null)
            {
                return e2.TypeCheck(visitor, contextInfo);
            }

            if (Start is ContextItemExpression && GetStep().HasSpecialProperty(StaticProperty.ORDERED_NODESET))
            {
                return GetStep();
            }

            if (GetStep() is ContextItemExpression && Start.HasSpecialProperty(StaticProperty.ORDERED_NODESET))
            {
                return Start;
            }

            if (GetStep() is AxisExpression && ((AxisExpression)GetStep()).Axis == AxisInfo.SELF && config.GetTypeHierarchy().IsSubType(startType, GetStep().GetItemType()))
            {
                return Start;
            }

            return this;
        }

        public virtual SlashExpression SimplifyDescendantPath(IStaticContext env)
        {
            Expression underlyingStep = GetStep();
            while (underlyingStep is FilterExpression)
            {
                if (((FilterExpression)underlyingStep).IsPositional(env.GetConfiguration().GetTypeHierarchy()))
                {
                    return null;
                }

                underlyingStep = ((FilterExpression)underlyingStep).GetSelectExpression();
            }

            if (!(underlyingStep is AxisExpression))
            {
                return null;
            }

            Expression st = Start;

            // detect .//x as a special case; this will appear as descendant-or-self.node()/x
            if (st is AxisExpression)
            {
                AxisExpression stax = (AxisExpression)st;
                if (stax.Axis != AxisInfo.DESCENDANT_OR_SELF)
                {
                    return null;
                }

                ContextItemExpression cie = new ContextItemExpression();
                ExpressionTool.CopyLocationInfo(this, cie);
                st = ExpressionTool.MakePathExpression(cie, stax.Copy(new RebindingMap()));
                ExpressionTool.CopyLocationInfo(this, st);
            }

            if (!(st is SlashExpression))
            {
                return null;
            }

            SlashExpression startPath = (SlashExpression)st;
            if (!(startPath.GetStep() is AxisExpression))
            {
                return null;
            }

            AxisExpression mid = (AxisExpression)startPath.GetStep();
            if (mid.Axis != AxisInfo.DESCENDANT_OR_SELF)
            {
                return null;
            }

            NodeTest test = mid.GetNodeTest();
            if (!(test == null || test is AnyNodeTest))
            {
                return null;
            }

            int underlyingAxis = ((AxisExpression)underlyingStep).Axis;
            if (underlyingAxis == AxisInfo.CHILD || underlyingAxis == AxisInfo.DESCENDANT || underlyingAxis == AxisInfo.DESCENDANT_OR_SELF)
            {
                int newAxis = underlyingAxis == AxisInfo.DESCENDANT_OR_SELF ? AxisInfo.DESCENDANT_OR_SELF : AxisInfo.DESCENDANT;
                Expression newStep = new AxisExpression(newAxis, ((AxisExpression)underlyingStep).GetNodeTest());
                ExpressionTool.CopyLocationInfo(this, newStep);
                underlyingStep = GetStep();

                // Add any filters to the new expression. We know they aren't
                // positional, so the order of the filters doesn't technically matter
                // (XPath section 2.3.4 explicitly allows us to change it.)
                // However, in the interests of predictable execution, hand-optimization, and
                // diagnosable error behaviour, we retain the original order.
                Stack<Expression> filters = new Stack<Expression>();
                while (underlyingStep is FilterExpression)
                {
                    filters.Push(((FilterExpression)underlyingStep).Filter);
                    underlyingStep = ((FilterExpression)underlyingStep).GetSelectExpression();
                }

                while (filters.Count > 0)
                {
                    newStep = new FilterExpression(newStep, filters.Pop());
                    ExpressionTool.CopyLocationInfo(GetStep(), newStep);
                }

                Expression newPath = ExpressionTool.MakePathExpression(startPath.Start, newStep);
                if (!(newPath is SlashExpression))
                {
                    return null;
                }

                ExpressionTool.CopyLocationInfo(this, newPath);
                ((SlashExpression)newPath).indexingDisabled = indexingDisabled;
                return (SlashExpression)newPath;
            }

            if (underlyingAxis == AxisInfo.ATTRIBUTE)
            {

                // turn the expression a//@b into a/descendant-or-self::*/@b
                Expression newStep = new AxisExpression(AxisInfo.DESCENDANT_OR_SELF, NodeKindTest.ELEMENT);
                ExpressionTool.CopyLocationInfo(this, newStep);
                Expression e2 = ExpressionTool.MakePathExpression(startPath.Start, newStep);
                Expression e3 = ExpressionTool.MakePathExpression(e2, GetStep());
                if (!(e3 is SlashExpression))
                {
                    return null;
                }

                ExpressionTool.CopyLocationInfo(this, e3);
                return (SlashExpression)e3;
            }

            return null;
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {
            Configuration config = visitor.GetConfiguration();
            TypeHierarchy th = config.GetTypeHierarchy();
            Optimizer opt = visitor.ObtainOptimizer();
            Lhs.Optimize(visitor, contextItemType);
            if (Literal.IsEmptySequence(Start))
            {
                return Literal.MakeEmptySequence();
            }

            ContextItemStaticInfo cit = visitor.GetConfiguration().MakeContextItemStaticInfo(Start.GetItemType(), false);
            cit.ContextSettingExpression = Start;
            Rhs.Optimize(visitor, cit);
            if (Literal.IsEmptySequence(GetStep()))
            {
                return Literal.MakeEmptySequence();
            }

            if (Start is RootExpression && th.IsSubType(contextItemType.GetItemType(), NodeKindTest.DOCUMENT))
            {

                // remove unnecessary leading "/" - helps streaming
                return GetStep();
            }


            // Try to simplify descendant-or-self.node()/child.node
            Expression e2 = SimplifyDescendantPath(visitor.StaticContext);
            if (e2 != null)
            {
                return e2.Optimize(visitor, contextItemType);
            }


            // Rewrite a/b[filter] as (a/b)[filter] to improve the chance of indexing
            if (!indexingDisabled)
            {
                Expression firstStep = FirstStep;
                if (!(firstStep.IsCallOn(typeof(Doc)) || firstStep.IsCallOn(typeof(DocumentFn))))
                {

                    // Avoid the rewrite if the path starts with doc() for streaming reasons
                    Expression lastStep = LastStep;
                    if (lastStep is FilterExpression && !((FilterExpression)lastStep).IsPositional(th))
                    {
                        Expression leading = LeadingSteps;
                        Expression p2 = ExpressionTool.MakePathExpression(leading, ((FilterExpression)lastStep).GetSelectExpression());
                        Expression f2 = new FilterExpression(p2, ((FilterExpression)lastStep).Filter);
                        ExpressionTool.CopyLocationInfo(this, f2);
                        return f2.Optimize(visitor, contextItemType);
                    }
                }

                if (!visitor.IsOptimizeForStreaming())
                {
                    Expression k = opt.ConvertPathExpressionToKey(this, visitor);
                    if (k != null)
                    {
                        return k.TypeCheck(visitor, contextItemType).Optimize(visitor, contextItemType);
                    }
                }
            }


            // Replace //x/y by descendant.y[parent.x] to eliminate the need for sorting
            // into document order, and to make the expression streamable
            e2 = TryToMakeSorted(visitor, contextItemType);
            if (e2 != null)
            {
                return e2;
            }


            // Replace $x/child.abcd by a SimpleStepExpression, to avoid the need for creating
            // a new dynamic context at run-time.
            if (GetStep() is AxisExpression)
            {
                if (!Cardinality.AllowsMany(Start.GetCardinality()))
                {
                    SimpleStepExpression sse = new SimpleStepExpression(Start, GetStep());
                    ExpressionTool.CopyLocationInfo(this, sse);
                    sse.ParentExpression = ParentExpression;
                    return sse;
                }
                else
                {
                    contextFree = true;
                }
            }

            if (Start is RootExpression && GetStep().IsCallOn(typeof(KeyFn)))
            {

                // This happens after optimizations to convert filter expressions to key() calls
                SystemFunctionCall keyCall = (SystemFunctionCall)GetStep();
                if (keyCall.GetArity() == 3 && keyCall.GetArg(2) is ContextItemExpression)
                {
                    keyCall.SetArg(2, new RootExpression());
                    keyCall.ParentExpression = ParentExpression;
                    ExpressionTool.ResetStaticProperties(keyCall);
                    return keyCall;
                }
            }

            if (visitor.IsOptimizeForStreaming())
            {

                // rewrite a/copy-of(.) as copy-of(a)
                Expression rawStep = ExpressionTool.UnfilteredExpression(GetStep(), true);
                if (rawStep is CopyOf && ((CopyOf)rawStep).Select is ContextItemExpression)
                {
                    ((CopyOf)rawStep).Select = Start;
                    rawStep.ResetLocalStaticProperties();
                    GetStep().ResetLocalStaticProperties();
                    return GetStep();
                }
            }

            return this;
        }

        public virtual SlashExpression TryToMakeAbsolute()
        {
            Expression first = FirstStep;
            if (first.GetItemType().PrimitiveType == Types.Type.DOCUMENT)
            {
                return this;
            }

            if (first is AxisExpression)
            {

                // This second test allows keys to be built. See XMark q9.
                ItemType contextItemType = ((AxisExpression)first).ContextItemType;
                if (contextItemType != null && contextItemType.PrimitiveType == Types.Type.DOCUMENT)
                {
                    RootExpression root = new RootExpression();
                    ExpressionTool.CopyLocationInfo(this, root);
                    Expression path = ExpressionTool.MakePathExpression(root, this.Copy(new RebindingMap()));
                    if (!(path is SlashExpression))
                    {
                        return null;
                    }

                    ExpressionTool.CopyLocationInfo(this, path);
                    return (SlashExpression)path;
                }
            }

            if (first is DocumentSorter && ((DocumentSorter)first).BaseExpression is SlashExpression)
            {

                // see test case filter-001 in xqts-extra
                SlashExpression se = (SlashExpression)((DocumentSorter)first).BaseExpression;
                SlashExpression se2 = se.TryToMakeAbsolute();
                if (se2 != null)
                {
                    if (se2 == se)
                    {
                        return this;
                    }
                    else
                    {
                        Expression rest = RemainingSteps;
                        DocumentSorter ds = new DocumentSorter(se2);
                        return new SlashExpression(ds, rest);
                    }
                }
            }

            return null;
        }

        public virtual Expression TryToMakeSorted(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {

            Configuration config = visitor.GetConfiguration();
            TypeHierarchy th = config.GetTypeHierarchy();
            Optimizer opt = visitor.ObtainOptimizer();
            Expression s1 = ExpressionTool.UnfilteredExpression(Start, false);
            if (!(s1 is AxisExpression && ((AxisExpression)s1).Axis == AxisInfo.DESCENDANT))
            {
                return null;
            }

            Expression s2 = ExpressionTool.UnfilteredExpression(GetStep(), false);
            if (!(s2 is AxisExpression && ((AxisExpression)s2).Axis == AxisInfo.CHILD))
            {
                return null;
            }


            // We're in business; construct the new expression
            Expression x = Start.Copy(new RebindingMap());
            AxisExpression ax = (AxisExpression)ExpressionTool.UnfilteredExpression(x, false);
            ax.Axis = AxisInfo.PARENT;
            Expression y = GetStep().Copy(new RebindingMap());
            AxisExpression ay = (AxisExpression)ExpressionTool.UnfilteredExpression(y, false);
            ay.Axis = AxisInfo.DESCENDANT;
            Expression k = new FilterExpression(y, x);

            // If we're not starting at the root, ensure we go down at least one level
            if (!th.IsSubType(contextItemType.GetItemType(), NodeKindTest.DOCUMENT))
            {
                k = new SlashExpression(new AxisExpression(AxisInfo.CHILD, NodeKindTest.ELEMENT), k);
                ExpressionTool.CopyLocationInfo(this, k);
                opt.Trace("Rewrote descendant.X/child.Y as child::*/descendant.Y[parent.X]", k);
            }
            else
            {
                ExpressionTool.CopyLocationInfo(this, k);
                opt.Trace("Rewrote descendant.X/child.Y as descendant.Y[parent.X]", k);
            }

            return k;
        }

        public override Expression Unordered(bool retainAllNodes, bool forStreaming)
        {
            if ((GetStep().Dependencies & (StaticProperty.DEPENDS_ON_POSITION | StaticProperty.DEPENDS_ON_LAST)) == 0)
            {
                Start = Start.Unordered(retainAllNodes, forStreaming);
            }

            SetStep(GetStep().Unordered(retainAllNodes, forStreaming));
            return this;
        }

        public override PathMap.PathMapNodeSet AddToPathMap(PathMap pathMap, PathMap.PathMapNodeSet pathMapNodeSet)
        {
            PathMap.PathMapNodeSet target = Start.AddToPathMap(pathMap, pathMapNodeSet);
            return GetStep().AddToPathMap(pathMap, target);
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            Expression exp = ExpressionTool.MakePathExpression(Start.Copy(rebindings), GetStep().Copy(rebindings));
            ExpressionTool.CopyLocationInfo(this, exp);
            if (exp is SlashExpression)
            {
                ((SlashExpression)exp).indexingDisabled = indexingDisabled;
            }

            return exp;
        }

        protected override int ComputeSpecialProperties()
        {
            int startProperties = Start.GetSpecialProperties();
            int stepProperties = GetStep().GetSpecialProperties();
            if ((stepProperties & StaticProperty.ALL_NODES_NEWLY_CREATED) != 0)
            {

                // Deem copies/snapshots to be in document order
                return StaticProperty.ORDERED_NODESET | StaticProperty.PEER_NODESET | StaticProperty.NO_NODES_NEWLY_CREATED;
            }


            int p = 0;
            if (!Cardinality.AllowsMany(Start.GetCardinality()))
            {
                startProperties |= StaticProperty.ORDERED_NODESET | StaticProperty.PEER_NODESET | StaticProperty.SINGLE_DOCUMENT_NODESET;
            }

            if (!Cardinality.AllowsMany(GetStep().GetCardinality()))
            {
                stepProperties |= StaticProperty.ORDERED_NODESET | StaticProperty.PEER_NODESET | StaticProperty.SINGLE_DOCUMENT_NODESET;
            }

            if ((startProperties & stepProperties & StaticProperty.CONTEXT_DOCUMENT_NODESET) != 0)
            {
                p |= StaticProperty.CONTEXT_DOCUMENT_NODESET;
            }

            if (((startProperties & StaticProperty.SINGLE_DOCUMENT_NODESET) != 0) && ((stepProperties & StaticProperty.CONTEXT_DOCUMENT_NODESET) != 0))
            {
                p |= StaticProperty.SINGLE_DOCUMENT_NODESET;
            }

            if ((startProperties & stepProperties & StaticProperty.PEER_NODESET) != 0)
            {
                p |= StaticProperty.PEER_NODESET;
            }

            if ((startProperties & stepProperties & StaticProperty.SUBTREE_NODESET) != 0)
            {
                p |= StaticProperty.SUBTREE_NODESET;
            }

            if (TestNaturallySorted(startProperties, stepProperties))
            {
                p |= StaticProperty.ORDERED_NODESET;
            }

            if (TestNaturallyReverseSorted())
            {
                p |= StaticProperty.REVERSE_DOCUMENT_ORDER;
            }

            if ((startProperties & stepProperties & StaticProperty.NO_NODES_NEWLY_CREATED) != 0)
            {
                p |= StaticProperty.NO_NODES_NEWLY_CREATED;
            }

            return p;
        }

        private bool TestNaturallySorted(int startProperties, int stepProperties)
        {

            // display(20);
            if ((stepProperties & StaticProperty.ORDERED_NODESET) == 0)
            {
                return false;
            }

            if (Cardinality.AllowsMany(Start.GetCardinality()))
            {
                if ((startProperties & StaticProperty.ORDERED_NODESET) == 0)
                {
                    return false;
                }
            }
            else
            {

                //if ((stepProperties & StaticProperty.ORDERED_NODESET) != 0) {
                return true; //}
            }


            // We know now that both the start and the step are sorted. But this does
            // not necessarily mean that the combination is sorted.
            // The result is sorted if the start is sorted and the step selects attributes
            // or namespaces
            if ((stepProperties & StaticProperty.ATTRIBUTE_NS_NODESET) != 0)
            {
                return true;
            }


            // The result is sorted if the step is creative (e.g. a call to copy-of())
            if ((stepProperties & StaticProperty.ALL_NODES_NEWLY_CREATED) != 0)
            {
                return true;
            }


            // The result is sorted if the start selects "peer nodes" (that @is, a node-set in which
            // no node is an ancestor of another) and the step selects within the subtree rooted
            // at the context node
            return ((startProperties & StaticProperty.PEER_NODESET) != 0) && ((stepProperties & StaticProperty.SUBTREE_NODESET) != 0);
        }

        //}
        private bool TestNaturallyReverseSorted()
        {

            // Some examples of path expressions that are naturally reverse sorted:
            //     ancestor::*/@x
            //     ../preceding-sibling.x
            //     $x[1]/preceding-sibling.node()
            // This information is used to do a simple reversal of the nodes
            // instead of a full sort, which is significantly cheaper, especially
            // when using tree models (such as DOM and JDOM) in which comparing
            // nodes in document order is an expensive operation.
            if (!Cardinality.AllowsMany(Start.GetCardinality()) && (GetStep() is AxisExpression))
            {
                return !AxisInfo.isForwards[((AxisExpression)GetStep()).Axis];
            }

            return !Cardinality.AllowsMany(GetStep().GetCardinality()) && (Start is AxisExpression) && !AxisInfo.isForwards[((AxisExpression)Start).Axis];
        }

        //}
        protected override int ComputeCardinality()
        {
            int c1 = Start.GetCardinality();
            int c2 = GetStep().GetCardinality();
            return Cardinality.Multiply(c1, c2);
        }

        //}
        public override Patterns.Pattern ToPattern(Configuration config)
        {
            Expression head = LeadingSteps;
            Expression tail = LastStep;
            if (head is ItemChecker)
            {

                // No need to type check the context item
                ItemChecker checker = (ItemChecker)head;
                if (checker.BaseExpression is ContextItemExpression)
                {
                    return tail.ToPattern(config);
                }
            }
            else if (tail is VennExpression)
            {

                // Bug 4645. Rewrite a/(b|c) as (a/b union a/c). Note this rewrite isn't safe for
                // the "intersect" and "except" operators, except in special cases
                VennExpression ve = (VennExpression)tail;
                if (ve.@operator == Token.UNION)
                {
                    Expression lhExpansion = new SlashExpression(head.Copy(new RebindingMap()), ve.GetLhsExpression());
                    Expression rhExpansion = new SlashExpression(head.Copy(new RebindingMap()), ve.GetRhsExpression());
                    VennExpression topExpansion = new VennExpression(lhExpansion, ve.@operator, rhExpansion);
                    return topExpansion.ToPattern(config);
                }
            }

            Patterns.Pattern tailPattern = tail.ToPattern(config);
            if (tailPattern is NodeTestPattern)
            {
                if (tailPattern.GetItemType() is ErrorType)
                {
                    return tailPattern;
                }
            }
            else if (tailPattern is GeneralNodePattern)
            {
                return new GeneralNodePattern(this, (NodeTest)tailPattern.GetItemType());
            }

            int axis = AxisInfo.PARENT;
            Patterns.Pattern headPattern = null;
            if (head is SlashExpression)
            {
                SlashExpression start = (SlashExpression)head;
                if (start.GetActionExpression() is AxisExpression)
                {
                    AxisExpression mid = (AxisExpression)start.GetActionExpression();
                    if (mid.Axis == AxisInfo.DESCENDANT_OR_SELF && (mid.GetNodeTest() == null || mid.GetNodeTest() is AnyNodeTest))
                    {
                        axis = AxisInfo.ANCESTOR;
                        headPattern = start.GetSelectExpression().ToPattern(config);
                    }
                }
            }

            if (headPattern == null)
            {
                axis = PatternMaker.GetAxisForPathStep(tail);
                headPattern = head.ToPattern(config);
            }

            return new AncestorQualifiedPattern(tailPattern, headPattern, axis);
        }

        //}
        public virtual bool IsContextFree()
        {
            return contextFree;
        }

        //}
        public virtual void SetContextFree(bool free)
        {
            this.contextFree = free;
        }

        //}
        /// <summary>
        /// Is this expression the same as another expression?
        /// </summary>
        public override bool Equals(object other)
        {
            if (!(other is SlashExpression))
            {
                return false;
            }

            SlashExpression p = (SlashExpression)other;
            return Start.IsEqual(p.Start) && GetStep().IsEqual(p.GetStep());
        }

        //}
        protected override int ComputeHashCode()
        {
            return "SlashExpression".GetHashCode() + Start.GetHashCode() + GetStep().GetHashCode();
        }

        //}
        public override ISequenceIterator Iterate(IXPathContext context)
        {
            return MakeElaborator().ElaborateForPull().Iterate(context); //        // This class delivers the result of the path expression in unsorted order,
            //        // without removal of duplicates. If sorting and deduplication are needed,
            //        // this is achieved by wrapping the path expression in a DocumentSorter
            //
            //            // See bug 4730: the step might have been changed to something else
            //            return MappingIterator.map(
            //                    getStart().iterate(context),
            //                    item -> ((AxisExpression) step).iterate((NodeInfo)item));
            //        }
            //
        }

        //}
        //
        //
        public override void Export(ExpressionPresenter destination)
        {
            destination.StartElement("slash", this);
            if (this is SimpleStepExpression)
            {
                destination.EmitAttribute("simple", "1");
            }
            else if (IsContextFree())
            {
                destination.EmitAttribute("simple", "2");
            }

            Start.Export(destination);
            GetStep().Export(destination);
            destination.EndElement();
        }

        //}
        //
        //
        public override string ToString()
        {
            return ExpressionTool.Parenthesize(Start) + "/" + ExpressionTool.Parenthesize(GetStep());
        }

        //}
        //
        //
        public override string ToShortString()
        {
            return ExpressionTool.ParenthesizeShort(Start) + "/" + ExpressionTool.ParenthesizeShort(GetStep());
        }

        //}
        //
        //
        private void GatherSteps(IList<Expression> list)
        {
            if (Start is SlashExpression)
            {
                ((SlashExpression)Start).GatherSteps(list);
            }
            else
            {
                list.Add(Start);
            }

            if (GetStep() is SlashExpression)
            {
                ((SlashExpression)GetStep()).GatherSteps(list);
            }
            else
            {
                list.Add(GetStep());
            }
        }

        //}
        //
        //
        private Expression RebuildSteps(IList<Expression> list)
        {
            if (list.Count == 1)
            {
                return list[0].Copy(new RebindingMap());
            }
            else
            {
                return new SlashExpression(list[0].Copy(new RebindingMap()), RebuildSteps(list.GetRange(1, (list.Count) - (1))));
            }
        }

        //}
        //
        //
        public virtual bool IsAbsolute()
        {
            return FirstStep.GetItemType().PrimitiveType == Types.Type.DOCUMENT;
        }

        //}
        //
        //
        // sic
        public override Elaborator GetElaborator()
        {
            return new SlashExprElaborator();
        }

        //}
        //
        //
        // sic
        /// <summary>
        /// Elaborator for a slash expression. (This actually corresponds to the "!" @operator, not to "/")
        /// </summary>
        internal sealed class SlashExprElaborator : PullElaborator
        {
            public override IPullEvaluator ElaborateForPull()
            {
                SlashExpression expr = (SlashExpression)GetExpression();
                IPullEvaluator select = expr.GetSelectExpression().MakeElaborator().ElaborateForPull();
                IPullEvaluator action = expr.GetActionExpression().MakeElaborator().ElaborateForPull();
                if (expr.contextFree && expr.GetStep() is AxisExpression)
                {
                    AxisExpression step = (AxisExpression)expr.GetStep();
                    return (context) => MappingIterator.IMap(select.Iterate(context), (item) => step.Iterate((NodeInfo)item));
                }
                else
                {
                    IContextMappingFunction mapper = (cxt) => action.Iterate(cxt);
                    return (context) =>
                    {
                        XPathContextMinor c2 = context.NewMinorContext();
                        c2.TrackFocus(select.Iterate(context));
                        return new ContextMappingIterator(mapper, c2);
                    };
                }
            }

            public override IPushEvaluator ElaborateForPush()
            {
                SlashExpression expr = (SlashExpression)GetExpression();
                IPullEvaluator select = expr.GetSelectExpression().MakeElaborator().ElaborateForPull();
                IPushEvaluator action = expr.GetActionExpression().MakeElaborator().ElaborateForPush();
                if (expr.contextFree && expr.GetStep() is AxisExpression)
                {
                    AxisExpression step = (AxisExpression)expr.GetStep();
                    return (@out, context) =>
                    {
                        ISequenceIterator outer = select.Iterate(context);
                        for (IItem a; (a = outer.Next()) != null;)
                        {
                            IAxisIterator inner = step.Iterate((NodeInfo)a);
                            for (NodeInfo b; (b = inner.Next()) != null;)
                            {
                                @out.Append(b, expr.GetLocation(), ReceiverOption.ALL_NAMESPACES);
                            }
                        }

                        return null;
                    };
                }
                else
                {
                    return (@out, context) =>
                    {
                        XPathContextMinor c2 = context.NewMinorContext();
                        IFocusIterator iter = c2.TrackFocus(select.Iterate(context));
                        ITailCall tc = null;
                        while (iter.Next() != null)
                        {
                            DispatchTailCall(tc);
                            tc = action.ProcessLeavingTail(@out, c2);
                        }

                        return tc;
                    };
                }
            }
        }
    }
}
