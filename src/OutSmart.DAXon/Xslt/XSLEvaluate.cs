////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Values.Maps;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Linq;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// Handler for xsl:evaluate elements in XSLT 3.0 stylesheet. <br>
    /// </summary>
    public class XSLEvaluate : StyleElement
    {
        Expression xpath;
        SequenceType requiredType = SequenceType.ANY_SEQUENCE;
        Expression namespaceContext;
        Expression contextItem;
        Expression baseUri;
        Expression schemaAware;
        Expression withParams;
        Expression options;
        bool hasFallbackChildren;

        protected virtual ItemType ReturnedItemType => AnyItemType.GetInstance();

        // OK
        public virtual Expression TargetExpression => xpath;

        public virtual Expression BaseUriExpression => baseUri;

        public virtual Expression NamespaceContextExpression => namespaceContext;

        public virtual Expression SchemaAwareExpression => schemaAware;

        public virtual Expression WithParamsExpression => withParams;

        public virtual Expression OptionsExpression => options;
        public override bool IsInstruction()
        {
            return true;
        }

        protected override bool IsPermittedChild(StyleElement child)
        {
            return child is XSLLocalParam;
        }

        protected override bool MayContainSequenceConstructor()
        {
            return false;
        }

        public override void PrepareAttributes()
        {
            IAttributeMap atts = Attributes();
            string xpathAtt = null;
            string asAtt = null;
            string contextItemAtt = null;
            string schemaAwareAtt = null;
            string withParamsAtt = null;
            foreach (AttributeInfo att in atts)
            {
                INodeName attName = att.GetNodeName();
                string f = attName.DisplayName;
                switch (f)
                {
                    case "xpath":
                        xpathAtt = att.Value;
                        xpath = MakeExpression(xpathAtt, att);
                        break;
                    case "as":
                        asAtt = att.Value;
                        break;
                    case "context-item":
                        contextItemAtt = att.Value;
                        contextItem = MakeExpression(contextItemAtt, att);
                        break;
                    case "base-uri":
                        string baseUriAtt = att.Value;
                        baseUri = MakeAttributeValueTemplate(baseUriAtt, att);
                        break;
                    case "namespace-context":
                        string namespaceContextAtt = att.Value;
                        namespaceContext = MakeExpression(namespaceContextAtt, att);
                        break;
                    case "schema-aware":
                        schemaAwareAtt = Whitespace.Trim(att.Value);
                        schemaAware = MakeAttributeValueTemplate(schemaAwareAtt, att);
                        break;
                    case "with-params":
                        withParamsAtt = att.Value;
                        withParams = MakeExpression(withParamsAtt, att);
                        break;
                    default:
                        if (attName.GetLocalPart().Equals("options") && attName.GetNamespaceUri().Equals(NamespaceUri.SAXON))
                        {
                            if (IsExtensionAttributeAllowed(attName.DisplayName))
                            {
                                options = MakeExpression(att.Value, att);
                            }
                        }
                        else
                        {
                            CheckUnknownAttribute(attName);
                        }

                        break;
                }
            }

            if (xpathAtt == null)
            {
                ReportAbsence("xpath");
            }

            if (asAtt != null)
            {
                try
                {
                    requiredType = MakeSequenceType(asAtt);
                }
                catch (XPathException e)
                {
                    CompileErrorInAttribute(e, "as");
                }
            }

            if (contextItemAtt == null)
            {
                contextItem = Literal.MakeEmptySequence();
            }

            if (schemaAwareAtt == null)
            {
                schemaAware = new StringLiteral("no");
            }
            else if (schemaAware is StringLiteral)
            {
                CheckAttributeValue("schema-aware", schemaAwareAtt, true, StyleElement.YES_NO);
            }

            if (withParamsAtt == null)
            {
                withParamsAtt = "map{}";
                withParams = MakeExpression(withParamsAtt, null);
            }

            if (options == null)
            {
                options = MakeExpression("map{}", null);
            }
        }

        public override void Validate(ComponentDeclaration decl)
        {
            ContainingPackage.SetRetainUnusedFunctions();
            if (xpath == null)
            {
                xpath = new StringLiteral("''");
            }

            xpath = TypeCheck("xpath", xpath);
            baseUri = TypeCheck("base-uri", baseUri);
            contextItem = TypeCheck("context-item", contextItem);
            namespaceContext = TypeCheck("namespace-context", namespaceContext);
            schemaAware = TypeCheck("schema-aware", schemaAware);
            withParams = TypeCheck("with-params", withParams);
            options = TypeCheck("options", options);
            foreach (NodeInfo child in Children())
            {
                if (child is XSLWithParam)
                {
                }
                else if (child is XSLFallback)
                {
                    hasFallbackChildren = true;
                }
                else if (child.GetNodeKind() == Types.Type.TEXT)
                {

                    // with xml:space=preserve, white space nodes may still be there
                    if (!Whitespace.IsAllWhite(child.UnicodeStringValue))
                    {
                        CompileError("No character data is allowed within xsl:evaluate", "XTSE0010");
                    }
                }
                else
                {
                    CompileError("Child element " + Err.Wrap(child.DisplayName, Err.ELEMENT) + " is not allowed as a child of xsl:evaluate", "XTSE0010");
                }
            }

            try
            {
                ExpressionVisitor visitor = MakeExpressionVisitor();
                TypeChecker tc = GetConfiguration().GetTypeChecker(false);
                Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.INSTRUCTION, "xsl:evaluate/xpath", 0);
                xpath = tc.StaticTypeCheck(xpath, SequenceType.SINGLE_STRING, role, visitor);
                role = () => new RoleDiagnostic(RoleDiagnostic.INSTRUCTION, "xsl:evaluate/context-item", 0, "XTTE3210");
                contextItem = tc.StaticTypeCheck(contextItem, SequenceType.OPTIONAL_ITEM, role, visitor);
                role = () => new RoleDiagnostic(RoleDiagnostic.INSTRUCTION, "xsl:evaluate/namespace-context", 0, "XTTE3170");
                if (namespaceContext != null)
                {
                    namespaceContext = tc.StaticTypeCheck(namespaceContext, SequenceType.SINGLE_NODE, role, visitor);
                }

                role = () => new RoleDiagnostic(RoleDiagnostic.INSTRUCTION, "xsl:evaluate/with-params", 0, "XTTE3170");
                withParams = tc.StaticTypeCheck(withParams, SequenceType.MakeSequenceType(MapType.ANY_MAP_TYPE, StaticProperty.EXACTLY_ONE), role, visitor);
                role = () => new RoleDiagnostic(RoleDiagnostic.INSTRUCTION, "xsl:evaluate/saxon:options", 0);
                options = tc.StaticTypeCheck(options, SequenceType.MakeSequenceType(MapType.ANY_MAP_TYPE, StaticProperty.EXACTLY_ONE), role, visitor);
            }
            catch (XPathException err)
            {
                CompileError(err);
            }
        }

        public virtual Expression GetContextItemExpression()
        {
            return contextItem;
        }

        public virtual SequenceType GetRequiredType()
        {
            return requiredType;
        }

        public override Expression Compile(Compilation exec, ComponentDeclaration decl)
        {
            if (GetConfiguration().GetBooleanProperty(Feature<bool>.DISABLE_XSL_EVALUATE))
            {

                // If xsl:evaluate is statically disabled then we should execute any fallback children
                validationError = new XmlProcessingIncident("xsl:evaluate is not available in this configuration", "XTDE3175");
                return FallbackProcessing(exec, decl, this);
            }
            else
            {
                Expression evaluateExpr = GetConfiguration().MakeEvaluateInstruction(this, decl);
                if (evaluateExpr is ErrorExpression)
                {
                    return evaluateExpr;
                }


                // If there are any xsl:fallback children, we need to compile them, in case xsl:evaluate
                // is dynamically disabled at run-time.
                if (hasFallbackChildren)
                {

                    // Generate a conditional expression switched on the value of system-property('xsl:supports-dynamic-evaluation')
                    Expression[] conditions = new Expression[2];
                    Expression sysProp = SystemFunction.MakeCall("system-property", MakeRetainedStaticContext(), new StringLiteral("Q{" + NamespaceConstant.XSLT + "}supports-dynamic-evaluation"));
                    conditions[0] = new ValueComparison(sysProp, Token.FEQ, new StringLiteral("no"));
                    conditions[1] = Literal.MakeLiteral(BooleanValue.TRUE);
                    Expression[] actions = new Expression[2];
                    IList<Expression> fallbackExpressions = new List<Expression>();
                    foreach (NodeInfo child in Children(new TypeIsInstancePredicate(typeof(XSLFallback))))
                    {
                        fallbackExpressions.Add(((XSLFallback)child).CompileSequenceConstructor(exec, decl, false));
                    }

                    actions[0] = new Block(fallbackExpressions.ToArray());
                    actions[1] = evaluateExpr;
                    return new Choose(conditions, actions);
                }
                else
                {
                    return evaluateExpr;
                }
            }
        }
    }
}