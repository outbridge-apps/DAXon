////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Trees.Tiny;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Collections;
using System;
using System.Collections.Generic;
using System.Text;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Types;
namespace OutSmart.DAXon.Expressions
{
    internal sealed class AxisExpression : Expression
    {
        private int axis;
        private NodeTest test;
        private Types.ItemType itemType;
        private ContextItemStaticInfo staticInfo = ContextItemStaticInfo.DEFAULT;
        private bool doneTypeCheck;
        private bool doneOptimize;

        public override string ExpressionName => "axisStep";

        /*
     * Get a string representation of a name to use in diagnostics
     */
        public Types.ItemType ContextItemType => staticInfo.GetItemType();

        /*
     * Get a string representation of a name to use in diagnostics
     */
        public override double Cost
        {
            get
            {
                switch (axis)
                {
                    case AxisInfo.SELF:
                    case AxisInfo.PARENT:
                    case AxisInfo.ATTRIBUTE:
                        return 1;
                    case AxisInfo.CHILD:
                    case AxisInfo.FOLLOWING_SIBLING:
                    case AxisInfo.PRECEDING_SIBLING:
                    case AxisInfo.ANCESTOR:
                    case AxisInfo.ANCESTOR_OR_SELF:
                        return 5;
                    default:
                        return 20;
                }
            }
        }

        /*
     * Get a string representation of a name to use in diagnostics
     */
        public override int IntrinsicDependencies => StaticProperty.DEPENDS_ON_CONTEXT_ITEM;

        /*
     * Get a string representation of a name to use in diagnostics
     */
        public int Axis { get => axis; set => this.axis = value; }

        /*
     * Get a string representation of a name to use in diagnostics
     */
        public override int ImplementationMethod => ITERATE_METHOD;

        /*
     * Get a string representation of a name to use in diagnostics
     */
        public override string StreamerName => "AxisExpression";
        public AxisExpression(int axis, NodeTest nodeTest)
        {
            this.axis = axis;
            this.test = nodeTest;
        }

        /// <summary>
        /// Simplify an expression
        /// </summary>
        public override Expression Simplify()
        {
            Expression e2 = base.Simplify();
            if (e2 != this)
            {
                return e2;
            }

            if ((test == null || test == AnyNodeTest.GetInstance()) && (axis == AxisInfo.PARENT || axis == AxisInfo.ANCESTOR))
            {

                // get more precise type information for parent/ancestor nodes
                test = MultipleNodeKindTest.PARENT_NODE;
            }

            return this;
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            Types.ItemType contextItemType = contextInfo.GetItemType();
            bool noWarnings = doneOptimize || (doneTypeCheck && this.staticInfo.GetItemType().Equals(contextItemType));
            doneTypeCheck = true;
            if (contextItemType == ErrorType.GetInstance())
            {

                // There is no context item. In principle we could raise XPTY0020 ("Context item is not a node"),
                // which is a type error and therefore can be thrown statically. But many test cases expect
                // XPDY0002 ("Context item absent") which for inexplicable reasons is a dynamic error rather than
                // a type error, and therefore cannot be raised until execution time.
                throw new XPathException("Axis step " + this + " cannot be used here: the context item is absent").WithErrorCode("XPDY0002").WithLocation(GetLocation());
            }
            else
            {
                staticInfo = contextInfo;
            }

            Configuration config = visitor.GetConfiguration();
            if ((Genre)contextItemType.GetGenre() != Genre.NODE)
            {
                TypeHierarchy th = config.GetTypeHierarchy();
                Affinity relation = th.Relationship(contextItemType, AnyNodeTest.GetInstance());
                if (relation == Affinity.DISJOINT)
                {
                    throw new XPathException("Axis step " + this + " cannot be used here: the context item is not a node").AsTypeError().WithErrorCode("XPTY0020").WithLocation(GetLocation());
                }
                else if (relation == Affinity.OVERLAPS || relation == Affinity.SUBSUMES)
                {

                    // need to insert a dynamic check of the context item type
                    Expression thisExp = CheckPlausibility(visitor, contextInfo, !noWarnings);
                    if (Literal.IsEmptySequence(thisExp))
                    {
                        return thisExp;
                    }

                    ContextItemExpression exp = new ContextItemExpression();
                    ExpressionTool.CopyLocationInfo(this, exp);
                    Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.AXIS_STEP, "", axis, "XPTY0020");
                    ItemChecker checker = new ItemChecker(exp, AnyNodeTest.GetInstance(), role);
                    ExpressionTool.CopyLocationInfo(this, checker);
                    SimpleStepExpression step = new SimpleStepExpression(checker, thisExp);
                    ExpressionTool.CopyLocationInfo(this, step);
                    return step;
                }
            }

            if (visitor.StaticContext.GetOptimizerOptions().IsSet(OptimizerOptions.VOID_EXPRESSIONS))
            {
                return CheckPlausibility(visitor, contextInfo, !noWarnings);
            }
            else
            {
                return this;
            }
        }

