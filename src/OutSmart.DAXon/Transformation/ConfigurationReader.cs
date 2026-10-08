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
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.XPath;
using OutSmart.DAXon.Transformation.Packages;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal.Net;
using System;
using System.Globalization;
using System.Collections.Generic;
using System.Text;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal.Collections;
using OutSmart.DAXon.Internal.Resolver;
namespace OutSmart.DAXon.Transformation
{
    /// <summary>
    /// Class used to read a config.xml file and transfer all settings from the file to the Configuration
    /// </summary>
    public class ConfigurationReader : IReceiver
    {
        private const int OBSOLETE_PROPERTY = -9999;
        private int level;
        private string section;
        private string subsection;
        private readonly StringBuilder buffer = new StringBuilder(100);
        protected Configuration targetConfig;
        private readonly IList<IXmlProcessingError> errors = new List<IXmlProcessingError>();
        private PackageLibrary packageLibrary;
        private PackageDetails currentPackage;
        private Configuration baseConfiguration;
        private PipelineConfiguration pipe;
        private readonly Stack<string> localNameStack = new Stack<string>();
        private string systemId;
        private List<string> catalogFiles;
        public ConfigurationReader()
        {
        }

        public virtual void SetPipelineConfiguration(PipelineConfiguration pipe)
        {
            this.pipe = pipe;
        }

        public virtual PipelineConfiguration GetPipelineConfiguration()
        {
            return pipe;
        }

        public virtual void SetSystemId(string systemId)
        {
            this.systemId = systemId;
        }

        public virtual void Open()
        {
        }

        public virtual void StartDocument(int properties)
        {
        }

        public virtual void SetUnparsedEntity(string name, string systemID, string publicID)
        {
        }

        public virtual void ProcessingInstruction(string name, UnicodeString data, ILocation location, int properties)
        {
        }

        public virtual void Comment(UnicodeString content, ILocation location, int properties)
        {
        }

        public virtual void Close()
        {
        }

        public virtual void Dispose()
        {
        }

        public virtual string GetSystemId()
        {
            return systemId;
        }

        public virtual void SetBaseConfiguration(Configuration @base)
        {
            this.baseConfiguration = @base;
        }

        public virtual Configuration MakeConfiguration(ResolvedResource source)
        {
            Configuration localConfig = baseConfiguration;
            if (localConfig == null)
            {
                if (pipe == null)
                {
                    localConfig = new Configuration();
                    SetPipelineConfiguration(localConfig.MakePipelineConfiguration());
                }
            }
            else
            {
                if (pipe == null)
                {
                    pipe = localConfig.MakePipelineConfiguration();
                }
            }

            SetSystemId(source.SystemId);
            IActiveSource activeSource = source.ToActiveSource();
            activeSource.Deliver(this, new ParseOptions());

            // TODO: set an error handler
            // TODO: set location information
            if (errors.Count > 0)
            {
                IErrorReporter reporter;
                IXmlProcessingError foundFatal = null;
                if (targetConfig == null)
                {
                    reporter = new StandardErrorReporter();
                }
                else
                {
                    reporter = targetConfig.MakeErrorReporter();
                }

                foreach (IXmlProcessingError err in errors)
                {
                    if (!err.IsWarning())
                    {
                        if (foundFatal == null)
                        {
                            foundFatal = err;
                        }
                    }

                    reporter.Report(err.AsWarning());
                }

                if (foundFatal != null)
                {
                    throw XPathException.FromXmlProcessingError(foundFatal);
                }
            }

            if (baseConfiguration != null)
            {
                targetConfig.ImportLicenseDetails(baseConfiguration);
            }

            return targetConfig;
        }

        public virtual void EndDocument()
        {
            if (targetConfig != null)
            {
                targetConfig.DefaultXsltCompilerInfo.SetPackageLibrary(packageLibrary);
            }
        }

