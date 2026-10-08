////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Transformation.Packages;
using OutSmart.DAXon.Trees;
using OutSmart.DAXon.Trees.Linked;
using OutSmart.DAXon.Internal.Numerics;
using OutSmart.DAXon.Internal.Net;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal;
using System.IO;
namespace OutSmart.DAXon.Xslt
{
    internal sealed class UseWhenFilter : ProxyReceiver
    {
        private int depthOfHole = 0;
        private bool emptyStylesheetElement = false;
        private readonly Stack<NamespaceUri> defaultNamespaceStack = new Stack<NamespaceUri>();
        private readonly Stack<int> versionStack = new Stack<int>();
        private readonly DateTimeValue currentDateTime = DateTimeValue.GetCurrentDateTime(null);
        private readonly Compilation compilation;
        private readonly Stack<string> systemIdStack = new Stack<string>();
        private readonly Stack<URI> baseUriStack = new Stack<URI>();
        private readonly NestedIntegerValue precedence;
        private int importCount = 0;
        private bool dropUnderscoredAttributes;
        private readonly LinkedTreeBuilder treeBuilder;
        public UseWhenFilter(Compilation compilation, IReceiver next, NestedIntegerValue precedence) : base(next)
        {
            this.compilation = compilation;
            this.precedence = precedence;

            // tries to avoid assuming it will always be true
            treeBuilder = (LinkedTreeBuilder)next;
        }

        /// <summary>
        /// Start of document
        /// </summary>
        public override void Open()
        {
            nextReceiver.Open();
            string sysId = GetSystemId();
            if (sysId == null)
            {
                sysId = "";
            }

            systemIdStack.Push(sysId);
            try
            {
                baseUriStack.Push(new URI(sysId));
            }
            catch (URISyntaxException e)
            {
                try
                {
                    baseUriStack.Push(new Uri(Path.GetFullPath(sysId)).AbsoluteUri);
                }
                catch (Exception ex)
                {
                    // Neither a URI nor a path: with nothing pushed, the first element found the stack empty
                    throw new XPathException("Invalid URI for stylesheet: " + sysId);
                }
            }
        }

        public override void StartElement(INodeName elemName, ISchemaType type, IAttributeMap attributes, NamespaceMap namespaces, ILocation location, int properties)
        {
            int fp = elemName.ObtainFingerprint(GetNamePool());
            bool inXsltNamespace = elemName.HasURI(NamespaceUri.XSLT);
            NamespaceUri stdAttUri = inXsltNamespace ? NamespaceUri.NULL : NamespaceUri.XSLT;
            DocumentImpl includedDoc = null;
            ParsedAttributes pa = StartElementProcessAttributes(elemName, attributes, namespaces, inXsltNamespace, stdAttUri);
            defaultNamespaceStack.Push(pa.xpathDefaultNamespaceAtt);
            if (emptyStylesheetElement)
            {
                depthOfHole++;
                return;
            }

            if (depthOfHole == 0)
            {
                URI baseUri = ProcessBaseUri(location, pa.xmlBaseAtt);
                bool ignore = false;
                int version = GetVersion(pa, fp);
                versionStack.Push(version);
                if (inXsltNamespace && defaultNamespaceStack.Count == 2 && version > 30 && !ElementAvailable.IsXslt30Element(fp))
                {

                    // top level unknown XSLT element is ignored in forwards-compatibility mode
                    ignore = true;
                }

                if (pa.hasShadowAttributes && !ignore)
                {
                    attributes = ProcessShadowAttributes(elemName, attributes, namespaces, location, baseUri);
                    string uw = attributes.GetValue(stdAttUri, "use-when");
                    if (uw != null)
                    {
                        pa.useWhenAtt = uw;
                    }
                }

                if (!ignore)
                {
                    if (CheckUseEvaluateWhen(pa, fp, location, baseUri, namespaces, elemName, stdAttUri))
                    {
                        return;
                    }
                }

                if (inXsltNamespace)
                {
                    includedDoc = HandleXsltElement(elemName, baseUri, fp, pa, attributes, namespaces, location);
                }

                dropUnderscoredAttributes = inXsltNamespace;
                nextReceiver.StartElement(elemName, type, attributes, namespaces, location, properties);
                CheckTargetDocument(includedDoc);
            }
            else
            {
                depthOfHole++;
            }
        }