        private Expression CheckPlausibility(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo, bool warnings)
        {
            IStaticContext env = visitor.StaticContext;
            Configuration config = env.GetConfiguration();
            Types.ItemType contextType = contextInfo.GetItemType();
            if (!(contextType is NodeTest))
            {
                contextType = AnyNodeTest.GetInstance();
            }


            // New code in terms of UTypes
            // Test whether the requested nodetest is consistent with the requested axis
            if (test != null && !AxisInfo.GetTargetUType(UType.ANY_NODE, axis).Overlaps(test.GetUType()))
            {
                if (warnings)
                {
                    visitor.IssueWarning("The " + AxisInfo.axisName[axis] + " axis will never select " + test.GetUType().ToStringWithIndefiniteArticle(), DAXonErrorCode.SXWN9037, GetLocation());
                }

                return Literal.MakeEmptySequence();
            }

            if (test is NameTest && axis == AxisInfo.NAMESPACE && !((NameTest)test).GetNamespaceURI().IsEmpty())
            {
                if (warnings)
                {
                    visitor.IssueWarning("The names of namespace nodes are never prefixed, so this axis step will never select anything", DAXonErrorCode.SXWN9037, GetLocation());
                }

                return Literal.MakeEmptySequence();
            }


            // Test whether the axis ever selects anything, when starting at this context node
            UType originUType = contextType.GetUType();
            UType targetUType = AxisInfo.GetTargetUType(originUType, axis);
            UType testUType = test == null ? UType.ANY_NODE : test.GetUType();
            if (targetUType.Equals(UType.VOID))
            {
                if (warnings)
                {
                    visitor.IssueWarning("The " + AxisInfo.axisName[axis] + " axis starting at " + originUType.ToStringWithIndefiniteArticle() + " will never select anything", DAXonErrorCode.SXWN9037, GetLocation());
                }

                return Literal.MakeEmptySequence();
            }

            if (contextInfo.IsParentless() && (axis == AxisInfo.PARENT || axis == AxisInfo.ANCESTOR))
            {
                if (warnings)
                {
                    visitor.IssueWarning("The " + AxisInfo.axisName[axis] + " axis will never select anything because the context item is parentless", DAXonErrorCode.SXWN9037, GetLocation());
                }

                return Literal.MakeEmptySequence();
            }


            // Test whether the axis ever selects a node of the right kind, when starting at this context node
            if (!targetUType.Overlaps(testUType))
            {
                if (warnings)
                {
                    visitor.IssueWarning("The " + AxisInfo.axisName[axis] + " axis starting at " + originUType.ToStringWithIndefiniteArticle() + " will never select " + test.GetUType().ToStringWithIndefiniteArticle(), DAXonErrorCode.SXWN9037, GetLocation());
                }

                return Literal.MakeEmptySequence();
            }


            // For an X-or-self axis, if X never selects anything, then substitute the self axis.
            int nonSelf = AxisInfo.excludeSelfAxis[axis];
            UType kind = test == null ? UType.ANY_NODE : test.GetUType();
            if (axis != nonSelf)
            {
                UType nonSelfTarget = AxisInfo.GetTargetUType(originUType, nonSelf);
                if (!nonSelfTarget.Overlaps(testUType))
                {
                    axis = AxisInfo.SELF;
                    targetUType = AxisInfo.GetTargetUType(originUType, axis);
                }
            }

            Types.ItemType target = targetUType.ToItemType();
            if (test == null || test is AnyNodeTest)
            {
                itemType = target;
            }
            else if (target is AnyNodeTest || targetUType.Subsumes(test.GetUType()))
            {
                itemType = test;
            }
            else
            {
                itemType = new CombinedNodeTest((NodeTest)target, Token.INTERSECT, test);
            }

            int origin = contextType.PrimitiveType;
            if (test != null)
            {

                // If the content type of the context item is known, see whether the node test can select anything
                if (contextType is DocumentNodeTest && kind.Equals(UType.ELEMENT))
                {
                    NodeTest elementTest = ((DocumentNodeTest)contextType).ElementTest;
                    IntSet outermostElementNames = elementTest.RequiredNodeNames;
                    if (outermostElementNames != null)
                    {
                        IntSet selectedElementNames = test.RequiredNodeNames;
                        if (selectedElementNames != null)
                        {
                            if (axis == AxisInfo.CHILD)
                            {

                                // check that the name appearing in the step is one of the names allowed by the nodetest
                                if (selectedElementNames.Intersect(outermostElementNames).IsEmpty())
                                {
                                    if (warnings)
                                    {
                                        visitor.IssueWarning("Starting at a document node, the step is selecting an element whose name " + "is not among the names of child elements permitted for this document node type", DAXonErrorCode.SXWN9037, GetLocation());
                                    }

                                    return Literal.MakeEmptySequence();
                                }

                                if (env.GetPackageData().IsSchemaAware() && elementTest is ISchemaNodeTest && outermostElementNames.Count == 1)
                                {
                                    IIntIterator oeni = outermostElementNames.IIterator();
                                    int outermostElementName = oeni.MoveNext() ? oeni.Current : -1;
                                    ISchemaDeclaration decl = config.GetElementDeclaration(outermostElementName);
                                    if (decl == null)
                                    {
                                        if (warnings)
                                        {
                                            visitor.IssueWarning("Element " + config.GetNamePool().GetEQName(outermostElementName) + " is not declared in the schema", DAXonErrorCode.SXWN9037, GetLocation());
                                        }

                                        itemType = elementTest;
                                    }
                                    else
                                    {
                                        itemType = new CombinedNodeTest(elementTest, Token.INTERSECT, new ContentTypeTest(Types.Type.ELEMENT, decl.GetType(), config, true));
                                    }
                                }
                                else
                                {
                                    itemType = elementTest;
                                }

                                return this;
                            }
                            else if (axis == AxisInfo.DESCENDANT)
                            {

                                // check that the name appearing in the step is one of the names allowed by the nodetest
                                bool canMatchOutermost = !selectedElementNames.Intersect(outermostElementNames).IsEmpty();
                                if (!canMatchOutermost)
                                {

                                    // The expression /descendant.x starting at the document node doesn't match the outermost
                                    // element, so replace it by child::*/descendant.x, and check that
                                    Expression path = ExpressionTool.MakePathExpression(new AxisExpression(AxisInfo.CHILD, elementTest), new AxisExpression(AxisInfo.DESCENDANT, test));
                                    ExpressionTool.CopyLocationInfo(this, path);
                                    return path.TypeCheck(visitor, contextInfo);
                                }
                            }
                        }
                    }
                }

                ISchemaType contentType = ((NodeTest)contextType).ContentType;
                if (contentType == AnyType.INSTANCE)
                {

                    // fast exit in non-schema-aware case
                    return this;
                }

                if (!env.GetPackageData().IsSchemaAware())
                {
                    ISchemaType ct = test.ContentType;
                    if (!(ct == AnyType.INSTANCE || ct == Untyped.INSTANCE || ct == AnySimpleType.INSTANCE || ct == BuiltInAtomicType.ANY_ATOMIC || ct == BuiltInAtomicType.UNTYPED_ATOMIC || ct == BuiltInAtomicType.STRING))
                    {
                        if (warnings)
                        {
                            visitor.IssueWarning("The " + AxisInfo.axisName[axis] + " axis will never select any typed nodes, " + "because the expression is being compiled in an environment that is not schema-aware", DAXonErrorCode.SXWN9037, GetLocation());
                        }

                        return Literal.MakeEmptySequence();
                    }
                }

                int targetfp = test.Fingerprint;
                StructuredQName targetName = test.MatchingNodeName;
                if (contentType.IsSimpleType())
                {
                    if (warnings)
                    {
                        if ((axis == AxisInfo.CHILD || axis == AxisInfo.DESCENDANT || axis == AxisInfo.DESCENDANT_OR_SELF) && UType.PARENT_NODE_KINDS.Union(UType.ATTRIBUTE).Subsumes(kind))
                        {
                            visitor.IssueWarning("The " + AxisInfo.axisName[axis] + " axis will never select any " + kind + " nodes when starting at " + (origin == Types.Type.ATTRIBUTE ? "an attribute node" : GetStartingNodeDescription(contentType)), DAXonErrorCode.SXWN9037, GetLocation());
                        }
                        else if (axis == AxisInfo.CHILD && kind.Equals(UType.TEXT) && (ParentExpression is Atomizer))
                        {
                            visitor.IssueWarning("Selecting the text nodes of an element with simple content may give the " + "wrong answer in the presence of comments or processing instructions. It is usually " + "better to omit the '/text()' step", DAXonErrorCode.SXWN9037, GetLocation());
                        }
                        else if (axis == AxisInfo.ATTRIBUTE)
                        {
                            bool found = false;
                            if (targetfp == -1)
                            {
                                foreach (ISchemaType extension in config.GetExtensionsOfType(contentType))
                                {
                                    if (((IComplexType)extension).AllowsAttributes())
                                    {
                                        found = true;
                                        break;
                                    }
                                }
                            }
                            else
                            {
                                foreach (ISchemaType extension in config.GetExtensionsOfType(contentType))
                                {
                                    try
                                    {
                                        if (((IComplexType)extension).GetAttributeUseType(targetName) != null)
                                        {
                                            found = true;
                                            break;
                                        }
                                    }
                                    catch (SchemaException)
                                    {
                                    }
                                }
                            }

                            if (!found)
                            {
                                visitor.IssueWarning("The " + AxisInfo.axisName[axis] + " axis will never select " + (targetName == null ? "any attribute nodes" : "an attribute node named " + GetDiagnosticName(targetName, env)) + " when starting at " + GetStartingNodeDescription(contentType), DAXonErrorCode.SXWN9037, GetLocation()); // Despite the warning, leave the expression unchanged. This is because
                                // we don't necessarily know about all extended types at compile time:
                                // in particular, we don't seal the XML Schema namespace to block extensions
                                // of built-in types
                            }
                        }
                    }
                }
                else if (((IComplexType)contentType).IsSimpleContent() && (axis == AxisInfo.CHILD || axis == AxisInfo.DESCENDANT || axis == AxisInfo.DESCENDANT_OR_SELF) && UType.PARENT_NODE_KINDS.Subsumes(kind))
                {

                    // We don't need to consider extended types here, because a type with complex content
                    // can never be defined as an extension of a type with simple content
                    if (warnings)
                    {
                        visitor.IssueWarning("The " + AxisInfo.axisName[axis] + " axis will never select any " + kind + " nodes when starting at " + GetStartingNodeDescription(contentType) + ", as this type requires simple content", DAXonErrorCode.SXWN9037, GetLocation());
                    }

                    return Literal.MakeEmptySequence();
                }
                else if (((IComplexType)contentType).IsEmptyContent() && (axis == AxisInfo.CHILD || axis == AxisInfo.DESCENDANT || axis == AxisInfo.DESCENDANT_OR_SELF))
                {
                    foreach (ISchemaType extension in config.GetExtensionsOfType(contentType))
                    {
                        if (!((IComplexType)extension).IsEmptyContent())
                        {
                            return this;
                        }
                    }

                    if (warnings)
                    {
                        visitor.IssueWarning("The " + AxisInfo.axisName[axis] + " axis will never select any" + " nodes when starting at " + GetStartingNodeDescription(contentType) + ", as this type requires empty content", DAXonErrorCode.SXWN9037, GetLocation());
                    }

                    return Literal.MakeEmptySequence();
                }
                else if (axis == AxisInfo.ATTRIBUTE)
                {
                    if (targetfp == -1)
                    {
                        if (warnings)
                        {
                            if (!((IComplexType)contentType).AllowsAttributes())
                            {
                                visitor.IssueWarning("The complex type " + contentType.Description + " allows no attributes other than the standard attributes in the xsi namespace", DAXonErrorCode.SXWN9037, GetLocation());
                            }
                        }
                    }
                    else
                    {
                        try
                        {
                            ISchemaType schemaType;
                            if (targetfp == StandardNames.XSI_TYPE)
                            {
                                schemaType = BuiltInAtomicType.QNAME;
                            }
                            else if (targetfp == StandardNames.XSI_SCHEMA_LOCATION)
                            {
                                schemaType = (ISchemaType)BuiltInListType.ANY_URIS;
                            }
                            else if (targetfp == StandardNames.XSI_NO_NAMESPACE_SCHEMA_LOCATION)
                            {
                                schemaType = BuiltInAtomicType.ANY_URI;
                            }
                            else if (targetfp == StandardNames.XSI_NIL)
                            {
                                schemaType = BuiltInAtomicType.BOOLEAN;
                            }
                            else
                            {
                                schemaType = ((IComplexType)contentType).GetAttributeUseType(targetName);
                            }

                            if (schemaType == null)
                            {
                                if (warnings)
                                {
                                    visitor.IssueWarning("The complex type " + contentType.Description + " does not allow an attribute named " + GetDiagnosticName(targetName, env), DAXonErrorCode.SXWN9037, GetLocation());
                                    return Literal.MakeEmptySequence();
                                }
                            }
                            else
                            {
                                itemType = new CombinedNodeTest(test, Token.INTERSECT, new ContentTypeTest(Types.Type.ATTRIBUTE, schemaType, config, false));
                            }
                        }
                        catch (SchemaException)
                        {
                        }
                    }
                }
                else if (axis == AxisInfo.CHILD && kind.Equals(UType.ELEMENT))
                {
                    try
                    {
                        int childfp = targetfp;
                        if (targetName == null)
                        {

                            // select="child::*"
                            if (((IComplexType)contentType).ContainsElementWildcard())
                            {
                                return this;
                            }

                            IntHashSet children = new IntHashSet();
                            ((IComplexType)contentType).GatherAllPermittedChildren(children, false);
                            if (children.IsEmpty())
                            {
                                if (warnings)
                                {
                                    visitor.IssueWarning("The complex type " + contentType.Description + " does not allow children", DAXonErrorCode.SXWN9037, GetLocation());
                                }

                                return Literal.MakeEmptySequence();
                            }


                            //                            if (children.contains(-1)) {
                            //                                return this;
                            //                            }
                            if (children.Count == 1)
                            {
                                IIntIterator iter = children.IIterator();
                                if (iter.MoveNext())
                                {
                                    childfp = iter.Current;
                                }
                            }
                            else
                            {
                                return this;
                            }
                        }

                        ISchemaType schemaType = ((IComplexType)contentType).GetElementParticleType(childfp, true);
                        if (schemaType == null)
                        {
                            if (warnings)
                            {
                                StructuredQName childElement = GetConfiguration().GetNamePool().GetStructuredQName(childfp);
                                string message = "The complex type " + contentType.Description + " does not allow a child element named " + GetDiagnosticName(childElement, env);
                                IntHashSet permitted = new IntHashSet();
                                ((IComplexType)contentType).GatherAllPermittedChildren(permitted, false);
                                if (!permitted.Contains(-1))
                                {
                                    IIntIterator kids = permitted.IIterator();
                                    while (kids.MoveNext())
                                    {
                                        int kid = kids.Current;
                                        StructuredQName sq = GetConfiguration().GetNamePool().GetStructuredQName(kid);
                                        if (sq.GetLocalPart().Equals(childElement.GetLocalPart()) && kid != childfp)
                                        {
                                            message += ". Perhaps the namespace is " + (childElement.HasURI(NamespaceUri.NULL) ? "missing" : "wrong") + ", and " + sq.EQName + " was intended?";
                                            break;
                                        }
                                    }
                                }

                                visitor.IssueWarning(message, DAXonErrorCode.SXWN9037, GetLocation());
                            }

                            return Literal.MakeEmptySequence();
                        }
                        else
                        {
                            itemType = new CombinedNodeTest(test, Token.INTERSECT, new ContentTypeTest(Types.Type.ELEMENT, schemaType, config, true));
                            int computedCardinality = ((IComplexType)contentType).GetElementParticleCardinality(childfp, true);
                            ExpressionTool.ResetStaticProperties(this);
                            if (computedCardinality == StaticProperty.ALLOWS_ZERO)
                            {

                                // this shouldn't happen, because we've already checked for this a different way.
                                // but it's worth being safe (there was a bug involving an incorrect inference here)
                                StructuredQName childElement = GetConfiguration().GetNamePool().GetStructuredQName(childfp);
                                visitor.IssueWarning("The complex type " + contentType.Description + " appears not to allow a child element named " + GetDiagnosticName(childElement, env), DAXonErrorCode.SXWN9037, GetLocation());
                                return Literal.MakeEmptySequence();
                            }

                            if (!Cardinality.AllowsMany(computedCardinality) && !(ParentExpression is FirstItemExpression) && !visitor.IsOptimizeForPatternMatching())
                            {

                                // if there can be at most one child of this name, create a FirstItemExpression
                                // to stop the search after the first one is found
                                return FirstItemExpression.MakeFirstItemExpression(this);
                            }
                        }
                    }
                    catch (SchemaException)
                    {
                    }
                }
                else if (axis == AxisInfo.DESCENDANT && kind.Equals(UType.ELEMENT) && targetfp != -1)
                {

                    // when searching for a specific element on the descendant axis, try to produce a more
                    // specific path that avoids searching branches of the tree where the element cannot occur
                    try
                    {
                        IntHashSet descendants = new IntHashSet();
                        ((IComplexType)contentType).GatherAllPermittedDescendants(descendants);
                        if (descendants.Contains(-1))
                        {
                            return this;
                        }

                        if (descendants.Contains(targetfp))
                        {
                            IntHashSet children = new IntHashSet();
                            ((IComplexType)contentType).GatherAllPermittedChildren(children, false);
                            IntHashSet usefulChildren = new IntHashSet();
                            bool considerSelf = false;
                            bool considerDescendants = false;
                            IIntIterator kids = children.IIterator();
                            while (kids.MoveNext())
                            {
                                int c = kids.Current;
                                if (c == targetfp)
                                {
                                    usefulChildren.Add(c);
                                    considerSelf = true;
                                }

                                ISchemaType st = ((IComplexType)contentType).GetElementParticleType(c, true);
                                if (st == null)
                                {
                                    throw new InvalidOperationException("Can't find type for child element " + c);
                                }

                                if (st is IComplexType)
                                {
                                    IntHashSet subDescendants = new IntHashSet();
                                    ((IComplexType)st).GatherAllPermittedDescendants(subDescendants);
                                    if (subDescendants.Contains(targetfp))
                                    {
                                        usefulChildren.Add(c);
                                        considerDescendants = true;
                                    }
                                }
                            }

                            itemType = test;
                            if (considerDescendants)
                            {
                                ISchemaType st = ((IComplexType)contentType).GetDescendantElementType(targetfp);
                                if (st != AnyType.INSTANCE)
                                {
                                    itemType = new CombinedNodeTest(test, Token.INTERSECT, new ContentTypeTest(Types.Type.ELEMENT, st, config, true));
                                } //return this;
                            }

                            if (usefulChildren.Count < children.Count)
                            {
                                NodeTest childTest = MakeUnionNodeTest(usefulChildren, config.GetNamePool());
                                AxisExpression first = new AxisExpression(AxisInfo.CHILD, childTest);
                                ExpressionTool.CopyLocationInfo(this, first);
                                int nextAxis;
                                if (considerSelf)
                                {
                                    nextAxis = considerDescendants ? AxisInfo.DESCENDANT_OR_SELF : AxisInfo.SELF;
                                }
                                else
                                {
                                    nextAxis = AxisInfo.DESCENDANT;
                                }

                                AxisExpression next = new AxisExpression(nextAxis, (NodeTest)itemType);
                                ExpressionTool.CopyLocationInfo(this, next);
                                Expression path = ExpressionTool.MakePathExpression(first, next);
                                ExpressionTool.CopyLocationInfo(this, path);
                                return path.TypeCheck(visitor, contextInfo);
                            }
                        }
                        else
                        {
                            if (warnings)
                            {
                                visitor.IssueWarning("The complex type " + contentType.Description + " does not allow a descendant element named " + GetDiagnosticName(targetName, env), DAXonErrorCode.SXWN9037, GetLocation());
                            }
                        }
                    }
                    catch (SchemaException e)
                    {
                        throw new InvalidOperationException(e.Message, e);
                    }
                }
            }

            return this;
        }

