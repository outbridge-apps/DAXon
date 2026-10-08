////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using System;
using System.Collections.Generic;
using System.Text;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Types;
namespace OutSmart.DAXon.Expressions.Instructions
{
    internal sealed class FixedElement : ElementCreator
    {
        private readonly INodeName elementName;
        protected NamespaceMap namespaceBindings;
        private Types.ItemType itemType;

        public INodeName FixedElementName => elementName;

        public override string ExpressionName => "element";
        public FixedElement(INodeName elementName, NamespaceMap namespaceBindings, bool inheritNamespacesToChildren, bool inheritNamespacesFromParent, ISchemaType schemaType, int validation)
        {
            this.elementName = elementName;
            this.namespaceBindings = namespaceBindings;
            this.bequeathNamespacesToChildren = inheritNamespacesToChildren;
            this.inheritNamespacesFromParent = inheritNamespacesFromParent;
            SetValidationAction(validation, schemaType);
            preservingTypes = schemaType == null && validation == Validation.PRESERVE;
        }

        public override void SetLocation(ILocation id)
        {
            base.SetLocation(id);
        }

        public override IEnumerable<Operand> Operands()
        {
            return contentOp;
        }

        public override Expression Simplify()
        {
            preservingTypes |= !GetPackageData().IsSchemaAware();
            return base.Simplify();
        }