        private void CheckTargetDocument(DocumentImpl includedDoc)
        {
            if (includedDoc != null)
            {
                XSLGeneralIncorporate node = (XSLGeneralIncorporate)treeBuilder.CurrentParentNode;
                node.SetTargetDocument(includedDoc);
            }
        }

        private bool CheckUseEvaluateWhen(ParsedAttributes pa, int fp, ILocation location, URI baseUri, NamespaceMap namespaces, INodeName elemName, NamespaceUri stdAttUri)
        {
            if (pa.useWhenAtt != null)
            {
                AttributeLocation attLoc = new AttributeLocation(elemName.GetStructuredQName(), new StructuredQName("", stdAttUri, "use-when"), location);
                if (!EvaluateUseWhen(pa.useWhenAtt, attLoc, baseUri.ToString(), namespaces))
                {
                    if (fp == StandardNames.XSL_STYLESHEET || fp == StandardNames.XSL_TRANSFORM || fp == StandardNames.XSL_PACKAGE)
                    {
                        emptyStylesheetElement = true;
                    }
                    else
                    {
                        depthOfHole = 1;
                        return true;
                    }
                }
            }

            return false;
        }

        private int GetVersion(ParsedAttributes pa, int fp)
        {
            int version = int.MinValue;
            if (pa.versionAtt != null && fp != StandardNames.XSL_OUTPUT)
            {
                version = ProcessVersionAttribute(pa.versionAtt);
            }

            if (version == int.MinValue)
            {
                version = versionStack.Count == 0 ? 30 : versionStack.Peek();
            }

            return version;
        }

        private DocumentImpl ProcessIncludeImport(INodeName elemName, ILocation location, URI baseUri, string href, bool isImport)
        {
            if (href == null)
            {
                throw new XPathException("Missing href attribute on " + elemName.DisplayName, "XTSE0010");
            }

            Configuration config = GetConfiguration();
            IResourceResolver resolver = compilation.GetCompilerInfo().ResourceResolver;
            string baseUriStr = baseUri.ToString();
            DocumentKey key = DocumentFn.ComputeDocumentKey(href, baseUriStr, compilation.GetPackageData(), false);
            Dictionary<DocumentKey, ITreeInfo> map = compilation.StylesheetModules;
            if (map.ContainsKey(key))
            {
                return (DocumentImpl)map.GetOrDefault(key);
            }
            else
            {
                ResourceRequest request = new ResourceRequest();
                request.relativeUri = href;
                request.baseUri = baseUriStr;
                request.uri = key.AbsoluteURI;
                request.nature = ResourceRequest.XSLT_NATURE;
                request.purpose = ResourceRequest.ANY_PURPOSE;
                ResolvedResource source;
                try
                {
                    source = request.Resolve(resolver, config.GetResourceResolver(), new DirectResourceResolver(config));
                }
                catch (XPathException e)
                {
                    // A module no resolver can go for - a relative href with no absolute base URI behind it
                    e.MaybeSetErrorCode("XTSE0165");
                    throw;
                }

                if (source == null)
                {
                    throw new XPathException("Unable to resolve " + elemName.DisplayName + " stylesheet URI " + href, "XTSE0165").WithLocation(location);
                }

                if (source.IsEmpty)
                {
                    source = new ResolvedResource { TextReader = new StringReader("<xsl:transform version='3.0' xmlns:xsl='http://www.w3.org/1999/XSL/Transform'/>") };
                }

                NestedIntegerValue newPrecedence = precedence;
                if (isImport)
                {
                    newPrecedence = precedence.Stem.Append(precedence.Leaf - 1).Append(2 * ++importCount);
                }

                try
                {
                    DocumentImpl includedDoc = StylesheetModule.LoadStylesheetModule(source, false, compilation, newPrecedence);
                    map[key] = includedDoc;
                    return includedDoc;
                }
                // Filtered: every clause below is set-if-unset plus a report-once, so on an outer
                // level of a nested include the body has nothing left to do - and an unentered
                // filter costs no funclet and no re-dispatch on the unwind, which is what bounds
                // include nesting (AW). The XTSE0180 exception to that: a cycle error still has to
                // be re-coded to XTSE0210 by an importing level above the level that raised it.
                catch (XPathException e) when (!e.HasBeenReported() || e.HasErrorCode("XTSE0180"))
                {
                    e.MaybeSetErrorCode("XTSE0165");
                    if (e.HasErrorCode(DAXonErrorCode.SXXP0003))
                    {
                        e.SetErrorCode("XTSE0165");
                    }
                    else if (e.HasErrorCode("XTSE0180"))
                    {
                        if (isImport)
                        {
                            e.SetErrorCode("XTSE0210");
                        }
                    }

                    e.MaybeSetLocation(location);
                    if (!e.HasBeenReported())
                    {
                        compilation.ReportError(e);
                    }

                    throw;
                }
            }
        }

