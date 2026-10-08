////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
//import com.saxonica.ee.stream.Posture;
//import com.saxonica.ee.stream.Streamability;
//import com.saxonica.ee.stream.Sweep;
//import com.saxonica.ee.trans.ContextItemStaticInfoEE;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Patterns
{
    public class NodeSetPattern : Pattern
    {
        private readonly Operand selectionOp;
        private ItemType itemType;
        private CachedEvaluator<IPullEvaluator> selectionEvaluator;   // matching runs per node tested

        public virtual Expression SelectionExpression => selectionOp.GetChildExpression();

        public override int Dependencies => SelectionExpression.Dependencies;
        public NodeSetPattern(Expression exp)
        {
            selectionOp = new Operand(this, exp, OperandRole.NAVIGATE);
        }

        public override IEnumerable<Operand> Operands()
        {
            return selectionOp;
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {
            selectionOp.SetChildExpression(SelectionExpression.TypeCheck(visitor, contextItemType));
            Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.MATCH_PATTERN, SelectionExpression.ToString(), 0);
            TypeChecker tc = visitor.GetConfiguration().GetTypeChecker(false);
            Expression @checked;
            try
            {
                @checked = tc.StaticTypeCheck(SelectionExpression, SequenceType.NODE_SEQUENCE, role, visitor);
            }
            catch (XPathException e)
            {
                visitor.IssueWarning("Pattern will never match anything. " + e.Message, DAXonErrorCode.SXWN9015, GetLocation());
                @checked = Literal.MakeEmptySequence();
            }

            selectionOp.SetChildExpression(@checked);
            itemType = SelectionExpression.GetItemType();
            return this;
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            visitor.ObtainOptimizer().OptimizeNodeSetPattern(this);
            return this;
        }

        public virtual void SetItemType(ItemType type)
        {
            this.itemType = type;
        }

        public override int AllocateSlots(SlotManager slotManager, int nextFree)
        {
            return ExpressionTool.AllocateSlots(SelectionExpression, nextFree, slotManager);
        }

        public override ISequenceIterator SelectNodes(ITreeInfo doc, IXPathContext context)
        {
            IXPathContext c2 = context.NewMinorContext();
            ManualIterator mi = new ManualIterator(doc.GetRootNode());
            c2.SetCurrentIterator(mi);
            return SelectionExpression.Iterate(c2);
        }

        public override bool Matches(IItem item, IXPathContext context)
        {
            if (item is NodeInfo)
            {
                try
                {
                    Expression exp = SelectionExpression;
                    if (exp is GlobalVariableReference)
                    {
                        // Upstream relies on the covariant GroundedValue return; global variables always ground.
                        IGroundedValue value = (IGroundedValue)((GlobalVariableReference)exp).EvaluateVariable(context);
                        return value.ContainsNode((NodeInfo)item);
                    }
                    else
                    {
                        ISequenceIterator iter = CachedEvaluator<IPullEvaluator>.Get(ref selectionEvaluator, exp, e => e.ElaborateForPull())(context);
                        return SingletonIntersectExpression.ContainsNode(iter, (NodeInfo)item);
                    }
                }
                catch (XPathException.Circularity)
                {
                    throw;
                }
                // No StackOverflow catch: RecursionDepthError (a foreign type since round BC) is not
                // an XPathException, so it propagates untouched; the old subtype is never thrown here.
                catch (XPathException)
                {

                    // treat pattern matching errors as a non-match
                    return false;
                }
                catch (UncheckedXPathException)
                {

                    // treat pattern matching errors as a non-match
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        public override UType GetUType()
        {
            return GetItemType().GetUType();
        }

        /// <summary>
        /// Get a NodeTest that all the nodes matching this pattern must satisfy
        /// </summary>
        public override ItemType GetItemType()
        {
            if (itemType == null)
            {
                itemType = SelectionExpression.GetItemType();
            }

            if (itemType is NodeTest)
            {
                return itemType;
            }
            else
            {
                return AnyNodeTest.GetInstance();
            }
        }

        /// <summary>
        /// Get a NodeTest that all the nodes matching this pattern must satisfy
        /// </summary>
        public override bool Equals(object other)
        {
            return (other is NodeSetPattern) && ((NodeSetPattern)other).SelectionExpression.IsEqual(SelectionExpression);
        }

        protected override int ComputeHashCode()
        {
            return 0x73108728 ^ SelectionExpression.GetHashCode();
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            NodeSetPattern n = new NodeSetPattern(SelectionExpression.Copy(rebindings));
            ExpressionTool.CopyLocationInfo(this, n);
            n.OriginalText = OriginalText;
            return n;
        }

        public override void Export(ExpressionPresenter presenter)
        {
            presenter.StartElement("p.nodeSet");
            if (itemType != null)
            {
                presenter.EmitAttribute("test", AlphaCode.FromItemType(itemType));
            }

            SelectionExpression.Export(presenter);
            presenter.EndElement();
        }
    }
}