        /*
     * Get a string representation of a name to use in diagnostics
     */
        private static string GetDiagnosticName(StructuredQName name, IStaticContext env)
        {
            NamespaceUri uri = name.GetNamespaceUri();
            if (uri.IsEmpty())
            {
                return name.GetLocalPart();
            }
            else
            {
                INamespaceResolver resolver = env.GetNamespaceResolver();
                IEnumerator<string> it = resolver.IteratePrefixes();
                while (it.MoveNext())
                {
                    string prefix = it.Current;
                    if (uri.Equals(resolver.GetURIForPrefix(prefix, true)))
                    {
                        if ((prefix.Length == 0))
                        {
                            return "Q{" + uri + "}" + name.GetLocalPart();
                        }
                        else
                        {
                            return prefix + ":" + name.GetLocalPart();
                        }
                    }
                }
            }

            return "Q{" + uri + "}" + name.GetLocalPart();
        }

        /*
     * Get a string representation of a name to use in diagnostics
     */
        private static string GetStartingNodeDescription(ISchemaType type)
        {
            string s = type.Description;
            if (s.StartsWith("of element", StringComparison.Ordinal))
            {
                return "a valid element named" + s.Substring("of element".Length);
            }
            else if (s.StartsWith("of attribute", StringComparison.Ordinal))
            {
                return "a valid attribute named" + s.Substring("of attribute".Length);
            }
            else
            {
                return "a node with " + (type.IsSimpleType() ? "simple" : "complex") + " type " + s;
            }
        }

