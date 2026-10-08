////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Expressions.Instructions
{
    internal sealed class ComputedElement : ElementCreator
    {
        private readonly Operand nameOp;
        private Operand namespaceOp;
        private readonly bool allowNameAsQName;
        private ItemType itemType;

        /*defaultNamespace,*/
        public override int InstructionNameCode => StandardNames.XSL_ELEMENT;
        public ComputedElement(Expression elementName, Expression @namespace, ISchemaType schemaType, int validation, bool inheritNamespaces, bool allowQName)
        {
            nameOp = new Operand(this, elementName, OperandRole.SINGLE_ATOMIC);
            if (@namespace != null)
            {
                namespaceOp = new Operand(this, @namespace, OperandRole.SINGLE_ATOMIC);
            }

            SetValidationAction(validation, schemaType);
            preservingTypes = schemaType == null && validation == Validation.PRESERVE;
            this.bequeathNamespacesToChildren = inheritNamespaces;
            allowNameAsQName = allowQName;
        }

        public Expression GetNameExp()
        {
            return nameOp.GetChildExpression();
        }

        public Expression GetNamespaceExp()
        {
            return namespaceOp == null ? null : namespaceOp.GetChildExpression();
        }

        protected void SetNameExp(Expression elementName)
        {
            nameOp.SetChildExpression(elementName);
        }

        protected void SetNamespaceExp(Expression @namespace)
        {
            if (namespaceOp == null)
            {
                namespaceOp = new Operand(this, @namespace, OperandRole.SINGLE_ATOMIC);
            }
            else
            {
                namespaceOp.SetChildExpression(@namespace);
            }
        }

        public override IEnumerable<Operand> Operands()
        {
            return OperandSparseList(contentOp, nameOp, namespaceOp);
        }

        public INamespaceResolver GetNamespaceResolver()
        {
            return GetRetainedStaticContext();
        }

        public override Expression Simplify()
        {
            SetNameExp(GetNameExp().Simplify());
            if (GetNamespaceExp() != null)
            {
                SetNamespaceExp(GetNamespaceExp().Simplify());
            }

            Configuration config = GetConfiguration();
            bool schemaAware = GetPackageData().IsSchemaAware();
            preservingTypes |= !schemaAware;
            ISchemaType schemaType = GetSchemaType();
            if (schemaType != null)
            {
                itemType = new ContentTypeTest(Types.Type.ELEMENT, schemaType, config, false);
                schemaType.AnalyzeContentExpression(GetContentExpression(), Types.Type.ELEMENT);
            }
            else if (GetValidationAction() == Validation.STRIP || !schemaAware)
            {
                itemType = new ContentTypeTest(Types.Type.ELEMENT, Untyped.INSTANCE, config, false);
            }
            else
            {

                // paradoxically, we know less about the type if validation="strict" is specified!
                // We know that it won't be untyped, but we have no way of representing that.
                itemType = NodeKindTest.ELEMENT;
            }

            return base.Simplify();
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            base.TypeCheck(visitor, contextInfo);
            Configuration config = visitor.GetConfiguration();
            TypeHierarchy th = config.GetTypeHierarchy();
            if (allowNameAsQName)
            {

                // Can only happen in XQuery
                Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.INSTRUCTION, "element/name", 0);
                SetNameExp(config.GetTypeChecker(false).StaticTypeCheck(GetNameExp(), SequenceType.SINGLE_ATOMIC, role, visitor));
                ItemType supplied = GetNameExp().GetItemType();
                if (th.Relationship(supplied, BuiltInAtomicType.STRING) == Affinity.DISJOINT && th.Relationship(supplied, BuiltInAtomicType.UNTYPED_ATOMIC) == Affinity.DISJOINT && th.Relationship(supplied, BuiltInAtomicType.QNAME) == Affinity.DISJOINT)
                {
                    throw new XPathException("The name of a constructed element must be a string, QName, or untypedAtomic").WithErrorCode("XPTY0004").AsTypeError().WithLocation(GetLocation());
                }
            }
            else
            {
                if (!th.IsSubType(GetNameExp().GetItemType(), BuiltInAtomicType.STRING))
                {
                    SetNameExp(SystemFunction.MakeCall("string", GetRetainedStaticContext(), GetNameExp()));
                }
            }

            if (Literal.IsAtomic(GetNameExp()))
            {

                // Check we have a valid lexical QName, whose prefix @is in scope where necessary
                try
                {
                    AtomicValue val = (AtomicValue)((Literal)GetNameExp()).GroundedValue;
                    if (val is StringValue)
                    {
                        string[] parts = NameChecker.CheckQNameParts(val.GetStringValue());
                        if (GetNamespaceExp() == null)
                        {
                            string prefix = parts[0];
                            NamespaceUri uri = GetNamespaceResolver().GetURIForPrefix(prefix, true);
                            if (uri == null)
                            {
                                throw new XPathException("Prefix " + prefix + " has not been declared").WithErrorCode("XPST0081").AsStaticError();
                            }

                            SetNamespaceExp(new StringLiteral(uri.ToString()));
                        }
                    }
                }
                catch (XPathException e)
                {
                    throw e.MaybeWithErrorCode(IsXSLT() ? "XTDE0820" : "XQDY0074").ReplacingErrorCode("FORG0001", IsXSLT() ? "XTDE0820" : "XQDY0074").ReplacingErrorCode("XPST0081", IsXSLT() ? "XTDE0830" : "XQDY0074").MaybeWithLocation(GetLocation()).AsStaticError();
                }
            }

            return base.TypeCheck(visitor, contextInfo);
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            ComputedElement ce = new ComputedElement(GetNameExp().Copy(rebindings), GetNamespaceExp() == null ? null : GetNamespaceExp().Copy(rebindings), GetSchemaType(), GetValidationAction(), bequeathNamespacesToChildren, allowNameAsQName);
            ExpressionTool.CopyLocationInfo(this, ce);
            ce.SetContentExpression(GetContentExpression().Copy(rebindings));
            return ce;
        }

        /*defaultNamespace,*/
        public override ItemType GetItemType()
        {
            if (itemType == null)
            {
                return base.GetItemType();
            }

            return itemType;
        }

        /*defaultNamespace,*/
        public override void CheckPermittedContents(ISchemaType parentType, bool whole)
        {
            if (parentType is ISimpleType || ((IComplexType)parentType).IsSimpleContent())
            {
                string msg = "Elements are not permitted here: the containing element ";
                if (parentType is ISimpleType)
                {
                    if (parentType.IsAnonymousType())
                    {
                        msg += "is defined to have a simple type";
                    }
                    else
                    {
                        msg += "is of simple type " + parentType.Description;
                    }
                }
                else
                {
                    msg += "has a complex type with simple content";
                }

                throw new XPathException(msg).AsTypeError().WithLocation(GetLocation());
            } // NOTE: we could in principle check that if all the elements permitted in the content of the parentType
            // themselves have a simple type (not uncommon, perhaps) then this element must not have element content.
        }

        /*defaultNamespace,*/
        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("compElem", this);
            string flags = GetInheritanceFlags();
            if (IsLocal())
            {
                flags += "l";
            }

            if (!(flags.Length == 0))
            {
                @out.EmitAttribute("flags", flags);
            }

            ExportValidationAndType(@out);
            @out.SetChildRole("name");
            GetNameExp().Export(@out);
            if (GetNamespaceExp() != null)
            {
                @out.SetChildRole("namespace");
                GetNamespaceExp().Export(@out);
            }

            @out.SetChildRole("content");
            GetContentExpression().Export(@out);
            @out.EndElement();
        }

        /*defaultNamespace,*/
        public override Elaborator GetElaborator()
        {
            return new ComputedElementElaborator();
        }


        /*defaultNamespace,*/
        /// <summary>
        /// Elaborator for a FixedElement (literal result element) expression.
        /// </summary>
        internal sealed class ComputedElementElaborator : ComplexNodePushElaborator
        {
            public override IPushEvaluator ElaborateForPush()
            {
                ComputedElement expr = (ComputedElement)GetExpression();
                bool isXsltInstruction = expr.IsXSLT();
                IPushEvaluator contentPusher = expr.GetContentExpression().MakeElaborator().ElaborateForPush();
                IStringEvaluator namespaceEvaluator = expr.GetNamespaceExp() == null ? null : expr.GetNamespaceExp().MakeElaborator().ElaborateForString(true);
                IItemEvaluator localNameEvaluator = expr.GetNameExp().MakeElaborator().ElaborateForItem();
                IItemEvaluator nodeNameEvaluator = (context) =>
                {
                    Controller controller = context.GetController();
                    string prefix;
                    string localName;
                    NamespaceUri uri = null;
                    AtomicValue nameValue = (AtomicValue)localNameEvaluator.Eval(context);
                    if (nameValue == null)
                    {
                        string errorCode = isXsltInstruction ? "XTDE0820" : "XPTY0004";
                        XPathException err1 = new XPathException("Invalid element name (empty sequence)", errorCode, expr.GetLocation());
                        throw DynamicError(expr.GetLocation(), err1, context);
                    }

                    if (nameValue is StringValue)
                    {

                        // which includes UntypedAtomic
                        // this will always be the case in XSLT
                        string rawName = nameValue.GetStringValue();
                        rawName = Whitespace.Trim(rawName);

                        // this will always be the case in XSLT
                        if (rawName.StartsWith("Q{", StringComparison.Ordinal) && expr.allowNameAsQName)
                        {

                            // Unclear whether this is allowed: see https://github.com/w3c/qtspecs/issues/9
                            // It clearly is NOT allowed in XSLT 3.0 (though for no good reason)
                            try
                            {
                                StructuredQName qn = StructuredQName.FromEQName(rawName);
                                prefix = "";
                                localName = qn.GetLocalPart();
                                uri = qn.GetNamespaceUri();
                            }
                            catch (ArgumentException e)
                            {
                                throw new XPathException("Invalid EQName in computed element constructor: " + e.Message, "XQDY0074");
                            }

                            if (!NameChecker.IsValidNCName(localName))
                            {
                                throw new XPathException("Local part of EQName in computed element constructor is invalid", "XQDY0074");
                            }
                        }
                        else
                        {
                            try
                            {
                                string[] parts = NameChecker.GetQNameParts(rawName);
                                prefix = parts[0];
                                localName = parts[1];
                            }
                            catch (QNameException err)
                            {
                                string message = "Invalid element name. " + err.GetMessage();
                                if (rawName.Length == 0)
                                {
                                    message = "Supplied element name is a zero-length string";
                                }

                                string errorCode = isXsltInstruction ? "XTDE0820" : "XQDY0074";
                                XPathException err1 = new XPathException(message, errorCode, expr.GetLocation());
                                throw DynamicError(expr.GetLocation(), err1, context);
                            }
                        }
                    }
                    else if (nameValue is QNameValue && expr.allowNameAsQName)
                    {

                        // this is allowed in XQuery
                        localName = ((QNameValue)nameValue).LocalName;
                        uri = ((QNameValue)nameValue).GetNamespaceURI();
                        prefix = ((QNameValue)nameValue).GetPrefix();
                        if (prefix.Equals("xmlns"))
                        {
                            XPathException err = new XPathException("Computed element name has prefix xmlns", "XQDY0096", expr.GetLocation());
                            throw DynamicError(expr.GetLocation(), err, context);
                        }
                    }
                    else
                    {
                        string errorCode = isXsltInstruction ? "XTDE0820" : "XPTY0004";
                        XPathException err = new XPathException("Computed element name has incorrect type", errorCode, expr.GetLocation());
                        err.SetIsTypeError(true);
                        throw DynamicError(expr.GetLocation(), err, context);
                    }

                    if (namespaceEvaluator == null && uri == null)
                    {
                        uri = expr.GetRetainedStaticContext().GetURIForPrefix(prefix, true);
                        if (uri == null)
                        {
                            string errorCode = isXsltInstruction ? "XTDE0830" : prefix.Equals("xmlns") ? "XQDY0096" : "XQDY0074";
                            XPathException err = new XPathException("Undeclared prefix in element name: " + prefix, errorCode, expr.GetLocation());
                            throw DynamicError(expr.GetLocation(), err, context);
                        }
                    }
                    else
                    {
                        if (uri == null)
                        {
                            string nsUri = namespaceEvaluator.Eval(context);
                            uri = NamespaceUri.Of(nsUri);

                            // TODO: bypass check if it's a string literal
                            if (!StandardURIChecker.GetInstance().IsValidURI(uri.ToString()))
                            {
                                XPathException de = new XPathException("The value of the namespace attribute must be a valid URI", "XTDE0835", expr.GetLocation());
                                throw DynamicError(expr.GetLocation(), de, context);
                            }
                        }

                        if (uri.IsEmpty())
                        {

                            // there is a special rule for this case in the specification;
                            // we force the element to go in the null namespace
                            prefix = "";
                        }

                        if (prefix.Equals("xmlns"))
                        {

                            // this isn't a legal prefix so we mustn't use it
                            prefix = "x-xmlns";
                        }
                    }

                    if (uri.Equals(NamespaceUri.XMLNS))
                    {
                        string errorCode = isXsltInstruction ? "XTDE0835" : "XQDY0096";
                        XPathException err = new XPathException("Cannot create element in namespace " + uri, errorCode, expr.GetLocation());
                        throw DynamicError(expr.GetLocation(), err, context);
                    }

                    if (uri.Equals(NamespaceUri.XML) != prefix.Equals("xml"))
                    {
                        string message;
                        if (prefix.Equals("xml"))
                        {
                            message = "When the prefix is 'xml', the namespace URI must be " + NamespaceConstant.XML;
                        }
                        else
                        {
                            message = "When the namespace URI is " + NamespaceConstant.XML + ", the prefix must be 'xml'";
                        }

                        string errorCode = isXsltInstruction ? "XTDE0835" : "XQDY0096";
                        XPathException err = new XPathException(message, errorCode, expr.GetLocation());
                        throw DynamicError(expr.GetLocation(), err, context);
                    }

                    return new QNameValue(prefix, uri, localName);
                };
                ISchemaType typeCode = expr.GetValidationAction() == Validation.PRESERVE ? AnyType.INSTANCE : Untyped.INSTANCE;
                return (@out, context) =>
                {
                    try
                    {
                        QNameValue elemName = (QNameValue)nodeNameEvaluator.Eval(context);
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
                        @out.StartElement(new FingerprintedQName(elemName.GetStructuredQName()), typeCode, expr.GetLocation(), properties);

                        // process subordinate instructions to generate attributes and content
                        ITailCall tc = contentPusher.ProcessLeavingTail(@out, context);
                        Expression.DispatchTailCall(tc);

                        // output the element end tag (which will fail if validation fails)
                        @out.EndElement();
                    }
                    catch (XPathException e) when (!(e is XPathException.StackOverflow))
                    {
                        // Filtered: see FixedElement - xsl:element nests per recursion level too.
                        throw e.MaybeWithLocation(expr.GetLocation()).MaybeWithContext(context);
                    }

                    return null;
                };
            }
        }
    }
}
