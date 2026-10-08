////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Utilities;
using OutSmart.DAXon.Values;
using System;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Types;
namespace OutSmart.DAXon.Expressions.Instructions
{
    internal sealed class FixedAttribute : AttributeCreator
    {
        private readonly INodeName nodeName;

        public override int InstructionNameCode => StandardNames.XSL_ATTRIBUTE;

        public override string ExpressionName => "att";

        public INodeName AttributeName => nodeName;

        public int AttributeFingerprint => nodeName.Fingerprint;
        public FixedAttribute(INodeName nodeName, int validationAction, ISimpleType schemaType)
        {
            this.nodeName = nodeName;
            SetSchemaType(schemaType);
            SetValidationAction(validationAction);
            SetOptions(ReceiverOption.NONE);
        }

        public override void GatherProperties(Action<string, object> consumer)
        {
            consumer("name",AttributeName);
        }

        public override void LocalTypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {

            // If attribute name is xml:id, add whitespace normalization
            if (nodeName.Equals(StandardNames.XML_ID_NAME) && !Select.IsCallOn(typeof(NormalizeSpace_1)))
            {
                Expression select = SystemFunction.MakeCall("normalize-space", GetRetainedStaticContext(), Select);
                Select = select;
            }

            Configuration config = visitor.GetConfiguration();
            ConversionRules rules = config.GetConversionRules();
            ISimpleType schemaType = GetSchemaType();
            string errorCode = "XTTE1540";
            if (schemaType == null)
            {
                int validation = GetValidationAction();
                if (validation == Validation.STRICT)
                {
                    ISchemaDeclaration decl = config.GetAttributeDeclaration(nodeName.GetStructuredQName());
                    if (decl == null)
                    {
                        throw new XPathException("Strict validation fails: there is no global attribute declaration for " + nodeName.DisplayName).WithErrorCode("XTTE1510").WithLocation(GetLocation());
                    }

                    schemaType = (ISimpleType)decl.GetType();
                    errorCode = "XTTE1510";
                }
                else if (validation == Validation.LAX)
                {
                    ISchemaDeclaration decl = config.GetAttributeDeclaration(nodeName.GetStructuredQName());
                    if (decl != null)
                    {
                        schemaType = (ISimpleType)decl.GetType();
                        errorCode = "XTTE1515";
                    }
                    else
                    {
                        visitor.StaticContext.IssueWarning("Lax validation has no effect: there is no global attribute declaration for " + nodeName.DisplayName, DAXonErrorCode.SXWN9031, GetLocation());
                    }
                }
            }


            // Attempt early validation if possible
            if (Literal.IsAtomic(Select) && schemaType != null && !schemaType.IsNamespaceSensitive())
            {
                UnicodeString value = ((Literal)Select).GroundedValue.UnicodeStringValue;
                ValidationFailure err = schemaType.ValidateContent(value, DummyNamespaceResolver.GetInstance(), rules);
                if (err != null)
                {
                    throw new XPathException("Attribute value " + Err.Wrap(value, Err.VALUE) + " does not the match the required type " + schemaType.Description + ". " + err.GetMessage()).WithErrorCode(errorCode);
                }
            }


            // If value is @fixed, test whether there are any special characters that might need to be
            // escaped when the time comes for serialization
            if (Select is StringLiteral)
            {
                bool special = false;
                string val = ((StringLiteral)Select).Stringify();
                for (int k = 0; k < val.Length; k++)
                {
                    char c = val[k];
                    if ((int)c < 33 || (int)c > 126 || c == '<' || c == '>' || c == '&' || c == '"' || c == '\'')
                    {
                        special = true;
                        break;
                    }
                }

                if (!special)
                {
                    SetNoSpecialChars();
                }
            }
        }

        public override int GetCardinality()
        {
            return StaticProperty.EXACTLY_ONE;
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            FixedAttribute exp = new FixedAttribute(nodeName, GetValidationAction(), GetSchemaType());
            ExpressionTool.CopyLocationInfo(this, exp);
            exp.Select = Select.Copy(rebindings);
            exp.SetInstruction(IsInstruction());
            return exp;
        }

        public override INodeName EvaluateNodeName(IXPathContext context)
        {
            return nodeName;
        }

