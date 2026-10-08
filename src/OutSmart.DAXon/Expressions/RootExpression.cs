////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Types;
namespace OutSmart.DAXon.Expressions
{
    internal sealed class RootExpression : Expression
    {
        private bool contextMaybeUndefined = true;
        private bool doneWarnings = false;

        public override int ImplementationMethod => EVALUATE_METHOD;

        public override int IntrinsicDependencies => StaticProperty.DEPENDS_ON_CONTEXT_DOCUMENT;

        public override string ExpressionName => "root";

        public override string StreamerName => "RootExpression";
        public RootExpression()
        {
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            TypeHierarchy th = visitor.GetConfiguration().GetTypeHierarchy();
            if (contextInfo == null || contextInfo.GetItemType() == null || contextInfo.GetItemType().Equals(ErrorType.GetInstance()))
            {
                throw new XPathException(NoContextMessage() + ": the context item is absent").WithErrorCode("XPDY0002").AsTypeError().WithLocation(GetLocation());
            }
            else if (!doneWarnings && contextInfo.IsParentless() && th.Relationship(contextInfo.GetItemType(), NodeKindTest.DOCUMENT) == Affinity.DISJOINT)
            {
                visitor.IssueWarning(NoContextMessage() + ": the context item is parentless and is not a document node", DAXonErrorCode.SXWN9026, GetLocation());
                doneWarnings = true;
            }

            contextMaybeUndefined = contextInfo.IsPossiblyAbsent();
            if (th.IsSubType(contextInfo.GetItemType(), NodeKindTest.DOCUMENT))
            {

                // this rewrite is important for streamability analysis
                ContextItemExpression cie = new ContextItemExpression();
                ExpressionTool.CopyLocationInfo(this, cie);
                cie.SetStaticInfo(contextInfo);
                return cie;
            }

            Affinity relation = th.Relationship(contextInfo.GetItemType(), AnyNodeTest.GetInstance());
            if (relation == Affinity.DISJOINT)
            {
                throw new XPathException(NoContextMessage() + ": the context item is not a node").WithErrorCode("XPTY0020").AsTypeError().WithLocation(GetLocation());
            }

            return this;
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {

            // repeat the check: in XSLT insufficient information is available the first time
            return TypeCheck(visitor, contextItemType);
        }

        protected override int ComputeSpecialProperties()
        {
            return StaticProperty.ORDERED_NODESET | StaticProperty.CONTEXT_DOCUMENT_NODESET | StaticProperty.SINGLE_DOCUMENT_NODESET | StaticProperty.NO_NODES_NEWLY_CREATED;
        }

        private string NoContextMessage()
        {
            return "Leading '/' selects nothing";
        }

        /// <summary>
        /// Is this expression the same as another expression?
        /// </summary>
        public override bool Equals(object other)
        {
            return other is RootExpression;
        }

        protected override int ComputeCardinality()
        {
            return StaticProperty.EXACTLY_ONE;
        }

        public override ItemType GetItemType()
        {
            return NodeKindTest.DOCUMENT;
        }

        public override UType GetStaticUType(UType contextItemType)
        {
            return UType.DOCUMENT;
        }

        protected override int ComputeHashCode()
        {
            return "RootExpression".GetHashCode();
        }

        public NodeInfo GetNode(IXPathContext context)
        {
            IItem current = context.GetContextItem();
            if (current == null)
            {
                DynamicError("Finding root of tree: the context item is absent", "XPDY0002", context);
            }

            if (current is NodeInfo)
            {
                NodeInfo doc = ((NodeInfo)current).Root;
                if (doc.GetNodeKind() != Types.Type.DOCUMENT)
                {
                    DynamicError("The root of the tree containing the context item is not a document node", "XPDY0050", context);
                }

                return doc;
            }

            TypeError("Finding root of tree: the context item is not a node", "XPTY0020", context);

            // dummy return; we never get here
            return null;
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            RootExpression exp = new RootExpression();
            ExpressionTool.CopyLocationInfo(this, exp);
            return exp;
        }

        public override Patterns.Pattern ToPattern(Configuration config)
        {
            return new NodeTestPattern(NodeKindTest.DOCUMENT);
        }

        public override PathMap.PathMapNodeSet AddToPathMap(PathMap pathMap, PathMap.PathMapNodeSet pathMapNodeSet)
        {
            if (pathMapNodeSet == null)
            {
                ContextItemExpression cie = new ContextItemExpression();
                ExpressionTool.CopyLocationInfo(this, cie);
                pathMapNodeSet = new PathMap.PathMapNodeSet(pathMap.MakeNewRoot(cie));
            }

            return pathMapNodeSet.CreateArc(AxisInfo.ANCESTOR_OR_SELF, NodeKindTest.DOCUMENT);
        }

        public override string ToString()
        {
            return "(/)";
        }

        public override void Export(ExpressionPresenter destination)
        {
            destination.StartElement("root", this);
            destination.EndElement();
        }

        public override ISequenceIterator Iterate(IXPathContext context)
        {
            return SingletonIterator.MakeIterator(GetNode(context));
        }

        public override IItem EvaluateItem(IXPathContext context)
        {
            return GetNode(context);
        }

        public override bool EffectiveBooleanValue(IXPathContext context)
        {
            return GetNode(context) != null;
        }

        public override Elaborator GetElaborator()
        {
            return new RootExprElaborator();
        }

        /// <summary>
        /// Elaborator for a root expression ({@code /})
        /// </summary>
        internal class RootExprElaborator : ItemElaborator
        {
            public override IItemEvaluator ElaborateForItem()
            {
                RootExpression expr = (RootExpression)GetExpression();
                return (context) => expr.GetNode(context);
            }
        }
    }
}
