////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Expressions.Instructions
{
    internal sealed class ComputedAttribute : AttributeCreator
    {
        private readonly Operand nameOp;
        private Operand namespaceOp;
        private readonly bool allowNameAsQName;

        public override int InstructionNameCode => StandardNames.XSL_ATTRIBUTE;

        public Expression NameExp
        {
            get => nameOp.GetChildExpression(); set
            {
                nameOp.SetChildExpression(value);
            }
        }
        public ComputedAttribute(Expression attributeName, Expression @namespace, int validationAction, ISimpleType schemaType, bool allowNameAsQName)
        {
            nameOp = new Operand(this, attributeName, OperandRole.SINGLE_ATOMIC);
            if (@namespace != null)
            {
                namespaceOp = new Operand(this, @namespace, OperandRole.SINGLE_ATOMIC);
            }

            SetSchemaType(schemaType);
            SetValidationAction(validationAction);
            SetOptions(ReceiverOption.NONE);
            this.allowNameAsQName = allowNameAsQName;
        }

        public override void SetRejectDuplicates()
        {
            SetOptions(GetOptions() | ReceiverOption.REJECT_DUPLICATES);
        }

        public Expression GetNamespaceExp()
        {
            return namespaceOp == null ? null : namespaceOp.GetChildExpression();
        }

        public void SetNamespace(Expression @namespace)
        {
            if (@namespace != null)
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
        }

        public override IEnumerable<Operand> Operands()
        {
            return OperandSparseList(selectOp, nameOp, namespaceOp);
        }

        public INamespaceResolver GetNamespaceResolver()
        {
            return GetRetainedStaticContext();
        }

        public override Types.ItemType GetItemType()
        {
            return NodeKindTest.ATTRIBUTE;
        }

        public override int GetCardinality()
        {
            return StaticProperty.ALLOWS_ZERO_OR_ONE;
        }

        protected override int ComputeSpecialProperties()
        {
            return base.ComputeSpecialProperties() | StaticProperty.SINGLE_DOCUMENT_NODESET;
        }

        public override void LocalTypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {
            nameOp.TypeCheck(visitor, contextItemType);
            Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.INSTRUCTION, "attribute/name", 0);
            Configuration config = visitor.GetConfiguration();
            TypeHierarchy th = config.GetTypeHierarchy();
            if (allowNameAsQName)
            {

                // Can only happen in XQuery
                NameExp = config.GetTypeChecker(false).StaticTypeCheck(NameExp, Values.SequenceType.SINGLE_ATOMIC, role, visitor);
                Types.ItemType nameItemType = NameExp.GetItemType();
                bool maybeString = th.Relationship(nameItemType, BuiltInAtomicType.STRING) != Affinity.DISJOINT || th.Relationship(nameItemType, BuiltInAtomicType.UNTYPED_ATOMIC) != Affinity.DISJOINT;
                bool maybeQName = th.Relationship(nameItemType, BuiltInAtomicType.QNAME) != Affinity.DISJOINT;
                if (!(maybeString || maybeQName))
                {
                    throw new XPathException("The attribute name must be either an xs:string, an xs:QName, or untyped atomic").WithErrorCode("XPTY0004").AsTypeError().WithLocation(GetLocation());
                }
            }
            else
            {
                if (!th.IsSubType(NameExp.GetItemType(), BuiltInAtomicType.STRING))
                {
                    NameExp = SystemFunction.MakeCall("string", GetRetainedStaticContext(), NameExp);
                }
            }

            if (GetNamespaceExp() != null)
            {
                namespaceOp.TypeCheck(visitor, contextItemType);
            }

            if (Literal.IsAtomic(NameExp))
            {

                // Check we have a valid lexical QName, whose prefix @is in scope where necessary
                try
                {
                    AtomicValue val = (AtomicValue)((Literal)NameExp).GroundedValue;
                    if (val is StringValue)
                    {
                        string[] parts = NameChecker.CheckQNameParts(val.GetStringValue());
                        if (GetNamespaceExp() == null)
                        {
                            NamespaceUri uri = GetNamespaceResolver().GetURIForPrefix(parts[0], false);
                            if (uri == null)
                            {
                                string message = "Prefix " + parts[0] + " has not been declared";
                                if (IsXSLT())
                                {
                                    throw new XPathException(message, "XTDE0860").AsStaticError();
                                }
                                else
                                {
                                    throw new XPathException(message, "XQDY0074");
                                }
                            }

                            SetNamespace(new StringLiteral(uri.ToString()));
                        }
                    }
                }
                catch (XPathException e)
                {
                    if (e.ErrorCodeQName == null || e.HasErrorCode("FORG0001"))
                    {
                        e.SetErrorCode(IsXSLT() ? "XTDE0850" : "XQDY0074");
                    }

                    throw e.MaybeWithLocation(GetLocation()).AsStaticError();
                }
            }
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {
            Expression exp = base.Optimize(visitor, contextItemType);
            if (exp != this)
            {
                return exp;
            }


            // If the name is known statically, use a FixedAttribute instead
            if (NameExp is Literal && (GetNamespaceExp() == null || GetNamespaceExp() is Literal))
            {
                IXPathContext context = visitor.StaticContext.MakeEarlyEvaluationContext();
                INodeName nc = EvaluateNodeName(context);
                FixedAttribute fa = new FixedAttribute(nc, GetValidationAction(), GetSchemaType());
                fa.Select = Select;
                return fa;
            }

            return this;
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            ComputedAttribute exp = new ComputedAttribute(NameExp == null ? null : NameExp.Copy(rebindings), GetNamespaceExp() == null ? null : GetNamespaceExp().Copy(rebindings), GetValidationAction(), GetSchemaType(), allowNameAsQName);
            ExpressionTool.CopyLocationInfo(this, exp);
            exp.Select = Select.Copy(rebindings);
            exp.SetInstruction(IsInstruction());
            return exp;
        }

        public override void CheckPermittedContents(ISchemaType parentType, bool whole)
        {
            if (parentType is ISimpleType)
            {
                string msg = "Attributes are not permitted here: ";
                if (parentType.IsAnonymousType())
                {
                    msg += "the containing element is defined to have a simple type";
                }
                else
                {
                    msg += "the containing element is of simple type " + parentType.Description;
                }

                throw new XPathException(msg).AsTypeError().WithLocation(GetLocation());
            }
        }

        public override INodeName EvaluateNodeName(IXPathContext context)
        {
            IItem nameValue = NameExp.EvaluateItem(context);
            return ValidateNodeName(nameValue, context);
        }

        private INodeName ValidateNodeName(IItem nameValue, IXPathContext context)
        {
            NamePool pool = context.GetNamePool();
            string prefix;
            string localName;
            NamespaceUri uri = null;
            if (nameValue is StringValue)
            {

                // this will always be the case in XSLT
                string rawName = nameValue.GetStringValue();
                rawName = Whitespace.Trim(rawName); // required in XSLT; possibly wrong in XQuery
                if (rawName.StartsWith("Q{", StringComparison.Ordinal) && allowNameAsQName)
                {

                    // not allowed in XSLT; a little unclear in XQuery
                    try
                    {
                        StructuredQName qn = StructuredQName.FromEQName(rawName);
                        prefix = "";
                        localName = qn.GetLocalPart();
                        uri = qn.GetNamespaceUri();
                    }
                    catch (ArgumentException e)
                    {
                        throw new XPathException("Invalid EQName in computed attribute constructor: " + e.Message, "XQDY0074");
                    }

                    if (!NameChecker.IsValidNCName(localName))
                    {
                        throw new XPathException("Local part of EQName in computed attribute constructor is invalid", "XQDY0074");
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
                        string errorCode = IsXSLT() ? "XTDE0850" : "XQDY0074";
                        XPathException err1 = new XPathException("Invalid attribute name: " + rawName, errorCode, this.GetLocation());
                        throw DynamicError(GetLocation(), err1, context);
                    }

                    if (rawName.Equals("xmlns"))
                    {
                        if (GetNamespaceExp() == null)
                        {
                            string errorCode = IsXSLT() ? "XTDE0855" : "XQDY0044";
                            XPathException err = new XPathException("Invalid attribute name: " + rawName, errorCode, this.GetLocation());
                            throw DynamicError(GetLocation(), err, context);
                        }
                    }

                    if (prefix.Equals("xmlns"))
                    {
                        if (GetNamespaceExp() == null)
                        {
                            string errorCode = IsXSLT() ? "XTDE0860" : "XQDY0044";
                            XPathException err = new XPathException("Invalid attribute name: " + rawName, errorCode, this.GetLocation());
                            throw DynamicError(GetLocation(), err, context);
                        }
                        else
                        {

                            // ignore the prefix "xmlns"
                            prefix = "";
                        }
                    }
                }
            }
            else if (nameValue is QNameValue && allowNameAsQName)
            {

                // this is allowed in XQuery
                localName = ((QNameValue)nameValue).LocalName;
                uri = ((QNameValue)nameValue).GetNamespaceURI();
                if (localName.Equals("xmlns") && uri.IsEmpty())
                {
                    XPathException err = new XPathException("Invalid attribute name: xmlns", "XQDY0044", this.GetLocation());
                    throw DynamicError(GetLocation(), err, context);
                }

                if (uri.IsEmpty())
                {
                    prefix = "";
                }
                else
                {
                    prefix = ((QNameValue)nameValue).GetPrefix();
                    if ((prefix.Length == 0))
                    {
                        prefix = pool.SuggestPrefixForURI(uri);
                        if (prefix == null)
                        {
                            prefix = "ns0"; // If the prefix is a duplicate, a different one will be substituted
                        }
                    }

                    if (uri.Equals(NamespaceUri.XML) != (prefix == "xml"))
                    {
                        string message;
                        if (prefix == "xml")
                        {
                            message = "When the prefix is 'xml', the namespace URI must be " + NamespaceConstant.XML;
                        }
                        else
                        {
                            message = "When the namespace URI is " + NamespaceConstant.XML + ", the prefix must be 'xml'";
                        }

                        string errorCode = IsXSLT() ? "XTDE0835" : "XQDY0044";
                        XPathException err = new XPathException(message, errorCode, this.GetLocation());
                        throw DynamicError(GetLocation(), err, context);
                    }
                }

                if (prefix == "xmlns")
                {
                    XPathException err = new XPathException("Invalid attribute namespace: http://www.w3.org/2000/xmlns/", "XQDY0044", this.GetLocation());
                    throw DynamicError(GetLocation(), err, context);
                }
            }
            else
            {
                XPathException err = new XPathException("Attribute name must be either a string or a QName", "XPTY0004", this.GetLocation());
                err.SetIsTypeError(true);
                throw DynamicError(GetLocation(), err, context);
            }

            if (GetNamespaceExp() == null && uri == null)
            {
                if ((prefix.Length == 0))
                {
                    uri = NamespaceUri.NULL;
                }
                else
                {
                    uri = GetRetainedStaticContext().GetURIForPrefix(prefix, false);
                    if (uri == null)
                    {
                        string errorCode = IsXSLT() ? "XTDE0860" : "XQDY0074";
                        XPathException err = new XPathException("Undeclared prefix in attribute name: " + prefix, errorCode, this.GetLocation());
                        throw DynamicError(GetLocation(), err, context);
                    }
                }
            }
            else
            {
                if (uri == null)
                {

                    // generate a name using the supplied namespace URI
                    if (GetNamespaceExp() is StringLiteral)
                    {
                        uri = NamespaceUri.Of(((StringLiteral)GetNamespaceExp()).Stringify());
                    }
                    else
                    {
                        uri = NamespaceUri.Of(GetNamespaceExp().EvaluateAsString(context).ToString());
                        if (!StandardURIChecker.GetInstance().IsValidURI(uri.ToString()))
                        {
                            XPathException de = new XPathException("The value of the namespace attribute must be a valid URI", "XTDE0865", this.GetLocation());
                            throw DynamicError(GetLocation(), de, context);
                        }
                    }
                }

                if (uri.IsEmpty())
                {

                    // there is a special rule for this case in the XSLT specification;
                    // we force the attribute to go in the null namespace
                    prefix = "";
                }
                else
                {

                    // if a suggested prefix is given, use it; otherwise try to find a prefix
                    // associated with this URI; if all else fails, invent one.
                    if ((prefix.Length == 0))
                    {
                        prefix = pool.SuggestPrefixForURI(uri);
                        if (prefix == null)
                        {
                            prefix = "ns0"; // this will be replaced later if it is already in use
                        }
                    }
                    else if (uri.Equals(NamespaceUri.XML))
                    {
                        // only the prefix xml names the XML namespace: the name is given it, as namespace fixup would
                        prefix = "xml";
                    }
                }
            }

            if (uri.Equals(NamespaceUri.XMLNS))
            {
                string errorCode = IsXSLT() ? "XTDE0865" : "XQDY0044";
                XPathException err = new XPathException("Cannot create attribute in namespace " + uri, errorCode, this.GetLocation());
                throw DynamicError(GetLocation(), err, context);
            }

            return new FingerprintedQName(prefix, uri, localName);
        }

        // we force the attribute to go in the null @namespace
        public override IItem EvaluateItem(IXPathContext context)
        {
            IItem node = base.EvaluateItem(context);
            ValidateOrphanAttribute((Orphan)node, context);
            return node;
        }

        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("compAtt", this);
            if (GetValidationAction() != Validation.SKIP)
            {
                @out.EmitAttribute("validation", Validation.Describe(GetValidationAction()));
            }

            ISimpleType type = GetSchemaType();
            if (type != null)
            {
                @out.EmitAttribute("type", type.GetStructuredQName());
            }

            string flags = "";
            if (IsLocal())
            {
                flags += "l";
            }

            if (!(flags.Length == 0))
            {
                @out.EmitAttribute("flags", flags);
            }

            @out.SetChildRole("name");
            NameExp.Export(@out);
            if (GetNamespaceExp() != null)
            {
                @out.SetChildRole("namespace");
                GetNamespaceExp().Export(@out);
            }

            @out.SetChildRole("select");
            Select.Export(@out);
            @out.EndElement();
        }

        public override Elaborator GetElaborator()
        {
            return new ComputedAttributeElaborator();
        }

        private sealed class ComputedAttributeElaborator : SimpleNodePushElaborator
        {
            public override IPushEvaluator ElaborateForPush()
            {
                ComputedAttribute expr = (ComputedAttribute)GetExpression();
                ILocation loc = expr.GetLocation();
                int options = expr.GetOptions();
                IItemEvaluator nameEval = expr.NameExp.MakeElaborator().ElaborateForItem();
                if (expr.GetSchemaType() != null || expr.GetValidationAction() == Validation.STRICT || expr.GetValidationAction() == Validation.LAX)
                {
                    IUnicodeStringEvaluator contentEval = expr.Select.MakeElaborator().ElaborateForUnicodeString(true);
                    return (output, context) =>
                    {
                        IItem nameItem = nameEval.Eval(context);
                        INodeName name = expr.ValidateNodeName(nameItem, context);
                        UnicodeString content = contentEval.Eval(context);
                        ISimpleType ann = expr.Validate(name, content, context);
                        if (name.Equals(StandardNames.XML_ID_NAME))
                        {
                            content = Whitespace.CollapseWhitespace(content);
                        }

                        try
                        {
                            output.Attribute(name, ann, content.ToString(), loc, options);
                        }
                        catch (XPathException err)
                        {
                            throw DynamicError(loc, err, context);
                        }

                        return null;
                    };
                }
                else
                {
                    IStringEvaluator contentEval = expr.Select.MakeElaborator().ElaborateForString(true);
                    return (output, context) =>
                    {
                        IItem nameItem = nameEval.Eval(context);
                        INodeName name = expr.ValidateNodeName(nameItem, context);
                        string content = contentEval.Eval(context);
                        if (name.Equals(StandardNames.XML_ID_NAME))
                        {
                            content = Whitespace.CollapseWhitespace(content);
                        }

                        try
                        {
                            output.Attribute(name, BuiltInAtomicType.UNTYPED_ATOMIC, content, loc, options);
                        }
                        catch (XPathException err)
                        {
                            throw Instruction.DynamicError(loc, err, context);
                        }

                        return null;
                    };
                }
            }

            public override IItemEvaluator ElaborateForItem()
            {
                ComputedAttribute expr = (ComputedAttribute)GetExpression();
                if (expr.GetSchemaType() != null || expr.GetValidationAction() == Validation.STRICT || expr.GetValidationAction() == Validation.LAX)
                {
                    IItemEvaluator superEval = base.ElaborateForItem();
                    return (context) =>
                    {
                        Orphan o = (Orphan)superEval.Eval(context);
                        expr.ValidateOrphanAttribute(o, context);
                        return o;
                    };
                }
                else
                {
                    return base.ElaborateForItem();
                }
            }
        }
    }
}