        public virtual void StartElement(INodeName elemName, ISchemaType type, IAttributeMap atts, NamespaceMap namespaces, ILocation location, int properties)
        {
            NamespaceUri uri = elemName.GetNamespaceUri();
            string localName = elemName.GetLocalPart();
            localNameStack.Push(localName);
            buffer.Length = 0;
            if (NamespaceUri.SAXON_CONFIGURATION.Equals(uri))
            {
                if (level == 0)
                {
                    if (localName != "configuration")
                    {
                        Error(localName, null, null, "configuration");
                    }

                    string edition = atts.GetValue("edition");
                    if (edition == null)
                    {
                        edition = "HE";
                    }

                    switch (edition)
                    {
                        case "HE":
                            targetConfig = new Configuration();
                            break;
                        case "PE":
                            targetConfig = Configuration.MakeLicensedConfiguration("com.saxonica.config.ProfessionalConfiguration");
                            break;
                        case "EE":
                            targetConfig = Configuration.MakeLicensedConfiguration("com.saxonica.config.EnterpriseConfiguration");
                            break;
                        default:
                            Error("configuration", "edition", edition, "HE|PE|EE");
                            targetConfig = new Configuration();
                            break;
                    }

                    if (baseConfiguration != null)
                    {
                        targetConfig.SetNamePool(baseConfiguration.GetNamePool());
                        targetConfig.DocumentNumberAllocator = baseConfiguration.DocumentNumberAllocator;
                    }

                    packageLibrary = new PackageLibrary(targetConfig.DefaultXsltCompilerInfo);
                    string licenseLoc = atts.GetValue("licenseFileLocation");
                    if (licenseLoc != null && !edition.Equals("HE"))
                    {
                        string @base = GetSystemId();
                        try
                        {
                            URI absoluteLoc = ResolveURI.MakeAbsolute(licenseLoc, @base);
                            targetConfig.SetConfigurationProperty(FeatureKeys.LICENSE_FILE_LOCATION, absoluteLoc.ToString());
                        }
                        catch (Exception err)
                        {
                            XmlProcessingIncident incident = new XmlProcessingIncident("Failed to process license at " + licenseLoc);
                            incident.SetCause((Exception)err);
                            errors.Add(incident);
                        }
                    }

                    string targetEdition = atts.GetValue("targetEdition");
                    if (targetEdition != null)
                    {
                        packageLibrary.GetCompilerInfo().TargetEdition = targetEdition;
                    }

                    string label = atts.GetValue("label");
                    if (label != null)
                    {
                        targetConfig.Label = label;
                    }
                }

                if (level == 1)
                {
                    section = localName;
                    if (localName == "global")
                    {
                        ReadGlobalElement(atts);
                    }
                    else if (localName == "serialization")
                    {
                        ReadSerializationElement(atts, namespaces);
                    }
                    else if (localName == "xquery")
                    {
                        ReadXQueryElement(atts);
                    }
                    else if (localName == "xslt")
                    {
                        ReadXsltElement(atts);
                    }
                    else if (localName == "xsltPackages")
                    {
                    }
                    else if (localName == "xsd")
                    {
                        ReadXsdElement(atts);
                    }
                    else if (localName == "resources")
                    {

                        // Initialize the list of catalog files
                        catalogFiles = new List<string>();
                    }
                    else if (localName == "collations")
                    {
                    }
                    else if (localName == "localizations")
                    {
                        ReadLocalizationsElement(atts);
                    }
                    else
                    {
                        Error(localName, null, null, null);
                    }
                }
                else if (level == 2)
                {
                    subsection = localName;
                    switch (section)
                    {
                        case "resources":
                            if (localName == "fileExtension")
                            {
                                ReadFileExtension(atts);
                            }


                            // no action until endElement()
                            break;
                        case "collations":
                            if (localName != "collation")
                            {
                                Error(localName, null, null, "collation");
                            }
                            else
                            {
                                ReadCollation(atts);
                            }

                            break;
                        case "localizations":
                            if (localName != "localization")
                            {
                                Error(localName, null, null, "localization");
                            }
                            else
                            {
                                ReadLocalization(atts);
                            }

                            break;
                        case "xslt":
                            if (localName == "extensionElement")
                            {
                                ReadExtensionElement(atts);
                            }
                            else
                            {
                                Error(localName, null, null, null);
                            }

                            break;
                        case "xsltPackages":
                            if (localName == "package")
                            {
                                ReadXsltPackage(atts);
                            }

                            break;
                    }
                }
                else if (level == 3)
                {
                    if (subsection == "package")
                    {
                        if (localName == "withParam")
                        {
                            ReadWithParam(atts, namespaces);
                        }
                        else
                        {
                            Error(localName, null, null, null);
                        }
                    }
                }
            }
            else
            {
                XmlProcessingIncident incident = new XmlProcessingIncident("Configuration elements must be in namespace " + NamespaceConstant.SAXON_CONFIGURATION);
                errors.Add(incident);
            }

            level++;
        }

