////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Xslt
{
    internal sealed class XSLResultDocument : StyleElement
    {
        public static readonly HashSet<string> fans = new HashSet<string>(); // formatting attribute names

        public static StructuredQName METHOD = NamespaceUri.NULL.QName("method");
        public static StructuredQName BUILD_TREE = new StructuredQName("", NamespaceUri.NULL, "build-tree");

        private Expression href;
        private StructuredQName formatQName; // used when format is a literal string
        private Expression formatExpression; // used when format is an AVT
        private int validationAction = Validation.STRIP;
        private ISchemaType schemaType = null;
        private readonly Dictionary<StructuredQName, Expression> serializationAttributes = new Dictionary<StructuredQName, Expression>(10);
        private bool async = true;
        static XSLResultDocument()
        {
            fans.Add("allow-duplicate-names");
            fans.Add("build-tree");
            fans.Add("byte-order-mark");
            fans.Add("cdata-section-elements");
            fans.Add("doctype-public");
            fans.Add("doctype-system");
            fans.Add("encoding");
            fans.Add("escape-solidus");
            fans.Add("escape-uri-attributes");
            fans.Add("html-version");
            fans.Add("include-content-type");
            fans.Add("indent");
            fans.Add("item-separator");
            fans.Add("json-node-output-method");
            fans.Add("media-type");
            fans.Add("method");
            fans.Add("normalization-form");
            fans.Add("omit-xml-declaration");
            fans.Add("output-version");
            fans.Add("parameter-document");
            fans.Add("standalone");
            fans.Add("suppress-indentation");
            fans.Add("undeclare-prefixes");
            fans.Add(DAXonOutputKeys.ATTRIBUTE_ORDER);
            fans.Add(DAXonOutputKeys.CANONICAL);
            fans.Add(DAXonOutputKeys.CHARACTER_REPRESENTATION);
            fans.Add(DAXonOutputKeys.DOUBLE_SPACE);
            fans.Add(DAXonOutputKeys.INDENT_SPACES);
            fans.Add(DAXonOutputKeys.INTERNAL_DTD_SUBSET);
            fans.Add(DAXonOutputKeys.LINE_LENGTH);
            fans.Add(DAXonOutputKeys.NEWLINE);
            fans.Add(DAXonOutputKeys.NEXT_IN_CHAIN);
            fans.Add(DAXonOutputKeys.RECOGNIZE_BINARY);
            fans.Add(DAXonOutputKeys.REQUIRE_WELL_FORMED);
            fans.Add(DAXonOutputKeys.PROPERTY_ORDER);
            fans.Add(DAXonOutputKeys.SINGLE_QUOTES);
            fans.Add(DAXonOutputKeys.SUPPLY_SOURCE_LOCATOR);
        }
        public override bool IsInstruction()
        {
            return true;
        }

        protected override bool MayContainSequenceConstructor()
        {
            return true;
        }

        public override void PrepareAttributes()
        {
            string formatAttribute = null;
            string hrefAttribute = null;
            string validationAtt = null;
            string typeAtt = null;
            string useCharacterMapsAtt = null;
            foreach (AttributeInfo att in Attributes())
            {
                INodeName attName = att.GetNodeName();
                StructuredQName name = attName.GetStructuredQName();
                string value = att.Value;
                string f = name.ClarkName;
                if (f.Equals("format"))
                {
                    formatAttribute = Whitespace.Trim(value);
                    formatExpression = MakeAttributeValueTemplate(formatAttribute, att);
                }
                else if (f.Equals("href"))
                {
                    hrefAttribute = Whitespace.Trim(value);
                    href = MakeAttributeValueTemplate(hrefAttribute, att);
                }
                else if (f.Equals("validation"))
                {
                    validationAtt = Whitespace.Trim(value);
                }
                else if (f.Equals("type"))
                {
                    typeAtt = Whitespace.Trim(value);
                }
                else if (f.Equals("use-character-maps"))
                {
                    useCharacterMapsAtt = Whitespace.Trim(value);
                }
                else if (fans.Contains(f) || (f.StartsWith("{", StringComparison.Ordinal) && !StandardNames.SAXON_ASYCHRONOUS.Equals(f)))
                {

                    // this is a serialization attribute
                    string val = value;
                    if (!f.Equals(DAXonOutputKeys.ITEM_SEPARATOR) && !f.Equals(DAXonOutputKeys.NEWLINE))
                    {
                        val = Whitespace.Trim(value);
                    }

                    if (f.Equals(DAXonOutputKeys.ESCAPE_SOLIDUS))
                    {
                        RequireXslt40Attribute(f);
                    }

                    Expression exp = MakeAttributeValueTemplate(val, att);
                    serializationAttributes[name] = exp;
                }
                else if (name.GetLocalPart().Equals("asynchronous") && name.HasURI(NamespaceUri.SAXON))
                {
                    async = ProcessBooleanAttribute("saxon:asynchronous", value);
                    if (GetCompilation().GetCompilerInfo().IsCompileWithTracing())
                    {
                        async = false;
                    }
                    else if (!"EE".Equals(GetConfiguration().EditionCode))
                    {
                        IssueWarning("saxon:asynchronous - ignored when not running Saxon-EE", DAXonErrorCode.SXWN9013);
                        async = false;
                    }
                }
                else
                {
                    CheckUnknownAttribute(attName);
                }
            }

            if (formatAttribute != null)
            {
                if (formatExpression is StringLiteral)
                {
                    formatQName = MakeQName(((StringLiteral)formatExpression).Stringify(), "XTDE1460", "format");
                    formatExpression = null;
                }
                else
                {
                    GetPrincipalStylesheetModule().SetNeedsDynamicOutputProperties(true);
                }
            }

            if (validationAtt == null)
            {
                validationAction = DefaultValidation;
            }
            else
            {
                validationAction = ValidateValidationAttribute(validationAtt);
            }

            if (typeAtt != null)
            {
                if (!IsSchemaAware())
                {
                    CompileError("The @type attribute is available only with a schema-aware XSLT processor", "XTSE1660");
                }

                schemaType = GetSchemaType(typeAtt);
                validationAction = Validation.BY_TYPE;
            }

            if (typeAtt != null && validationAtt != null)
            {
                CompileError("The @validation and @type attributes are mutually exclusive", "XTSE1505");
            }

            if (useCharacterMapsAtt != null)
            {
                string s = XSLOutput.PrepareCharacterMaps(this, useCharacterMapsAtt, new Properties());
                serializationAttributes[new StructuredQName("", NamespaceUri.NULL, "use-character-maps")] = new StringLiteral(s);
            }
        }

        public override void Validate(ComponentDeclaration decl)
        {
            if (href != null && !GetConfiguration().GetBooleanProperty(Feature<bool>.ALLOW_EXTERNAL_FUNCTIONS))
            {
                CompileError("xsl:result-document is disabled when extension functions are disabled");
            }

            href = TypeCheck("href", href);
            formatExpression = TypeCheck("format", formatExpression);
            foreach (StructuredQName prop in serializationAttributes.Keys)
            {
                Expression exp1 = serializationAttributes.GetOrDefault(prop);
                Expression exp2 = TypeCheck(prop.DisplayName, exp1);
                if (exp1 != exp2)
                {
                    serializationAttributes[prop] = exp2;
                }
            }

            ContainingPackage.SetCreatesSecondaryResultDocuments(true);
        }
        public override Expression Compile(Compilation exec, ComponentDeclaration decl)
        {

            // Check that the call is not within xsl:variable or xsl:function.
            // This is a dynamic error, but worth detecting statically.
            // In fact this is a bit of a fudge. If a function or variable is inlined, we sometimes don't detect
            // XTDE1480 at run-time. Doing this static check improves our chances, though it won't catch all cases.
            IAxisIterator ai = IterateAxis(AxisInfo.ANCESTOR);
            NodeInfo node;
            while ((node = ai.Next()) != null)
            {
                if (node is XSLGeneralVariable || (node is XSLFunction && !((XSLFunction)node).IsUpdating()))
                {
                    IssueWarning("An xsl:result-document instruction inside " + node.DisplayName + " will always fail at run-time", "XTDE1480");
                    return new ErrorExpression("Call to xsl:result-document while in temporary output state", "XTDE1480", false);
                }
            }

            Properties globalProps;
            if (formatExpression == null)
            {
                try
                {
                    globalProps = GetPrincipalStylesheetModule().GatherOutputProperties(formatQName);
                }
                catch (XPathException err)
                {
                    CompileError("Named output format has not been defined", "XTDE1460");
                    return null;
                }
            }
            else
            {
                globalProps = new Properties();
                GetPrincipalStylesheetModule().SetNeedsDynamicOutputProperties(true);
            }


            // If no serialization method was specified, we can work it out statically if the
            // first contained instruction is a literal result element. This saves effort at run-time.
            string method = null;
            if (formatExpression == null && globalProps.GetProperty("method") == null && serializationAttributes.GetOrDefault(METHOD) == null)
            {
                IAxisIterator kids = IterateAxis(AxisInfo.CHILD);
                NodeInfo first = kids.Next();
                if (first is LiteralResultElement)
                {
                    if (first.GetNamespaceUri().Equals(NamespaceUri.XHTML) && first.GetLocalPart().Equals("html"))
                    {
                        method = "xhtml";
                    }
                    else if (first.GetLocalPart().Equals("html", StringComparison.OrdinalIgnoreCase) && first.GetNamespaceUri().IsEmpty())
                    {
                        method = "html";
                    }
                    else
                    {
                        method = "xml";
                    }

                    globalProps.SetProperty("method", method);
                }
            }

            Properties localProps = new Properties();
            HashSet<StructuredQName> @fixed = new HashSet<StructuredQName>();
            INamespaceResolver namespaceResolver = GetStaticContext().GetNamespaceResolver();
            foreach (StructuredQName property in serializationAttributes.Keys)
            {
                Expression exp = serializationAttributes.GetOrDefault(property);
                if (exp is StringLiteral)
                {
                    string s = ((StringLiteral)exp).Stringify();
                    string lname = property.GetLocalPart();
                    NamespaceUri uri = property.GetNamespaceUri();
                    try
                    {
                        ResultDocument.SetSerializationProperty(localProps, uri, lname, s, namespaceResolver, false, exec.GetConfiguration());
                        @fixed.Add(property);
                        if (property.Equals(METHOD))
                        {
                            method = s;
                        }
                    }
                    catch (XPathException e)
                    {
                        if (e.ErrorCodeQName.HasURI(NamespaceUri.SAXON))
                        {
                            CompileWarning(e.Message, e.ErrorCodeQName);
                        }
                        else
                        {
                            CompileError(e.WithErrorCode("XTSE0020"));
                        }
                    }
                }
            }

            foreach (StructuredQName p in @fixed)
            {
                serializationAttributes.Remove(p);
            }

            ResultDocument inst = new ResultDocument(globalProps, localProps, href, formatExpression, validationAction, schemaType, serializationAttributes, ContainingPackage.GetCharacterMapIndex());
            Expression content = CompileSequenceConstructor(exec, decl, true);
            if (content == null)
            {
                content = Literal.MakeLiteral(EmptySequence.GetInstance());
            }

            inst.SetContentExpression(content);
            inst.SetAsynchronous(async);
            inst.SetLocation(SaveLocation());
            return inst;
        }
    }
}