        public override void CheckPermittedContents(ISchemaType parentType, bool whole)
        {
            int fp = nodeName.Fingerprint;
            if (fp == StandardNames.XSI_TYPE || fp == StandardNames.XSI_SCHEMA_LOCATION || fp == StandardNames.XSI_NIL || fp == StandardNames.XSI_NO_NAMESPACE_SCHEMA_LOCATION)
            {
                return;
            }

            if (parentType is ISimpleType)
            {
                XPathException err = new XPathException("Attribute " + nodeName.DisplayName + " is not permitted in the content model of the simple type " + parentType.Description).AsTypeError().WithLocation(GetLocation()).WithErrorCode(GetPackageData().IsXSLT() ? "XTTE1510" : "XQDY0027");
                throw err;
            }

            ISchemaType type;
            try
            {
                type = ((IComplexType)parentType).GetAttributeUseType(nodeName.GetStructuredQName());
            }
            catch (SchemaException e)
            {
                throw new XPathException(e?.Message);
            }

            if (type == null)
            {
                throw new XPathException("Attribute " + nodeName.DisplayName + " is not permitted in the content model of the complex type " + parentType.Description).AsTypeError().WithLocation(GetLocation()).WithErrorCode(GetPackageData().IsXSLT() ? "XTTE1510" : "XQDY0027");
            }

            try
            {

                // When select is a SimpleContentConstructor, this does nothing
                Select.CheckPermittedContents(type, true);
            }
            catch (XPathException e)
            {
                throw e.MaybeWithLocation(GetLocation());
            }
        }

        public override IItem EvaluateItem(IXPathContext context)
        {
            Orphan o = (Orphan)base.EvaluateItem(context);
            ValidateOrphanAttribute(o, context);
            return o;
        }

        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("att", this);
            @out.EmitAttribute("name", nodeName.DisplayName);
            if (!nodeName.GetStructuredQName().HasURI(NamespaceUri.NULL))
            {
                @out.EmitAttribute("nsuri", nodeName.GetStructuredQName().GetNamespaceUri().ToString());
            }

            if (GetValidationAction() != Validation.SKIP && GetValidationAction() != Validation.BY_TYPE)
            {
                @out.EmitAttribute("validation", Validation.Describe(GetValidationAction()));
            }

            if (GetSchemaType() != null)
            {
                @out.EmitAttribute("type", GetSchemaType().GetStructuredQName());
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

            Select.Export(@out);
            @out.EndElement();
        }

        public override string ToShortString()
        {
            return "attr{" + nodeName.DisplayName + "=...}";
        }

        public override Elaborator GetElaborator()
        {
            return new FixedAttributeElaborator();
        }

        private sealed class FixedAttributeElaborator : SimpleNodePushElaborator
        {
            public override IPushEvaluator ElaborateForPush()
            {
                FixedAttribute expr = (FixedAttribute)GetExpression();
                INodeName name = expr.nodeName;
                ILocation loc = expr.GetLocation();
                int options = expr.GetOptions();
                bool collapse = name.Equals(StandardNames.XML_ID_NAME);
                if (collapse || expr.GetSchemaType() != null || expr.GetValidationAction() == Validation.STRICT || expr.GetValidationAction() == Validation.LAX)
                {
                    IUnicodeStringEvaluator contentEval = Elaboration.FusedChildValue.TryFuse(expr.Select)
                        ?? expr.Select.MakeElaborator().ElaborateForUnicodeString(true);
                    return (output, context) =>
                    {
                        UnicodeString content = contentEval.Eval(context);
                        ISimpleType ann = expr.Validate(name, content, context);
                        if (collapse)
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
                    // AVT shapes over a child element take the fused TinyTree read (P18)
                    IStringEvaluator contentEval = Elaboration.FusedChildValue.TryFuseString(expr.Select)
                        ?? expr.Select.MakeElaborator().ElaborateForString(true);
                    if (expr.Select is Literal)
                    {

                        // constant value (attr="literal"): evaluate once at elaboration; a throwing
                        // literal keeps the per-call path so the error stays a dynamic one
                        string constContent = null;
                        bool hoisted = false;
                        try
                        {
                            constContent = contentEval.Eval(null);
                            hoisted = constContent != null;
                        }
                        catch (XPathException)
                        {
                        }

                        if (hoisted)
                        {
                            return (output, context) =>
                            {
                                try
                                {
                                    output.Attribute(name, BuiltInAtomicType.UNTYPED_ATOMIC, constContent, loc, options);
                                }
                                catch (XPathException err)
                                {
                                    throw Instruction.DynamicError(loc, err, context);
                                }

                                return null;
                            };
                        }
                    }

                    return (output, context) =>
                    {
                        string content = contentEval.Eval(context);
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
                FixedAttribute expr = (FixedAttribute)GetExpression();
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