        private ParsedAttributes StartElementProcessAttributes(INodeName elemName, IAttributeMap attributes, NamespaceMap namespaces, bool inXsltNamespace, NamespaceUri stdAttUri)
        {
            bool inSaxonNamespace = elemName.HasURI(NamespaceUri.SAXON);
            ParsedAttributes pa = new ParsedAttributes();
            foreach (AttributeInfo att in attributes)
            {
                INodeName attName = att.GetNodeName();
                attName.ObtainFingerprint(GetNamePool());
                string local = attName.GetLocalPart();
                bool underscored = local.StartsWith("_", StringComparison.Ordinal);
                if (local.Equals("default-mode") && (attName.HasURI(NamespaceUri.XSLT) != inXsltNamespace))
                {
                    RegisterModeName(att.Value, namespaces);
                }

                if (attName.HasURI(stdAttUri))
                {
                    ProcessAttributeLocal(att, local, pa);
                    if (underscored && attName.HasURI(NamespaceUri.NULL) && (inXsltNamespace || inSaxonNamespace))
                    {
                        pa.hasShadowAttributes = true;
                    }
                }
                else if (inSaxonNamespace || attName.HasURI(NamespaceUri.SAXON))
                {
                    if (underscored)
                    {
                        pa.hasShadowAttributes = true;
                    }
                }
                else if (attName.HasURI(NamespaceUri.XML))
                {
                    if (local.Equals("base"))
                    {
                        pa.xmlBaseAtt = att.Value;
                    }
                }
            }

            return pa;
        }

        private void ProcessAttributeLocal(AttributeInfo att, string local, ParsedAttributes pa)
        {
            switch (local)
            {
                case "xpath-default-namespace":
                    pa.xpathDefaultNamespaceAtt = NamespaceUri.Of(att.Value);
                    break;
                case "version":
                    pa.versionAtt = att.Value;
                    break;
                case "use-when":
                    pa.useWhenAtt = att.Value;
                    break;
                case "static":
                    pa.staticAtt = att.Value;
                    break;
            }
        }

        private DocumentImpl HandleXsltElement(INodeName elemName, URI baseUri, int fp, ParsedAttributes pa, IAttributeMap attributes, NamespaceMap namespaces, ILocation location)
        {
            DocumentImpl includedDoc = null;
            if (fp == StandardNames.XSL_APPLY_TEMPLATES)
            {
                RegisterModeName(attributes.GetValue("mode"), namespaces);
                return null;
            }
            else if (defaultNamespaceStack.Count == 2)
            {
                switch (fp)
                {
                    case StandardNames.XSL_VARIABLE:
                    case StandardNames.XSL_PARAM:
                        if (pa.hasShadowAttributes)
                        {
                            pa.staticAtt = attributes.GetValue("static");
                        }

                        if (pa.staticAtt != null)
                        {
                            string staticStr = Whitespace.Trim(pa.staticAtt);
                            if (StyleElement.IsYes(staticStr))
                            {
                                ProcessStaticVariable(elemName, attributes, namespaces, location, baseUri, precedence);
                            }
                        }

                        break;
                    case StandardNames.XSL_INCLUDE:
                    case StandardNames.XSL_IMPORT:

                        // We need to process the included/imported stylesheet now, because its static variables
                        // can be used later in this module
                        string href = attributes.GetValue("href");
                        includedDoc = ProcessIncludeImport(elemName, location, baseUri, href, fp == StandardNames.XSL_IMPORT);
                        break;
                    case StandardNames.XSL_IMPORT_SCHEMA:
                        compilation.SetSchemaAware(true); // bug 3105
                        break;
                    case StandardNames.XSL_USE_PACKAGE:
                        if (precedence.Depth > 1)
                        {
                            throw new XPathException("xsl:use-package cannot appear in an imported stylesheet", "XTSE3008");
                        }

                        string name = attributes.GetValue("name");
                        string pversion = attributes.GetValue("package-version");
                        if (name != null)
                        {
                            try
                            {
                                UsePack use = new UsePack(name, pversion, location.SaveLocation());
                                compilation.RegisterPackageDependency(use);
                            }
                            catch (XPathException err)
                            {
                            }
                        }

                        break;
                    case StandardNames.XSL_MODE:
                        RegisterModeName(attributes.GetValue("name"), namespaces);
                        break;
                    case StandardNames.XSL_TEMPLATE:
                        RegisterModeNames(attributes.GetValue("mode"), namespaces);
                        break;
                }
            }

            return includedDoc;
        }

