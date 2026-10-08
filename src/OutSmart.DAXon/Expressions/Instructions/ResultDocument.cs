////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Serialization;
using OutSmart.DAXon.Xslt;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Net;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Text;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Expressions.Instructions
{
    public class ResultDocument : Instruction, IValidatingInstruction, IInstructionWithComplexContent, IContextOriginator
    {
        private Operand hrefOp;
        private Operand formatOp; // null if format was known at compile time
        private Operand contentOp;
        private bool async = false;
        private readonly Properties globalProperties;
        private readonly Properties localProperties;
        private ParseOptions validationOptions;
        private readonly Dictionary<StructuredQName, Operand> serializationAttributes;
        private bool resolveAgainstStaticBase = false; // used with fn:put()
        private readonly CharacterMapIndex characterMapIndex;

        public virtual ParseOptions ValidationOptions => validationOptions;

        public virtual Expression FormatExpression
        {
            get => formatOp == null ? null : formatOp.GetChildExpression(); set
            {
                formatOp.SetChildExpression(value);
            }
        }

        public override int IntrinsicDependencies => StaticProperty.HAS_SIDE_EFFECTS;

        public override int InstructionNameCode => StandardNames.XSL_RESULT_DOCUMENT;

        public override string StreamerName => "ResultDocument";

        public virtual Expression Href
        {
            get => hrefOp == null ? null : hrefOp.GetChildExpression(); set
            {
                hrefOp.SetChildExpression(value);
            }
        }
        public ResultDocument(Properties globalProperties, Properties localProperties, Expression href, Expression formatExpression, int validationAction, ISchemaType schemaType, Dictionary<StructuredQName, Expression> serializationAttributes, CharacterMapIndex characterMapIndex)
        {
            this.globalProperties = globalProperties;
            this.localProperties = localProperties;
            if (href != null)
            {
                hrefOp = new Operand(this, href, OperandRole.SINGLE_ATOMIC);
            }

            if (formatExpression != null)
            {
                formatOp = new Operand(this, formatExpression, OperandRole.SINGLE_ATOMIC);
            }

            SetValidationAction(validationAction, schemaType);
            this.serializationAttributes = new Dictionary<StructuredQName, Operand>(serializationAttributes.Count);
            foreach (KeyValuePair<StructuredQName, Expression> entry in serializationAttributes)
            {
                this.serializationAttributes[entry.Key] = new Operand(this, entry.Value, OperandRole.SINGLE_ATOMIC);
            }

            this.characterMapIndex = characterMapIndex;

            //this.nsResolver = nsResolver;
            foreach (Expression e in serializationAttributes.Values)
            {
                AdoptChildExpression(e);
            }
        }

        public virtual void SetContentExpression(Expression content)
        {
            contentOp = new Operand(this, content, OperandRole.SINGLE_ATOMIC);
        }

        public virtual void SetSchemaType(ISchemaType type)
        {
            if (validationOptions == null)
            {
                validationOptions = new ParseOptions();
            }

            validationOptions = validationOptions.WithSchemaValidationMode(Validation.BY_TYPE).WithTopLevelType(type);
        }

        public ISchemaType GetSchemaType()
        {
            return validationOptions == null ? null : validationOptions.TopLevelType;
        }

        public virtual bool IsResolveAgainstStaticBase()
        {
            return resolveAgainstStaticBase;
        }

        public virtual void SetValidationAction(int mode, ISchemaType schemaType)
        {
            bool preservingTypes = mode == Validation.PRESERVE && schemaType == null;
            if (!preservingTypes)
            {
                if (validationOptions == null)
                {
                    validationOptions = new ParseOptions().WithSchemaValidationMode(mode).WithTopLevelType(schemaType);
                }
            }
        }

        public int GetValidationAction()
        {
            return validationOptions == null ? Validation.PRESERVE : validationOptions.GetSchemaValidationMode();
        }

        public virtual void SetUseStaticBaseUri(bool staticBase)
        {
            resolveAgainstStaticBase = staticBase;
        }

        public virtual void SetAsynchronous(bool async)
        {
            this.async = async;
        }

        public virtual bool IsAsynchronous()
        {
            return async;
        }

        public override bool IsMultiThreaded(Configuration config)
        {
            return IsAsynchronous() && config.IsLicensedFeature(Configuration.LicenseFeature.SCHEMA_VALIDATION) && config.GetBooleanProperty(Feature<bool>.ALLOW_MULTITHREADING);
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            TypeCheckChildren(visitor, contextInfo);
            string method = GetStaticSerializationProperty(XSLResultDocument.METHOD);
            bool contentDependentMethod = method == null && formatOp == null && !serializationAttributes.ContainsKey(XSLResultDocument.METHOD);
            bool buildTree = GetStaticSerializationProperty(XSLResultDocument.BUILD_TREE) == "yes";
            if (buildTree || contentDependentMethod || method == "xml" || method == "html" || method == "xhtml" || method == "text")
            {
                try
                {
                    DocumentInstr.CheckContentSequence(visitor.StaticContext, contentOp, validationOptions);
                }
                catch (XPathException err)
                {
                    throw err.MaybeWithLocation(GetLocation());
                }
            }

            return this;
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            OptimizeChildren(visitor, contextInfo);
            if (IsAsynchronous())
            {
                Expression e = ParentExpression;
                while (e != null)
                {
                    if (e is LetExpression && ExpressionTool.DependsOnVariable(GetContentExpression(), new IBinding[] { (LetExpression)e }))
                    {
                        ((LetExpression)e).SetNeedsEagerEvaluation(true);
                    }

                    e = e.ParentExpression;
                }
            }

            return this;
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            Dictionary<StructuredQName, Expression> map = new Dictionary<StructuredQName, Expression>();
            foreach (KeyValuePair<StructuredQName, Operand> entry in serializationAttributes)
            {
                map[entry.Key] = entry.Value.GetChildExpression().Copy(rebindings);
            }

            ResultDocument r = new ResultDocument(globalProperties, localProperties, Href == null ? null : Href.Copy(rebindings), FormatExpression == null ? null : FormatExpression.Copy(rebindings), GetValidationAction(), GetSchemaType(), map, characterMapIndex);
            ExpressionTool.CopyLocationInfo(this, r);
            r.SetContentExpression(GetContentExpression().Copy(rebindings));
            r.resolveAgainstStaticBase = resolveAgainstStaticBase;
            r.async = async;
            return r;
        }

        public override ItemType GetItemType()
        {
            return ErrorType.GetInstance();
        }

        public override IEnumerable<Operand> Operands()
        {
            List<Operand> list = new List<Operand>(6);
            list.Add(contentOp);
            if (hrefOp != null)
            {
                list.Add(hrefOp);
            }

            if (formatOp != null)
            {
                list.Add(formatOp);
            }

            list.AddRange(serializationAttributes.Values);
            return list;
        }

        public override PathMap.PathMapNodeSet AddToPathMap(PathMap pathMap, PathMap.PathMapNodeSet pathMapNodeSet)
        {
            PathMap.PathMapNodeSet result = base.AddToPathMap(pathMap, pathMapNodeSet);
            result.SetReturnable(false);
            return new PathMap.PathMapNodeSet(pathMap.MakeNewRoot(this));
        }

        public virtual void Process(IPushEvaluator content, IXPathContext context)
        {
            CheckNotTemporaryOutputState(context);
            context.GetConfiguration().ProcessResultDocument(this, content, context);
        }

        public virtual void ProcessInstruction(IPushEvaluator content, IXPathContext context)
        {
            XsltController controller = (XsltController)context.GetController();
            string savedOutputUri = context.CurrentOutputUri;
            ComplexContentOutputter @out = ProcessLeft(context);
            bool failed = false;
            try
            {
                ITailCall tc = content.ProcessLeavingTail(@out, context);
                DispatchTailCall(tc);
            }
            catch (XPathException err)
            {
                failed = true;
                throw err.MaybeWithLocation(GetLocation()).MaybeWithContext(context);
            }
            finally
            {
                try
                {
                    @out.Close();
                }
                catch (XPathException e)
                {
                    if (!failed)
                    {

                        throw;
                    } // Otherwise, no further action; report the original error in preference. Bug 4227
                }
            }

            context.CurrentOutputUri = savedOutputUri;
        }

        public virtual ComplexContentOutputter ProcessLeft(IXPathContext context)
        {
            XsltController controller = (XsltController)context.GetController();
            Configuration config = controller.GetConfiguration();
            CheckNotTemporaryOutputState(context);
            Properties computedLocalProps = GatherOutputProperties(context);
            if (computedLocalProps.GetProperty(DAXonOutputKeys.PARAMETER_DOCUMENT) != null && StaticBaseURIString != null)
            {
                try
                {
                    string abs = ResolveURI.MakeAbsolute(computedLocalProps.GetProperty(DAXonOutputKeys.PARAMETER_DOCUMENT), StaticBaseURIString).ToASCIIString();
                    computedLocalProps.SetProperty(DAXonOutputKeys.PARAMETER_DOCUMENT, abs);
                }
                catch (URISyntaxException e)
                {
                    throw XPathException.MakeXPathException(e);
                }
            }

            SerializationProperties serParams = new SerializationProperties(computedLocalProps, characterMapIndex);

            // If validation was requested, create a function that instantiates a validator
            // which can then be injected at an appropriate point into the output pipeline
            if (validationOptions != null && validationOptions.GetSchemaValidationMode() != Validation.PRESERVE)
            {
                serParams.ValidationFactory = (output) =>
                {

                    // Validation can add redundant namespace declarations so we
                    // need to follow it with a namespace reducer
                    NamespaceReducer reducer = new NamespaceReducer(output);
                    return config.GetDocumentValidator(reducer, output.GetSystemId(), validationOptions, GetLocation());
                };
            }

            IReceiver @out = null;
            IResultDocumentResolver resolver;
            string hrefValue = "";
            if (Href != null)
            {
                IUnicodeStringEvaluator hrefEval = Href.MakeElaborator().ElaborateForUnicodeString(true);
                hrefValue = IriToUri.IriToUriFn(hrefEval.Eval(context)).ToString();
            }

            if ((hrefValue.Length == 0) || hrefValue.Equals(controller.BaseOutputURI))
            {
                PrincipalOutputGatekeeper gateKeeper = controller.Gatekeeper;
                if (gateKeeper != null)
                {
                    gateKeeper.UseAsSecondary();
                    @out = gateKeeper.MakeReceiver(serParams);
                }
            }

            if (@out == null)
            {
                try
                {
                    resolver = controller.ResultDocumentResolver;
                    if (resolver == null)
                    {
                        resolver = StandardResultDocumentResolver.GetInstance();
                    }

                    @out = MakeReceiver(hrefValue, StaticBaseURIString, context, resolver, serParams, resolveAgainstStaticBase);
                    TraceDestination(context, @out);
                }
                catch (XPathException e)
                {
                    throw e.MaybeWithLocation(GetLocation()).MaybeWithContext(context);
                }
            }

            @out.GetPipelineConfiguration().SetController(controller);
            string systemId = @out.GetSystemId();
            NamespaceReducer nr = new NamespaceReducer(@out);
            ComplexContentOutputter cco = new ComplexContentOutputter(nr);
            cco.SetSystemId(systemId);

            context.CurrentOutputUri = systemId;
            cco.Open();
            return cco;
        }

        public virtual CharacterMapIndex GetCharacterMapIndex()
        {
            return characterMapIndex;
        }

        private void CheckNotTemporaryOutputState(IXPathContext context)
        {
            if (context.TemporaryOutputState != 0)
            {
                throw new XPathException("Cannot execute xsl:result-document while evaluating xsl:" + context.GetNamePool().GetLocalName(context.TemporaryOutputState)).WithErrorCode("XTDE1480").WithLocation(GetLocation());
            }
        }

        public static IReceiver MakeReceiver(string hrefValue, string baseURI, IXPathContext context, IResultDocumentResolver resolver, SerializationProperties @params, bool resolveAgainstStaticBase)
        {
            Controller controller = context.GetController();
            try
            {
                string @base;
                if (resolveAgainstStaticBase)
                {
                    @base = baseURI;
                }
                else
                {
                    @base = controller.BaseOutputURI;
                }

                try
                {
                    IReceiver @out = resolver.Resolve(context, hrefValue, @base, @params);
                    string systemId = @out.GetSystemId();
                    if (systemId == null)
                    {
                        systemId = ResolveURI.MakeAbsolute(hrefValue, @base).ToASCIIString();
                        @out.SetSystemId(systemId);
                    }

                    CheckAcceptableUri(context, systemId);
                    return @out;
                }
                catch (XPathException e)
                {
                    throw;
                }
                catch (Exception err)
                {
                    throw new XPathException("Exception thrown by output resolver", err);
                }
            }
            catch (XPathException e)
            {
                throw XPathException.MakeXPathException(e);
            }
        }

        public static void TraceDestination(IXPathContext context, IResultTarget result)
        {
            Configuration config = context.GetConfiguration();
            bool timing = config.IsTiming();
            if (timing)
            {
                string dest = result.GetSystemId();
                if (dest == null)
                {
                    if (result is StreamResult)
                    {
                        dest = "anonymous output stream";
                    }
                    else
                    {
                        dest = result.GetType().FullName;
                    }
                }

                config.Logger.Info("Writing to " + dest);
            }
        }

        public static void CheckAcceptableUri(IXPathContext context, string uri)
        {
            XsltController controller = (XsltController)context.GetController();
            if (uri != null)
            {
                if (controller.GetDocumentPool().Find(uri) != null)
                {
                    throw new XPathException("Cannot write to a URI that has already been read: " + (uri.Equals(Controller.ANONYMOUS_PRINCIPAL_OUTPUT_URI) ? "(implicit output URI)" : uri)).WithXPathContext(context).WithErrorCode("XTDE1500");
                }

                DocumentKey documentKey = new DocumentKey(uri);

                lock (controller.syncLock)
                {
                    if (!controller.CheckUniqueOutputDestination(documentKey))
                    {
                        throw new XPathException("Cannot write more than one result document to the same URI: " + (uri.Equals(Controller.ANONYMOUS_PRINCIPAL_OUTPUT_URI) ? "(implicit output URI)" : uri)).WithXPathContext(context).WithErrorCode("XTDE1490");
                    }
                    else
                    {
                        controller.AddUnavailableOutputDestination(documentKey);
                    }
                }
            }
        }

        public virtual Properties GatherOutputProperties(IXPathContext context)
        {
            Controller controller = context.GetController();
            Configuration config = context.GetConfiguration();
            Properties computedGlobalProps = globalProperties;
            INamespaceResolver nsResolver = GetRetainedStaticContext();
            if (FormatExpression != null)
            {

                // format was an AVT and now needs to be computed
                StructuredQName qName;
                string format = FormatExpression.EvaluateAsString(context).ToString();
                if (format.StartsWith("Q{", StringComparison.Ordinal))
                {
                    qName = StructuredQName.FromEQName(format);
                }
                else
                {
                    string[] parts;
                    try
                    {
                        parts = NameChecker.GetQNameParts(format);
                    }
                    catch (QNameException e)
                    {
                        throw new XPathException("The requested output format " + Err.Wrap(format) + " is not a valid QName").WithErrorCode("XTDE1460").WithXPathContext(context).WithLocation(FormatExpression.GetLocation());
                    }

                    NamespaceUri uri = nsResolver.GetURIForPrefix(parts[0], false);
                    if (uri == null)
                    {
                        throw new XPathException("The namespace prefix in the format name " + format + " is undeclared").WithLocation(FormatExpression.GetLocation()).WithErrorCode("XTDE1460").WithXPathContext(context);
                    }

                    qName = new StructuredQName(parts[0], uri, parts[1]);
                }

                computedGlobalProps = ((StylesheetPackage)GetRetainedStaticContext().GetPackageData()).GetNamedOutputProperties(qName);
                if (computedGlobalProps == null)
                {
                    throw new XPathException("There is no xsl:output format named " + format).WithErrorCode("XTDE1460").WithXPathContext(context);
                }
            }


            // Now combine the properties specified on xsl:result-document with those specified on xsl:output
            Properties computedLocalProps = new Properties(computedGlobalProps);

            // First handle the properties with fixed values on xsl:result-document
            foreach (string key in localProperties.StringPropertyNames())
            {
                StructuredQName qName = StructuredQName.FromClarkName(key);
                try
                {
                    SetSerializationProperty(computedLocalProps, qName.GetNamespaceUri(), qName.GetLocalPart(), localProperties.GetProperty(key), nsResolver, true, config);
                }
                catch (XPathException e)
                {
                    throw e.WithErrorCode("XTDE0030").MaybeWithLocation(GetLocation());
                }
            }


            // Now add the properties that were specified as AVTs
            if (serializationAttributes.Count > 0)
            {
                foreach (KeyValuePair<StructuredQName, Operand> entry in serializationAttributes)
                {
                    string value = entry.Value.GetChildExpression().EvaluateAsString(context).ToString();
                    string lname = entry.Key.GetLocalPart();
                    NamespaceUri uri = entry.Key.GetNamespaceUri();
                    try
                    {
                        SetSerializationProperty(computedLocalProps, uri, lname, value, nsResolver, false, config);
                    }
                    catch (XPathException e)
                    {
                        e.SetErrorCode("XTDE0030");
                        e.MaybeSetLocation(GetLocation());
                        e.MaybeSetContext(context);
                        if (e.ErrorCodeQName.HasURI(NamespaceUri.SAXON) && e.ErrorCodeQName.GetLocalPart().Substring(0, 4) == "SXWN")
                        {
                            XmlProcessingException ee = new XmlProcessingException(e);
                            ee.SetWarning(true);
                            controller.ErrorReporter.Report(ee);
                        }
                        else
                        {
                            throw;
                        }
                    }
                }
            }


            // For choosing the default output method, avoid using the backwards-compatibility rules
            computedLocalProps.SetProperty(DAXonOutputKeys.STYLESHEET_VERSION, "30");
            return computedLocalProps;
        }

        public virtual string GetStaticSerializationProperty(StructuredQName name)
        {
            string clarkName = name.ClarkName;
            string local = localProperties.GetProperty(clarkName);
            if (local != null)
            {
                return local;
            }

            if (serializationAttributes.ContainsKey(name))
            {
                return null; // value is computed dynamically
            }

            return globalProperties.GetProperty(clarkName);
        }

        public static void SetSerializationProperty(Properties details, NamespaceUri uri, string lname, string value, INamespaceResolver nsResolver, bool prevalidated, Configuration config)
        {
            SerializerFactory sf = config.SerializerFactory;
            string clarkName = lname;
            if (!uri.IsEmpty())
            {
                clarkName = "{" + uri + "}" + lname;
            }

            if (uri.IsEmpty() || NamespaceUri.SAXON.Equals(uri))
            {
                switch (clarkName)
                {
                    case "method":
                        value = Whitespace.Trim(value);
                        if (value.StartsWith("Q{}", StringComparison.Ordinal) && value.Length > 3)
                        {
                            value = value.Substring(3);
                        }

                        if (value.Equals("xml") || value.Equals("html") || value.Equals("text") || value.Equals("xhtml") || value.Equals("json") || value.Equals("adaptive") || prevalidated || value.StartsWith("{", StringComparison.Ordinal))
                        {
                            details.SetProperty(DAXonOutputKeys.METHOD, value);
                        }
                        else if (value.StartsWith("Q{", StringComparison.Ordinal))
                        {
                            details.SetProperty(DAXonOutputKeys.METHOD, value.Substring(1));
                        }
                        else
                        {
                            string[] parts;
                            try
                            {
                                parts = NameChecker.GetQNameParts(value);
                                string prefix = parts[0];
                                if ((prefix.Length == 0))
                                {
                                    throw new XPathException("method must be xml, html, xhtml, text, json, adaptive, or a prefixed name").WithErrorCode("SEPM0016").AsStaticError();
                                }
                                else if (nsResolver != null)
                                {
                                    NamespaceUri muri = nsResolver.GetURIForPrefix(prefix, false);
                                    if (muri == null)
                                    {
                                        throw new XPathException("Namespace prefix '" + prefix + "' has not been declared").WithErrorCode("SEPM0016").AsStaticError();
                                    }

                                    details.SetProperty(DAXonOutputKeys.METHOD, '{' + muri.ToString() + '}' + parts[1]);
                                }
                                else
                                {
                                    details.SetProperty(DAXonOutputKeys.METHOD, value);
                                }
                            }
                            catch (QNameException e)
                            {
                                throw new XPathException("Invalid method name. " + e.GetMessage()).WithErrorCode("SEPM0016").AsStaticError();
                            }
                        }

                        break;
                    case "use-character-maps":

                        // The use-character-maps attribute is always turned into a Clark-format name at compile time
                        string existing = details.GetProperty(DAXonOutputKeys.USE_CHARACTER_MAPS);
                        if (existing == null)
                        {
                            existing = "";
                        }

                        details.SetProperty(DAXonOutputKeys.USE_CHARACTER_MAPS, existing + value);
                        break;
                    case "cdata-section-elements":
                        ProcessListOfNodeNames(details, clarkName, value, nsResolver, true, prevalidated, false);
                        break;
                    case "suppress-indentation":
                        ProcessListOfNodeNames(details, clarkName, value, nsResolver, true, prevalidated, false);
                        break;
                    case DAXonOutputKeys.DOUBLE_SPACE:
                        ProcessListOfNodeNames(details, clarkName, value, nsResolver, true, prevalidated, false);
                        break;
                    case DAXonOutputKeys.ATTRIBUTE_ORDER:
                        ProcessListOfNodeNames(details, clarkName, value, nsResolver, false, prevalidated, true);
                        break;
                    case DAXonOutputKeys.NEXT_IN_CHAIN:

                        //                XPathException e = new XPathException("saxon:next-in-chain property is available only on xsl:output");
                        //                e.setErrorCodeQName(
                        //                        new StructuredQName("saxon", NamespaceConstant.SAXON, DAXonErrorCode.SXWN9004));
                        //                throw e;
                        break;
                    default:

                        // all other properties in the default or Saxon namespaces
                        if (clarkName.Equals("output-version"))
                        {
                            clarkName = "version";
                        }

                        if (!prevalidated)
                        {
                            try
                            {
                                if (!DAXonOutputKeys.IsUnstrippedProperty(clarkName))
                                {

                                    // TODO: whitespace rules seem to vary for different interfaces
                                    value = Whitespace.Trim(value);
                                }

                                value = sf.CheckOutputProperty(clarkName, value);
                            }
                            catch (XPathException err)
                            {
                                err.MaybeSetErrorCode("SEPM0016");
                                throw;
                            }
                        }

                        details.SetProperty(clarkName, value);
                        break;
                }
            }
            else
            {

                // properties in user-defined namespaces
                details.SetProperty('{' + uri.ToString() + '}' + lname, value);
            }
        }

        private static void ProcessListOfNodeNames(Properties details, string key, string value, INamespaceResolver nsResolver, bool useDefaultNS, bool prevalidated, bool allowStar)
        {
            string existing = details.GetProperty(key);
            if (existing == null)
            {
                existing = "";
            }

            string s = DAXonOutputKeys.ParseListOfNodeNames(value, nsResolver, useDefaultNS, prevalidated, allowStar, "SEPM0016");
            details.SetProperty(key, existing + s);
        }

        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("resultDoc", this);
            @out.EmitAttribute("global", ExportProperties(globalProperties));
            @out.EmitAttribute("local", ExportProperties(localProperties));
            if (GetValidationAction() != Validation.SKIP && GetValidationAction() != Validation.BY_TYPE)
            {
                @out.EmitAttribute("validation", Validation.Describe(GetValidationAction()));
            }

            ISchemaType schemaType = GetSchemaType();
            if (schemaType != null)
            {
                @out.EmitAttribute("type", schemaType.GetStructuredQName());
            }

            if (async)
            {
                @out.EmitAttribute("flags", "a");
            }

            if (Href != null)
            {
                @out.SetChildRole("href");
                Href.Export(@out);
            }

            if (FormatExpression != null)
            {
                @out.SetChildRole("format");
                FormatExpression.Export(@out);
            }

            foreach (KeyValuePair<StructuredQName, Operand> p in serializationAttributes)
            {
                StructuredQName name = p.Key;
                Expression value = p.Value.GetChildExpression();
                @out.SetChildRole(name.EQName);
                value.Export(@out);
            }

            @out.SetChildRole("content");
            GetContentExpression().Export(@out);
            @out.EndElement();
        }

        private string ExportProperties(Properties props)
        {
            StringBuilder writer = new StringBuilder();
            foreach (string key in props.StringPropertyNames())
            {
                string val = props.GetProperty(key);
                if (key.Equals(DAXonOutputKeys.ITEM_SEPARATOR) || key.Equals(DAXonOutputKeys.NEWLINE))
                {
                    val = ExpressionPresenter.JsEscape(val);
                }

                if (key.Equals(DAXonOutputKeys.USE_CHARACTER_MAPS) || key.Equals(DAXonOutputKeys.METHOD))
                {

                    // TODO: other QName-valued fields such as cdata-section-elements??
                    val = val.Replace("{", "Q{");
                }

                string adjustedKey = key.StartsWith("{", StringComparison.Ordinal) ? "Q" + key : key;
                writer.Append(adjustedKey).Append('=').Append(val).Append("\n");
            }

            return writer.ToString();
        }

        public static void ProcessXslOutputElement(NodeInfo element, Properties props, IXPathContext c)
        {
            INamespaceResolver resolver = element.AllNamespaces;
            foreach (AttributeInfo att in element.Attributes())
            {
                NamespaceUri uri = att.GetNodeName().GetNamespaceUri();
                string local = att.GetNodeName().GetLocalPart();
                string val = Whitespace.Trim(att.Value);
                SetSerializationProperty(props, uri, local, val, resolver, false, c.GetConfiguration());
            }
        }

        public Expression GetContentExpression()
        {
            return contentOp.GetChildExpression();
        }

        public override Elaborator GetElaborator()
        {
            return new ResultDocumentElaborator();
        }

        private sealed class ResultDocumentElaborator : PushElaborator
        {
            public override IPushEvaluator ElaborateForPush()
            {
                ResultDocument expr = (ResultDocument)GetExpression();
                IPushEvaluator contentPush = expr.GetContentExpression().MakeElaborator().ElaborateForPush();
                return (output, context) =>
                {
                    expr.CheckNotTemporaryOutputState(context);
                    context.GetConfiguration().ProcessResultDocument(expr, contentPush, context);
                    return null;
                };
            }
        }
    }
}