        private void ReadGlobalElement(IAttributeMap atts)
        {
            Properties props = new Properties();
            foreach (AttributeInfo a in atts)
            {
                string name = a.GetNodeName().GetLocalPart();
                string value = a.Value;
                if (!(value.Length == 0) && a.GetNodeName().GetNamespaceUri().IsEmpty())
                {
                    props.SetProperty(name, value);
                }
            }

            props.SetProperty("#element", "global");
            ApplyProperty(props, "allowedProtocols", FeatureCode.ALLOWED_PROTOCOLS, "JN");
            ApplyProperty(props, "allowExternalFunctions", FeatureCode.ALLOW_EXTERNAL_FUNCTIONS, "J");
            ApplyProperty(props, "allowMultiThreading", FeatureCode.ALLOW_MULTITHREADING, "JN");
            ApplyProperty(props, "allowOldJavaUriFormat", FeatureCode.ALLOW_OLD_JAVA_URI_FORMAT, "J");
            ApplyProperty(props, "allowSyntaxExtensions", FeatureCode.ALLOW_SYNTAX_EXTENSIONS, "JN");
            ApplyProperty(props, "collationUriResolver", FeatureCode.COLLATION_URI_RESOLVER_CLASS, "J");
            ApplyProperty(props, "collectionFinder", FeatureCode.COLLECTION_FINDER_CLASS, "J");
            ApplyProperty(props, "compileWithTracing", FeatureCode.COMPILE_WITH_TRACING, "JN");
            ApplyProperty(props, "debugByteCode", OBSOLETE_PROPERTY, "J");
            ApplyProperty(props, "debugByteCodeDirectory", OBSOLETE_PROPERTY, "J");
            ApplyProperty(props, "defaultCollation", FeatureCode.DEFAULT_COLLATION, "JN");
            ApplyProperty(props, "defaultCollection", FeatureCode.DEFAULT_COLLECTION, "JN");
            ApplyProperty(props, "defaultRegexEngine", FeatureCode.DEFAULT_REGEX_ENGINE, "J");
            ApplyProperty(props, "displayByteCode", OBSOLETE_PROPERTY, "J");
            ApplyProperty(props, "dtdValidation", FeatureCode.DTD_VALIDATION, "JN");
            ApplyProperty(props, "dtdValidationRecoverable", FeatureCode.DTD_VALIDATION_RECOVERABLE, "J");
            ApplyProperty(props, "eagerEvaluation", FeatureCode.EAGER_EVALUATION, "JN");
            ApplyProperty(props, "entityResolver", FeatureCode.ENTITY_RESOLVER_CLASS, "J");
            ApplyProperty(props, "environmentVariableResolver", FeatureCode.ENVIRONMENT_VARIABLE_RESOLVER_CLASS, "J");
            ApplyProperty(props, "errorListener", FeatureCode.ERROR_LISTENER_CLASS, "J");
            ApplyProperty(props, "expandAttributeDefaults", FeatureCode.EXPAND_ATTRIBUTE_DEFAULTS, "JN");
            ApplyProperty(props, "generateByteCode", OBSOLETE_PROPERTY, "J");
            ApplyProperty(props, "ignoreSAXSourceParser", FeatureCode.IGNORE_SAX_SOURCE_PARSER, "J");
            ApplyProperty(props, "lazyConstructionMode", OBSOLETE_PROPERTY, "JN");
            ApplyProperty(props, "lineNumbering", FeatureCode.LINE_NUMBERING, "JN");
            ApplyProperty(props, "markDefaultedAttributes", FeatureCode.MARK_DEFAULTED_ATTRIBUTES, "J");
            ApplyProperty(props, "maxCompiledClasses", OBSOLETE_PROPERTY, "J");
            ApplyProperty(props, "monitorHotSpotByteCode", OBSOLETE_PROPERTY, "J");
            ApplyProperty(props, "optimizationLevel", FeatureCode.OPTIMIZATION_LEVEL, "JN");
            ApplyProperty(props, "parser", FeatureCode.SOURCE_PARSER_CLASS, "J");
            ApplyProperty(props, "preEvaluateDoc", FeatureCode.PRE_EVALUATE_DOC_FUNCTION, "JN");
            ApplyProperty(props, "recognizeUriQueryParameters", FeatureCode.RECOGNIZE_URI_QUERY_PARAMETERS, "JN");
            ApplyProperty(props, "regexBacktrackingLimit", FeatureCode.REGEX_BACKTRACKING_LIMIT, "JN");
            ApplyProperty(props, "resourceResolver", FeatureCode.RESOURCE_RESOLVER_CLASS, "J");
            ApplyProperty(props, "retainNodeForDiagnostics", FeatureCode.RETAIN_NODE_FOR_DIAGNOSTICS, "JN");
            ApplyProperty(props, "schemaValidation", FeatureCode.SCHEMA_VALIDATION_MODE, "JN");
            ApplyProperty(props, "serializerFactory", FeatureCode.SERIALIZER_FACTORY_CLASS, "J");
            ApplyProperty(props, "sourceResolver", FeatureCode.SOURCE_RESOLVER_CLASS, "J");
            ApplyProperty(props, "stableCollectionUri", FeatureCode.STABLE_COLLECTION_URI, "JN");
            ApplyProperty(props, "stableUnparsedText", FeatureCode.STABLE_UNPARSED_TEXT, "JN");
            ApplyProperty(props, "standardErrorOutputFile", FeatureCode.STANDARD_ERROR_OUTPUT_FILE, "JN");
            ApplyProperty(props, "streamability", FeatureCode.STREAMABILITY, "JN");
            ApplyProperty(props, "streamingFallback", FeatureCode.STREAMING_FALLBACK, "JN");
            ApplyProperty(props, "stripSpace", FeatureCode.STRIP_WHITESPACE, "JN");
            ApplyProperty(props, "styleParser", FeatureCode.STYLE_PARSER_CLASS, "J");
            ApplyProperty(props, "suppressEvaluationExpiryWarning", FeatureCode.SUPPRESS_EVALUATION_EXPIRY_WARNING, "JN");
            ApplyProperty(props, "suppressXPathWarnings", FeatureCode.SUPPRESS_XPATH_WARNINGS, "JN");
            ApplyProperty(props, "suppressXsltNamespaceCheck", FeatureCode.SUPPRESS_XSLT_NAMESPACE_CHECK, "JN");
            ApplyProperty(props, "thresholdForFunctionInlining", FeatureCode.THRESHOLD_FOR_FUNCTION_INLINING, "JN");
            ApplyProperty(props, "thresholdForHotspotByteCode", OBSOLETE_PROPERTY, "J");
            ApplyProperty(props, "timing", FeatureCode.TIMING, "JN");
            ApplyProperty(props, "traceExternalFunctions", FeatureCode.TRACE_EXTERNAL_FUNCTIONS, "JN");
            ApplyProperty(props, "traceListener", FeatureCode.TRACE_LISTENER_CLASS, "J");
            ApplyProperty(props, "traceListenerOutputFile", FeatureCode.TRACE_LISTENER_OUTPUT_FILE, "JN");
            ApplyProperty(props, "traceOptimizerDecisions", FeatureCode.TRACE_OPTIMIZER_DECISIONS, "JN");
            ApplyProperty(props, "treeModel", FeatureCode.TREE_MODEL_NAME, "JN");

            // Two spellings accepted: see bug #6201. The correct one (according to the schema) is "unparsedTextURIResolver"
            ApplyProperty(props, "unparsedTextUriResolver", FeatureCode.UNPARSED_TEXT_URI_RESOLVER_CLASS, "J");
            ApplyProperty(props, "unparsedTextURIResolver", FeatureCode.UNPARSED_TEXT_URI_RESOLVER_CLASS, "J");
            ApplyProperty(props, "uriResolver", FeatureCode.URI_RESOLVER_CLASS, "J");
            ApplyProperty(props, "usePiDisableOutputEscaping", FeatureCode.USE_PI_DISABLE_OUTPUT_ESCAPING, "JN");
            ApplyProperty(props, "useTypedValueCache", FeatureCode.USE_TYPED_VALUE_CACHE, "JN");
            ApplyProperty(props, "validationComments", FeatureCode.VALIDATION_COMMENTS, "JN");
            ApplyProperty(props, "validationWarnings", FeatureCode.VALIDATION_WARNINGS, "JN");
            ApplyProperty(props, "versionOfXml", FeatureCode.XML_VERSION, "J");
            ApplyProperty(props, "xInclude", FeatureCode.XINCLUDE, "J");
            ApplyProperty(props, "xpathVersionForXsd", FeatureCode.XPATH_VERSION_FOR_XSD, "JN");
            ApplyProperty(props, "xpathVersionForXslt", FeatureCode.XPATH_VERSION_FOR_XSLT, "JN");
            ApplyProperty(props, "zipUriPattern", FeatureCode.ZIP_URI_PATTERN, "JN");
            foreach (string name in props.StringPropertyNames())
            {
                if (!name.Equals("#element"))
                {
                    Error("global", name, props.GetProperty(name), "#unrecognized");
                }
            }
        }

