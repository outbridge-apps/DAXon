////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Expressions
{
    internal class VennExpression : BinaryExpression
    {

        public override string ExpressionName
        {
            get
            {
                switch (@operator)
                {
                    case Token.UNION:
                        return "union";
                    case Token.INTERSECT:
                        return "intersect";
                    case Token.EXCEPT:
                        return "except";
                    default:
                        return "unknown";
                }
            }
        }

        public override int ImplementationMethod => ITERATE_METHOD;

        public override string StreamerName => "VennExpression";
        public VennExpression(Expression p1, int op, Expression p2) : base(p1, op, p2)
        {
        }

        public override Expression Simplify()
        {

            // Force both operands to be sorted in document order. If this turns out to be unnecessary, it will
            // get optimized away
            if (!(GetLhsExpression() is DocumentSorter))
            {
                SetLhsExpression(new DocumentSorter(GetLhsExpression()));
            }

            if (!(GetRhsExpression() is DocumentSorter))
            {
                SetRhsExpression(new DocumentSorter(GetRhsExpression()));
            }

            base.Simplify();
            return this;
        }

        public override ItemType GetItemType()
        {
            ItemType t1 = GetLhsExpression().GetItemType();
            if (@operator == Token.UNION)
            {
                ItemType t2 = GetRhsExpression().GetItemType();
                TypeHierarchy th = GetConfiguration().GetTypeHierarchy();
                return Types.Type.GetCommonSuperType(t1, t2, th);
            }
            else
            {
                return t1;
            }
        }

        public override UType GetStaticUType(UType contextItemType)
        {
            switch (@operator)
            {
                case Token.UNION:
                    return GetLhsExpression().GetStaticUType(contextItemType).Union(GetRhsExpression().GetStaticUType(contextItemType));
                case Token.INTERSECT:
                    return GetLhsExpression().GetStaticUType(contextItemType).Intersection(GetRhsExpression().GetStaticUType(contextItemType));
                case Token.EXCEPT:
                default:
                    return GetLhsExpression().GetStaticUType(contextItemType);
            }
        }

        protected override int ComputeCardinality()
        {
            int c1 = GetLhsExpression().GetCardinality();
            int c2 = GetRhsExpression().GetCardinality();
            switch (@operator)
            {
                case Token.UNION:
                    if (Literal.IsEmptySequence(GetLhsExpression()))
                    {
                        return c2;
                    }

                    if (Literal.IsEmptySequence(GetRhsExpression()))
                    {
                        return c1;
                    }

                    return c1 | c2 | StaticProperty.ALLOWS_ONE | StaticProperty.ALLOWS_MANY;
                case Token.INTERSECT:
                    if (Literal.IsEmptySequence(GetLhsExpression()))
                    {
                        return StaticProperty.EMPTY;
                    }

                    if (Literal.IsEmptySequence(GetRhsExpression()))
                    {
                        return StaticProperty.EMPTY;
                    }

                    return (c1 & c2) | StaticProperty.ALLOWS_ZERO | StaticProperty.ALLOWS_ONE;
                case Token.EXCEPT:
                    if (Literal.IsEmptySequence(GetLhsExpression()))
                    {
                        return StaticProperty.EMPTY;
                    }

                    if (Literal.IsEmptySequence(GetRhsExpression()))
                    {
                        return c1;
                    }

                    return c1 | StaticProperty.ALLOWS_ZERO | StaticProperty.ALLOWS_ONE;
            }

            return StaticProperty.ALLOWS_ZERO_OR_MORE;
        }

        protected override int ComputeSpecialProperties()
        {
            int prop0 = GetLhsExpression().GetSpecialProperties();
            int prop1 = GetRhsExpression().GetSpecialProperties();
            int props = StaticProperty.ORDERED_NODESET;
            if (TestContextDocumentNodeSet(prop0, prop1))
            {
                props |= StaticProperty.CONTEXT_DOCUMENT_NODESET;
            }

            if (TestSubTree(prop0, prop1))
            {
                props |= StaticProperty.SUBTREE_NODESET;
            }

            if (CreatesNoNewNodes(prop0, prop1))
            {
                props |= StaticProperty.NO_NODES_NEWLY_CREATED;
            }

            return props;
        }

        private bool TestContextDocumentNodeSet(int prop0, int prop1)
        {
            switch (@operator)
            {
                case Token.UNION:
                    return (prop0 & prop1 & StaticProperty.CONTEXT_DOCUMENT_NODESET) != 0;
                case Token.INTERSECT:
                    return ((prop0 | prop1) & StaticProperty.CONTEXT_DOCUMENT_NODESET) != 0;
                case Token.EXCEPT:
                    return (prop0 & StaticProperty.CONTEXT_DOCUMENT_NODESET) != 0;
            }

            return false;
        }

        public virtual void GatherComponents(int @operator, HashSet<Expression> set)
        {
            if (GetLhsExpression() is VennExpression && ((VennExpression)GetLhsExpression()).@operator == @operator)
            {
                ((VennExpression)GetLhsExpression()).GatherComponents(@operator, set);
            }
            else
            {
                set.Add(GetLhsExpression());
            }

            if (GetRhsExpression() is VennExpression && ((VennExpression)GetRhsExpression()).@operator == @operator)
            {
                ((VennExpression)GetRhsExpression()).GatherComponents(@operator, set);
            }
            else
            {
                set.Add(GetRhsExpression());
            }
        }

        private bool TestSubTree(int prop0, int prop1)
        {
            switch (@operator)
            {
                case Token.UNION:
                    return (prop0 & prop1 & StaticProperty.SUBTREE_NODESET) != 0;
                case Token.INTERSECT:
                    return ((prop0 | prop1) & StaticProperty.SUBTREE_NODESET) != 0;
                case Token.EXCEPT:
                    return (prop0 & StaticProperty.SUBTREE_NODESET) != 0;
            }

            return false;
        }

        private bool CreatesNoNewNodes(int prop0, int prop1)
        {
            return (prop0 & StaticProperty.NO_NODES_NEWLY_CREATED) != 0 && (prop1 & StaticProperty.NO_NODES_NEWLY_CREATED) != 0;
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            Configuration config = visitor.GetConfiguration();
            TypeChecker tc = config.GetTypeChecker(false);
            Lhs.TypeCheck(visitor, contextInfo);
            Rhs.TypeCheck(visitor, contextInfo);
            if (!(GetLhsExpression() is Patterns.Pattern))
            {
                Func<RoleDiagnostic> role0 = () => new RoleDiagnostic(RoleDiagnostic.BINARY_EXPR, Token.tokens[@operator], 0);
                SetLhsExpression(tc.StaticTypeCheck(GetLhsExpression(), SequenceType.NODE_SEQUENCE, role0, visitor));
            }

            if (!(GetRhsExpression() is Patterns.Pattern))
            {
                Func<RoleDiagnostic> role1 = () => new RoleDiagnostic(RoleDiagnostic.BINARY_EXPR, Token.tokens[@operator], 1);
                SetRhsExpression(tc.StaticTypeCheck(GetRhsExpression(), SequenceType.NODE_SEQUENCE, role1, visitor));
            }


            // For the intersect and except operators, if the types are disjoint then we can simplify
            if (@operator != Token.UNION)
            {
                TypeHierarchy th = config.GetTypeHierarchy();
                ItemType t0 = GetLhsExpression().GetItemType();
                ItemType t1 = GetRhsExpression().GetItemType();
                if (th.Relationship(t0, t1) == Affinity.DISJOINT)
                {
                    if (@operator == Token.INTERSECT)
                    {
                        return Literal.MakeEmptySequence();
                    }
                    else
                    {
                        if (GetLhsExpression().HasSpecialProperty(StaticProperty.ORDERED_NODESET))
                        {
                            return GetLhsExpression();
                        }
                        else
                        {
                            return new DocumentSorter(GetLhsExpression());
                        }
                    }
                }
            }

            return this;
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {
            Expression e = base.Optimize(visitor, contextItemType);
            if (e != this)
            {
                return e;
            }

            Configuration config = visitor.GetConfiguration();
            TypeHierarchy th = config.GetTypeHierarchy();

            // If either operand is an empty sequence, simplify the expression. This can happen
            // after reduction with constructs of the form //a[condition] | //b[not(condition)],
            // common in XPath 1.0 because there were no conditional expressions.
            Expression lhs = GetLhsExpression();
            Expression rhs = GetRhsExpression();
            switch (@operator)
            {
                case Token.UNION:
                    if (Literal.IsEmptySequence(lhs) && (rhs.GetSpecialProperties() & StaticProperty.ORDERED_NODESET) != 0)
                    {
                        return rhs;
                    }

                    if (Literal.IsEmptySequence(rhs) && (lhs.GetSpecialProperties() & StaticProperty.ORDERED_NODESET) != 0)
                    {
                        return lhs;
                    }

                    if (ContextItemWithCurrentGroup(lhs, rhs))
                    {
                        return rhs;
                    }

                    if (ContextItemWithCurrentGroup(rhs, lhs))
                    {
                        return lhs;
                    }

                    break;
                case Token.INTERSECT:
                    if (Literal.IsEmptySequence(lhs))
                    {
                        return lhs;
                    }

                    if (Literal.IsEmptySequence(rhs))
                    {
                        return rhs;
                    }

                    if (ContextItemWithCurrentGroup(lhs, rhs))
                    {
                        return lhs;
                    }

                    if (ContextItemWithCurrentGroup(rhs, lhs))
                    {
                        return rhs;
                    }

                    break;
                case Token.EXCEPT:
                    if (Literal.IsEmptySequence(lhs))
                    {
                        return lhs;
                    }

                    if (Literal.IsEmptySequence(rhs) && (lhs.GetSpecialProperties() & StaticProperty.ORDERED_NODESET) != 0)
                    {
                        return lhs;
                    }

                    if (ContextItemWithCurrentGroup(lhs, rhs))
                    {
                        return Literal.MakeEmptySequence();
                    }

                    if (ContextItemWithCurrentGroup(rhs, lhs))
                    {

                        // Test case si-group-055.
                        // The streaming code has problems with (current-group() except .) so we
                        // optimize it away. This is a bit of a hack, because the difficulty may
                        // affect other expressions as well. The problem arises because part of the
                        // pattern needs to be evaluated with each node in the group as anchor node,
                        // and part with only the first node in the group as anchor node.
                        return new TailExpression(lhs, 2);
                    }

                    break;
            }


            // If both are axis expressions on the same axis, merge them
            // ie. rewrite (axis.test1 | axis.test2) as axis::(test1 | test2)
            if (lhs is AxisExpression && rhs is AxisExpression)
            {
                AxisExpression a1 = (AxisExpression)lhs;
                AxisExpression a2 = (AxisExpression)rhs;
                if (a1.Axis == a2.Axis)
                {
                    if (a1.GetNodeTest().Equals(a2.GetNodeTest()))
                    {
                        if (@operator == Token.EXCEPT)
                        {
                            return Literal.MakeEmptySequence();
                        }
                        else
                        {
                            return a1;
                        }
                    }
                    else
                    {
                        AxisExpression ax = new AxisExpression(a1.Axis, new CombinedNodeTest(a1.GetNodeTest(), @operator, a2.GetNodeTest()));
                        ExpressionTool.CopyLocationInfo(this, ax);
                        return ax;
                    }
                }
            }


            // If both are path expressions starting the same way, merge them
            // i.e. rewrite (/X | /Y) as /(X|Y). This applies recursively, so that
            // /A/B/C | /A/B/D becomes /A/B/child::(C|D)
            // This optimization was previously done for all three operators. However, it's not safe for "except":
            // A//B except A//C//B cannot be rewritten as A/descendant-or-self.node()/(B except C//B). As a quick
            // fix, the optimization has been retained for "union" but dropped for "intersect" and "except". Need to
            // do a more rigorous analysis of the conditions under which it is safe.
            // TODO: generalize this code to handle all distributive operators, and expressions involving multiple
            //   unions (p/x | p/y | p/z)
            if (lhs is SlashExpression && rhs is SlashExpression && @operator == Token.UNION)
            {
                SlashExpression path1 = (SlashExpression)lhs;
                SlashExpression path2 = (SlashExpression)rhs;
                if (path1.FirstStep.IsEqual(path2.FirstStep))
                {
                    VennExpression venn = new VennExpression(path1.RemainingSteps, @operator, path2.RemainingSteps);
                    ExpressionTool.CopyLocationInfo(this, venn);
                    Expression path = ExpressionTool.MakePathExpression(path1.FirstStep, venn);
                    ExpressionTool.CopyLocationInfo(this, path);
                    return path.Optimize(visitor, contextItemType);
                }
            }


            // Try merging two non-positional filter expressions:
            // A[exp0] | A[exp1] becomes A[exp0 or exp1]
            if (lhs is FilterExpression && rhs is FilterExpression)
            {
                FilterExpression exp0 = (FilterExpression)lhs;
                FilterExpression exp1 = (FilterExpression)rhs;
                if (!exp0.IsPositional(th) && !exp1.IsPositional(th) && exp0.GetSelectExpression().IsEqual(exp1.GetSelectExpression()))
                {
                    Expression filter;
                    switch (@operator)
                    {
                        case Token.UNION:
                            filter = new OrExpression(exp0.Filter, exp1.Filter);
                            break;
                        case Token.INTERSECT:
                            filter = new AndExpression(exp0.Filter, exp1.Filter);
                            break;
                        case Token.EXCEPT:
                            Expression negate2 = SystemFunction.MakeCall("not", GetRetainedStaticContext(), exp1.Filter);
                            filter = new AndExpression(exp0.Filter, negate2);
                            break;
                        default:
                            throw new InvalidOperationException("Unknown operator " + @operator);
                    }

                    ExpressionTool.CopyLocationInfo(this, filter);
                    FilterExpression f = new FilterExpression(exp0.GetSelectExpression(), filter);
                    ExpressionTool.CopyLocationInfo(this, f);
                    return f.Simplify().TypeCheck(visitor, contextItemType).Optimize(visitor, contextItemType);
                }
            }


            // Convert @*|node() into @*,node() to eliminate the sorted merge operation
            // Avoid doing this when streaming because xsl:value-of select="@*,node()" is not currently streamable
            if (!visitor.IsOptimizeForStreaming() && @operator == Token.UNION && lhs is AxisExpression && rhs is AxisExpression)
            {
                AxisExpression a0 = (AxisExpression)lhs;
                AxisExpression a1 = (AxisExpression)rhs;
                if (a0.Axis == AxisInfo.ATTRIBUTE && a1.Axis == AxisInfo.CHILD)
                {
                    return new Block(new Expression[] { lhs, rhs });
                }
                else if (a1.Axis == AxisInfo.ATTRIBUTE && a0.Axis == AxisInfo.CHILD)
                {
                    return new Block(new Expression[] { rhs, lhs });
                }
            }


            // Convert (A intersect B) to use a serial search where one operand is a singleton
            if (@operator == Token.INTERSECT && !Cardinality.AllowsMany(lhs.GetCardinality()))
            {
                return new SingletonIntersectExpression(lhs, @operator, rhs.Unordered(false, false));
            }

            if (@operator == Token.INTERSECT && !Cardinality.AllowsMany(rhs.GetCardinality()))
            {
                return new SingletonIntersectExpression(rhs, @operator, lhs.Unordered(false, false));
            }


            // If the types of the operands are disjoint, simplify "intersect" and "except"
            if (OperandsAreDisjoint(th))
            {
                if (@operator == Token.INTERSECT)
                {
                    return Literal.MakeEmptySequence();
                }
                else if (@operator == Token.EXCEPT)
                {
                    if ((lhs.GetSpecialProperties() & StaticProperty.ORDERED_NODESET) != 0)
                    {
                        return lhs;
                    }
                    else
                    {
                        return new DocumentSorter(lhs);
                    }
                }
            }

            return this;
        }

        private bool OperandsAreDisjoint(TypeHierarchy th)
        {
            return th.Relationship(GetLhsExpression().GetItemType(), GetRhsExpression().GetItemType()) == Affinity.DISJOINT;
        }

        private bool ContextItemWithCurrentGroup(Expression lhs, Expression rhs)
        {
            if (lhs is ContextItemExpression && rhs is CurrentGroupCall)
            {
                Expression focusSetter = ExpressionTool.GetFocusSettingContainer(lhs);
                Expression forEachGroup = ((CurrentGroupCall)rhs).ControllingInstruction;
                return forEachGroup != null && focusSetter == forEachGroup;
            }

            return false;
        }

        public override Expression Unordered(bool retainAllNodes, bool forStreaming)
        {
            if (@operator == Token.UNION && !forStreaming && OperandsAreDisjoint(GetConfiguration().GetTypeHierarchy()))
            {

                // replace union operator by comma operator to avoid cost of sorting into document order. See XMark q7
                Block block = new Block(new Expression[] { GetLhsExpression(), GetRhsExpression() });
                ExpressionTool.CopyLocationInfo(this, block);
                return block;
            }

            return this;
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            VennExpression exp = new VennExpression(GetLhsExpression().Copy(rebindings), @operator, GetRhsExpression().Copy(rebindings));
            ExpressionTool.CopyLocationInfo(this, exp);
            return exp;
        }

        protected override OperandRole GetOperandRole(int arg)
        {
            return OperandRole.SAME_FOCUS_ACTION;
        }

        public override bool Equals(object other)
        {

            // NOTE: it's possible that the method in the superclass is already adequate for this
            if (other is VennExpression)
            {
                VennExpression b = (VennExpression)other;
                if (@operator != b.@operator)
                {
                    return false;
                }

                if (GetLhsExpression().IsEqual(b.GetLhsExpression()) && GetRhsExpression().IsEqual(b.GetRhsExpression()))
                {
                    return true;
                }

                if (@operator == Token.UNION || @operator == Token.INTERSECT)
                {

                    // These are commutative and associative, so for example (A|B)|C equals B|(A|C)
                    HashSet<Expression> s0 = new HashSet<Expression>();
                    GatherComponents(@operator, s0);
                    HashSet<Expression> s1 = new HashSet<Expression>();
                    ((VennExpression)other).GatherComponents(@operator, s1);
                    return s0.Equals(s1);
                }
            }

            return false;
        }

        protected override int ComputeHashCode()
        {
            return GetLhsExpression().GetHashCode() ^ GetRhsExpression().GetHashCode();
        }

        public override Patterns.Pattern ToPattern(Configuration config)
        {
            if (IsPredicatePattern(GetLhsExpression()) || IsPredicatePattern(GetRhsExpression()))
            {
                throw new XPathException("Cannot use a predicate pattern as an operand of a union, intersect, or except operator", "XTSE0340");
            }

            if (@operator == Token.UNION)
            {
                return new UnionPattern(GetLhsExpression().ToPattern(config), GetRhsExpression().ToPattern(config));
            }
            else
            {

                // Bug #5368 means it's dangerous to assume that the expression (A except B) can be translated
                // into a pattern that matches a node if A matches and B does not. We can only do this in special
                // cases, in particular (a) where both operands use the attribute or child axis, and (b) where
                // one of the patterns is anchored at the root of the tree (for example //xxx/yyy or $var/xxx or id('x')/xxx)
                int commonAxis = ExpressionTool.GetAxisNavigation(this);
                if (commonAxis == AxisInfo.CHILD || commonAxis == AxisInfo.ATTRIBUTE || IndependentOfContextItem(GetLhsExpression()) || IndependentOfContextItem(GetRhsExpression()))
                {
                    if (@operator == Token.EXCEPT)
                    {
                        return new ExceptPattern(GetLhsExpression().ToPattern(config), GetRhsExpression().ToPattern(config));
                    }
                    else
                    {
                        return new IntersectPattern(GetLhsExpression().ToPattern(config), GetRhsExpression().ToPattern(config));
                    }
                }

                return new GeneralNodePattern(this, (NodeTest)GetItemType());
            }
        }

        private bool IndependentOfContextItem(Expression exp)
        {
            return (exp.Dependencies & StaticProperty.DEPENDS_ON_CONTEXT_ITEM) == 0;
        }

        private bool IsPredicatePattern(Expression exp)
        {
            if (exp is ItemChecker)
            {
                exp = ((ItemChecker)exp).BaseExpression;
            }

            return exp is FilterExpression && (((FilterExpression)exp).GetSelectExpression() is ContextItemExpression);
        }

        protected override string Tag()
        {
            if (@operator == Token.UNION)
            {
                return "union";
            }

            return Token.tokens[@operator];
        }

        public override ISequenceIterator Iterate(IXPathContext c)
        {
            switch (@operator)
            {
                case Token.UNION:
                    {

                        // If either of the operands is a union expression, then we merge its component
                        // iterators into a single multi-way union iterator
                        IList<ISequenceIterator> operands = new List<ISequenceIterator>();
                        GatherUnionLeafIterators(operands, c);
                        return (ISequenceIterator)new UnionIterator(operands, GlobalOrderComparer.GetInstance());
                    }

                case Token.INTERSECT:
                    {
                        ISequenceIterator i1 = GetLhsExpression().Iterate(c);
                        ISequenceIterator i2 = GetRhsExpression().Iterate(c);
                        return (ISequenceIterator)new IntersectionIterator(i1, i2, GlobalOrderComparer.GetInstance());
                    }

                case Token.EXCEPT:
                    {
                        ISequenceIterator i1 = GetLhsExpression().Iterate(c);
                        ISequenceIterator i2 = GetRhsExpression().Iterate(c);
                        return (ISequenceIterator)new DifferenceIterator(i1, i2, GlobalOrderComparer.GetInstance());
                    }
            }

            throw new NotSupportedException("Unknown operator in Venn Expression");
        }

        private void GatherUnionLeafIterators(IList<ISequenceIterator> leafIterators, IXPathContext context)
        {
            Expression e1 = GetLhsExpression();
            if (e1 is VennExpression && ((VennExpression)e1).@operator == Token.UNION)
            {
                ((VennExpression)e1).GatherUnionLeafIterators(leafIterators, context);
            }
            else
            {
                leafIterators.Add(e1.Iterate(context));
            }

            Expression e2 = GetRhsExpression();
            if (e2 is VennExpression && ((VennExpression)e2).@operator == Token.UNION)
            {
                ((VennExpression)e2).GatherUnionLeafIterators(leafIterators, context);
            }
            else
            {
                leafIterators.Add(e2.Iterate(context));
            }
        }

        public override bool EffectiveBooleanValue(IXPathContext context)
        {
            if (@operator == Token.UNION)
            {

                // NOTE: this optimization was probably already done statically
                return GetLhsExpression().EffectiveBooleanValue(context) || GetRhsExpression().EffectiveBooleanValue(context);
            }
            else
            {
                return base.EffectiveBooleanValue(context);
            }
        }

        public override Elaborator GetElaborator()
        {
            return new VennElaborator();
        }

        internal sealed class VennElaborator : PullElaborator
        {
            public override IPullEvaluator ElaborateForPull()
            {
                VennExpression exp = (VennExpression)GetExpression();
                if (exp.Operator == Token.UNION)
                {
                    IList<IPullEvaluator> leafEvaluators = new List<IPullEvaluator>();
                    GatherUnionLeafEvaluators(exp, leafEvaluators);
                    return (context) =>
                    {
                        IList<ISequenceIterator> iterators = new List<ISequenceIterator>(leafEvaluators.Count);
                        foreach (IPullEvaluator evaluator in leafEvaluators)
                        {
                            iterators.Add(evaluator.Iterate(context));
                        }

                        return (ISequenceIterator)new UnionIterator(iterators, GlobalOrderComparer.GetInstance());
                    };
                }
                else
                {
                    IPullEvaluator p1 = exp.GetLhsExpression().MakeElaborator().ElaborateForPull();
                    IPullEvaluator p2 = exp.GetRhsExpression().MakeElaborator().ElaborateForPull();
                    if (exp.Operator == Token.INTERSECT)
                    {
                        return (context) => (ISequenceIterator)new IntersectionIterator(p1.Iterate(context), p2.Iterate(context), GlobalOrderComparer.GetInstance());
                    }
                    else
                    {
                        return (context) => (ISequenceIterator)new DifferenceIterator(p1.Iterate(context), p2.Iterate(context), GlobalOrderComparer.GetInstance());
                    }
                }
            }

            private static void GatherUnionLeafEvaluators(VennExpression exp, IList<IPullEvaluator> leafEvaluators)
            {
                ExpressionTool.ProbeTreeDepth();
                Expression e1 = exp.GetLhsExpression();
                if (e1 is VennExpression && ((VennExpression)e1).Operator == Token.UNION)
                {
                    GatherUnionLeafEvaluators(((VennExpression)e1), leafEvaluators);
                }
                else
                {
                    leafEvaluators.Add(e1.MakeElaborator().ElaborateForPull());
                }

                Expression e2 = exp.GetRhsExpression();
                if (e2 is VennExpression && ((VennExpression)e2).Operator == Token.UNION)
                {
                    GatherUnionLeafEvaluators(((VennExpression)e2), leafEvaluators);
                }
                else
                {
                    leafEvaluators.Add(e2.MakeElaborator().ElaborateForPull());
                }
            }
        }
    }
}
