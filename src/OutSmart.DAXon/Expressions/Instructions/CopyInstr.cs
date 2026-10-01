////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Expressions.Instructions
{
    /// <summary>
    /// Handler for xsl:copy elements in stylesheet. This only handles copying of the context item. An xsl:copy
    /// with a select attribute is handled by wrapping the instruction in an xsl:for-each.
    /// </summary>
    internal sealed class CopyInstr : ElementCreator
    {
        private bool copyNamespaces;
        private ItemType selectItemType = AnyItemType.GetInstance();
        private ItemType resultItemType;

        public override int IntrinsicDependencies => StaticProperty.DEPENDS_ON_CONTEXT_ITEM;

        public override int InstructionNameCode => StandardNames.XSL_COPY;

        public override string StreamerName => "Copy";

        public CopyInstr(bool copyNamespaces, bool inheritNamespaces, ISchemaType schemaType, int validation)
        {
            this.copyNamespaces = copyNamespaces;
            this.bequeathNamespacesToChildren = inheritNamespaces;
            SetValidationAction(validation, schemaType);
            preservingTypes = schemaType == null && validation == Validation.PRESERVE;
        }

        public override Expression Simplify()
        {
            preservingTypes |= !GetPackageData().IsSchemaAware();
            return base.Simplify();
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            TypeCheckChildren(visitor, contextInfo);

            selectItemType = contextInfo.GetItemType();

            if (selectItemType == ErrorType.GetInstance())
            {
                throw new XPathException("No context item supplied for xsl:copy", "XTTE0945").AsTypeError().WithLocation(GetLocation());
            }

            if (selectItemType is NodeTest)
            {
                switch (selectItemType.PrimitiveType)
                {
                    // For elements and attributes, assume the type annotation will change
                    case Types.Type.ELEMENT:
                        this.resultItemType = NodeKindTest.ELEMENT;
                        break;
                    case Types.Type.DOCUMENT:
                        this.resultItemType = NodeKindTest.DOCUMENT;
                        break;
                    case Types.Type.ATTRIBUTE:
                    case Types.Type.TEXT:
                    case Types.Type.COMMENT:
                    case Types.Type.PROCESSING_INSTRUCTION:
                    case Types.Type.NAMESPACE:
                        ContextItemExpression dot = new ContextItemExpression();
                        ExpressionTool.CopyLocationInfo(this, dot);
                        CopyOf c = new CopyOf(dot, copyNamespaces, GetValidationAction(), GetSchemaType(), false);
                        ExpressionTool.CopyLocationInfo(this, c);
                        return c.TypeCheck(visitor, contextInfo);
                    default:
                        this.resultItemType = selectItemType;
                        break;
                }
            }
            else
            {
                this.resultItemType = selectItemType;
            }

            CheckContentSequence(visitor.StaticContext);
            return this;
        }

        /// <summary>
        /// Copy this expression (don't be confused by the method name). This makes a deep copy.
        /// </summary>
        public override Expression Copy(RebindingMap rebindings)
        {
            CopyInstr copy = new CopyInstr(copyNamespaces, bequeathNamespacesToChildren, GetSchemaType(), GetValidationAction());
            ExpressionTool.CopyLocationInfo(this, copy);
            copy.SetContentExpression(GetContentExpression().Copy(rebindings));
            copy.resultItemType = resultItemType;
            return copy;
        }

        public override IEnumerable<Operand> Operands()
        {
            return contentOp;
        }

        /// <summary>
        /// Get the item type of the result of this instruction.
        /// </summary>
        public override ItemType GetItemType()
        {
            if (resultItemType != null)
            {
                return resultItemType;
            }
            else
            {
                TypeHierarchy th = GetConfiguration().GetTypeHierarchy();
                resultItemType = ComputeItemType(th);
                return resultItemType;
            }
        }

        private ItemType ComputeItemType(TypeHierarchy th)
        {
            ItemType selectItemType = this.selectItemType;
            if (!GetPackageData().IsSchemaAware())
            {
                return selectItemType;
            }

            if (selectItemType.GetUType().Overlaps(UType.ANY_ATOMIC.Union(UType.FUNCTION)))
            {
                return selectItemType;
            }

            // The rest of the code handles the complications of schema-awareness
            Configuration config = th.GetConfiguration();
            if (GetSchemaType() != null)
            {
                Affinity e = th.Relationship(selectItemType, NodeKindTest.ELEMENT);
                if (e == Affinity.SAME_TYPE || e == Affinity.SUBSUMED_BY)
                {
                    return new ContentTypeTest(Types.Type.ELEMENT, GetSchemaType(), config, false);
                }

                Affinity a = th.Relationship(selectItemType, NodeKindTest.ATTRIBUTE);
                if (a == Affinity.SAME_TYPE || a == Affinity.SUBSUMED_BY)
                {
                    return new ContentTypeTest(Types.Type.ATTRIBUTE, GetSchemaType(), config, false);
                }

                return AnyNodeTest.GetInstance();
            }
            else
            {
                switch (GetValidationAction())
                {
                    case Validation.PRESERVE:
                        return selectItemType;
                    case Validation.STRIP:
                        {
                            Affinity e = th.Relationship(selectItemType, NodeKindTest.ELEMENT);
                            if (e == Affinity.SAME_TYPE || e == Affinity.SUBSUMED_BY)
                            {
                                return new ContentTypeTest(Types.Type.ELEMENT, Untyped.GetInstance(), config, false);
                            }

                            Affinity a = th.Relationship(selectItemType, NodeKindTest.ATTRIBUTE);
                            if (a == Affinity.SAME_TYPE || a == Affinity.SUBSUMED_BY)
                            {
                                return new ContentTypeTest(Types.Type.ATTRIBUTE, BuiltInAtomicType.UNTYPED_ATOMIC, config, false);
                            }

                            if (e != Affinity.DISJOINT || a != Affinity.DISJOINT)
                            {
                                // it might be an element or attribute
                                return AnyNodeTest.GetInstance();
                            }
                            else
                            {
                                // it can't be an element or attribute, so stripping type annotations can't affect it
                                return selectItemType;
                            }
                        }

                    case Validation.STRICT:
                    case Validation.LAX:
                        if (selectItemType is NodeTest)
                        {
                            int fp = ((NodeTest)selectItemType).Fingerprint;
                            if (fp != -1)
                            {
                                Affinity e = th.Relationship(selectItemType, NodeKindTest.ELEMENT);
                                if (e == Affinity.SAME_TYPE || e == Affinity.SUBSUMED_BY)
                                {
                                    ISchemaDeclaration elem = config.GetElementDeclaration(fp);
                                    if (elem != null)
                                    {
                                        try
                                        {
                                            return new ContentTypeTest(Types.Type.ELEMENT, elem.GetType(), config, false);
                                        }
                                        catch (MissingComponentException)
                                        {
                                            return new ContentTypeTest(Types.Type.ELEMENT, AnyType.GetInstance(), config, false);
                                        }
                                    }
                                    else
                                    {
                                        // No element declaration now, but there might be one at run-time
                                        return new ContentTypeTest(Types.Type.ELEMENT, AnyType.GetInstance(), config, false);
                                    }
                                }

                                Affinity a = th.Relationship(selectItemType, NodeKindTest.ATTRIBUTE);
                                if (a == Affinity.SAME_TYPE || a == Affinity.SUBSUMED_BY)
                                {
                                    ISchemaDeclaration attr = config.GetAttributeDeclaration(fp);
                                    if (attr != null)
                                    {
                                        try
                                        {
                                            return new ContentTypeTest(Types.Type.ATTRIBUTE, attr.GetType(), config, false);
                                        }
                                        catch (MissingComponentException)
                                        {
                                            return new ContentTypeTest(Types.Type.ATTRIBUTE, AnySimpleType.GetInstance(), config, false);
                                        }
                                    }
                                    else
                                    {
                                        // No attribute declaration now, but there might be one at run-time
                                        return new ContentTypeTest(Types.Type.ATTRIBUTE, AnySimpleType.GetInstance(), config, false);
                                    }
                                }
                            }
                            else
                            {
                                Affinity e = th.Relationship(selectItemType, NodeKindTest.ELEMENT);
                                if (e == Affinity.SAME_TYPE || e == Affinity.SUBSUMED_BY)
                                {
                                    return NodeKindTest.ELEMENT;
                                }

                                Affinity a = th.Relationship(selectItemType, NodeKindTest.ATTRIBUTE);
                                if (a == Affinity.SAME_TYPE || a == Affinity.SUBSUMED_BY)
                                {
                                    return NodeKindTest.ATTRIBUTE;
                                }
                            }

                            return AnyNodeTest.GetInstance();
                        }
                        else if (selectItemType is IAtomicType)
                        {
                            return selectItemType;
                        }
                        else
                        {
                            return AnyItemType.GetInstance();
                        }

                    default:
                        throw new InvalidOperationException();
                }
            }
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {
            Expression exp = base.Optimize(visitor, contextItemType);
            if (exp == this)
            {
                if (resultItemType == null)
                {
                    resultItemType = ComputeItemType(visitor.GetConfiguration().GetTypeHierarchy());
                }

                if (visitor.IsOptimizeForStreaming())
                {
                    UType type = contextItemType.GetItemType().GetUType();
                    if (!type.Intersection(MultipleNodeKindTest.LEAF.GetUType()).Equals(UType.VOID))
                    {
                        // Bug 4346: only do this optimization once
                        Expression p = ParentExpression;
                        if (p is Choose && ((Choose)p).Size() == 2 && ((Choose)p).GetAction(1) == this && ((Choose)p).GetAction(0) is CopyOf)
                        {
                            return exp;
                        }

                        Expression copyOf = new CopyOf(new ContextItemExpression(), false, GetValidationAction(), GetSchemaType(), false);
                        NodeTest leafTest = new MultipleNodeKindTest(type.Intersection(MultipleNodeKindTest.LEAF.GetUType()));
                        Expression[] conditions = new Expression[]
                        {
                            new InstanceOfExpression(new ContextItemExpression(), SequenceType.MakeSequenceType(leafTest, StaticProperty.EXACTLY_ONE)),
                            Literal.MakeLiteral(BooleanValue.TRUE, this)
                        };
                        Expression[] actions = new Expression[] { copyOf, this };
                        Choose choose = new Choose(conditions, actions);
                        ExpressionTool.CopyLocationInfo(this, choose);
                        return choose;
                    }
                }
            }

            return exp;
        }

        public static void CopyUnparsedEntities(NodeInfo source, Outputter @out)
        {
            IEnumerator<string> unparsedEntities = source.GetTreeInfo().UnparsedEntityNames;
            while (unparsedEntities.MoveNext())
            {
                string n = unparsedEntities.Current;
                string[] details = source.GetTreeInfo().GetUnparsedEntity(n);
                @out.SetUnparsedEntity(n, details[0], details[1]);
            }
        }

        /// <summary>
        /// Evaluate as an item
        /// </summary>
        public override IItem EvaluateItem(IXPathContext context)
        {
            return MakeElaborator().ElaborateForItem().Eval(context);
        }

        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("copy", this);
            ExportValidationAndType(@out);
            string flags = "";
            if (copyNamespaces)
            {
                flags = "c";
            }

            if (bequeathNamespacesToChildren)
            {
                flags += "i";
            }

            if (inheritNamespacesFromParent)
            {
                flags += "n";
            }

            if (IsLocal())
            {
                flags += "l";
            }

            @out.EmitAttribute("flags", flags);
            string sType = SequenceType.MakeSequenceType(selectItemType, GetCardinality()).ToAlphaCode();
            @out.EmitAttribute("sit", sType);
            @out.SetChildRole("content");
            GetContentExpression().Export(@out);
            @out.EndElement();
        }

        public override Elaborator GetElaborator()
        {
            return new CopyElaborator();
        }

        internal sealed class CopyElaborator : PushElaborator
        {
            public override IPushEvaluator ElaborateForPush()
            {
                CopyInstr expr = (CopyInstr)GetExpression();
                IPushEvaluator contentPush = expr.GetContentExpression().MakeElaborator().ElaborateForPush();
                ISchemaType typeCode = expr.GetValidationAction() == Validation.PRESERVE ? (ISchemaType)AnyType.GetInstance() : Untyped.GetInstance();
                int properties = ReceiverOption.NONE;
                if (!expr.bequeathNamespacesToChildren)
                {
                    properties |= ReceiverOption.DISINHERIT_NAMESPACES;
                }

                if (!expr.inheritNamespacesFromParent)
                {
                    properties |= ReceiverOption.REFUSE_NAMESPACES;
                }

                properties |= ReceiverOption.ALL_NAMESPACES;
                int finalProperties = properties;
                return (output, context) =>
                {
                    Controller controller = context.GetController();
                    IItem item = context.GetContextItem();
                    if (item == null)
                    {
                        throw new XPathException("There is no context item for xsl:copy", "XTTE0945").AsTypeError().WithLocation(expr.GetLocation()).WithXPathContext(context);
                    }

                    if (!(item is NodeInfo))
                    {
                        output.Append(item, expr.GetLocation(), ReceiverOption.ALL_NAMESPACES);
                        return null;
                    }

                    NodeInfo source = (NodeInfo)item;
                    switch (source.GetNodeKind())
                    {
                        case Types.Type.ELEMENT:
                            try
                            {
                                INodeName elemName = NameOfNode.MakeName(source);
                                IReceiver elemOut = output;
                                if (!expr.preservingTypes)
                                {
                                    ParseOptions options = expr.ValidationOptions.WithTopLevelElement(elemName.GetStructuredQName());
                                    context.GetConfiguration().PrepareValidationReporting(context, options);
                                    IReceiver validator = context.GetConfiguration().GetElementValidator(elemOut, options, expr.GetLocation());
                                    if (validator != elemOut)
                                    {
                                        output = new ComplexContentOutputter(validator);
                                    }
                                }

                                if (output.GetSystemId() == null)
                                {
                                    output.SetSystemId(source.GetBaseURI());
                                }

                                output.StartElement(elemName, typeCode, expr.GetLocation(), finalProperties);

                                // output the required namespace nodes via a callback
                                if (expr.copyNamespaces)
                                {
                                    output.Namespaces(source.AllNamespaces, ReceiverOption.NAMESPACE_OK);
                                }
                                else
                                {
                                    // Always output the namespace of the element name itself
                                    NamespaceBinding ns = elemName.GetNamespaceBinding();
                                    if (!ns.IsDefaultUndeclaration())
                                    {
                                        output.Namespace(ns.GetPrefix(), ns.GetNamespaceUri(), ReceiverOption.NONE);
                                    }
                                }

                                // process subordinate instructions to generate attributes and content
                                Expression.DispatchTailCall(contentPush.ProcessLeavingTail(output, context));

                                // output the element end tag (which will fail if validation fails)
                                output.EndElement();
                            }
                            catch (XPathException e) when (!(e is XPathException.StackOverflow))
                            {
                                // Filtered, not caught: xsl:copy nests once per level of the input
                                // tree, and a catch that rethrows re-enters exception dispatch from
                                // the deep point at ~20KB of stack per level. Decorating a
                                // stack-guard abort that way costs more stack than the descent did,
                                // so SXLM0001 must fly through untouched.
                                throw e.MaybeWithLocation(expr.GetLocation()).MaybeWithContext(context);
                            }

                            return null;
                        case Types.Type.ATTRIBUTE:
                            if (expr.GetSchemaType() is IComplexType)
                            {
                                expr.DynamicError("Cannot copy an attribute when the type requested for validation is a complex type", "XTTE1535", context);
                            }

                            try
                            {
                                CopyOf.CopyAttribute(source, (ISimpleType)expr.GetSchemaType(), expr.GetValidationAction(), expr, output, context, false);
                            }
                            catch (NoOpenStartTagException err)
                            {
                                throw DynamicError(expr.GetLocation(), err.WithXPathContext(context), context);
                            }

                            break;
                        case Types.Type.TEXT:
                            UnicodeString tval = source.UnicodeStringValue;
                            output.Characters(tval, expr.GetLocation(), ReceiverOption.NONE);
                            break;
                        case Types.Type.PROCESSING_INSTRUCTION:
                            UnicodeString pval = source.UnicodeStringValue;
                            output.ProcessingInstruction(source.DisplayName, pval, expr.GetLocation(), ReceiverOption.NONE);
                            break;
                        case Types.Type.COMMENT:
                            UnicodeString cval = source.UnicodeStringValue;
                            output.Comment(cval, expr.GetLocation(), ReceiverOption.NONE);
                            break;
                        case Types.Type.NAMESPACE:
                            output.Namespace(((NodeInfo)item).GetLocalPart(), NamespaceUri.Of(item.GetStringValue()), ReceiverOption.NONE);
                            break;
                        case Types.Type.DOCUMENT:
                            if (!expr.preservingTypes)
                            {
                                ParseOptions options = expr.ValidationOptions.WithSpaceStrippingRule(NoElementsSpaceStrippingRule.GetInstance());
                                controller.GetConfiguration().PrepareValidationReporting(context, options);
                                IReceiver val = controller.GetConfiguration().GetDocumentValidator(output, source.GetBaseURI(), options, expr.GetLocation());
                                output = new ComplexContentOutputter(val);
                            }

                            if (output.GetSystemId() == null)
                            {
                                output.SetSystemId(source.GetBaseURI());
                            }

                            output.StartDocument(ReceiverOption.NONE);
                            CopyUnparsedEntities(source, output);
                            Expression.DispatchTailCall(contentPush.ProcessLeavingTail(output, context));
                            output.EndDocument();
                            break;
                        default:
                            throw new ArgumentException("Unknown node kind " + source.GetNodeKind());
                    }

                    return null;
                };
            }
        }
    }
}