        private void ApplyProperty(Properties props, string attributeName, int featureCode, string flags)
        {
            string value = props.GetProperty(attributeName);
            if (value != null)
            {
                if (featureCode == OBSOLETE_PROPERTY)
                {
                    Error(props.GetProperty("#element"), attributeName, value, "#obsolete");
                    props.Remove(attributeName);
                    return;
                }

                if (!CheckPlatform(flags))
                {
                    Error(props.GetProperty("#element"), attributeName, value, "Property " + attributeName + " is not available in SaxonCS");
                    return;
                }

                try
                {
                    targetConfig.SetConfigurationProperty(FeatureIndex.GetData(featureCode).uri, value);
                    props.Remove(attributeName);
                }
                catch (ArgumentException e)
                {
                    string message = e.Message;
                    if (message.StartsWith(attributeName, StringComparison.Ordinal))
                    {
                        message = message.Replace(attributeName, "Value");
                    }

                    if (message.StartsWith("Unknown configuration property", StringComparison.Ordinal))
                    {
                        message = "Property " + attributeName + " is not available in Saxon-" + targetConfig.EditionCode;
                    }

                    Error(props.GetProperty("#element"), attributeName, value, message);
                }
            }
        }

        private bool CheckPlatform(string flags)
        {
            return flags.Contains("J");
        }

        private void ReadSerializationElement(IAttributeMap atts, NamespaceMap nsMap)
        {
            Properties props = new Properties();
            foreach (AttributeInfo a in atts)
            {
                NamespaceUri uri = a.GetNodeName().GetNamespaceUri();
                string name = a.GetNodeName().GetLocalPart();
                string value = a.Value;
                if ((value.Length == 0))
                {
                    continue;
                }

                try
                {
                    ResultDocument.SetSerializationProperty(props, uri, name, value, nsMap, false, targetConfig);
                }
                catch (XPathException e)
                {
                    errors.Add(new XmlProcessingException(e));
                }
            }

            targetConfig.DefaultSerializationProperties = props;
        }

