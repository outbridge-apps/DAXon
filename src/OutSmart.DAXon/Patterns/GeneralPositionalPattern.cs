////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Trees.Utilities;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System.Collections.Generic;

namespace OutSmart.DAXon.Patterns
{
    // Faithful port of net.sf.saxon.pattern.GeneralPositionalPattern (Saxon 12.9). Was a hollow stub whose
    // implicit conversion to Pattern threw, so any match pattern with a complex positional predicate
    // (match="x[position() gt 2]", match="x[last()]") crashed at compile.
    // A pattern of the form A[P] where A is an axis expression using the child axis and P depends on position.
    internal sealed class GeneralPositionalPattern : Pattern
    {
        private readonly NodeTest nodeTest;
        private Expression positionExpr;
        private CachedEvaluator<IItemEvaluator> positionEvaluator;   // matching runs per node tested
        private bool usesPosition = true;

        public override int Dependencies => positionExpr.Dependencies & (StaticProperty.DEPENDS_ON_LOCAL_VARIABLES | StaticProperty.DEPENDS_ON_USER_FUNCTIONS);

        public override int Fingerprint => nodeTest.Fingerprint;

        public GeneralPositionalPattern(NodeTest @base, Expression positionExpr)
        {
            this.nodeTest = @base;
            this.positionExpr = positionExpr;
        }

        public override IEnumerable<Operand> Operands()
        {
            return new Operand(this, positionExpr, OperandRole.FOCUS_CONTROLLED_ACTION);
        }

        public override Expression Simplify()
        {
            positionExpr = positionExpr.Simplify();
            return this;
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {
            // analyze each component of the pattern
            ContextItemStaticInfo cit = visitor.GetConfiguration().MakeContextItemStaticInfo(GetItemType(), false);
            positionExpr = positionExpr.TypeCheck(visitor, cit);
            positionExpr = ExpressionTool.UnsortedIfHomogeneous(positionExpr, false);
            return this;
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            Configuration config = visitor.GetConfiguration();
            ContextItemStaticInfo cit = config.MakeContextItemStaticInfo(GetItemType(), false);
            positionExpr = positionExpr.Optimize(visitor, cit);

            if (Literal.IsConstantBoolean(positionExpr, true))
            {
                return new NodeTestPattern(nodeTest);
            }
            else if (Literal.IsConstantBoolean(positionExpr, false))
            {
                // if a filter is constant false, the pattern doesn't match anything
                return new NodeTestPattern(ErrorType.GetInstance());
            }

            if ((positionExpr.Dependencies & StaticProperty.DEPENDS_ON_POSITION) == 0)
            {
                usesPosition = false;
            }

            // See if the expression is now known to be non-positional (see bugs 1908, 1992, test mode-0011)
            if (!FilterExpression.IsPositionalFilter(positionExpr, config.GetTypeHierarchy()))
            {
                int axis = AxisInfo.CHILD;
                if (nodeTest.PrimitiveType == OutSmart.DAXon.Types.Type.ATTRIBUTE)
                {
                    axis = AxisInfo.ATTRIBUTE;
                }
                else if (nodeTest.PrimitiveType == OutSmart.DAXon.Types.Type.NAMESPACE)
                {
                    axis = AxisInfo.NAMESPACE;
                }

                AxisExpression ae = new AxisExpression(axis, nodeTest);
                FilterExpression fe = new FilterExpression(ae, positionExpr);
                return ((Pattern)PatternMaker.FromExpression(fe, config, true)).TypeCheck(visitor, contextInfo);
            }

            return this;
        }

        public override int AllocateSlots(SlotManager slotManager, int nextFree)
        {
            return ExpressionTool.AllocateSlots(positionExpr, nextFree, slotManager);
        }

        public override bool Matches(IItem item, IXPathContext context)
        {
            return item is NodeInfo && MatchesBeneathAnchor((NodeInfo)item, null, context);
        }

        public override bool MatchesBeneathAnchor(NodeInfo node, NodeInfo anchor, IXPathContext context)
        {
            return InternalMatches(node, anchor, context);
        }

        /// <summary>
        /// Test whether the pattern matches, but without changing the current() node
        /// </summary>
        private bool InternalMatches(NodeInfo node, NodeInfo anchor, IXPathContext context)
        {
            if (!nodeTest.Test(node))
            {
                return false;
            }

            IXPathContext c2 = context.NewMinorContext();
            ManualIterator iter = new ManualIterator(node);
            c2.SetCurrentIterator(iter);

            try
            {
                IXPathContext c = c2;
                int actualPosition = -1;
                if (usesPosition)
                {
                    actualPosition = GetActualPosition(node, int.MaxValue, context.GetCurrentIterator());
                    ManualIterator man = new ManualIterator(node, actualPosition);
                    IXPathContext c3 = c2.NewMinorContext();
                    c3.SetCurrentIterator(man);
                    c = c3;
                }

                IItemEvaluator positionEval = CachedEvaluator<IItemEvaluator>.Get(ref positionEvaluator, positionExpr, e => e.ElaborateForItem());
                IItem predicate = positionEval(c);
                if (predicate is NumericValue)
                {
                    NumericValue position = (NumericValue)positionEval(context);
                    int requiredPos = position.AsSubscript();
                    if (actualPosition < 0 && requiredPos != -1)
                    {
                        actualPosition = GetActualPosition(node, requiredPos, context.GetCurrentIterator());
                    }

                    return requiredPos != -1 && actualPosition == requiredPos;
                }
                else
                {
                    return ExpressionTool.EffectiveBooleanValue(predicate);
                }
            }
            catch (XPathException.Circularity)
            {
                throw;
            }
            // No StackOverflow catch: RecursionDepthError (a foreign type since round BC) passes
            // through the XPathException catch below untouched; the old subtype is never thrown here.
            catch (XPathException e)
            {
                HandleDynamicError(e, c2);
                return false;
            }
        }

        private int GetActualPosition(NodeInfo node, int max, IFocusIterator iterator)
        {
            if (iterator is FocusTrackingIterator)
            {
                // This path makes use of cached information
                return ((FocusTrackingIterator)iterator).GetSiblingPosition(node, nodeTest, max);
            }

            return Navigator.GetSiblingPosition(node, nodeTest, max);
        }

        public override UType GetUType()
        {
            return nodeTest.GetUType();
        }

        public override ItemType GetItemType()
        {
            return nodeTest;
        }

        public override bool Equals(object other)
        {
            if (other is GeneralPositionalPattern)
            {
                GeneralPositionalPattern fp = (GeneralPositionalPattern)other;
                return nodeTest.Equals(fp.nodeTest) && positionExpr.IsEqual(fp.positionExpr);
            }
            else
            {
                return false;
            }
        }

        protected override int ComputeHashCode()
        {
            return nodeTest.GetHashCode() ^ positionExpr.GetHashCode();
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            GeneralPositionalPattern n = new GeneralPositionalPattern(nodeTest.Copy(), positionExpr.Copy(rebindings));
            ExpressionTool.CopyLocationInfo(this, n);
            n.OriginalText = OriginalText;
            return n;
        }

        public override string Reconstruct()
        {
            return nodeTest + "[" + positionExpr + "]";
        }

        public override void Export(ExpressionPresenter presenter)
        {
            presenter.StartElement("p.genPos");
            presenter.EmitAttribute("test", AlphaCode.FromItemType(nodeTest));
            if (!usesPosition)
            {
                // flag is this way around for backwards compatibility with 9.8
                presenter.EmitAttribute("flags", "P");
            }

            positionExpr.Export(presenter);
            presenter.EndElement();
        }
    }
}