        protected override void CheckContentSequence(IStaticContext env)
        {
            base.CheckContentSequence(env);
            itemType = ComputeFixedElementItemType(this, env, GetValidationAction(), GetSchemaType(), elementName, GetContentExpression());
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {
            Expression e = base.Optimize(visitor, contextItemType);
            if (e != this)
            {
                return e;
            }


            // Remove any unnecessary creation of namespace nodes by child literal result elements.
            // Specifically, if this instruction creates a namespace node, then a child literal result element
            // doesn't need to create the same namespace if all the following conditions are true:
            // (a) the child element @is in the same namespace as its parent, and
            // (b) this element doesn't specify xsl:inherit-namespaces="no"
            // (c) the child element is incapable of creating attributes in a non-null namespace
            if (!bequeathNamespacesToChildren)
            {
                return this;
            }


            //            if (getContentExpression() instanceof FixedElement) {
            //                FixedElement fixedContent = (FixedElement) getContentExpression();
            //                    fixedContent.removeRedundantNamespaces(visitor, namespaceBindings);
            //                }
            //                return this;
            //                for (Operand o : getContentExpression().operands()) {
            //                    if (exp instanceof FixedElement &&
            //                        ((FixedElement) exp).removeRedundantNamespaces(visitor, namespaceBindings);
            //                    }
            //                }
            return this;
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            FixedElement fe = new FixedElement(elementName, namespaceBindings, bequeathNamespacesToChildren, inheritNamespacesFromParent, GetSchemaType(), GetValidationAction());
            fe.SetContentExpression(GetContentExpression().Copy(rebindings));
            fe.preservingTypes = preservingTypes;
            ExpressionTool.CopyLocationInfo(this, fe);
            return fe;
        }

        private Types.ItemType ComputeFixedElementItemType(FixedElement instr, IStaticContext env, int validation, ISchemaType schemaType, INodeName elementName, Expression content)
        {
            Configuration config = env.GetConfiguration();
            Types.ItemType itemType;
            int fp = elementName.ObtainFingerprint(config.GetNamePool()); // Bug #2563 - namespaced alias in unoptimized export
            if (schemaType == null)
            {
                if (validation == Validation.STRICT)
                {
                    ISchemaDeclaration decl = config.GetElementDeclaration(fp);
                    if (decl == null)
                    {
                        throw new XPathException("There is no global element declaration for " + elementName.GetStructuredQName().EQName + ", so strict validation will fail").WithErrorCode(instr.IsXSLT() ? "XTTE1512" : "XQDY0084").AsTypeError().WithLocation(instr.GetLocation());
                    }

                    if (decl.IsAbstract())
                    {
                        throw new XPathException("The element declaration for " + elementName.GetStructuredQName().EQName + " is abstract, so strict validation will fail").WithErrorCode(instr.IsXSLT() ? "XTTE1512" : "XQDY0084").AsTypeError().WithLocation(instr.GetLocation());
                    }

                    ISchemaType declaredType = decl.GetType();
                    ISchemaType xsiType = instr.GetXSIType(env);
                    if (xsiType != null)
                    {
                        schemaType = xsiType;
                    }
                    else
                    {
                        schemaType = declaredType;
                    }

                    itemType = new CombinedNodeTest(new NameTest(Types.Type.ELEMENT, fp, env.GetConfiguration().GetNamePool()), Token.INTERSECT, new ContentTypeTest(Types.Type.ELEMENT, schemaType, config, false));
                    if (xsiType != null || !decl.HasTypeAlternatives())
                    {
                        instr.ValidationOptions = instr.ValidationOptions.WithTopLevelType(schemaType);
                        try
                        {
                            schemaType.AnalyzeContentExpression(content, Types.Type.ELEMENT);
                        }
                        catch (XPathException e)
                        {
                            throw e.WithErrorCode(instr.IsXSLT() ? "XTTE1510" : "XQDY0027").WithLocation(instr.GetLocation());
                        }

                        if (xsiType != null)
                        {
                            try
                            {
                                config.CheckTypeDerivationIsOK(xsiType, declaredType, 0);
                            }
                            catch (SchemaException)
                            {
                                ValidationFailure ve = new ValidationFailure("The specified xsi:type " + xsiType.Description + " is not validly derived from the required type " + declaredType.Description);
                                ve.SetConstraintReference(1, "cvc-elt", "4.3");
                                ve.SetErrorCode(instr.IsXSLT() ? "XTTE1515" : "XQDY0027");
                                ve.Locator = instr.GetLocation();
                                throw ve.MakeException();
                            }
                        }
                    }
                }
                else if (validation == Validation.LAX)
                {
                    ISchemaDeclaration decl = config.GetElementDeclaration(fp);
                    if (decl == null)
                    {
                        env.IssueWarning("There is no global element declaration for " + elementName.DisplayName, DAXonErrorCode.SXWN9031, instr.GetLocation());
                        itemType = new NameTest(Types.Type.ELEMENT, fp, config.GetNamePool());
                    }
                    else
                    {
                        schemaType = decl.GetType();
                        instr.ValidationOptions = instr.ValidationOptions.WithTopLevelType(schemaType);
                        itemType = new CombinedNodeTest(new NameTest(Types.Type.ELEMENT, fp, config.GetNamePool()), Token.INTERSECT, new ContentTypeTest(Types.Type.ELEMENT, instr.GetSchemaType(), config, false));
                        try
                        {
                            schemaType.AnalyzeContentExpression(content, Types.Type.ELEMENT);
                        }
                        catch (XPathException e)
                        {
                            throw e.WithErrorCode(instr.IsXSLT() ? "XTTE1515" : "XQDY0027").WithLocation(instr.GetLocation());
                        }
                    }
                }
                else if (validation == Validation.PRESERVE)
                {

                    // we know the result will be an element of type xs:anyType
                    itemType = new CombinedNodeTest(new NameTest(Types.Type.ELEMENT, fp, config.GetNamePool()), Token.INTERSECT, new ContentTypeTest(Types.Type.ELEMENT, AnyType.INSTANCE, config, false));
                }
                else
                {

                    // we know the result will be an untyped element
                    itemType = new CombinedNodeTest(new NameTest(Types.Type.ELEMENT, fp, config.GetNamePool()), Token.INTERSECT, new ContentTypeTest(Types.Type.ELEMENT, Untyped.INSTANCE, config, false));
                }
            }
            else
            {
                itemType = new CombinedNodeTest(new NameTest(Types.Type.ELEMENT, fp, config.GetNamePool()), Token.INTERSECT, new ContentTypeTest(Types.Type.ELEMENT, schemaType, config, false));
                try
                {
                    schemaType.AnalyzeContentExpression(content, Types.Type.ELEMENT);
                }
                catch (XPathException e)
                {
                    throw e.WithErrorCode(instr.IsXSLT() ? "XTTE1540" : "XQDY0027").WithLocation(instr.GetLocation());
                }
            }

            return itemType;
        }

        public override Types.ItemType GetItemType()
        {
            if (itemType == null)
            {
                return base.GetItemType();
            }

            return itemType;
        }

        public override void GatherProperties(Action<string, object> consumer)
        {
            consumer("name",FixedElementName);
        }

        private ISchemaType GetXSIType(IStaticContext env)
        {
            if (GetContentExpression() is FixedAttribute)
            {
                return TestForXSIType((FixedAttribute)GetContentExpression(), env);
            }
            else if (GetContentExpression() is Block)
            {
                foreach (Operand o in GetContentExpression().Operands())
                {
                    Expression exp = o.GetChildExpression();
                    if (exp is FixedAttribute)
                    {
                        ISchemaType type = TestForXSIType((FixedAttribute)exp, env);
                        if (type != null)
                        {
                            return type;
                        }
                    }
                }

                return null;
            }
            else
            {
                return null;
            }
        }

        private ISchemaType TestForXSIType(FixedAttribute fat, IStaticContext env)
        {
            int att = fat.AttributeFingerprint;
            if (att == StandardNames.XSI_TYPE)
            {
                Expression attValue = fat.Select;
                if (attValue is StringLiteral)
                {
                    try
                    {
                        string[] parts = NameChecker.GetQNameParts(((StringLiteral)attValue).Stringify());

                        // The only namespace bindings we can trust are those declared on this element
                        // We could also trust those on enclosing LREs in the same function/template,
                        // but it's not a big win to go looking for them.
                        NamespaceUri uri = namespaceBindings.GetNamespaceUri(parts[0]);
                        if (uri == null)
                        {
                            return null;
                        }
                        else
                        {
                            return env.GetConfiguration().GetSchemaType(new StructuredQName("", uri, parts[1]));
                        }
                    }
                    catch (QNameException e)
                    {
                        throw new XPathException(e.GetMessage());
                    }
                }
            }

            return null;
        }

        public override void CheckPermittedContents(ISchemaType parentType, bool whole)
        {
            if (parentType is ISimpleType)
            {
                throw new XPathException("Element " + elementName.DisplayName + " is not permitted here: the containing element is of simple type " + parentType.Description).AsTypeError().WithLocation(GetLocation());
            }
            else if (((IComplexType)parentType).IsSimpleContent())
            {
                throw new XPathException("Element " + elementName.DisplayName + " is not permitted here: the containing element has a complex type with simple content").AsTypeError().WithLocation(GetLocation());
            }


            // Check that a sequence consisting of this element alone is valid against the content model
            if (whole)
            {
                Expression parent = ParentExpression;
                Block block = new Block(new Expression[] { this });
                parentType.AnalyzeContentExpression(block, Types.Type.ELEMENT);
                ParentExpression = parent;
            }

            ISchemaType type;
            try
            {
                int fp = elementName.ObtainFingerprint(GetConfiguration().GetNamePool());
                type = ((IComplexType)parentType).GetElementParticleType(fp, true);
            }
            catch (MissingComponentException e)
            {
                throw new XPathException(e.Message);
            }

            if (type == null)
            {
                XPathException err = new XPathException("Element " + elementName.DisplayName + " is not permitted in the content model of the complex type " + parentType.Description);
                err.SetIsTypeError(true);
                err.SetLocation(GetLocation());
                err.SetErrorCode(IsXSLT() ? "XTTE1510" : "XQDY0027");
                throw err;
            }

            if (type is AnyType)
            {
                return;
            }

            try
            {
                GetContentExpression().CheckPermittedContents(type, true);
            }
            catch (XPathException e)
            {
                throw e.MaybeWithLocation(GetLocation());
            }
        }

        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("elem", this);
            @out.EmitAttribute("name", elementName.DisplayName);
            @out.EmitAttribute("nsuri", elementName.GetNamespaceUri().ToString());
            string flags = GetInheritanceFlags();
            if (!elementName.GetNamespaceUri().IsEmpty() && (elementName.GetPrefix().Length == 0))
            {
                flags += "d"; // "d" to indicate default namespace
            }

            if (IsLocal())
            {
                flags += "l";
            }

            if (!(flags.Length == 0))
            {
                @out.EmitAttribute("flags", flags);
            }

            StringBuilder fsb = new StringBuilder(256);
            if (!namespaceBindings.IsEmpty())
            {
                foreach (NamespaceBinding ns in namespaceBindings)
                {
                    string prefix = ns.GetPrefix();
                    if (!prefix.Equals("xml"))
                    {
                        fsb.Append((prefix.Length == 0) ? "#" : prefix);
                        if (!ns.GetNamespaceUri().Equals(GetRetainedStaticContext().GetURIForPrefix(prefix, true)))
                        {
                            fsb.Append('=');
                            fsb.Append(ns.GetNamespaceUri());
                        }

                        fsb.Append(' ');
                    }
                }

                fsb.Length = fsb.Length - 1;
                @out.EmitAttribute("namespaces", fsb.ToString());
            }

            ExportValidationAndType(@out);
            GetContentExpression().Export(@out);
            @out.EndElement();
        }