        private void ReadCollation(IAttributeMap atts)
        {
            Properties props = new Properties();
            string collationUri = null;
            foreach (AttributeInfo a in atts)
            {
                NamespaceUri uri = a.GetNodeName().GetNamespaceUri();
                string name = a.GetNodeName().GetLocalPart();
                string value = a.Value;
                if (uri.IsEmpty())
                {
                    if ((value.Length == 0))
                    {
                        continue;
                    }

                    if (name == "uri")
                    {
                        collationUri = value;
                    }
                    else
                    {
                        props.SetProperty(name, value);
                    }
                }
            }

            if (collationUri == null)
            {
                errors.Add(new XmlProcessingIncident("collation specified with no uri"));
            }

            IStringCollator collator = null;
            try
            {
                collator = Core.Version.platform.MakeCollation(targetConfig, props, collationUri);
            }
            catch (XPathException e)
            {
                errors.Add(new XmlProcessingIncident(e.Message));
            }

            targetConfig.RegisterCollation(collationUri, collator);
        }

        private void ReadLocalizationsElement(IAttributeMap atts)
        {
            foreach (AttributeInfo a in atts)
            {
                NamespaceUri uri = a.GetNodeName().GetNamespaceUri();
                string name = a.GetNodeName().GetLocalPart();
                string value = a.Value;
                if (uri.IsEmpty())
                {
                    if (name == "defaultLanguage" && !(value.Length == 0))
                    {
                        targetConfig.SetConfigurationProperty(FeatureKeys.DEFAULT_LANGUAGE, value);
                    }

                    if (name == "defaultCountry" && !(value.Length == 0))
                    {
                        targetConfig.SetConfigurationProperty(FeatureKeys.DEFAULT_COUNTRY, value);
                    }
                }
            }
        }

        private void ReadLocalization(IAttributeMap atts)
        {
            string lang = null;
            Properties properties = new Properties();
            foreach (AttributeInfo a in atts)
            {
                NamespaceUri uri = a.GetNodeName().GetNamespaceUri();
                string name = a.GetNodeName().GetLocalPart();
                string value = a.Value;
                if (uri.IsEmpty())
                {
                    if (name == "lang" && !(value.Length == 0))
                    {
                        lang = value;
                    }
                    else if (!(value.Length == 0))
                    {
                        properties.SetProperty(name, value);
                    }
                }
            }

            if (lang != null)
            {
                LocalizerFactory factory = targetConfig.LocalizerFactory;
                if (factory != null)
                {
                    factory.SetLanguageProperties(lang, properties);
                }
            }
        }

        private void ReadFileExtension(IAttributeMap atts)
        {
            string extension = atts.GetValue("extension");
            string mediaType = atts.GetValue("mediaType");
            if (extension == null)
            {
                Error("fileExtension", "extension", null, null);
            }

            if (mediaType == null)
            {
                Error("fileExtension", "mediaType", null, null);
            }

            targetConfig.RegisterFileExtension(extension, mediaType);
        }

        protected virtual void ReadExtensionElement(IAttributeMap atts)
        {
            XmlProcessingIncident err = new XmlProcessingIncident("Extension elements are not available in Saxon" + Core.Version.SoftwarePlatform + "-" + targetConfig.EditionCode);

            //err.setLocation(Loc.makeFromSax(locator));     // TODO: reinstate location info for diagnostics
            errors.Add(err);
        }

        protected virtual void ReadXsltPackage(IAttributeMap atts)
        {
            string name = atts.GetValue("name");
            if (name == null)
            {
                string attName = "exportLocation";
                string location = atts.GetValue("exportLocation");
                URI uri = null;
                if (location == null)
                {
                    attName = "sourceLocation";
                    location = atts.GetValue("sourceLocation");
                }

                if (location == null)
                {
                    Error("package", attName, null, null);
                }

                try
                {
                    uri = ResolveURI.MakeAbsolute(location, GetSystemId());
                }
                catch (URISyntaxException)
                {
                    Error("package", attName, location, "Requires a valid URI.");
                }

                string file = new Uri(uri.ToString()).LocalPath;
                try
                {
                    packageLibrary.AddPackage(file);
                }
                catch (XPathException e)
                {
                    Error(e);
                }
            }
            else
            {
                string version = atts.GetValue("version");
                if (version == null)
                {
                    version = "1";
                }

                VersionedPackageName vpn = null;
                PackageDetails details = new PackageDetails();
                try
                {
                    vpn = new VersionedPackageName(name, version);
                }
                catch (XPathException)
                {
                    Error("package", "version", version, null);
                }

                details.nameAndVersion = vpn;
                currentPackage = details;
                string sourceLoc = atts.GetValue("sourceLocation");
                ResolvedResource source = null;
                if (sourceLoc != null)
                {
                    try
                    {
                        source = new ResolvedResource { SystemId = ResolveURI.MakeAbsolute(sourceLoc, GetSystemId()).ToString() };
                    }
                    catch (URISyntaxException)
                    {
                        Error("package", "sourceLocation", sourceLoc, "Requires a valid URI.");
                    }

                    details.sourceLocation = source;
                }

                string exportLoc = atts.GetValue("exportLocation");
                if (exportLoc != null)
                {
                    try
                    {
                        source = new ResolvedResource { SystemId = ResolveURI.MakeAbsolute(exportLoc, GetSystemId()).ToString() };
                    }
                    catch (URISyntaxException)
                    {
                        Error("package", "exportLocation", exportLoc, "Requires a valid URI.");
                    }

                    details.exportLocation = source;
                }

                string priority = atts.GetValue("priority");
                if (priority != null)
                {
                    try
                    {
                        details.priority = int.Parse(priority, CultureInfo.InvariantCulture);
                    }
                    catch (Exception err) when (err is FormatException || err is OverflowException)
                    {
                        Error("package", "priority", priority, "Requires an integer.");
                    }
                }

                details.baseName = atts.GetValue("base");
                details.shortName = atts.GetValue("shortName");
                packageLibrary.AddPackage(details);
            }
        }