        private void RegisterModeName(string modeAtt, INamespaceResolver nsResolver)
        {
            if (modeAtt != null && !modeAtt.StartsWith("#", StringComparison.Ordinal))
            {
                try
                {
                    StructuredQName qName = StructuredQName.FromLexicalQName((modeAtt), false, true, nsResolver);
                    compilation.AllKnownModeNames.Add(qName);
                }
                catch (XPathException e)
                {
                }
            }
        }

        private void RegisterModeNames(string modeAtt, INamespaceResolver nsResolver)
        {
            if (modeAtt != null)
            {
                string[] tokens = Whitespace.Trim(modeAtt).SplitRegex("[ \t\n\r]+");
                foreach (string token in tokens)
                {
                    RegisterModeName(token, nsResolver);
                }
            }
        }

        private void ProcessStaticVariable(INodeName elemName, IAttributeMap attributes, INamespaceResolver nsResolver, ILocation location, URI baseUri, NestedIntegerValue precedence)
        {
            string nameStr = attributes.GetValue(NamespaceUri.NULL, "name");
            string asStr = attributes.GetValue(NamespaceUri.NULL, "as");
            string requiredStr = Whitespace.Trim(attributes.GetValue(NamespaceUri.NULL, "required"));
            bool isRequired = StyleElement.IsYes(requiredStr);
            UseWhenStaticContext staticContext = new UseWhenStaticContext(compilation, nsResolver);
            staticContext.SetBaseURI(baseUri.ToString());
            staticContext.SetContainingLocation(new AttributeLocation(elemName.GetStructuredQName(), NamespaceUri.NULL.QName("as"), location));
            Values.SequenceType requiredType = Values.SequenceType.ANY_SEQUENCE;
            int languageLevel = compilation.GetConfiguration().GetConfigurationProperty(Feature<int>.XPATH_VERSION_FOR_XSLT);
            if (languageLevel == 30)
            {
                languageLevel = 305; // XPath 3.0 + XSLT extensions
            }

            staticContext.SetXPathLanguageLevel(languageLevel);
            if (asStr != null)
            {
                XPathParser parser = compilation.GetConfiguration().NewExpressionParser("XP", false, staticContext);
                requiredType = parser.ParseSequenceType(asStr, staticContext);
            }

            StructuredQName varName;
            try
            {
                varName = StructuredQName.FromLexicalQName((nameStr), false, true, nsResolver);
            }
            catch (XPathException err)
            {
                throw CreateXPathException("Invalid variable name:" + nameStr + ". " + err.Message, err.ErrorCodeQName, location);
            }

            bool isVariable = elemName.GetLocalPart().Equals("variable");
            bool isParam = elemName.GetLocalPart().Equals("param");
            bool isSupplied = isParam && compilation.Parameters.ContainsKey(varName);
            AttributeLocation attLoc = new AttributeLocation(elemName.GetStructuredQName(), NamespaceUri.NULL.QName("select"), location);
            if (isParam)
            {
                if (isRequired && !isSupplied)
                {
                    string selectStr = attributes.GetValue(NamespaceUri.NULL, "select");
                    if (selectStr != null)
                    {
                        throw CreateXPathException("Cannot supply a default value when required='yes'", NamespaceUri.ERR.QName("XTSE0010"), attLoc);
                    }
                    else
                    {
                        throw CreateXPathException("No value was supplied for the required static parameter $" + varName.DisplayName, NamespaceUri.ERR.QName("XTDE0050"), location);
                    }
                }

                if (isSupplied)
                {
                    ISequence suppliedValue = compilation.Parameters.ConvertParameterValue(varName, requiredType, true, staticContext.MakeEarlyEvaluationContext());
                    compilation.DeclareStaticVariable(varName, suppliedValue.Materialize(), precedence, isParam);
                }
            }

            if (isVariable || !isSupplied)
            {
                string selectStr = attributes.GetValue(NamespaceUri.NULL, "select");
                IGroundedValue value;
                if (selectStr == null)
                {
                    if (isVariable)
                    {
                        throw CreateXPathException("The select attribute is required for a static global variable", NamespaceUri.ERR.QName("XTSE0010"), location);
                    }
                    else if (!Cardinality.AllowsZero(requiredType.GetCardinality()))
                    {
                        throw CreateXPathException("The parameter is implicitly required because it does not accept an " + "empty sequence, but no value has been supplied", NamespaceUri.ERR.QName("XTDE0700"), location);
                    }
                    else
                    {
                        if (asStr == null)
                        {
                            value = StringValue.EMPTY_STRING;
                        }
                        else
                        {
                            value = EmptySequence.GetInstance();
                        }

                        compilation.DeclareStaticVariable(varName, value, precedence, isParam);
                    }
                }
                else
                {
                    try
                    {
                        staticContext.SetContainingLocation(attLoc);
                        ISequence sequence = EvaluateStatic(selectStr, location, staticContext);
                        value = sequence.Materialize();
                    }
                    catch (XPathException e)
                    {
                        throw CreateXPathException("Error in " + elemName.GetLocalPart() + " expression. " + e.Message, e.ErrorCodeQName, attLoc);
                    }
                }

                Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.VARIABLE, varName.DisplayName, 0, "XTDE0050");
                TypeHierarchy th = GetConfiguration().GetTypeHierarchy();
                ISequence seq = th.ApplyFunctionConversionRules(value, requiredType, role, attLoc);
                value = seq.Materialize();
                try
                {
                    compilation.DeclareStaticVariable(varName, value, precedence, isParam);
                }
                catch (XPathException e)
                {
                    throw CreateXPathException(e.Message, e.ErrorCodeQName, attLoc);
                }
            }
        }