        public override string ToString()
        {
            return "<" + elementName.GetStructuredQName().DisplayName + " {" + GetContentExpression().ToString() + "}/>";
        }

        public override string ToShortString()
        {
            return "<" + elementName.GetStructuredQName().DisplayName + " {" + GetContentExpression().ToShortString() + "}/>";
        }

        public override Elaborator GetElaborator()
        {
            return new FixedElementElaborator();
        }


        /// <summary>
        /// Elaborator for a FixedElement (literal result element) expression.
        /// </summary>
        internal sealed class FixedElementElaborator : ComplexNodePushElaborator
        {
            public override IPushEvaluator ElaborateForPush()
            {
                FixedElement expr = (FixedElement)GetExpression();
                IPushEvaluator contentPusher = expr.GetContentExpression().MakeElaborator().ElaborateForPush();
                ISchemaType typeCode = expr.GetValidationAction() == Validation.PRESERVE ? AnyType.INSTANCE : Untyped.INSTANCE;
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
                return (@out, context) =>
                {
                    try
                    {
                        INodeName elemName = expr.FixedElementName;
                        IReceiver elemOut = @out;
                        if (!expr.preservingTypes)
                        {
                            ParseOptions options = expr.ValidationOptions.WithTopLevelElement(elemName.GetStructuredQName());
                            context.GetConfiguration().PrepareValidationReporting(context, options);
                            IReceiver validator = context.GetConfiguration().GetElementValidator(elemOut, options, expr.GetLocation());
                            if (validator != elemOut)
                            {
                                @out = new ComplexContentOutputter(validator);
                            }
                        }

                        if (@out.GetSystemId() == null)
                        {
                            @out.SetSystemId(expr.StaticBaseURIString);
                        }

                        @out.StartElement(elemName, typeCode, expr.GetLocation(), finalProperties);

                        // output the required namespace nodes as one batch: CCO adopts the
                        // instruction's constant NamespaceMap directly (no per-binding Put allocations)
                        @out.Namespaces(expr.namespaceBindings, ReceiverOption.NAMESPACE_OK);


                        // process subordinate instructions to generate attributes and content
                        ITailCall tc = contentPusher.ProcessLeavingTail(@out, context);
                        Expression.DispatchTailCall(tc);

                        // output the element end tag (which will fail if validation fails)
                        @out.EndElement();
                    }
                    catch (XPathException e) when (!(e is XPathException.StackOverflow))
                    {
                        // Filtered: a literal result element nests once per level of a recursive
                        // template, and decorating from inside a catch re-enters exception dispatch
                        // at ~20KB of stack per level - far more than the descent itself cost.
                        throw e.MaybeWithLocation(expr.GetLocation()).MaybeWithContext(context);
                    }

                    return null;
                };
            }
        }
    }
}