        protected virtual void ReadWithParam(IAttributeMap atts, NamespaceMap nsMap)
        {
            if (currentPackage.exportLocation != null)
            {
                Error("withParam", null, null, "Not allowed when @exportLocation exists");
            }

            string name = atts.GetValue("name");
            if (name == null)
            {
                Error("withParam", "name", null, null);
            }

            QNameParser qp = new QNameParser(nsMap).WithAcceptEQName(true);
            StructuredQName qName = null;
            try
            {
                qName = qp.Parse(name, NamespaceUri.NULL);
            }
            catch (XPathException)
            {
                Error("withParam", "name", name, "Requires valid QName");
            }

            string select = atts.GetValue("select");
            if (select == null)
            {
                Error("withParam", "select", null, null);
            }

            IndependentContext env = new IndependentContext(targetConfig);
            env.SetNamespaceResolver(nsMap);
            XPathParser parser = new XPathParser(env);
            IGroundedValue value = null;
            try
            {
                Expression exp = parser.Parse(select, 0, Token.EOF, env);
                value = SequenceTool.ToGroundedValue(exp.Iterate(env.MakeEarlyEvaluationContext()));
            }
            catch (XPathException e)
            {
                Error(e);
            }
            catch (UncheckedXPathException e)
            {
                Error(e.GetXPathException());
            }

            if (currentPackage.staticParams == null)
            {
                currentPackage.staticParams = new Dictionary<StructuredQName, IGroundedValue>();
            }

            currentPackage.staticParams[qName] = value;
        }

        private void ReadXQueryElement(IAttributeMap atts)
        {
            Properties props = new Properties();
            foreach (AttributeInfo a in atts)
            {
                NamespaceUri uri = a.GetNodeName().GetNamespaceUri();
                string name = a.GetNodeName().GetLocalPart();
                string value = a.Value;
                if (!(value.Length == 0) && uri.IsEmpty())
                {
                    props.SetProperty(name, value);
                }
            }

            props.SetProperty("#element", "xquery");
            ApplyProperty(props, "allowUpdate", FeatureCode.XQUERY_ALLOW_UPDATE, "JN");
            ApplyProperty(props, "constructionMode", FeatureCode.XQUERY_CONSTRUCTION_MODE, "JN");
            ApplyProperty(props, "defaultElementNamespace", FeatureCode.XQUERY_DEFAULT_ELEMENT_NAMESPACE, "JN");
            ApplyProperty(props, "defaultFunctionNamespace", FeatureCode.XQUERY_DEFAULT_FUNCTION_NAMESPACE, "JN");
            ApplyProperty(props, "emptyLeast", FeatureCode.XQUERY_EMPTY_LEAST, "JN");
            ApplyProperty(props, "inheritNamespaces", FeatureCode.XQUERY_INHERIT_NAMESPACES, "JN");
            ApplyProperty(props, "moduleUriResolver", FeatureCode.MODULE_URI_RESOLVER_CLASS, "J");
            ApplyProperty(props, "multipleModuleImports", FeatureCode.XQUERY_MULTIPLE_MODULE_IMPORTS, "JN");
            ApplyProperty(props, "preserveBoundarySpace", FeatureCode.XQUERY_PRESERVE_BOUNDARY_SPACE, "JN");
            ApplyProperty(props, "preserveNamespaces", FeatureCode.XQUERY_PRESERVE_NAMESPACES, "JN");
            ApplyProperty(props, "requiredContextItemType", FeatureCode.XQUERY_REQUIRED_CONTEXT_ITEM_TYPE, "JN");
            ApplyProperty(props, "schemaAware", FeatureCode.XQUERY_SCHEMA_AWARE, "JN");
            ApplyProperty(props, "staticErrorListener", FeatureCode.XQUERY_STATIC_ERROR_LISTENER_CLASS, "J");
            ApplyProperty(props, "version", FeatureCode.XQUERY_VERSION, "JN");
            foreach (string name in props.StringPropertyNames())
            {
                if (!name.Equals("#element"))
                {
                    Error("xquery", name, props.GetProperty(name), "#unrecognized");
                }
            }
        }