        private IAttributeMap ProcessShadowAttributes(INodeName elemName, IAttributeMap attributes, INamespaceResolver nsResolver, ILocation location, URI baseUri)
        {
            Dictionary<INodeName, AttributeInfo> attMap = new Dictionary<INodeName, AttributeInfo>();
            foreach (AttributeInfo att in attributes)
            {
                INodeName attName = att.GetNodeName();
                attMap[attName] = att;
            }

            foreach (AttributeInfo att in attributes)
            {
                INodeName attName = att.GetNodeName();
                string local = attName.GetLocalPart();
                NamespaceUri uri = attName.GetNamespaceUri();
                if (local.StartsWith("_", StringComparison.Ordinal) && (uri.IsEmpty() || uri.Equals(NamespaceUri.SAXON)) && local.Length >= 2)
                {
                    string value = att.Value;
                    AttributeLocation attLocation = new AttributeLocation(elemName.GetStructuredQName(), attName.GetStructuredQName(), location);
                    string newValue = ProcessShadowAttribute(value, baseUri.ToString(), nsResolver, attLocation);
                    string plainName = local.Substring(1);
                    INodeName newName;
                    if (uri.IsEmpty())
                    {
                        newName = new NoNamespaceName(plainName);
                    }
                    else
                    {
                        newName = new FingerprintedQName(attName.GetPrefix(), NamespaceUri.SAXON, plainName);
                    }


                    // if a corresponding attribute exists with no underscore, overwrite it.
                    // Drop the shadow attribute itself.
                    AttributeInfo newAtt = new AttributeInfo(newName, att.GetType(), newValue, att.GetLocation(), ReceiverOption.NONE);
                    attMap[newName] = newAtt;
                    attMap.Remove(attName);
                }
            }

            IAttributeMap resultAtts = EmptyAttributeMap.GetInstance();
            foreach (AttributeInfo att in attMap.Values)
            {
                resultAtts = resultAtts.Put(new AttributeInfo(att.GetNodeName(), att.GetType(), att.Value, att.GetLocation(), att.GetProperties()));
            }

            return resultAtts;
        }