        /*
     * Get a string representation of a name to use in diagnostics
     */
        private NodeTest MakeUnionNodeTest(IntHashSet elements, NamePool pool)
        {
            NodeTest test = null;
            IIntIterator iter = elements.IIterator();
            while (iter.MoveNext())
            {
                int fp = iter.Current;
                NodeTest nextTest = new NameTest(Types.Type.ELEMENT, fp, pool);
                if (test == null)
                {
                    test = nextTest;
                }
                else
                {
                    test = new CombinedNodeTest(test, Token.UNION, nextTest);
                }
            }

            return test;
        }

        /*
     * Get a string representation of a name to use in diagnostics
     */
        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            doneOptimize = true; // This ensures no more warnings about empty axes, because (a) we've probably output the

            // warning already, and (b) we're now looking at a different expression from what the user
            // wrote. In particular, prevent spurious warnings after function inlining.
            staticInfo = contextInfo;
            return this;
        }

        /*
     * Get a string representation of a name to use in diagnostics
     */
        /// <summary>
        /// Is this expression the same as another expression?
        /// </summary>
        public override bool Equals(object other)
        {
            return other is AxisExpression && axis == ((AxisExpression)other).axis && object.Equals(test, ((AxisExpression)other).test);
        }

        /*
     * Get a string representation of a name to use in diagnostics
     */
        protected override int ComputeHashCode()
        {

            // generate an arbitrary hash code that depends on the axis and the node test
            int h = 9375162 + axis << 20;
            if (test != null)
            {
                h ^= test.PrimitiveType << 16;
                h ^= test.Fingerprint;
            }

            return h;
        }

        /*
     * Get a string representation of a name to use in diagnostics
     */
        public override Expression Copy(RebindingMap rebindings)
        {
            AxisExpression a2 = new AxisExpression(axis, test);
            a2.itemType = itemType;
            a2.staticInfo = staticInfo;
            a2.doneTypeCheck = doneTypeCheck;
            a2.doneOptimize = doneOptimize;
            ExpressionTool.CopyLocationInfo(this, a2);
            return a2;
        }

        /*
     * Get a string representation of a name to use in diagnostics
     */
        protected override int ComputeSpecialProperties()
        {
            return StaticProperty.CONTEXT_DOCUMENT_NODESET | StaticProperty.SINGLE_DOCUMENT_NODESET | StaticProperty.NO_NODES_NEWLY_CREATED | (AxisInfo.isForwards[axis] ? StaticProperty.ORDERED_NODESET : StaticProperty.REVERSE_DOCUMENT_ORDER) | (AxisInfo.isPeerAxis[axis] || IsPeerNodeTest(test) ? StaticProperty.PEER_NODESET : 0) | (AxisInfo.isSubtreeAxis[axis] ? StaticProperty.SUBTREE_NODESET : 0) | (axis == AxisInfo.ATTRIBUTE || axis == AxisInfo.NAMESPACE ? StaticProperty.ATTRIBUTE_NS_NODESET : 0);
        }

        /*
     * Get a string representation of a name to use in diagnostics
     */
        private static bool IsPeerNodeTest(NodeTest test)
        {
            if (test == null)
            {
                return false;
            }

            UType uType = test.GetUType();
            if (uType.Overlaps(UType.ELEMENT))
            {

                // can match elements; for the moment, assume these can contain each other
                return false;
            }
            else if (uType.Overlaps(UType.DOCUMENT))
            {

                // can match documents; return false if we can also match non-documents
                return uType.Equals(UType.DOCUMENT);
            }
            else
            {
                return true;
            }
        }

        /*
     * Get a string representation of a name to use in diagnostics
     */
        public override Types.ItemType GetItemType()
        {
            if (itemType != null)
            {
                return itemType;
            }

            int p = AxisInfo.principalNodeType[axis];
            switch (p)
            {
                case Types.Type.ATTRIBUTE:
                case Types.Type.NAMESPACE:
                    return NodeKindTest.MakeNodeKindTest(p);
                default:
                    if (test == null)
                    {
                        return AnyNodeTest.GetInstance();
                    }
                    else
                    {
                        return test;
                    }
            }
        }

        /*
     * Get a string representation of a name to use in diagnostics
     */
        public override UType GetStaticUType(UType contextItemType)
        {

            // See W3C bug 30032
            UType reachable = AxisInfo.GetTargetUType(contextItemType, axis);
            if (test == null)
            {
                return reachable;
            }
            else
            {
                return reachable.Intersection(test.GetUType());
            }
        }

        /*
     * Get a string representation of a name to use in diagnostics
     */
        protected override int ComputeCardinality()
        {
            NodeTest originNodeType;
            NodeTest nodeTest = test;
            Types.ItemType contextItemType = staticInfo.GetItemType();
            if (contextItemType is NodeTest)
            {
                originNodeType = (NodeTest)contextItemType;
            }
            else if (contextItemType is AnyItemType)
            {
                originNodeType = AnyNodeTest.GetInstance();
            }
            else
            {

                // context item not a node - we'll report a type error somewhere along the line
                return StaticProperty.ALLOWS_ZERO_OR_MORE;
            }

            if (axis == AxisInfo.ATTRIBUTE && nodeTest is NameTest)
            {
                ISchemaType contentType = originNodeType.ContentType;
                if (contentType is IComplexType)
                {
                    try
                    {
                        return ((IComplexType)contentType).GetAttributeUseCardinality(nodeTest.MatchingNodeName);
                    }
                    catch (SchemaException)
                    {

                        // shouldn't happen; play safe
                        return StaticProperty.ALLOWS_ZERO_OR_ONE;
                    }
                }
                else if (contentType is ISimpleType)
                {
                    return StaticProperty.EMPTY;
                }

                return StaticProperty.ALLOWS_ZERO_OR_ONE;
            }
            else if (axis == AxisInfo.CHILD && nodeTest is NameTest && nodeTest.PrimitiveType == Types.Type.ELEMENT)
            {
                ISchemaType contentType = originNodeType.ContentType;
                if (contentType is IComplexType)
                {
                    return ((IComplexType)contentType).GetElementParticleCardinality(nodeTest.Fingerprint, true);
                }
                else
                {
                    return StaticProperty.EMPTY;
                }
            }
            else if (axis == AxisInfo.DESCENDANT && nodeTest is NameTest && nodeTest.PrimitiveType == Types.Type.ELEMENT)
            {
                ISchemaType contentType = originNodeType.ContentType;
                if (contentType is IComplexType)
                {
                    try
                    {
                        return ((IComplexType)contentType).GetDescendantElementCardinality(nodeTest.Fingerprint);
                    }
                    catch (SchemaException)
                    {

                        // shouldn't happen; play safe
                        return StaticProperty.ALLOWS_ZERO_OR_MORE;
                    }
                }
                else
                {
                    return StaticProperty.EMPTY;
                }
            }
            else if (axis == AxisInfo.SELF)
            {
                return StaticProperty.ALLOWS_ZERO_OR_ONE;
            }
            else
            {
                return StaticProperty.ALLOWS_ZERO_OR_MORE;
            } // the parent axis isn't handled by this class
        }

        /*
     * Get a string representation of a name to use in diagnostics
     */
        public override bool IsSubtreeExpression()
        {
            return AxisInfo.isSubtreeAxis[axis];
        }

        /*
     * Get a string representation of a name to use in diagnostics
     */
        public NodeTest GetNodeTest()
        {
            return test;
        }

        /*
     * Get a string representation of a name to use in diagnostics
     */
        public override PathMap.PathMapNodeSet AddToPathMap(PathMap pathMap, PathMap.PathMapNodeSet pathMapNodeSet)
        {
            if (pathMapNodeSet == null)
            {
                ContextItemExpression cie = new ContextItemExpression();

                pathMapNodeSet = new PathMap.PathMapNodeSet(pathMap.MakeNewRoot(cie));
            }

            return pathMapNodeSet.CreateArc(axis, test == null ? AnyNodeTest.GetInstance() : test);
        }

        /*
     * Get a string representation of a name to use in diagnostics
     */
        public bool IsContextPossiblyUndefined()
        {
            return staticInfo.IsPossiblyAbsent();
        }

        /*
     * Get a string representation of a name to use in diagnostics
     */
        public override Patterns.Pattern ToPattern(Configuration config)
        {
            NodeTest test = GetNodeTest();
            Patterns.Pattern pat;
            if (test == null)
            {
                test = AnyNodeTest.GetInstance();
            }

            if (test is AnyNodeTest && (axis == AxisInfo.CHILD || axis == AxisInfo.DESCENDANT || axis == AxisInfo.SELF))
            {
                test = MultipleNodeKindTest.CHILD_NODE;
            }

            int kind = test.PrimitiveType;
            if (axis == AxisInfo.SELF)
            {
                pat = new NodeTestPattern(test);
            }
            else if (axis == AxisInfo.ATTRIBUTE)
            {
                if (kind == Types.Type.NODE)
                {

                    // attribute.node() matches any attribute, and only an attribute
                    pat = new NodeTestPattern(NodeKindTest.ATTRIBUTE);
                }
                else if (!AxisInfo.ContainsNodeKind(axis, kind))
                {

                    // for example, attribute.comment()
                    pat = new NodeTestPattern(ErrorType.GetInstance());
                }
                else
                {
                    pat = new NodeTestPattern(test);
                }
            }
            else if (axis == AxisInfo.CHILD || axis == AxisInfo.DESCENDANT || axis == AxisInfo.DESCENDANT_OR_SELF)
            {
                if (kind != Types.Type.NODE && !AxisInfo.ContainsNodeKind(axis, kind))
                {
                    pat = new NodeTestPattern(ErrorType.GetInstance());
                }
                else
                {
                    pat = new NodeTestPattern(test);
                }
            }
            else if (axis == AxisInfo.NAMESPACE)
            {
                if (kind == Types.Type.NODE)
                {

                    // namespace.node() matches any attribute, and only an attribute
                    pat = new NodeTestPattern(NodeKindTest.NAMESPACE);
                }
                else if (!AxisInfo.ContainsNodeKind(axis, kind))
                {

                    // for example, namespace.comment()
                    pat = new NodeTestPattern(ErrorType.GetInstance());
                }
                else
                {
                    pat = new NodeTestPattern(test);
                }
            }
            else
            {
                throw new XPathException("Only downwards axes are allowed in a pattern", "XTSE0340");
            }

            ExpressionTool.CopyLocationInfo(this, pat);
            return pat;
        }

        /*
     * Get a string representation of a name to use in diagnostics
     */
        public override ISequenceIterator Iterate(IXPathContext context)
        {
            IItem item = context.GetContextItem();
            if (item == null)
            {

                // Might as well do the test anyway, whether or not contextMaybeUndefined is set
                throw new XPathException("The context item for axis step " + this + " is absent").WithErrorCode("XPDY0002").WithXPathContext(context).WithLocation(GetLocation()).AsTypeError();
            }

            try
            {
                if (test == null)
                {
                    return ((NodeInfo)item).IterateAxis(axis);
                }
                else
                {
                    return ((NodeInfo)item).IterateAxis(axis, test);
                }
            }
            catch (InvalidCastException)
            {
                throw new XPathException("The context item for axis step " + this + " is not a node").WithErrorCode("XPTY0020").WithXPathContext(context).WithLocation(GetLocation()).AsTypeError();
            }
            catch (NotSupportedException err)
            {
                if (err.InnerException is XPathException)
                {
                    throw ((XPathException)err.InnerException).MaybeWithLocation(GetLocation()).MaybeWithContext(context);
                }
                else
                {

                    // the namespace axis is not supported for all tree implementations
                    DynamicError(err.Message, "XPST0010", context);
                    return null;
                }
            }
        }

        /*
     * Get a string representation of a name to use in diagnostics
     */
        public IAxisIterator Iterate(NodeInfo origin)
        {
            if (test == null)
            {
                return origin.IterateAxis(axis);
            }
            else
            {
                return origin.IterateAxis(axis, test);
            }
        }

        /*
     * Get a string representation of a name to use in diagnostics
     */
        public override void Export(ExpressionPresenter destination)
        {
            destination.StartElement("axis", this);
            destination.EmitAttribute("name", AxisInfo.axisName[axis]);
            destination.EmitAttribute("nodeTest", AlphaCode.FromItemType(test == null ? AnyNodeTest.GetInstance() : test));
            destination.EndElement();
        }

        /*
     * Get a string representation of a name to use in diagnostics
     */
        public override string ToString()
        {
            StringBuilder fsb = new StringBuilder(16);
            fsb.Append(AxisInfo.axisName[axis]);
            fsb.Append("::");
            fsb.Append(test == null ? "node()" : test.ToString());
            return fsb.ToString();
        }

        /*
     * Get a string representation of a name to use in diagnostics
     */
        public override string ToShortString()
        {
            StringBuilder fsb = new StringBuilder(16);
            if (axis == AxisInfo.CHILD)
            {
            }
            else if (axis == AxisInfo.ATTRIBUTE)
            {
                fsb.Append('@');
            }
            else
            {
                fsb.Append(AxisInfo.axisName[axis]);
                fsb.Append("::");
            }

            if (test == null)
            {
                fsb.Append("node()");
            }
            else if (test is NameTest)
            {
                if (((NameTest)test).GetNodeKind() != AxisInfo.principalNodeType[axis])
                {
                    fsb.Append(test.ToString());
                }
                else
                {
                    fsb.Append(test.MatchingNodeName.DisplayName);
                }
            }
            else
            {
                fsb.Append(test.ToString());
            }

            return fsb.ToString();
        }

        /*
     * Get a string representation of a name to use in diagnostics
     */
        /*Expression args[] = new Expression[1];
        args[0] = this.copy();
        pre.add(SystemFunctionCall.makeSystemFunction(
                "exists", args));*/
        public override Elaborator GetElaborator()
        {
            return new AxisExpressionElaborator();
        }

        /*
     * Get a string representation of a name to use in diagnostics
     */
        /*Expression args[] = new Expression[1];
        args[0] = this.copy();
        pre.add(SystemFunctionCall.makeSystemFunction(
                "exists", args));*/
        /// <summary>
        /// Elaborator for an AxisExpression
        /// </summary>
        internal sealed class AxisExpressionElaborator : PullElaborator
        {
            private void ReportDoesNotExist(Expression expression, IXPathContext context)
            {
                throw new XPathException("The context item for axis step " + expression + " is absent").WithErrorCode("XPDY0002").WithXPathContext(context).WithLocation(expression.GetLocation()).AsTypeError();
            }

            private void ReportIsNotNode(Expression expression, IXPathContext context)
            {
                throw new XPathException("The context item for axis step " + expression + " is not a node").WithErrorCode("XPTY0020").WithXPathContext(context).WithLocation(expression.GetLocation()).AsTypeError();
            }

            public override IBooleanEvaluator ElaborateForBoolean()
            {
                AxisExpression axisExpression = (AxisExpression)GetExpression();
                NodeTest kindOnly = axisExpression.GetNodeTest();

                // Existence test for child::element() (the [*] / not(*) predicate shape):
                // answered by a direct scan of the TinyTree sibling chain -- no axis iterator,
                // no node objects, no EBV frame. Anything else falls back to the generic
                // iterate-and-test evaluator (which also owns the error semantics).
                if (axisExpression.Axis == AxisInfo.CHILD
                    && kindOnly is NodeKindTest kindTest
                    && kindTest.GetNodeKind() == Types.Type.ELEMENT)
                {
                    IBooleanEvaluator fallback = base.ElaborateForBoolean();
                    return (context) =>
                    {
                        // TinyParentNodeImpl only: attribute/namespace wrappers index other arrays
                        if (context.GetContextItem() is TinyParentNodeImpl tn)
                        {
                            TinyTree tree = tn.Tree;
                            int nodeNr = tn.nodeNr;
                            if (!(nodeNr + 1 < tree.numberOfNodes && tree.depth[nodeNr + 1] > tree.depth[nodeNr]))
                            {
                                return false;   // no children at all (a textual element included)
                            }

                            byte[] kinds = tree.nodeKind;
                            int[] nxt = tree.next;
                            int child = nodeNr + 1;
                            while (child > nodeNr)
                            {
                                int k = kinds[child];
                                if (k == Types.Type.ELEMENT || k == Types.Type.TEXTUAL_ELEMENT)
                                {
                                    return true;
                                }

                                child = nxt[child];   // PARENT_POINTER links just continue the chain
                            }

                            return false;
                        }

                        return fallback.Eval(context);
                    };
                }

                return base.ElaborateForBoolean();
            }

            public override IPullEvaluator ElaborateForPull()
            {
                AxisExpression axisExpression = (AxisExpression)GetExpression();
                NodeTest test = axisExpression.GetNodeTest();
                int axis = axisExpression.Axis;

                // These variables are computed in the hope that the optimizer will remove runtime error tests
                // that aren't needed because the condition cannot occur
                bool checkContextItemExists = axisExpression.IsContextPossiblyUndefined();
                bool checkContextItemIsNode = (Genre)axisExpression.ContextItemType.GetGenre() != Genre.NODE;
                if (test == null || test is AnyNodeTest)
                {
                    return (context) =>
                    {
                        IItem item = context.GetContextItem();
                        if (checkContextItemExists && item == null)
                        {
                            ReportDoesNotExist(axisExpression, context);
                        }

                        if (checkContextItemIsNode && !(item is NodeInfo))
                        {
                            ReportIsNotNode(axisExpression, context);
                        }

                        return ((NodeInfo)item).IterateAxis(axis);
                    };
                }
                else
                {
                    return (context) =>
                    {
                        IItem item = context.GetContextItem();
                        if (checkContextItemExists && item == null)
                        {
                            ReportDoesNotExist(axisExpression, context);
                        }

                        if (checkContextItemIsNode && !(item is NodeInfo))
                        {
                            ReportIsNotNode(axisExpression, context);
                        }

                        return ((NodeInfo)item).IterateAxis(axis, test);
                    };
                }
            }
        }
    }
}