        private void ReadXsltElement(IAttributeMap atts)
        {
            Properties props = new Properties();
            foreach (AttributeInfo a in atts)
            {
                NamespaceUri uri = a.GetNodeName().GetNamespaceUri();
                string name = a.GetNodeName().GetLocalPart();
                string value = a.Value;
                if (!(value.Length == 0) && uri.IsEmpty())
                {
                    props.SetProperty(name, value);
                }
            }

            props.SetProperty("#element", "xslt");
            ApplyProperty(props, "disableXslEvaluate", FeatureCode.DISABLE_XSL_EVALUATE, "JN");
            ApplyProperty(props, "enableAssertions", FeatureCode.XSLT_ENABLE_ASSERTIONS, "JN");
            ApplyProperty(props, "initialMode", FeatureCode.XSLT_INITIAL_MODE, "JN");
            ApplyProperty(props, "initialTemplate", FeatureCode.XSLT_INITIAL_TEMPLATE, "JN");
            ApplyProperty(props, "messageEmitter", OBSOLETE_PROPERTY, "J");
            ApplyProperty(props, "outputUriResolver", FeatureCode.OUTPUT_URI_RESOLVER_CLASS, "J");
            ApplyProperty(props, "recoveryPolicy", OBSOLETE_PROPERTY, "JN");
            ApplyProperty(props, "resultDocumentThreads", FeatureCode.RESULT_DOCUMENT_THREADS, "JN");
            ApplyProperty(props, "schemaAware", FeatureCode.XSLT_SCHEMA_AWARE, "JN");
            ApplyProperty(props, "staticErrorListener", FeatureCode.XSLT_STATIC_ERROR_LISTENER_CLASS, "J");
            ApplyProperty(props, "staticUriResolver", FeatureCode.XSLT_STATIC_URI_RESOLVER_CLASS, "J");
            ApplyProperty(props, "strictStreamability", FeatureCode.STRICT_STREAMABILITY, "JN");
            ApplyProperty(props, "styleParser", FeatureCode.STYLE_PARSER_CLASS, "J");
            ApplyProperty(props, "version", FeatureCode.XSLT_VERSION, "JN");
            ApplyProperty(props, "versionWarning", OBSOLETE_PROPERTY, "JN");
            foreach (string name in props.StringPropertyNames())
            {
                if (!name.Equals("#element"))
                {
                    Error("xslt", name, props.GetProperty(name), "#unrecognized");
                }
            }
        }

        private void ReadXsdElement(IAttributeMap atts)
        {
            Properties props = new Properties();
            foreach (AttributeInfo a in atts)
            {
                NamespaceUri uri = a.GetNodeName().GetNamespaceUri();
                string name = a.GetNodeName().GetLocalPart();
                string value = a.Value;
                if (!(value.Length == 0) && uri.IsEmpty())
                {
                    props.SetProperty(name, value);
                }
            }

            props.SetProperty("#element", "xsd");
            ApplyProperty(props, "allowUnresolvedSchemaComponents", FeatureCode.ALLOW_UNRESOLVED_SCHEMA_COMPONENTS, "JN");
            ApplyProperty(props, "assertionsCanSeeComments", FeatureCode.ASSERTIONS_CAN_SEE_COMMENTS, "JN");
            ApplyProperty(props, "implicitSchemaImports", FeatureCode.IMPLICIT_SCHEMA_IMPORTS, "JN");
            ApplyProperty(props, "multipleSchemaImports", FeatureCode.MULTIPLE_SCHEMA_IMPORTS, "JN");
            ApplyProperty(props, "occurrenceLimits", FeatureCode.OCCURRENCE_LIMITS, "JN");
            ApplyProperty(props, "schemaUriResolver", FeatureCode.SCHEMA_URI_RESOLVER_CLASS, "J");
            ApplyProperty(props, "thresholdForCompilingTypes", OBSOLETE_PROPERTY, "JN");
            ApplyProperty(props, "useXsiSchemaLocation", FeatureCode.USE_XSI_SCHEMA_LOCATION, "JN");
            ApplyProperty(props, "version", FeatureCode.XSD_VERSION, "JN");
            foreach (string name in props.StringPropertyNames())
            {
                if (!name.Equals("#element"))
                {
                    Error("xsd", name, props.GetProperty(name), "#unrecognized");
                }
            }
        }