        private URI ProcessBaseUri(ILocation location, string xmlBaseAtt)
        {
            string systemId = location.GetSystemId();
            if (systemId == null)
            {
                systemId = GetSystemId();
            }

            URI baseUri;
            if (systemId == null || systemId.Equals(systemIdStack.Peek()))
            {
                baseUri = baseUriStack.Peek();
            }
            else
            {
                try
                {
                    baseUri = new URI(systemId);
                }
                catch (URISyntaxException e)
                {
                    throw new XPathException("Invalid URI for stylesheet entity: " + systemId);
                }
            }

            if (xmlBaseAtt != null)
            {
                try
                {
                    baseUri = baseUri.Resolve(xmlBaseAtt);
                }
                catch (ArgumentException iae)
                {
                    throw new XPathException("Invalid URI in xml:base attribute: " + xmlBaseAtt + ". " + iae.Message);
                }
            }

            baseUriStack.Push(baseUri);
            systemIdStack.Push(systemId);
            return baseUri;
        }

        private int ProcessVersionAttribute(string version)
        {
            if (version != null)
            {
                IConversionResult cr = BigDecimalValue.MakeDecimalValue(version, true);
                if (cr is ValidationFailure)
                {
                    throw new XPathException("Invalid version number: " + version, "XTSE0110");
                }

                DecimalValue d = (DecimalValue)cr.AsAtomic();
                return (d.GetDecimalValue() * BigDecimal.Ten).IntValue();
            }
            else
            {
                return int.MinValue;
            }
        }

        private string ProcessShadowAttribute(string expression, string baseUri, INamespaceResolver nsResolver, AttributeLocation loc)
        {
            UseWhenStaticContext staticContext = new UseWhenStaticContext(compilation, nsResolver);
            staticContext.SetBaseURI(baseUri);
            staticContext.SetContainingLocation(loc);
            SetNamespaceBindings(staticContext);
            Expression expr = AttributeValueTemplate.Make(expression, staticContext);
            expr = TypeCheck(expr, staticContext);
            SlotManager stackFrameMap = AllocateSlots(expression, expr);
            IXPathContext dynamicContext = MakeDynamicContext(staticContext);
            ((XPathContextMajor)dynamicContext).OpenStackFrame(stackFrameMap);
            return expr.EvaluateAsString(dynamicContext).ToString();
        }

        private XPathException CreateXPathException(string message, StructuredQName errorCode, ILocation location)
        {
            XPathException err = new XPathException(message);
            err.ErrorCodeQName = errorCode;
            err.SetIsStaticError(true);
            err.SetLocator(location.SaveLocation());
            GetPipelineConfiguration().GetErrorReporter().Report(new XmlProcessingException(err));
            err.SetHasBeenReported(true);
            return err;
        }

        /// <summary>
        /// End of element
        /// </summary>
        public override void EndElement()
        {
            defaultNamespaceStack.Pop();
            if (depthOfHole > 0)
            {
                depthOfHole--;
            }
            else
            {
                systemIdStack.Pop();
                baseUriStack.Pop();
                versionStack.Pop();
                nextReceiver.EndElement();
            }
        }

        /// <summary>
        /// Character data
        /// </summary>
        public override void Characters(UnicodeString chars, ILocation locationId, int properties)
        {
            if (depthOfHole == 0)
            {
                nextReceiver.Characters(chars, locationId, properties);
            }
        }

        /// <summary>
        /// Processing Instruction
        /// </summary>
        public override void ProcessingInstruction(string target, UnicodeString data, ILocation locationId, int properties)
        {
        }

        public override void Comment(UnicodeString chars, ILocation locationId, int properties)
        {
        }

