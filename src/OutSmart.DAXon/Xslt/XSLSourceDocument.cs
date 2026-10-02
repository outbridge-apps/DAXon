////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Accumulators;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// Handler for xsl:source-document element in XSLT 3.0 stylesheet. <br>
    /// </summary>
    internal sealed class XSLSourceDocument : StyleElement
    {
        private Expression href = null;
        private HashSet<Accumulator> accumulators = new HashSet<Accumulator>();
        private bool streaming = false;
        private ParseOptions parseOptions;
        public override bool IsInstruction()
        {
            return true;
        }

        protected override bool MayContainSequenceConstructor()
        {
            return true;
        }

        protected override bool? DeclaredStreamability()
        {
            return true;
        }

        public override void PrepareAttributes()
        {
            parseOptions = GetConfiguration().GetParseOptions();
            string hrefAtt = null;
            string validationAtt = null;
            string typeAtt = null;
            string useAccumulatorsAtt = null;
            foreach (AttributeInfo att in Attributes())
            {
                INodeName attName = att.GetNodeName();
                StructuredQName name = attName.GetStructuredQName();
                string value = att.Value;
                string f = name.ClarkName;
                if (f.Equals("href"))
                {
                    hrefAtt = value;
                    href = MakeAttributeValueTemplate(hrefAtt, att);
                }
                else if (f.Equals("validation"))
                {
                    validationAtt = Whitespace.Trim(value);
                }
                else if (f.Equals("type"))
                {
                    typeAtt = Whitespace.Trim(value);
                }
                else if (f.Equals("use-accumulators"))
                {
                    useAccumulatorsAtt = Whitespace.Trim(value);
                }
                else if (f.Equals("streamable"))
                {
                    streaming = ProcessStreamableAtt(value);
                }
                else if (attName.HasURI(NamespaceUri.SAXON))
                {
                    IsExtensionAttributeAllowed(attName.DisplayName);
                    string local = attName.GetLocalPart();
                    switch (local)
                    {
                        case "dtd-validation":
                            parseOptions = parseOptions.WithDTDValidationMode(ProcessBooleanAttribute(f, value) ? Validation.STRICT : Validation.SKIP);
                            break;
                        case "expand-attribute-defaults":
                            parseOptions = parseOptions.WithExpandAttributeDefaults(ProcessBooleanAttribute(f, value));
                            break;
                        case "line-numbering":
                            parseOptions = parseOptions.WithLineNumbering(ProcessBooleanAttribute(f, value));
                            break;
                        case "xinclude":
                            parseOptions = parseOptions.WithXIncludeAware(ProcessBooleanAttribute(f, value));

                            //                } else if (local.equals("tree-model")) {
                            //                    List<TreeModel> models = getConfiguration().getExternalObjectModels()
                            break;
                        case "validation-params":

                            // Schema validation parameters: ignored (schema-aware feature; this port is HE-level).
                            break;
                        case "strip-space":
                            switch (Whitespace.NormalizeWhitespace(StringView.Of(value)).ToString())
                            {
                                case "#all":
                                    parseOptions = parseOptions.WithSpaceStrippingRule(AllElementsSpaceStrippingRule.GetInstance());
                                    break;
                                case "#none":
                                    parseOptions = parseOptions.WithSpaceStrippingRule(NoElementsSpaceStrippingRule.GetInstance());
                                    break;
                                case "#ignorable":
                                    parseOptions = parseOptions.WithSpaceStrippingRule(IgnorableSpaceStrippingRule.GetInstance());
                                    break;
                                case "#default":
                                    parseOptions = parseOptions.WithSpaceStrippingRule(null);
                                    break;
                                default:
                                    InvalidAttribute("saxon:strip-space", "#all|#none|#ignorable|#default");
                                    break;
                            }

                            break;
                        default:
                            CheckUnknownAttribute(attName);
                            break;
                    }
                }
                else
                {
                    CheckUnknownAttribute(attName);
                }
            }

            if (hrefAtt == null)
            {
                ReportAbsence("href");
            }

            if (validationAtt != null)
            {
                int validation = ValidateValidationAttribute(validationAtt);
                parseOptions = parseOptions.WithSchemaValidationMode(validation);
            }

            if (typeAtt != null)
            {
                if (!IsSchemaAware())
                {
                    CompileError("The @type attribute is available only with a schema-aware XSLT processor", "XTSE1660");
                }

                parseOptions = parseOptions.WithSchemaValidationMode(Validation.BY_TYPE);
                parseOptions = parseOptions.WithTopLevelType(GetSchemaType(typeAtt));
            }

            if (typeAtt != null && validationAtt != null)
            {
                CompileError("The @validation and @type attributes are mutually exclusive", "XTSE1505");
            }

            if (useAccumulatorsAtt == null)
            {
                useAccumulatorsAtt = "";
            }

            AccumulatorRegistry registry = GetPrincipalStylesheetModule().GetStylesheetPackage().AccumulatorRegistry;
            accumulators = registry.GetUsedAccumulators(useAccumulatorsAtt, this);
        }

        public override void Validate(ComponentDeclaration decl)
        {

            href = TypeCheck("select", href);
            if (!HasChildNodes())
            {
                IssueWarning("An empty xsl:source-document instruction has no effect", DAXonErrorCode.SXWN9009);
            }
        }

        public override Expression Compile(Compilation exec, ComponentDeclaration decl)
        {
            Configuration config = GetConfiguration();
            if (parseOptions.SpaceStrippingRule == null)
            {
                parseOptions = parseOptions.WithSpaceStrippingRule(GetPackageData().SpaceStrippingRule);
            }

            parseOptions = parseOptions.WithApplicableAccumulators(accumulators);
            Expression action = CompileSequenceConstructor(exec, decl, false);
            if (action == null || Literal.IsEmptySequence(action))
            {

                // body of xsl:source-document is empty: it's a no-op.
                return Literal.MakeEmptySequence();
            }

            try
            {
                ExpressionVisitor visitor = MakeExpressionVisitor();
                action = action.Simplify();
                action = action.TypeCheck(visitor, config.MakeContextItemStaticInfo(NodeKindTest.DOCUMENT, false));
                return config.MakeStreamInstruction(href, action, streaming, parseOptions, null, SaveLocation(), MakeRetainedStaticContext());
            }
            catch (XPathException err)
            {
                CompileError(err);
                return null;
            }
        }
    }
}