        private void Error(string element, string attribute, string actual, string required)
        {
            XmlProcessingIncident err;
            if (attribute == null)
            {
                err = new XmlProcessingIncident("Invalid configuration element " + element);
            }
            else if (actual == null)
            {
                err = new XmlProcessingIncident("Missing configuration property " + element + "/@" + attribute);
            }
            else if (required.Equals("#unrecognized"))
            {
                err = new XmlProcessingIncident("Unrecognized configuration property " + element + "/@" + attribute);
            }
            else if (required.Equals("#obsolete"))
            {
                err = new XmlProcessingIncident("Obsolete configuration property " + element + "/@" + attribute).AsWarning();
            }
            else if (required.Contains("is not available in"))
            {
                err = new XmlProcessingIncident("Configuration property " + element + "/@" + attribute + ": " + required);
            }
            else
            {
                err = new XmlProcessingIncident("Invalid configuration property " + element + "/@" + attribute + ". Supplied value: '" + actual + "'; required: '" + required + "'");
            }


            errors.Add(err);
        }

        protected virtual void Error(XPathException err)
        {

            errors.Add(new XmlProcessingException(err));
        }

        protected virtual void ErrorClass(string element, string attribute, string actual, System.Type required, Exception cause)
        {
            XmlProcessingIncident err = new XmlProcessingIncident("Invalid configuration property " + element + (attribute == null ? "" : "/@" + attribute) + ". Supplied value '" + actual + "', required value is the name of a class that implements '" + required.FullName + "'");
            err.SetCause((Exception)cause);

            errors.Add(err);
        }

        public virtual void EndElement()
        {
            string localName = localNameStack.Pop();
            if (level == 3 && section == "resources")
            {
                string content = buffer.ToString();
                if (!(content.Length == 0))
                {
                    if (localName == "externalObjectModel")
                    {
                        try
                        {
                            IExternalObjectModel model = (IExternalObjectModel)targetConfig.GetInstance(content);
                            targetConfig.RegisterExternalObjectModel(model);
                        }
                        catch (XPathException e)
                        {
                            ErrorClass("externalObjectModel", null, content, typeof(IExternalObjectModel), e);
                        }
                        catch (InvalidCastException e)
                        {
                            ErrorClass("externalObjectModel", null, content, typeof(IExternalObjectModel), e);
                        }
                    }
                    else if (localName == "extensionFunction")
                    {
                        try
                        {
                            ExtensionFunctionDefinition model = (ExtensionFunctionDefinition)targetConfig.GetInstance(content);
                            targetConfig.RegisterExtensionFunction(model);
                        }
                        catch (XPathException e)
                        {
                            ErrorClass("extensionFunction", null, content, typeof(ExtensionFunctionDefinition), e);
                        }
                        catch (InvalidCastException e)
                        {
                            ErrorClass("extensionFunction", null, content, typeof(ExtensionFunctionDefinition), e);
                        }
                        catch (ArgumentException e)
                        {
                            ErrorClass("extensionFunction", null, content, typeof(ExtensionFunctionDefinition), e);
                        }
                    }
                    else if (localName == "schemaDocument")
                    {
                        try
                        {
                            ResolvedResource source = GetInputSource(content);
                            targetConfig.AddSchemaSource(source);
                        }
                        catch (XPathException e)
                        {
                            errors.Add(new XmlProcessingException(e));
                        }
                    }
                    else if (localName == "schemaComponentModel")
                    {
                        try
                        {
                            ResolvedResource source = GetInputSource(content);
                            targetConfig.ImportComponents(source);
                        }
                        catch (XPathException e)
                        {
                            errors.Add(new XmlProcessingException(e));
                        }
                    }
                    else if (localName == "catalogFile")
                    {
                        URI baseURI = URI.Create(systemId);
                        catalogFiles.Add(baseURI.Resolve(content).ToString());
                    }
                    else if (localName == "fileExtension")
                    {
                    }
                    else
                    {
                        Error(localName, null, null, null);
                    }
                }
            }

            if (level == 2 && localName == "resources" && catalogFiles.Count != 0 && targetConfig.GetResourceResolver() is CatalogResourceResolver)
            {
                ((CatalogResourceResolver)targetConfig.GetResourceResolver()).SetFeature((ResolverFeature.CATALOG_FILES).ToString(), catalogFiles);
            }

            level--;
            buffer.Length = 0;
        }

        // already done at startElement time
        private ResolvedResource GetInputSource(string href)
        {
            try
            {
                string @base = GetSystemId();
                URI abs = ResolveURI.MakeAbsolute(href, @base);
                return new ResolvedResource { SystemId = abs.ToString() };
            }
            catch (URISyntaxException e)
            {
                throw new XPathException(e.Message);
            }
        }

        public virtual void Characters(UnicodeString chars, ILocation location, int properties)
        {
            buffer.Append(chars.ToString());
        }

        // === Auto-generated stubs (StubGenerator Phase 3.1f) ===
        public virtual void Append(IItem item, ILocation locationId, int properties) => throw new InvalidOperationException("This receiver only accepts character events");
        public virtual void Append(IItem item) => throw new InvalidOperationException("This receiver only accepts character events");
        public virtual bool UsesTypeAnnotations() => false;
        public virtual bool HandlesAppend() => false;
    }
}