        private bool EvaluateUseWhen(string expression, AttributeLocation location, string baseUri, INamespaceResolver nsResolver)
        {
            UseWhenStaticContext staticContext = new UseWhenStaticContext(compilation, nsResolver);
            staticContext.SetBaseURI(baseUri);
            staticContext.SetContainingLocation(location);
            SetNamespaceBindings(staticContext);
            Expression expr = ExpressionTool.Make(expression, staticContext, 0, Token.EOF, null);
            expr.SetRetainedStaticContext(staticContext.MakeRetainedStaticContext());
            expr = TypeCheck(expr, staticContext);
            SlotManager stackFrameMap = AllocateSlots(expression, expr);
            IXPathContext dynamicContext = MakeDynamicContext(staticContext);

            ((XPathContextMajor)dynamicContext).OpenStackFrame(stackFrameMap);
            return expr.EffectiveBooleanValue(dynamicContext);
        }

        private SlotManager AllocateSlots(string expression, Expression expr)
        {
            SlotManager stackFrameMap = GetPipelineConfiguration().GetConfiguration().MakeSlotManager();
            if (expression.IndexOf('$') >= 0)
            {
                ExpressionTool.AllocateSlots(expr, stackFrameMap.NumberOfVariables, stackFrameMap);
            }

            return stackFrameMap;
        }

        private void SetNamespaceBindings(UseWhenStaticContext staticContext)
        {
            staticContext.SetDefaultElementNamespace(NamespaceUri.NULL);
            foreach (NamespaceUri uri in defaultNamespaceStack)
            {
                if (uri != null)
                {
                    staticContext.SetDefaultElementNamespace(uri);
                    break;
                }
            }
        }

        private Expression TypeCheck(Expression expr, UseWhenStaticContext staticContext)
        {
            Types.ItemType contextItemType = Types.Type.ITEM_TYPE;
            ContextItemStaticInfo cit = GetConfiguration().MakeContextItemStaticInfo(contextItemType, true);
            ExpressionVisitor visitor = ExpressionVisitor.Make(staticContext);
            return expr.TypeCheck(visitor, cit);
        }

        private IXPathContext MakeDynamicContext(UseWhenStaticContext staticContext)
        {
            Controller controller = new Controller(GetConfiguration());
            // Compile-time [xsl]use-when evaluation runs on its own controller, under the deadline its
            // compile armed: re-arming the Processor's limit here gave every static expression a fresh
            // budget (six static params of 250 ms compiled for 1.2 s under a 400 ms limit).
            controller.AdoptThreadDeadline();
            controller.GetExecutable().FunctionLibrary = (FunctionLibraryList)staticContext.GetFunctionLibrary();
            if (staticContext.GetXPathVersion() < 30)
            {
                controller.ResourceResolver = new ResourceResolverDelegate((request) =>
                {
                    throw new UncheckedXPathException("No external documents are available within an [xsl]use-when expression");
                });
            }

            controller.SetCurrentDateTime(currentDateTime);

            // this is to ensure that all use-when expressions in a module use the same date and time
            IXPathContext dynamicContext = controller.NewXPathContext();
            dynamicContext = dynamicContext.NewCleanContext();
            return dynamicContext;
        }

        public ISequence EvaluateStatic(string expression, ILocation locationId, UseWhenStaticContext staticContext)
        {
            try
            {
                SetNamespaceBindings(staticContext);
                Expression expr = ExpressionTool.Make(expression, staticContext, 0, Token.EOF, null);
                expr = TypeCheck(expr, staticContext);
                SlotManager stackFrameMap = GetPipelineConfiguration().GetConfiguration().MakeSlotManager();
                ExpressionTool.AllocateSlots(expr, stackFrameMap.NumberOfVariables, stackFrameMap);
                IXPathContext dynamicContext = MakeDynamicContext(staticContext);
                ((XPathContextMajor)dynamicContext).OpenStackFrame(stackFrameMap);
                return SequenceTool.ToGroundedValue(expr.Iterate(dynamicContext));
            }
            catch (UncheckedXPathException e)
            {
                throw e.GetXPathException();
            }
        }

        private sealed class ParsedAttributes
        {
            public NamespaceUri xpathDefaultNamespaceAtt = null;
            public string versionAtt = null;
            public string xmlBaseAtt = null;
            public string useWhenAtt = null;
            public string staticAtt = null;
            public bool hasShadowAttributes = false;
        }
    }
}
