////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Accumulators;
using OutSmart.DAXon.Expressions.Compatibility;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Numbering;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Values.Arrays;
using OutSmart.DAXon.Values.Maps;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.XQuery;
using OutSmart.DAXon.Regex;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Serialization;
using OutSmart.DAXon.Serialization.CharCodes;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.XPath;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation.Packages;
using OutSmart.DAXon.Trees.Tiny;
using OutSmart.DAXon.Trees.Utilities;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Net;
using OutSmart.DAXon.Internal.Caching;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions.Registry;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Collections.Trie;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Resources;
using OutSmart.DAXon.Xslt;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Collections;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
using OutSmart.DAXon.Internal.Streams;
using System.IO;

namespace OutSmart.DAXon.Core
{
    //@CSharpInjectMembers(code = {
    //        "    public void setErrorReporter(Action<Saxon.Hej.s9api.IXmlProcessingError> reporter) {"
    //                + "        setErrorReporter(new Saxon.Impl.Helpers.ErrorReportingAction(reporter));"
    //                + "    }"
    //})
    public class Configuration : INotationSet, Configuration.IApiProvider
    {
        internal readonly object syncLock = new object();
        /// <summary>
        /// Constant indicating the XML Version 1.0
        /// </summary>
        public const int XML10 = 10;
        /// <summary>
        /// Constant indicating the XML Version 1.1
        /// </summary>
        public const int XML11 = 11;
        public const int XSD10 = 10;
        public const int XSD11 = 11;
        protected static IntSet booleanFeatures = new IntHashSet(40);
        protected static IntSet stringFeatures = new IntHashSet(40);

        // Process-wide compiled-regex memo cache (Goal 6 / Appendix B). fn:matches/replace/tokenize/
        // analyze-string + xsl:analyze-string with a NON-literal pattern recompile the regex on every
        // call (RegexFunction pre-binds only literals), and a fresh Processor/Configuration per request
        // starts cold. A compiled ARegularExpression is immutable w.r.t. matching (a fresh REMatcher per
        // operation), so one instance is safely shared across threads AND Configurations. STATIC so it
        // survives Processor/Configuration re-instantiation; LFU-bounded (no clear-all thrash). The key
        // captures every result-affecting input incl. the config-dependent backtracking limit; warnings
        // are captured once and replayed so a hit emits identical diagnostics. ~1 program/entry x 256.
        private static readonly ClockCache<RegexCacheKey, RegexCacheEntry> CompiledRegexCache
            = new ClockCache<RegexCacheKey, RegexCacheEntry>(256);
        private static readonly string[] NoWarnings = new string[0];
        private IApiProvider apiProcessor = null;
        private CharacterSetFactory characterSetFactory;
        private readonly Dictionary<string, IStringCollator> collationMap = new Dictionary<string, IStringCollator>(10);
        private ICollationURIResolver collationResolver = new StandardCollationURIResolver();
        private string defaultCollationName = NamespaceConstant.CODEPOINT_COLLATION_URI;
        private Dictionary<string, IResourceCollection> registeredCollections = new Dictionary<string, IResourceCollection>();
        private ICollectionFinder collectionFinder;
        private IEnvironmentVariableResolver environmentVariableResolver = new StandardEnvironmentVariableResolver();
        private string defaultCollection = null;
        private ParseOptions defaultParseOptions = new ParseOptions();
        protected StaticQueryContext defaultStaticQueryContext;
        private StaticQueryContextFactory staticQueryContextFactory = new StaticQueryContextFactory();
        protected OptimizerOptions optimizerOptions = OptimizerOptions.FULL_HE_OPTIMIZATION;
        protected CompilerInfo defaultXsltCompilerInfo;
        private Func<Configuration, IErrorReporter> errorReporterFactory;
        protected IndependentContext staticContextForSystemFunctions;
        private string label = null;
        private DocumentNumberAllocator documentNumberAllocator = new DocumentNumberAllocator();
        private IDebugger debugger = null;
        private string defaultLanguage = Version.platform.GetDefaultLanguage();
        private string defaultCountry = Version.platform.DefaultCountry;
        private Properties defaultOutputProperties = new Properties();
        private IIDynamicLoader dynamicLoader = Version.platform.DefaultDynamicLoader;
        // Flag array indexed by FeatureCode, not a hash set: a bool element reads and writes
        // atomically and there is nothing to resize, so a host toggling a feature while another
        // thread transforms (contract-violating, but survivable) cannot tear the structure.
        private readonly bool[] enabledProperties = new bool[FeatureCode.MAX + 1];
        private readonly IntHashMap<string> stringProperties = new IntHashMap<string>(); // TODO: not yet widely used
        private IList<IExternalObjectModel> externalObjectModels = new List<IExternalObjectModel>(4);
        private readonly DocumentPool globalDocumentPool = new DocumentPool();
        private readonly IntegratedFunctionLibrary integratedFunctionLibrary = new IntegratedFunctionLibrary();
        private LocalizerFactory localizerFactory;
        private NamePool namePool = new NamePool();
        protected Optimizer optimizer = null;
        private SerializerFactory serializerFactory;
        private string sourceParserClass;
        private Logger traceOutput = new StandardLogger();
        private IModuleURIResolver standardModuleURIResolver;
        private string styleParserClass;
        private IUnparsedTextURIResolver unparsedTextURIResolver;
        private IXPathContext theConversionContext = null;
        private ConversionRules theConversionRules = null;
        private ITraceListener traceListener = null;
        private string traceListenerClass = null;
        private string traceListenerOutput = null;
        private string defaultRegexEngine = "S";
        protected TypeHierarchy typeHierarchy;
        private readonly TypeChecker typeChecker = new TypeChecker();
        private readonly TypeChecker10 typeChecker10 = new TypeChecker10();
        private IResourceResolver commonResolver;
        private ProtocolRestrictor protocolRestrictor = new ProtocolRestrictor("all");
        protected IntHashMap<FunctionLibraryList> builtInExtensionLibraryList = new IntHashMap<FunctionLibraryList>(4);
        protected int xsdVersion = XSD11;
        private int xmlVersion = XML10;
        private int xpathVersionForXsd = 20;
        private int xpathVersionForXslt = 31;
        private IComparer<string> mediaQueryEvaluator;
        private readonly Dictionary<string, string> fileExtensions = new Dictionary<string, string>();
        private readonly Dictionary<string, IResourceFactory> resourceFactoryMapping = new Dictionary<string, IResourceFactory>();
        private readonly Dictionary<NamespaceUri, IFunctionAnnotationHandler> functionAnnotationHandlers = new Dictionary<NamespaceUri, IFunctionAnnotationHandler>();
        private int regexBacktrackingLimit = 10000000;
        private readonly TreeStatistics treeStatistics = new TreeStatistics();

        // XSLT document() retrieval failure is a RECOVERABLE dynamic error (the recovery action is to return an
        // empty sequence). Off by default so fn:doc / document() raise FODC0002/FODC0005 (error-FODC0002a); the
        // QT3 driver turns it on for test-cases declaring <ignore_doc_failure satisfied="true"/>.
        private bool recoverFromDocFailures = false;

        public virtual string EditionCode => "HE";

        public virtual string ProductTitle => Version.ProductName + " " + Version.DistributionVersion + " (Saxon" + Version.platform.PlatformSuffix + "-" + EditionCode + " " + Version.ProductVersion + " base © Saxonica, MPL 2.0)";

        public virtual Properties LicenseFeatures => null;

        public virtual IIDynamicLoader DynamicLoader
        {
            get => dynamicLoader; set
            {
                this.dynamicLoader = value;
            }
        }

        public virtual Logger Logger
        {
            get => traceOutput; set
            {
                traceOutput = value;
            }
        }

        public virtual int XMLVersion
        {
            get => xmlVersion; set
            {
                xmlVersion = value;
                theConversionRules = null;
            }
        }

        public virtual IComparer<string> MediaQueryEvaluator
        {
            get => mediaQueryEvaluator; set
            {
                this.mediaQueryEvaluator = value;
            }
        }

        public virtual int XsdVersion
        {
            get => xsdVersion; set
            {
                xsdVersion = value;
            }
        }

        public virtual IXPathContext ConversionContext
        {
            get
            {
                if (theConversionContext == null)
                {
                    theConversionContext = new EarlyEvaluationContext(this);
                }

                return theConversionContext;
            }
        }

        public virtual IIntPredicateProxy ValidCharacterChecker
        {
            get
            {
                if (xmlVersion == XML10)
                {
                    return IntPredicateLambda.Of(XMLCharacterData.IsValid10);
                }
                else
                {
                    return IntPredicateLambda.Of(XMLCharacterData.IsValid11);
                }
            }
        }

        public virtual string TraceListenerClass
        {
            get => traceListenerClass; set
            {
                if (value == null)
                {
                    traceListenerClass = null;
                    SetCompileWithTracing(false);
                }
                else
                {
                    try
                    {
                        MakeTraceListener(value);
                    }
                    catch (XPathException err)
                    {
                        throw new ArgumentException(value + ": " + err.Message);
                    }

                    this.traceListenerClass = value;
                    SetCompileWithTracing(true);
                }
            }
        }

        public virtual string TraceListenerOutputFile
        {
            get => traceListenerOutput; set
            {
                traceListenerOutput = value;
            }
        }

        public virtual BuiltInFunctionSet XQueryUpdateFunctionSet => null;

        public virtual BuiltInFunctionSet VendorFunctionSet => VendorFunctionSetHE.GetInstance();

        public virtual ICollationURIResolver CollationURIResolver
        {
            get => collationResolver; set
            {
                collationResolver = value;
            }
        }

        public virtual string DefaultCollection
        {
            get => defaultCollection; set
            {
                defaultCollection = value;
            }
        }

        public virtual ICollectionFinder CollectionFinder
        {
            get => collectionFinder; set
            {
                collectionFinder = value;
            }
        }

        public virtual LocalizerFactory LocalizerFactory
        {
            get => localizerFactory; set
            {
                this.localizerFactory = value;
            }
        }

        public virtual string DefaultCountry
        {
            get => defaultCountry; set
            {
                defaultCountry = value;
            }
        }

        public virtual string DefaultRegexEngine
        {
            get => defaultRegexEngine; set
            {
                if (!(value == "J" || value == "N" || value == "S"))
                {
                    throw new ArgumentException("Regex engine must be S|J|N");
                }

                defaultRegexEngine = value;
            }
        }

        public virtual IUnparsedTextURIResolver UnparsedTextURIResolver
        {
            get => unparsedTextURIResolver; set
            {
                this.unparsedTextURIResolver = value;
            }
        }

        public virtual CompilerInfo DefaultXsltCompilerInfo => defaultXsltCompilerInfo;

        public virtual StaticQueryContext DefaultStaticQueryContext
        {
            get
            {
                if (defaultStaticQueryContext == null)
                {
                    defaultStaticQueryContext = MakeStaticQueryContext(false);
                }

                return defaultStaticQueryContext;
            }
        }

        public virtual string SourceParserClass
        {
            get => sourceParserClass; set
            {
                this.sourceParserClass = value;
            }
        }

        public virtual string StyleParserClass
        {
            get => styleParserClass; set
            {
                this.styleParserClass = value;
            }
        }

        public virtual SerializerFactory SerializerFactory
        {
            get => serializerFactory; set
            {
                serializerFactory = value;
            }
        }

        public virtual Properties DefaultSerializationProperties
        {
            get => defaultOutputProperties; set
            {
                defaultOutputProperties = value;
            }
        }

        public virtual int SchemaValidationMode
        {
            get => defaultParseOptions.GetSchemaValidationMode(); set
            {

                //                break;
                //            case Validation.LAX:
                //                if (!isLicensedFeature(LicenseFeature.SCHEMA_VALIDATION)) {
                //                    // if schema processing isn't supported, then there's never a schema, so lax validation is a no-op.
                //                    validationMode = Validation.STRIP;
                //                }
                //                break;
                //            case Validation.STRICT:
                //                checkLicensedFeature(LicenseFeature.SCHEMA_VALIDATION, "strict validation", -1);
                //                break;
                //            default:
                //                throw new ArgumentException("Unsupported validation mode " + validationMode);
                //        }
                defaultParseOptions = defaultParseOptions.WithSchemaValidationMode(value);
            }
        }

        public virtual DocumentNumberAllocator DocumentNumberAllocator
        {
            get => documentNumberAllocator; set
            {
                documentNumberAllocator = value;
            }
        }

        public virtual DocumentPool GlobalDocumentPool => globalDocumentPool;

        public virtual HashSet<NamespaceUri> ImportedNamespaces => new HashSet<NamespaceUri>();

        public virtual Collection<GlobalParam> DeclaredSchemaParameters => null;

        public virtual IDebugger Debugger
        {
            get => debugger; set
            {
                this.debugger = value;
            }
        }

        public virtual OptimizerOptions PermittedOptimizerOptions => OptimizerOptions.FULL_HE_OPTIMIZATION;

        public virtual ContextItemStaticInfo DefaultContextItemStaticInfo => ContextItemStaticInfo.DEFAULT;

        public virtual IList<IExternalObjectModel> ExternalObjectModels => externalObjectModels;

        public virtual string Label
        {
            get => label; set
            {
                this.label = value;
            }
        }

        static Configuration()
        {
            // Before anything parses or serializes: System.Xml resolves an input declaration on its own.
            PlatformEncodings.EnsureRegistered();
            booleanFeatures.Add(FeatureCode.ALLOW_EXTERNAL_FUNCTIONS);
            booleanFeatures.Add(FeatureCode.ALLOW_MULTITHREADING);
            booleanFeatures.Add(FeatureCode.ALLOW_SYNTAX_EXTENSIONS);
            booleanFeatures.Add(FeatureCode.ASSERTIONS_CAN_SEE_COMMENTS);
            booleanFeatures.Add(FeatureCode.COMPILE_WITH_TRACING);
            booleanFeatures.Add(FeatureCode.DEBUG_BYTE_CODE);
            booleanFeatures.Add(FeatureCode.DISABLE_XSL_EVALUATE);
            booleanFeatures.Add(FeatureCode.DISPLAY_BYTE_CODE);
            booleanFeatures.Add(FeatureCode.DTD_VALIDATION);
            booleanFeatures.Add(FeatureCode.EAGER_EVALUATION);
            booleanFeatures.Add(FeatureCode.EXPAND_ATTRIBUTE_DEFAULTS);
            booleanFeatures.Add(FeatureCode.EXPATH_FILE_DELETE_TEMPORARY_FILES);
            booleanFeatures.Add(FeatureCode.GENERATE_BYTE_CODE);
            booleanFeatures.Add(FeatureCode.IGNORE_SAX_SOURCE_PARSER);
            booleanFeatures.Add(FeatureCode.IMPLICIT_SCHEMA_IMPORTS);
            booleanFeatures.Add(FeatureCode.MARK_DEFAULTED_ATTRIBUTES);
            booleanFeatures.Add(FeatureCode.MONITOR_HOT_SPOT_BYTE_CODE);
            booleanFeatures.Add(FeatureCode.MULTIPLE_SCHEMA_IMPORTS);
            booleanFeatures.Add(FeatureCode.PRE_EVALUATE_DOC_FUNCTION);

            booleanFeatures.Add(FeatureCode.RECOGNIZE_URI_QUERY_PARAMETERS);
            booleanFeatures.Add(FeatureCode.RETAIN_DTD_ATTRIBUTE_TYPES);
            booleanFeatures.Add(FeatureCode.STABLE_COLLECTION_URI);
            booleanFeatures.Add(FeatureCode.STABLE_UNPARSED_TEXT);
            booleanFeatures.Add(FeatureCode.STREAMING_FALLBACK);
            booleanFeatures.Add(FeatureCode.STRICT_STREAMABILITY);
            booleanFeatures.Add(FeatureCode.SUPPRESS_EVALUATION_EXPIRY_WARNING);
            booleanFeatures.Add(FeatureCode.SUPPRESS_XPATH_WARNINGS);
            booleanFeatures.Add(FeatureCode.SUPPRESS_XSLT_NAMESPACE_CHECK);
            booleanFeatures.Add(FeatureCode.TRACE_EXTERNAL_FUNCTIONS);
            booleanFeatures.Add(FeatureCode.TRACE_OPTIMIZER_DECISIONS);
            booleanFeatures.Add(FeatureCode.USE_PI_DISABLE_OUTPUT_ESCAPING);
            booleanFeatures.Add(FeatureCode.USE_TYPED_VALUE_CACHE);
            booleanFeatures.Add(FeatureCode.XQUERY_MULTIPLE_MODULE_IMPORTS);
            booleanFeatures.Add(FeatureCode.RETAIN_NODE_FOR_DIAGNOSTICS);
            booleanFeatures.Add(FeatureCode.ALLOW_UNRESOLVED_SCHEMA_COMPONENTS);
            stringFeatures.Add(FeatureCode.ZIP_URI_PATTERN);
        }
        public Configuration()
        {
            Init();
        }

        public static Configuration NewConfiguration()
        {
            System.Type configurationClass = typeof(Configuration);
            try
            {
                return (Configuration)Activator.CreateInstance(configurationClass);
            }
            catch (Exception e)
            {
                throw new InvalidOperationException("Cannot instantiate a Configuration", e);
            }
        }

        public static Configuration NewLicensedConfiguration()
        {
            return new Configuration();
        }

        public static Configuration ReadConfiguration(ResolvedResource source)
        {
            Configuration tempConfig = NewConfiguration();
            return tempConfig.ReadConfigurationFile(source);
        }

        public static Configuration ReadConfiguration(ResolvedResource source, Configuration baseConfiguration)
        {
            Configuration tempConfig = NewConfiguration();
            return tempConfig.ReadConfigurationFile(source, baseConfiguration);
        }

        public static Configuration InstantiateConfiguration(string className)
        {
            System.Type theClass = System.Type.GetType(className);
            return (Configuration)Activator.CreateInstance(theClass);
        }

        public static bool IsAssertionsEnabled()
        {

            // Highly devious logic here. If assertions are enabled, the assertion is false, and a deliberate side-effect
            // of evaluating the assertion is that assertsEnabled is set to true. If assertions are not enabled, the assert
            // statement is not executed, so assertsEnabled is left as false.
            bool assertsEnabled = false;
            return assertsEnabled;
        }

        protected virtual Configuration ReadConfigurationFile(ResolvedResource source)
        {
            return MakeConfigurationReader().MakeConfiguration(source);
        }

        protected virtual Configuration ReadConfigurationFile(ResolvedResource source, Configuration baseConfiguration)
        {
            ConfigurationReader reader = MakeConfigurationReader();
            reader.SetBaseConfiguration(baseConfiguration);
            return reader.MakeConfiguration(source);
        }

        protected virtual ConfigurationReader MakeConfigurationReader()
        {
            return new ConfigurationReader();
        }

        protected virtual void Init()
        {

            // Note that some initializations have been moved here because C# is more restrictive about
            // how fields can be initialized when first declared.
            // There's a temptation to be lazy about instantiating the catalog resolver, but we want
            // to make sure that if someone subsequently overrides, for example, the URIResolver,
            // that their override wins.
            commonResolver = new CatalogResourceResolver();
            defaultXsltCompilerInfo = MakeCompilerInfo();

            //systemURIResolver = new StandardURIResolver(this);
            standardModuleURIResolver = Version.platform.MakeStandardModuleURIResolver(this);
            serializerFactory = new SerializerFactory(this);
            unparsedTextURIResolver = new StandardUnparsedTextResolver();
            mediaQueryEvaluator = Comparer<string>.Create((o1, o2) => 0);
            Version.platform.Initialize(this);

            // If the call to makeStandardModuleURIResolver during initialization, created
            // a StandardModuleURIResolver without a config, make sure it gets this config.
            if (standardModuleURIResolver is StandardModuleURIResolver)
            {
                ((StandardModuleURIResolver)standardModuleURIResolver).SetConfiguration(this);
            }


            InternalSetBooleanProperty(FeatureCode.ALLOW_EXTERNAL_FUNCTIONS, FeatureKeys.ALLOW_EXTERNAL_FUNCTIONS, true);
            InternalSetBooleanProperty(FeatureCode.DISABLE_XSL_EVALUATE, FeatureKeys.DISABLE_XSL_EVALUATE, false);

            string initializationClass = Environment.GetEnvironmentVariable("SAXON_INITIALIZER");
            if (initializationClass != null)
            {
                try
                {
                    IInitializer initializer = (IInitializer)GetInstance(initializationClass);
                    initializer.Initialize(this);
                }
                catch (XPathException e)
                {
                    Console.Error.WriteLine("Warning: Failed to invoke Saxon IInitializer " + initializationClass + ": " + e.Message);
                }
            }


            RegisterFileExtension("xml", "application/xml");
            RegisterFileExtension("html", "application/html");
            RegisterFileExtension("atom", "application/atom");
            RegisterFileExtension("xsl", "application/xml+xslt");
            RegisterFileExtension("xslt", "application/xml+xslt");
            RegisterFileExtension("xsd", "application/xml+xsd");
            RegisterFileExtension("txt", "text/plain");
            RegisterFileExtension("MF", "text/plain");
            RegisterFileExtension("class", "application/java");
            RegisterFileExtension("json", "application/json");
            RegisterFileExtension("", "application/unknown");
            RegisterMediaType("application/xml", XmlResource.FACTORY);
            RegisterMediaType("text/xml", XmlResource.FACTORY);
            RegisterMediaType("application/html", XmlResource.FACTORY);
            RegisterMediaType("text/html", XmlResource.FACTORY);
            RegisterMediaType("application/atom", XmlResource.FACTORY);
            RegisterMediaType("application/xml+xslt", XmlResource.FACTORY);
            RegisterMediaType("application/xml+xsd", XmlResource.FACTORY);
            RegisterMediaType("application/rdf+xml", XmlResource.FACTORY);
            RegisterMediaType("text/plain", UnparsedTextResource.FACTORY);
            RegisterMediaType("application/java", BinaryResource.FACTORY);
            RegisterMediaType("application/binary", BinaryResource.FACTORY);
            RegisterMediaType("application/json", JSONResource.FACTORY);
            RegisterMediaType("application/unknown", UnknownResource.FACTORY);
            RegisterFunctionAnnotationHandler(new XQueryFunctionAnnotationHandler());
        }

        public static Configuration MakeLicensedConfiguration(string className)
        {
            if (className == null)
            {
                className = "com.saxonica.config.ProfessionalConfiguration";
            }

            try
            {
                return InstantiateConfiguration(className);
            }
            catch (TypeLoadException e)
            {
                throw new InvalidOperationException(e?.Message, e);
            }
            catch (MissingMethodException e)
            {
                throw new InvalidOperationException(e?.Message, e);
            }
            catch (UnauthorizedAccessException e)
            {
                throw new InvalidOperationException(e?.Message, e);
            }
        }

        public virtual void ImportLicenseDetails(Configuration config)
        {
        }

        public virtual void SetProcessor(IApiProvider processor)
        {
            this.apiProcessor = processor;
        }

        public virtual IApiProvider GetProcessor()
        {
            return apiProcessor;
        }

        // The policy of the Processor this configuration serves, which nested Processors over it
        // inherit; null for the engine's own configurations, left unrestricted and uncapped.
        internal ResourceAccessPolicy ResourcePolicy
            => (GetProcessor() as Processor)?.Resources;

        public virtual void CheckLicensedFeature(int feature, string name, int localLicenseId)
        {
            string require = feature == LicenseFeature.PROFESSIONAL_EDITION ? "PE" : "EE";
            string message = "Requested feature (" + name + ") requires Saxon-" + require;
            if (!Version.softwareEdition.Equals("HE"))
            {
                string packageNs = Version.platform.IsDotNet() ? "Saxon.Eej.config" : "com.saxonica.config";
                message += ". You are using Saxon-" + Version.softwareEdition + " software, but the Configuration is an instance of " + GetType() + "; to use this feature you need to create an instance of " + (feature == LicenseFeature.PROFESSIONAL_EDITION ? packageNs + ".ProfessionalConfiguration" : packageNs + ".EnterpriseConfiguration");
            }

            throw new LicenseException(message, LicenseException.WRONG_CONFIGURATION);
        }

        public virtual void DisableLicensing()
        {
        }

        public virtual bool IsFeatureAllowedBySecondaryLicense(int localLicenseId, int feature)
        {
            return false;
        }

        public virtual bool IsLicensedFeature(int feature)
        {

            // changing this to true will do no good; it will cause Saxon to attempt to use the unavailable feature, rather than
            // recovering from its absence.
            return false;
        }

        public virtual void RequireProfessionalLicense(string featureName)
        {
            if (!IsLicensedFeature(LicenseFeature.PROFESSIONAL_EDITION))
            {
                throw new LicenseException("Use of " + featureName + " requires a license key for Saxon-PE or Saxon-EE", LicenseException.NOT_FOUND);
            }
        }

        public virtual string GetLicenseFeature(string name)
        {
            return null;
        }

        public virtual void DisplayLicenseMessage()
        {
        }

        public virtual int RegisterLocalLicense(string dmk)
        {
            return -1;
        }

        public virtual System.Type GetType(string className, bool tracing)
        {
            return dynamicLoader.GetType(className, tracing ? traceOutput : null);
        }

        public virtual object GetInstance(string className)
        {
            return dynamicLoader.GetInstance(className, IsTiming() ? traceOutput : null);
        }

        public virtual IResourceResolver GetResourceResolver()
        {
            if (commonResolver == null)
            {
                SetResourceResolver(new CatalogResourceResolver());
            }

            return commonResolver;
        }

        public virtual void SetResourceResolver(IResourceResolver resolver)
        {
            commonResolver = resolver;
        }

        public virtual void SetParameterizedURIResolver()
        {
            SetBooleanProperty(Feature<bool>.RECOGNIZE_URI_QUERY_PARAMETERS, true);
        }

        public virtual ProtocolRestrictor GetProtocolRestrictor()
        {
            return protocolRestrictor;
        }

        public virtual IResourceResolver MakeResourceResolver(string className)
        {
            object obj = dynamicLoader.GetInstance(className, null);
            if (obj is IResourceResolver)
            {
                return (IResourceResolver)obj;
            }

            throw new XPathException("Class " + className + " is not a IResourceResolver");
        }

        public virtual void SetErrorReporterFactory(Func<Configuration, IErrorReporter> factory)
        {
            errorReporterFactory = factory;
        }

        public virtual IErrorReporter MakeErrorReporter()
        {
            if (errorReporterFactory == null)
            {
                errorReporterFactory = (config) =>
                {
                    StandardErrorReporter reporter = new StandardErrorReporter();
                    reporter.Logger = config.Logger;
                    return reporter;
                };
            }

            return errorReporterFactory(this);
        }

        public virtual void ReportFatalError(XPathException err)
        {
            if (!err.HasBeenReported())
            {
                MakeErrorReporter().Report(new XmlProcessingException(err));
                err.SetHasBeenReported(true);
            }
        }

        public virtual void SetStandardErrorOutput(TextWriter @out)
        {
            if (traceOutput is StandardLogger)
            {
                ((StandardLogger)traceOutput).SetPrintStream(@out);
            }
        }

        public virtual ParseOptions GetParseOptions()
        {
            return defaultParseOptions;
        }

        public virtual void SetParseOptions(ParseOptions options)
        {
            defaultParseOptions = options;
        }

        public virtual void SetConversionRules(ConversionRules rules)
        {
            this.theConversionRules = rules;
        }

        public virtual ConversionRules GetConversionRules()
        {
            if (theConversionRules == null)
            {
                lock (syncLock)
                {
                    // Re-check: the null test above is outside the lock, so two threads that both
                    // saw null both get here and the second used to build a second rule set and
                    // overwrite the first - a duplicated converter cache, and two callers holding
                    // rule sets that are equal but not identical.
                    if (theConversionRules != null)
                    {
                        return theConversionRules;
                    }

                    ConversionRules cv = new ConversionRules();
                    cv.SetTypeHierarchy(GetTypeHierarchy());
                    cv.SetNotationSet(this);
                    if (xsdVersion == XSD10)
                    {
                        cv.StringToDoubleConverter = StringToDouble.GetInstance();
                        cv.SetURIChecker(StandardURIChecker.GetInstance()); // In XSD 1.1, there is no checking
                    }
                    else
                    {
                        cv.StringToDoubleConverter = StringToDouble11.GetInstance();
                    }

                    cv.SetAllowYearZero(xsdVersion != XSD10);
                    return theConversionRules = cv;
                }
            }
            else
            {
                return theConversionRules;
            }
        }

        public virtual int GetTreeModel()
        {
            return defaultParseOptions.Model.SymbolicValue;
        }

        public virtual void SetTreeModel(int treeModel)
        {
            defaultParseOptions = defaultParseOptions.WithModel(TreeModel.GetTreeModel(treeModel));
        }

        public virtual bool IsLineNumbering()
        {
            return defaultParseOptions.IsLineNumbering();
        }
        public virtual bool IsRecoverFromDocFailures()
        {
            return recoverFromDocFailures;
        }

        public virtual void SetRecoverFromDocFailures(bool value)
        {
            recoverFromDocFailures = value;
        }

        public virtual void SetLineNumbering(bool lineNumbering)
        {
            defaultParseOptions = defaultParseOptions.WithLineNumbering(lineNumbering);
        }

        public virtual void SetXIncludeAware(bool state)
        {
            defaultParseOptions = defaultParseOptions.WithXIncludeAware(state);
        }

        public virtual bool IsXIncludeAware()
        {
            return defaultParseOptions.IsXIncludeAware();
        }

        public virtual ITraceListener GetTraceListener()
        {
            return traceListener;
        }

        public virtual ITraceListener MakeTraceListener()
        {
            if (traceListener != null)
            {
                return traceListener;
            }
            else if (traceListenerClass != null)
            {
                try
                {
                    return MakeTraceListener(traceListenerClass);
                }
                catch (InvalidCastException e)
                {
                    throw new XPathException(e?.Message);
                }
            }
            else
            {
                return null;
            }
        }

        public virtual void SetTraceListener(ITraceListener traceListener)
        {
            this.traceListener = traceListener;
            SetCompileWithTracing(traceListener != null);
            InternalSetBooleanProperty(FeatureCode.ALLOW_MULTITHREADING, FeatureKeys.ALLOW_MULTITHREADING, false);
        }

        public virtual bool IsCompileWithTracing()
        {
            return GetBooleanProperty(Feature<bool>.COMPILE_WITH_TRACING);
        }

        public virtual void SetCompileWithTracing(bool trace)
        {
            InternalSetBooleanProperty(FeatureCode.COMPILE_WITH_TRACING, FeatureKeys.COMPILE_WITH_TRACING, trace);
            if (defaultXsltCompilerInfo != null)
            {
                if (trace)
                {
                    defaultXsltCompilerInfo.CodeInjector = new XSLTTraceCodeInjector();
                }
                else
                {
                    defaultXsltCompilerInfo.CodeInjector = null;
                }
            }

            DefaultStaticQueryContext.CodeInjector = trace ? new XQueryTraceCodeInjector() : null;
        }

        public virtual ITraceListener MakeTraceListener(string className)
        {
            object obj = dynamicLoader.GetInstance(className, null);
            if (obj is ITraceListener)
            {
                string destination = TraceListenerOutputFile;
                if (destination != null)
                {
                    try
                    {
                        ((ITraceListener)obj).SetOutputDestination(new StandardLogger(new StreamWriter(destination) { AutoFlush = true }));
                    }
                    catch (FileNotFoundException e)
                    {
                        throw new XPathException(e?.Message);
                    }
                }

                return (ITraceListener)obj;
            }

            throw new XPathException("Class " + className + " is not a ITraceListener");
        }

        public virtual BuiltInFunctionSet GetXSLTFunctionSet(int version)
        {
            if (version == 20)
            {
                return XPath20FunctionSet.GetInstance();
            }
            else if (version == 30 || version == 31 || version == 305)
            {
                return Functions.XSLT30FunctionSet.GetInstance();
            }
            else if (version == 40)
            {
                throw new ArgumentException("XSLT 4.0 requires Saxon-PE or higher");
            }
            else
            {
                throw new ArgumentException("Unsupported function library version " + version + " (must be 20|30|31|40)");
            }
        }

        public virtual BuiltInFunctionSet GetXPathFunctionSet(int version)
        {
            switch (version)
            {
                case 20:
                    return XPath20FunctionSet.GetInstance();
                case 30:
                case 305:
                    return XPath30FunctionSet.GetInstance();
                case 31:
                    return XPath31FunctionSet.GetInstance();
                case 40:
                    throw new ArgumentException("Version 4.0 requires Saxon-PE or higher");
                default:
                    return XPath31FunctionSet.GetInstance();
            }
        }

        public virtual SystemFunction MakeSystemFunction(string localName, int arity, int xpathVersion)
        {
            try
            {
                return GetXSLTFunctionSet(xpathVersion == 31 ? 30 : xpathVersion).MakeFunction(localName, arity);
            }
            catch (XPathException e)
            {
                return null;
            }
        }

        public virtual SystemFunction MakeSystemFunction40(string localName, int arity)
        {
            return MakeSystemFunction(localName, arity, 40);
        }

        public virtual void RegisterExtensionFunction(ExtensionFunctionDefinition function)
        {
            integratedFunctionLibrary.RegisterFunction(function);
        }

        public virtual IntegratedFunctionLibrary GetIntegratedFunctionLibrary()
        {
            return integratedFunctionLibrary;
        }

        public virtual FunctionLibraryList GetBuiltInExtensionLibraryList(int version)
        {
            lock (syncLock)
            {
                if (version != 40)
                {
                    version = 31;
                }

                FunctionLibraryList result = builtInExtensionLibraryList[version];
                if (result == null)
                {
                    result = MakeBuiltInExtensionLibraryList(version);
                    builtInExtensionLibraryList.Put(version, result);
                }

                return result;
            }
        }

        public virtual UseWhen30FunctionSet GetUseWhenFunctionLibrary(int version)
        {
            lock (syncLock)
            {
                return UseWhen30FunctionSet.GetInstance(version);
            }
        }

        protected virtual FunctionLibraryList MakeBuiltInExtensionLibraryList(int version)
        {
            FunctionLibraryList result = new FunctionLibraryList();
            result.AddFunctionLibrary(VendorFunctionSetHE.GetInstance());
            result.AddFunctionLibrary(MathFunctionSet.GetInstance());
            result.AddFunctionLibrary(MapFunctionSet.GetInstance(version));
            result.AddFunctionLibrary(ArrayFunctionSet.GetInstance(version));
            result.AddFunctionLibrary(ExsltCommonFunctionSet.GetInstance());
            return result;
        }

        public virtual SystemFunction BindSaxonExtensionFunction(string localName, int arity)
        {
            throw new NotSupportedException("The extension function saxon:" + localName + "#" + arity + " requires Saxon-PE or higher");
        }

        public virtual void AddExtensionBinders(FunctionLibraryList list)
        {
        }

        public virtual IFunctionLibrary LoadStubFunctionLibrary(ResolvedResource jsonSignatures)
        {
            throw new NotSupportedException();
        }

        public virtual IFunctionItem GetSystemFunction(StructuredQName name, int arity)
        {
            try
            {
                if (staticContextForSystemFunctions == null)
                {
                    staticContextForSystemFunctions = new IndependentContext(this);
                }

                IFunctionLibrary lib;
                NamespaceUri ns = name.GetNamespaceUri();
                int version = DefaultStaticQueryContext.LanguageVersion;
                if (ns.Equals(NamespaceUri.FN))
                {
                    lib = GetXPathFunctionSet(version);
                }
                else if (ns.Equals(NamespaceUri.SCHEMA))
                {
                    lib = new ConstructorFunctionLibrary(this);
                }
                else if (ns.Equals(NamespaceUri.MATH))
                {
                    lib = MathFunctionSet.GetInstance();
                }
                else if (ns.Equals(NamespaceUri.MAP_FUNCTIONS))
                {
                    lib = MapFunctionSet.GetInstance(version);
                }
                else if (ns.Equals(NamespaceUri.ARRAY_FUNCTIONS))
                {
                    lib = ArrayFunctionSet.GetInstance(version);
                }
                else
                {
                    FunctionLibraryList fll = new FunctionLibraryList();
                    fll.AddFunctionLibrary(GetBuiltInExtensionLibraryList(31));
                    fll.AddFunctionLibrary(new ConstructorFunctionLibrary(this));
                    fll.AddFunctionLibrary(GetIntegratedFunctionLibrary());
                    lib = fll;
                }

                SymbolicName.F symbolicName = new SymbolicName.F(name, arity);
                return lib.GetFunctionItem(symbolicName, staticContextForSystemFunctions);
            }
            catch (XPathException e)
            {
                return null;
            }
        }

        public virtual UserFunction NewUserFunction(bool memoFunction, FunctionStreamability streamability)
        {
            if (memoFunction)
            {
                return new MemoFunction();
            }
            else
            {
                return new UserFunction();
            }
        }

        public virtual void RegisterCollation(string collationURI, IStringCollator collator)
        {
            collationMap[collationURI] = collator;
        }

        public virtual IStringCollator GetCollation(string collationName)
        {
            if (collationName == null || collationName.Equals(NamespaceConstant.CODEPOINT_COLLATION_URI))
            {
                return CodepointCollator.GetInstance();
            }

            if (collationName.Equals(NamespaceConstant.HTML5_CASE_BLIND_COLLATION_URI))
            {
                return HTML5CaseBlindCollator.GetInstance();
            }

            if (collationName.StartsWith(AlphanumericCollator.PREFIX, StringComparison.Ordinal))
            {
                return new AlphanumericCollator(GetCollation(collationName.Substring(AlphanumericCollator.PREFIX.Length)));
            }

            IStringCollator collator = collationMap.GetOrDefault(collationName);
            if (collator == null)
            {
                collator = CollationURIResolver.Resolve(collationName, this);
            }

            return collator;
        }

        public virtual IStringCollator GetCollation(string collationURI, string baseURI)
        {
            if (collationURI.Equals(NamespaceConstant.CODEPOINT_COLLATION_URI))
            {
                return CodepointCollator.GetInstance();
            }

            try
            {
                string absoluteURI = ResolveURI.MakeAbsolute(collationURI, baseURI).ToString();
                return GetCollation(absoluteURI);
            }
            catch (URISyntaxException e)
            {
                throw new XPathException("Collation name is not a valid URI: " + collationURI + " (@base = " + baseURI + ")", "FOCH0002");
            }
        }

        public virtual IStringCollator GetCollation(string collationURI, string baseURI, string errorCode)
        {
            if (collationURI.Equals(NamespaceConstant.CODEPOINT_COLLATION_URI))
            {
                return CodepointCollator.GetInstance();
            }

            try
            {
                string absoluteURI = collationURI;
                if (baseURI != null)
                {
                    absoluteURI = ResolveURI.MakeAbsolute(collationURI, baseURI).ToString();
                }

                IStringCollator collator = GetCollation(absoluteURI);
                if (collator == null)
                {
                    throw new XPathException("Unknown collation " + absoluteURI, errorCode);
                }

                return collator;
            }
            catch (URISyntaxException e)
            {
                throw new XPathException("Collation name is not a valid URI: " + collationURI + " (@base = " + baseURI + ")", errorCode);
            }
        }

        public virtual string GetDefaultCollationName()
        {
            return defaultCollationName;
        }

        public virtual void RegisterCollection(string collectionURI, IResourceCollection collection)
        {
            registeredCollections[collectionURI] = collection;
        }

        public virtual IResourceCollection GetRegisteredCollection(string uri)
        {
            return registeredCollections.GetOrDefault(uri);
        }

        public virtual void RegisterFileExtension(string extension, string mediaType)
        {
            fileExtensions[extension] = mediaType;
        }

        public virtual void RegisterMediaType(string contentType, IResourceFactory factory)
        {
            resourceFactoryMapping[contentType] = factory;
        }

        public virtual string GetMediaTypeForFileExtension(string extension)
        {
            string mediaType = fileExtensions.GetOrDefault(extension);
            if (mediaType == null)
            {
                mediaType = fileExtensions.GetOrDefault("");
            }

            return mediaType;
        }

        public virtual IResourceFactory GetResourceFactoryForMediaType(string mediaType)
        {
            return resourceFactoryMapping.GetOrDefault(mediaType);
        }

        public virtual void SetDefaultLanguage(string language)
        {
            ValidationFailure vf = StringConverter.StringToLanguage.INSTANCE.Validate(StringView.Of(language).Tidy());
            if (vf != null)
            {
                throw new ArgumentException("The default language must be a valid language code");
            }

            defaultLanguage = language;
        }

        public virtual string GetDefaultLanguage()
        {
            return defaultLanguage;
        }

        public virtual IRegularExpression CompileRegularExpression(UnicodeString regex, string flags, string hostLanguage, IList<string> warnings)
        {
            // Backtracking limit is the only config-dependent input to the compiled program, so it goes
            // in the key (everything else the compile reads is the pattern/flags/host-language or the
            // process-constant Unicode tables). An invalid pattern makes the factory throw FORX0001/0002
            // and nothing is cached -- it re-throws on every call, exactly as before.
            int backtrackingLimit = GetConfigurationProperty(Feature<int>.REGEX_BACKTRACKING_LIMIT);
            RegexCacheKey key = new RegexCacheKey(regex.ToString(), flags ?? "", hostLanguage ?? "", backtrackingLimit);
            RegexCacheEntry entry = CompiledRegexCache.GetOrAdd(key, _ =>
            {
                List<string> captured = new List<string>();
                IRegularExpression compiled = Version.platform.CompileRegularExpression(this, regex, flags, hostLanguage, captured);
                return new RegexCacheEntry(compiled, captured.Count == 0 ? NoWarnings : captured.ToArray());
            });
            if (warnings != null && entry.Warnings.Length != 0)
            {
                foreach (string w in entry.Warnings)
                {
                    warnings.Add(w);
                }
            }
            return entry.Regex;
        }

        public virtual INumberer MakeNumberer(string language, string country)
        {
            if (localizerFactory == null)
            {
                // Non-English month/day names come from the OS culture when it knows the tag
                // (format-date lang='de' etc.); unknown tags keep the English fallback, which
                // FormatDate marks with the [Language: en] prefix.
                if (language != null && !language.StartsWith("en", StringComparison.Ordinal))
                {
                    System.Globalization.CultureInfo culture = DotNetPlatform.TryGetKnownCulture(language);
                    if (culture != null)
                    {
                        Numberer_bcl bcl = new Numberer_bcl(culture, language);
                        if (country != null)
                        {
                            bcl.Country = country;
                        }

                        return bcl;
                    }
                }

                Numberer_en numberer = new Numberer_en();
                if (language != null)
                {
                    numberer.SetLanguage(language);
                }

                if (country != null)
                {
                    numberer.Country = country;
                }

                return numberer;
            }
            else
            {
                INumberer numberer = localizerFactory.GetNumberer(language, country);
                if (numberer == null)
                {
                    numberer = new Numberer_en();
                }

                return numberer;
            }
        }

        public virtual void SetModuleURIResolver(IModuleURIResolver resolver)
        {
            DefaultStaticQueryContext.ModuleURIResolver = resolver;
        }

        public virtual void SetModuleURIResolver(string className)
        {
            object obj = dynamicLoader.GetInstance(className, null);
            if (obj is IModuleURIResolver)
            {
                if (obj is StandardModuleURIResolver)
                {
                    ((StandardModuleURIResolver)obj).SetConfiguration(this);
                }

                SetModuleURIResolver((IModuleURIResolver)obj);
            }
            else
            {
                throw new XPathException("Class " + className + " is not a IModuleURIResolver");
            }
        }

        public virtual IModuleURIResolver GetModuleURIResolver()
        {
            return DefaultStaticQueryContext.ModuleURIResolver;
        }

        public virtual IModuleURIResolver GetStandardModuleURIResolver()
        {
            return standardModuleURIResolver;
        }

        protected virtual StaticQueryContext MakeStaticQueryContext(bool copyFromDefault)
        {
            return staticQueryContextFactory.NewStaticQueryContext(this, copyFromDefault);
        }

        public virtual void RegisterFunctionAnnotationHandler(IFunctionAnnotationHandler handler)
        {
            functionAnnotationHandlers[handler.AssertionNamespace] = handler;
        }

        public virtual IFunctionAnnotationHandler GetFunctionAnnotationHandler(NamespaceUri @namespace)
        {
            return functionAnnotationHandlers.GetOrDefault(@namespace);
        }

        public virtual bool IsStreamabilityEnabled()
        {
            return false;
        }

        public virtual IOutputURIResolver GetOutputURIResolver()
        {
            return defaultXsltCompilerInfo.OutputURIResolver;
        }

        public virtual void SetOutputURIResolver(IOutputURIResolver outputURIResolver)
        {
            defaultXsltCompilerInfo.OutputURIResolver = outputURIResolver;
        }

        public virtual CharacterSetFactory GetCharacterSetFactory()
        {
            if (characterSetFactory == null)
            {
                characterSetFactory = new CharacterSetFactory();
            }

            return characterSetFactory;
        }

        public virtual SerializationProperties ObtainDefaultSerializationProperties()
        {
            return new SerializationProperties(defaultOutputProperties);
        }

        public virtual void ProcessResultDocument(ResultDocument instruction, IPushEvaluator content, IXPathContext context)
        {
            instruction.ProcessInstruction(content, context);
        }

        public virtual ISequenceIterator GetMultithreadedItemMappingIterator(ISequenceIterator @base, IItemMappingFunction action)
        {
            return new ItemMappingIterator(@base, action);
        }

        public virtual bool IsTiming()
        {
            return enabledProperties[FeatureCode.TIMING];
        }

        public virtual void SetTiming(bool timing)
        {
            enabledProperties[FeatureCode.TIMING] = timing;
        }

        public virtual bool IsVersionWarning()
        {
            return false;
        }

        public virtual void SetVersionWarning(bool warn)
        {
        }

        public virtual bool IsValidation()
        {
            return defaultParseOptions.DTDValidationMode == Validation.STRICT || defaultParseOptions.DTDValidationMode == Validation.LAX;
        }

        public virtual void SetValidation(bool validation)
        {
            defaultParseOptions = defaultParseOptions.WithDTDValidationMode(validation ? Validation.STRICT : Validation.STRIP);
        }

        public virtual IFilterFactory MakeDocumentProjector(PathMap.PathMapRoot map)
        {
            throw new NotSupportedException("Document projection requires Saxon-EE");
        }

        public virtual IFilterFactory MakeDocumentProjector(XQueryExpression exp)
        {
            throw new NotSupportedException("Document projection requires Saxon-EE");
        }

        public virtual void SetValidationWarnings(bool warn)
        {
            defaultParseOptions = defaultParseOptions.WithContinueAfterValidationErrors(warn);
        }

        public virtual bool IsValidationWarnings()
        {
            return defaultParseOptions.IsContinueAfterValidationErrors();
        }

        public virtual void SetExpandAttributeDefaults(bool expand)
        {
            defaultParseOptions = defaultParseOptions.WithExpandAttributeDefaults(expand);
        }

        public virtual bool IsExpandAttributeDefaults()
        {
            return defaultParseOptions.IsExpandAttributeDefaults();
        }

        public virtual NamePool GetNamePool()
        {
            return namePool;
        }

        /// <summary>
        /// Replace the name pool. Refused once the current pool has allocated a user-defined
        /// name: fingerprints are baked into compiled patterns (NameTest) and into each Mode's
        /// rule-chain index, so after a swap the same names allocate DIFFERENT fingerprints and
        /// every compiled rule stops matching - silently, with no error (round A2). Internal,
        /// but the NAME_POOL feature reaches it from public code, which is why the guard is here
        /// rather than on the API surface.
        /// </summary>
        internal virtual void SetNamePool(NamePool targetNamePool)
        {
            if (namePool != null && namePool != targetNamePool && namePool.UserDefinedNameCount > 0)
            {
                throw new InvalidOperationException(
                    "Cannot replace the NamePool of a Configuration that has already allocated "
                    + namePool.UserDefinedNameCount + " user-defined name(s): those fingerprints are baked into"
                    + " every stylesheet and document compiled or built against it, and a swap would make"
                    + " compiled template rules stop matching without any error. Use a new Processor instead.");
            }

            namePool = targetNamePool;
        }

        public virtual TypeHierarchy GetTypeHierarchy()
        {
            if (typeHierarchy == null)
            {
                typeHierarchy = new TypeHierarchy(this);
            }

            return typeHierarchy;
        }

        public virtual TypeChecker GetTypeChecker(bool backwardsCompatible)
        {
            if (backwardsCompatible)
            {
                return typeChecker10;
            }
            else
            {
                return typeChecker;
            }
        }

        public virtual TypeAliasManager MakeTypeAliasManager()
        {
            return new TypeAliasManager();
        }

        public virtual bool IsCompatible(Configuration other)
        {
            return namePool == other.namePool && documentNumberAllocator == other.documentNumberAllocator;
        }

        public virtual bool IsStripsAllWhiteSpace()
        {
            return defaultParseOptions.SpaceStrippingRule == AllElementsSpaceStrippingRule.GetInstance();
        }

        // GetSourceParser/ReuseSourceParser/GetStyleParser/ReuseStyleParser + MakeParser/LoadParser retired
        // (R4.1b): document/stylesheet parsing pumps directly through XmlReaderToReceiver (ActiveStreamSource);
        // the SAX XMLReader they fabricated was never read by the delivery path.






        public virtual void LoadSchema(string absoluteURI)
        {
            ReadSchema(MakePipelineConfiguration(), "", absoluteURI, null);
        }

        public virtual NamespaceUri ReadSchema(PipelineConfiguration pipe, string baseURI, string schemaLocation, NamespaceUri expected)
        {
            NeedEnterpriseEdition();
            return null;
        }

        public virtual void ReadMultipleSchemas(PipelineConfiguration pipe, string baseURI, IList<string> schemaLocations, NamespaceUri expected)
        {
            NeedEnterpriseEdition();
        }

        public virtual NamespaceUri ReadInlineSchema(NodeInfo root, NamespaceUri expected, IErrorReporter errorReporter)
        {
            NeedEnterpriseEdition();
            return null;
        }

        protected virtual void NeedEnterpriseEdition()
        {
            throw new NotSupportedException("You need the Enterprise Edition of Saxon (with an EnterpriseConfiguration) for this operation");
        }

        public virtual void AddSchemaSource(ResolvedResource schemaSource)
        {
            AddSchemaSource(schemaSource, MakeErrorReporter());
        }

        public virtual void AddSchemaSource(ResolvedResource schemaSource, IErrorReporter errorReporter)
        {
            NeedEnterpriseEdition();
        }

        public virtual void AddSchemaForBuiltInNamespace(NamespaceUri @namespace)
        {
        }

        public virtual bool IsSchemaAvailable(NamespaceUri targetNamespace)
        {
            return false;
        }

        public virtual void ClearSchemaCache()
        {
        }

        public virtual void SealNamespace(NamespaceUri @namespace)
        {
        }

        public virtual IEnumerable<ISchemaType> GetExtensionsOfType(ISchemaType type)
        {
            return new List<ISchemaType>();
        }

        public virtual void ImportComponents(ResolvedResource source)
        {
            NeedEnterpriseEdition();
        }

        public virtual void ExportComponents(IReceiver @out)
        {
            NeedEnterpriseEdition();
        }

        public virtual ISchemaDeclaration GetElementDeclaration(int fingerprint)
        {
            return null;
        }

        public virtual ISchemaDeclaration GetElementDeclaration(StructuredQName qName)
        {
            return null;
        }

        public virtual ISchemaDeclaration GetAttributeDeclaration(int fingerprint)
        {
            return null;
        }

        public virtual ISchemaDeclaration GetAttributeDeclaration(StructuredQName attributeName)
        {
            return null;
        }

        public virtual ISchemaType GetSchemaType(StructuredQName name)
        {
            if (name.HasURI(NamespaceUri.SCHEMA))
            {
                return (ISchemaType)BuiltInType.GetSchemaTypeByLocalName(name.GetLocalPart());
            }

            return null;
        }

        public virtual Types.ItemType MakeUserUnionType(IList<IAtomicType> memberTypes)
        {
            return null;
        }

        public virtual bool IsDeclaredNotation(NamespaceUri uri, string local)
        {
            return false;
        }

        public virtual void CheckTypeDerivationIsOK(ISchemaType derived, ISchemaType @base, int block)
        {
        }

        public virtual void PrepareValidationReporting(IXPathContext context, ParseOptions options)
        {
        }

        public virtual IReceiver GetDocumentValidator(IReceiver receiver, string systemId, ParseOptions validationOptions, ILocation initiatingLocation)
        {

            // non-schema-aware version
            return receiver;
        }

        public virtual IReceiver GetElementValidator(IReceiver receiver, ParseOptions validationOptions, ILocation locationId)
        {
            return receiver;
        }

        public virtual ISimpleType ValidateAttribute(StructuredQName nodeName, UnicodeString value, int validation)
        {
            return BuiltInAtomicType.UNTYPED_ATOMIC;
        }

        public virtual IReceiver GetAnnotationStripper(IReceiver destination)
        {
            return destination;
        }


        public virtual XPathParser NewExpressionParser(string language, bool updating, IStaticContext env)
        {
            if (language == "XQ")
            {
                if (updating)
                {
                    throw new XPathException("XQuery Update is supported only in Saxon-EE");
                }
                else
                {
                    return new XQueryParser(env);
                }
            }
            else if (language == "XP")
            {
                return new XPathParser(env);
            }
            else if (language == "PATTERN")
            {
                return new PatternParser(env);
            }
            else
            {
                throw new XPathException("Unknown expression language " + language);
            }
        }

        public virtual ExpressionPresenter NewExpressionExporter(string target, System.IO.Stream destination, StylesheetPackage rootPackage)
        {
            throw new XPathException("Exporting a stylesheet requires Saxon-EE");
        }

        public virtual SlotManager MakeSlotManager()
        {
            if (debugger == null)
            {
                return new SlotManager();
            }
            else
            {
                return debugger.MakeSlotManager();
            }
        }

        public virtual IReceiver MakeStreamingTransformer(Mode mode, ParameterSet ordinaryParams, ParameterSet tunnelParams, Outputter output, IXPathContext context)
        {
            throw new XPathException("Streaming is only available in Saxon-EE");
        }

        public virtual Expression MakeStreamInstruction(Expression hrefExp, Expression body, bool streaming, ParseOptions options, PackageData packageData, ILocation location, RetainedStaticContext rsc)
        {
            SourceDocument si = new SourceDocument(hrefExp, body, options);
            si.SetLocation(location);
            si.SetRetainedStaticContext(rsc);
            return si;
        }

        public virtual Func<ISequenceIterator, FocusTrackingIterator> GetFocusTrackerFactory(Executable exec, bool multithreaded)
        {
            return (iter => new FocusTrackingIterator(iter));
        }

        public virtual void CheckStrictStreamability(XSLTemplate template, Expression body)
        {
        }

        public virtual bool IsStreamedNode(NodeInfo node)
        {
            return false; // streaming needs Saxon-EE
            // TODO: make this a property of a node (or of a ITreeInfo)
        }

        public virtual OptimizerOptions GetOptimizerOptions()
        {
            return optimizerOptions.Intersect(OptimizerOptions.FULL_HE_OPTIMIZATION);
        }

        public virtual Optimizer ObtainOptimizer()
        {
            if (optimizer == null)
            {
                optimizer = new Optimizer(this);
                optimizer.SetOptimizerOptions(optimizerOptions.Intersect(OptimizerOptions.FULL_HE_OPTIMIZATION));
                return optimizer;
            }
            else
            {
                return optimizer;
            }
        }

        public virtual Optimizer ObtainOptimizer(OptimizerOptions options)
        {
            Optimizer optimizer = new Optimizer(this);
            optimizer.SetOptimizerOptions(options.Intersect(OptimizerOptions.FULL_HE_OPTIMIZATION));
            return optimizer;
        }

        public virtual ContextItemStaticInfo MakeContextItemStaticInfo(Types.ItemType itemType, bool maybeUndefined)
        {
            return new ContextItemStaticInfo(itemType, maybeUndefined);
        }

        public virtual XQueryExpression MakeXQueryExpression(Expression exp, QueryModule mainModule, bool streaming)
        {
            XQueryExpression xqe = new XQueryExpression(exp, mainModule, false);
            if (mainModule.CodeInjector != null)
            {
                mainModule.CodeInjector.Process(xqe);
            }

            return xqe;
        }

        public virtual IGroundedValue MakeSequenceExtent(Expression expression, int @ref, IXPathContext context)
        {
            try
            {
                return SequenceTool.ToGroundedValue(expression.Iterate(context));
            }
            catch (UncheckedXPathException e)
            {
                throw e.GetXPathException();
            }
        }

        public virtual StyleNodeFactory MakeStyleNodeFactory(Compilation compilation)
        {
            return new StyleNodeFactory(this, compilation);
        }

        public virtual Expression MakeEvaluateInstruction(XSLEvaluate source, ComponentDeclaration decl)
        {
            Expression xpath = source.TargetExpression;
            Values.SequenceType requiredType = source.GetRequiredType();
            Expression contextItem = source.GetContextItemExpression();
            Expression baseUri = source.BaseUriExpression;
            Expression namespaceContext = source.NamespaceContextExpression;
            Expression schemaAware = source.SchemaAwareExpression;
            Expression withParams = source.WithParamsExpression;
            EvaluateInstr inst = new EvaluateInstr(xpath, requiredType, contextItem, baseUri, namespaceContext, schemaAware);
            WithParam[] @params = source.GetWithParamInstructions(inst, source.GetCompilation(), decl, false);
            inst.ActualParams = @params;
            inst.DynamicParams = withParams;
            inst.SetDefaultXPathNamespace(source.DefaultXPathNamespace);
            inst.SetOptionsExpression(source.OptionsExpression);
            return inst;
        }

        public virtual StylesheetPackage MakeStylesheetPackage()
        {
            return new StylesheetPackage(this);
        }

        public virtual AccumulatorRegistry MakeAccumulatorRegistry()
        {
            return new AccumulatorRegistry();
        }

        public virtual void RegisterExternalObjectModel(IExternalObjectModel model)
        {

            //        code removed by bug 5725
            //        try {
            //        } catch (XPathException e) {
            //            // If the model can't be loaded, do nothing
            if (externalObjectModels == null)
            {
                externalObjectModels = new List<IExternalObjectModel>(4);
            }

            if (!externalObjectModels.Contains(model))
            {
                externalObjectModels.Add(model);
            }
        }

        public virtual void DeregisterExternalObjectModel(IExternalObjectModel model)
        {

            // copy the list in case of concurrency issues
            IList<IExternalObjectModel> newList = new List<IExternalObjectModel>(externalObjectModels.Count);
            foreach (IExternalObjectModel existing in externalObjectModels)
            {
                if (existing != model)
                {
                    newList.Add(existing);
                }
            }

            externalObjectModels = newList;
        }

        public virtual void ClearExternalObjectModels()
        {
            externalObjectModels = new List<IExternalObjectModel>();
        }

        public virtual IExternalObjectModel GetExternalObjectModel(string uri)
        {
            foreach (IExternalObjectModel model in externalObjectModels)
            {
                if (model.IdentifyingURI.Equals(uri))
                {
                    return model;
                }
            }

            return null;
        }

        public virtual IExternalObjectModel GetExternalObjectModel(System.Type nodeClass)
        {
            foreach (IExternalObjectModel model in externalObjectModels)
            {
                PJConverter converter = model.GetPJConverter(nodeClass);
                if (converter != null)
                {
                    return model;
                }
            }

            return null;
        }

        public virtual Dictionary<string, IFunctionItem> MakeMethodMap(System.Type externalClass, string required)
        {
            throw new NotSupportedException();
        }

        public virtual MapItem ExternalObjectAsMap(ObjectValue<object> value, string required)
        {
            throw new NotSupportedException();
        }

        public virtual Expression MakeObjectLookupExpression(Expression lhs, Expression rhs)
        {
            throw new NotSupportedException();
        }


        public virtual bool IsExtensionElementAvailable(StructuredQName qName)
        {
            return false;
        }

        public virtual void SetStaticQueryContextFactory(StaticQueryContextFactory factory)
        {
            staticQueryContextFactory = factory;
        }

        public virtual StaticQueryContext NewStaticQueryContext()
        {
            return MakeStaticQueryContext(true);
        }

        public virtual IPendingUpdateList NewPendingUpdateList()
        {
            throw new NotSupportedException("XQuery update is supported only in Saxon-EE");
        }

        public virtual PipelineConfiguration MakePipelineConfiguration()
        {
            PipelineConfiguration pipe = new PipelineConfiguration(this, defaultParseOptions);
            pipe.SetErrorReporter(MakeErrorReporter());
            return pipe;
        }

        public virtual ISchemaURIResolver MakeSchemaURIResolver(IResourceResolver resolver)
        {
            return null;
        }

        public static Configuration GetConfiguration(IXPathContext context)
        {
            return context.GetConfiguration();
        }




        public virtual ITreeInfo BuildDocumentTree(ResolvedResource resource)
        {
            return BuildDocumentTree(resource, defaultParseOptions);
        }

        // P5: build a tree from a native resolved resource (Stream/TextReader/NodeInfo carrier). Its filters
        // are folded into the parse options, then it is delivered as an IActiveSource.
        public virtual ITreeInfo BuildDocumentTree(ResolvedResource resource, ParseOptions parseOptions)
        {
            if (resource == null)
            {
                throw new NullReferenceException("resource");
            }

            ParseOptions options = parseOptions ?? defaultParseOptions;
            if (resource.Filters != null)
            {
                foreach (IFilterFactory f in resource.Filters)
                {
                    options = options.WithFilter(f);
                }
            }

            return BuildDocumentTree(resource.ToActiveSource(), options);
        }

        // P5: build a tree from an already-active source (a node source, ActiveStreamSource, EventSource, …).
        public virtual ITreeInfo BuildDocumentTree(IActiveSource src2, ParseOptions parseOptions)
        {
            if (src2 == null)
            {
                throw new NullReferenceException("source");
            }

            ParseOptions options = (parseOptions ?? defaultParseOptions).ApplyDefaults(this);

            // Create an appropriate Builder
            TreeModel treeModel = options.Model;

            // Decide whether line numbering is in use
            bool lineNumbering = options.IsLineNumbering();
            PipelineConfiguration pipe = MakePipelineConfiguration();
            pipe.SetParseOptions(options);
            Builder builder = treeModel.MakeBuilder(pipe);
            builder.SetTiming(IsTiming());
            builder.SetLineNumbering(lineNumbering);
            builder.SetPipelineConfiguration(pipe);
            builder.SetSystemId(src2.GetSystemId());
            Sender.Send(src2, builder, options);

            // Get the constructed document
            NodeInfo newdoc = builder.CurrentRoot;
            if (newdoc.GetNodeKind() != Types.Type.DOCUMENT)
            {
                throw new XPathException("Source object represents a node other than a document node");
            }


            // Reset the builder, detaching it from the constructed document
            builder.Reset();

            // Return the constructed document
            return newdoc.GetTreeInfo();
        }

        // Source-free document build (P5): parse a System.Xml.XmlReader straight into a tree via the direct
        // XmlReaderToReceiver path, with the same builder / pipeline / space-stripping setup as
        // BuildDocumentTree(Source). Used by the .NET-native DocumentBuilder input so the common build path
        // never constructs a JAXP Source.
        public virtual ITreeInfo BuildDocumentTree(System.Xml.XmlReader reader, string systemId, ParseOptions parseOptions)
        {
            ParseOptions options = (parseOptions ?? defaultParseOptions).ApplyDefaults(this);
            TreeModel treeModel = options.Model;
            bool lineNumbering = options.IsLineNumbering();
            PipelineConfiguration pipe = MakePipelineConfiguration();
            pipe.SetParseOptions(options);
            Builder builder = treeModel.MakeBuilder(pipe);
            builder.SetTiming(IsTiming());
            builder.SetLineNumbering(lineNumbering);
            builder.SetPipelineConfiguration(pipe);
            builder.SetSystemId(systemId);
            Sender.Send(reader, systemId, builder, options);
            NodeInfo newdoc = builder.CurrentRoot;
            if (newdoc.GetNodeKind() != Types.Type.DOCUMENT)
            {
                throw new XPathException("Source object represents a node other than a document node");
            }

            builder.Reset();
            return newdoc.GetTreeInfo();
        }

        public virtual TreeStatistics GetTreeStatistics()
        {
            return treeStatistics;
        }

        public virtual IReceiver MakeEmitter(string eqName, Properties props)
        {
            StructuredQName sqName = StructuredQName.FromEQName(eqName);
            string className = sqName.GetLocalPart();
            object handler;
            try
            {
                handler = dynamicLoader.GetInstance(className, null);
            }
            catch (XPathException e)
            {
                throw new XPathException("Cannot create user-supplied output method. " + e.Message, DAXonErrorCode.SXCH0004);
            }

            if (handler is IReceiver)
            {
                return (IReceiver)handler;
            }
            else
            {
                throw new XPathException("Output method " + className + " is not a IReceiver");
            }
        }

        public virtual void SetConfigurationProperty(string name, object value)
        {
            if (FeatureIndex.Exists(name))
            {
                SetFeature(FeatureIndex.GetData(name), value);
            }
            else if (name.StartsWith(FeatureKeys.XML_PARSER_FEATURE, StringComparison.Ordinal))
            {
                string uri = name.Substring(FeatureKeys.XML_PARSER_FEATURE.Length);
                uri = Uri.UnescapeDataString(uri);
                defaultParseOptions = defaultParseOptions.WithParserFeature(uri, RequireBoolean(name, value));
            }
            else if (name.StartsWith(FeatureKeys.XML_PARSER_PROPERTY, StringComparison.Ordinal))
            {
                string uri = name.Substring(FeatureKeys.XML_PARSER_PROPERTY.Length);
                uri = Uri.UnescapeDataString(uri);
                defaultParseOptions = defaultParseOptions.WithParserProperty(uri, value);
            }
            else
            {
                throw new ArgumentException("Unrecognized configuration feature: " + name);
            }
        }

        public virtual void SetConfigurationProperty<T>(Feature<T> feature, T value)
        {
            SetFeature(FeatureIndex.GetData(feature.code), value);
        }

        protected virtual void SetFeature(FeatureData feature, object value)
        {
            string name = feature.uri;
            int code = feature.code;
            if (booleanFeatures.Contains(code))
            {
                if (code == FeatureCode.COMPILE_WITH_TRACING)
                {
                    bool b = RequireBoolean(name, value);
                    SetCompileWithTracing(b);
                }
                else if (code == FeatureCode.DTD_VALIDATION)
                {
                    bool b = RequireBoolean(name, value);
                    SetValidation(b);
                }
                else if (code == FeatureCode.EXPAND_ATTRIBUTE_DEFAULTS)
                {
                    bool b = RequireBoolean(name, value);
                    SetExpandAttributeDefaults(b);
                }
                else if (code == FeatureCode.ALLOW_SYNTAX_EXTENSIONS)
                {
                    bool b = RequireBoolean(name, value);
                    defaultXsltCompilerInfo.XsltVersion = b ? 40 : 30;
                    DefaultStaticQueryContext.LanguageVersion = b ? 40 : 30;
                }

                InternalSetBooleanProperty(code, name, value);
            }
            else if (stringFeatures.Contains(code))
            {
                stringProperties.Put(code, RequireString(name, value));
            }
            else
            {
                switch (code)
                {
                    case FeatureCode.ALLOWED_PROTOCOLS:
                        protocolRestrictor = new ProtocolRestrictor((string)value);
                        IResourceResolver existing = GetResourceResolver();
                        SetResourceResolver(protocolRestrictor.AsResourceResolver(existing));
                        break;
                    case FeatureCode.COLLATION_URI_RESOLVER:
                        if (!(value is ICollationURIResolver))
                        {
                            throw new ArgumentException("COLLATION_URI_RESOLVER value must be an instance of OutSmart.DAXon.Lib.ICollationURIResolver");
                        }

                        CollationURIResolver = (ICollationURIResolver)value;
                        break;
                    case FeatureCode.COLLATION_URI_RESOLVER_CLASS:
                        CollationURIResolver = (ICollationURIResolver)InstantiateClassName(name, value, typeof(ICollationURIResolver));
                        break;
                    case FeatureCode.COLLECTION_FINDER:
                        if (!(value is ICollectionFinder))
                        {
                            throw new ArgumentException("COLLECTION_FINDER value must be an instance of OutSmart.DAXon.Lib.ICollectionFinder");
                        }

                        CollectionFinder = (ICollectionFinder)value;
                        break;
                    case FeatureCode.COLLECTION_FINDER_CLASS:
                        CollectionFinder = (ICollectionFinder)InstantiateClassName(name, value, typeof(ICollectionFinder));
                        break;
                    case FeatureCode.DEFAULT_COLLATION:
                        defaultCollationName = value.ToString();
                        break;
                    case FeatureCode.DEFAULT_COLLECTION:
                        DefaultCollection = value.ToString();
                        break;
                    case FeatureCode.DEFAULT_COUNTRY:
                        DefaultCountry = value.ToString();
                        break;
                    case FeatureCode.DEFAULT_LANGUAGE:
                        SetDefaultLanguage(value.ToString());
                        break;
                    case FeatureCode.DEFAULT_REGEX_ENGINE:
                        DefaultRegexEngine = value.ToString();
                        break;
                    case FeatureCode.DTD_VALIDATION_RECOVERABLE:
                        {
                            bool b = RequireBoolean(name, value);
                            if (b)
                            {
                                defaultParseOptions = defaultParseOptions.WithDTDValidationMode(Validation.LAX);
                            }
                            else
                            {
                                defaultParseOptions = defaultParseOptions.WithDTDValidationMode(IsValidation() ? Validation.STRICT : Validation.SKIP);
                            }

                            InternalSetBooleanProperty(code, name, b);
                            break;
                        }

                    case FeatureCode.ENTITY_RESOLVER_CLASS:
                        // SAX-free engine: keep only the class name; its presence flips on external-entity resolution.
                        if ("".Equals(value))
                        {
                            defaultParseOptions = defaultParseOptions.WithEntityResolverClass(null);
                        }
                        else
                        {
                            defaultParseOptions = defaultParseOptions.WithEntityResolverClass((string)value);
                        }

                        break;
                    case FeatureCode.ENVIRONMENT_VARIABLE_RESOLVER:
                        if (!(value is IEnvironmentVariableResolver))
                        {
                            throw new ArgumentException("ENVIRONMENT_VARIABLE_RESOLVER value must be an instance of OutSmart.DAXon.Lib.IEnvironmentVariableResolver");
                        }

                        environmentVariableResolver = (IEnvironmentVariableResolver)value;
                        break;
                    case FeatureCode.ENVIRONMENT_VARIABLE_RESOLVER_CLASS:
                        environmentVariableResolver = (IEnvironmentVariableResolver)InstantiateClassName(name, value, typeof(IEnvironmentVariableResolver));
                        break;
                    case FeatureCode.ERROR_LISTENER_CLASS:

                        // No action, obsolete
                        break;
                    case FeatureCode.LINE_NUMBERING:
                        {
                            bool b = RequireBoolean(name, value);
                            SetLineNumbering(b);
                            break;
                        }

                    case FeatureCode.MESSAGE_EMITTER_CLASS:
                        {

                            // obsolete: ignored
                            break;
                        }

                    case FeatureCode.MODULE_URI_RESOLVER:
                        if (!(value is IModuleURIResolver))
                        {
                            throw new ArgumentException("MODULE_URI_RESOLVER value must be an instance of OutSmart.DAXon.Lib.IModuleURIResolver");
                        }

                        SetModuleURIResolver((IModuleURIResolver)value);
                        break;
                    case FeatureCode.MODULE_URI_RESOLVER_CLASS:
                        IModuleURIResolver resolver = (IModuleURIResolver)InstantiateClassName(name, value, typeof(IModuleURIResolver));
                        if (resolver is StandardModuleURIResolver)
                        {
                            ((StandardModuleURIResolver)resolver).SetConfiguration(this);
                        }

                        SetModuleURIResolver(resolver);
                        break;
                    case FeatureCode.NAME_POOL:
                        if (!(value is NamePool))
                        {
                            throw new ArgumentException("NAME_POOL value must be an instance of OutSmart.DAXon.Model.NamePool");
                        }

                        SetNamePool((NamePool)value);
                        break;
                    case FeatureCode.OPTIMIZATION_LEVEL:
                        if (value is int)
                        {

                            // See Saxon bug 2076. It seems Ant passes an integer value as an integer, not as a string. Not tested.
                            // Integer values retained for compatibility: 0=none, 10 = all
                            int v = (int)value;
                            optimizerOptions = v == 0 ? new OptimizerOptions(0) : OptimizerOptions.FULL_EE_OPTIMIZATION.Intersect(PermittedOptimizerOptions);
                        }
                        else
                        {
                            string s = RequireString(name, value);
                            if (s.MatchesRegex("[0-9]+"))
                            {

                                // For backwards compatibility
                                optimizerOptions = s == "0" ? new OptimizerOptions(0) : OptimizerOptions.FULL_EE_OPTIMIZATION.Intersect(PermittedOptimizerOptions);
                            }
                            else
                            {
                                optimizerOptions = new OptimizerOptions(s).Intersect(PermittedOptimizerOptions);
                            }
                        }

                        if (optimizer != null)
                        {
                            optimizer.SetOptimizerOptions(optimizerOptions);
                        }

                        defaultXsltCompilerInfo.SetOptimizerOptions(optimizerOptions);
                        break;
                    case FeatureCode.OUTPUT_URI_RESOLVER:
                        if (!(value is IOutputURIResolver))
                        {
                            throw new ArgumentException("OUTPUT_URI_RESOLVER value must be an instance of OutSmart.DAXon.Lib.IOutputURIResolver");
                        }

                        SetOutputURIResolver((IOutputURIResolver)value);
                        break;
                    case FeatureCode.OUTPUT_URI_RESOLVER_CLASS:
                        SetOutputURIResolver((IOutputURIResolver)InstantiateClassName(name, value, typeof(IOutputURIResolver)));
                        break;
                    case FeatureCode.RECOVERY_POLICY:

                        // Obsolete: no action
                        break;
                    case FeatureCode.RECOVERY_POLICY_NAME:

                        // Obsolete: no action
                        break;
                    case FeatureCode.REGEX_BACKTRACKING_LIMIT:
                        regexBacktrackingLimit = RequireInteger(name, value);
                        break;
                    case FeatureCode.SERIALIZER_FACTORY_CLASS:
                        SerializerFactory = (SerializerFactory)InstantiateClassName(name, value, typeof(SerializerFactory));
                        break;
                    case FeatureCode.SCHEMA_VALIDATION:
                        {
                            SchemaValidationMode = RequireInteger(name, value);
                            break;
                        }

                    case FeatureCode.SCHEMA_VALIDATION_MODE:
                        string mode = RequireString(name, value);
                        SchemaValidationMode = Validation.GetCode(mode);
                        break;
                    case FeatureCode.SOURCE_PARSER_CLASS:
                        SourceParserClass = RequireString(name, value);
                        break;
                    case FeatureCode.STANDARD_ERROR_OUTPUT_FILE:

                        // Note, this property is write-only
                        try
                        {
                            bool append = true;
                            bool autoFlush = true;
                            SetStandardErrorOutput(new StreamWriter((string)value, append) { AutoFlush = autoFlush });
                        }
                        catch (FileNotFoundException fnf)
                        {
                            throw new ArgumentException(fnf?.Message, fnf);
                        }

                        break;
                    case FeatureCode.STRIP_WHITESPACE:
                        {
                            string s = RequireString(name, value);
                            ISpaceStrippingRule rule;
                            switch (s)
                            {
                                case "all":
                                    rule = AllElementsSpaceStrippingRule.GetInstance();
                                    break;
                                case "none":
                                    rule = NoElementsSpaceStrippingRule.GetInstance();
                                    break;
                                case "ignorable":
                                    rule = IgnorableSpaceStrippingRule.GetInstance();
                                    break;
                                default:
                                    throw new ArgumentException("Unrecognized value STRIP_WHITESPACE = '" + value + "': must be 'all', 'none', or 'ignorable'");
                            }

                            defaultParseOptions = defaultParseOptions.WithSpaceStrippingRule(rule);
                            break;
                        }

                    case FeatureCode.STYLE_PARSER_CLASS:
                        StyleParserClass = RequireString(name, value);
                        break;
                    case FeatureCode.TIMING:
                        SetTiming(RequireBoolean(name, value));
                        break;
                    case FeatureCode.TRACE_LISTENER:
                        if (!(value is ITraceListener))
                        {
                            throw new ArgumentException("TRACE_LISTENER is of wrong class");
                        }

                        SetTraceListener((ITraceListener)value);
                        break;
                    case FeatureCode.TRACE_LISTENER_CLASS:
                        TraceListenerClass = RequireString(name, value);
                        break;
                    case FeatureCode.TRACE_LISTENER_OUTPUT_FILE:
                        TraceListenerOutputFile = RequireString(name, value);
                        break;
                    case FeatureCode.TREE_MODEL:
                        SetTreeModel(RequireInteger(name, value));
                        break;
                    case FeatureCode.TREE_MODEL_NAME:
                        {
                            string s = RequireString(name, value);
                            switch (s)
                            {
                                case "tinyTree":
                                    SetTreeModel(Builder.TINY_TREE);
                                    break;
                                case "tinyTreeCondensed":
                                    SetTreeModel(Builder.TINY_TREE_CONDENSED);
                                    break;
                                case "linkedTree":
                                    SetTreeModel(Builder.LINKED_TREE);
                                    break;
                                case "jdom":
                                    SetTreeModel(Builder.JDOM_TREE);
                                    break;
                                case "jdom2":
                                    SetTreeModel(Builder.JDOM2_TREE);
                                    break;
                                default:
                                    throw new ArgumentException("Unrecognized value TREE_MODEL_NAME = '" + value + "': must be linkedTree|tinyTree|tinyTreeCondensed");
                            }

                            break;
                        }

                    case FeatureCode.UNPARSED_TEXT_URI_RESOLVER:
                        UnparsedTextURIResolver = (IUnparsedTextURIResolver)value;
                        break;
                    case FeatureCode.UNPARSED_TEXT_URI_RESOLVER_CLASS:
                        UnparsedTextURIResolver = (IUnparsedTextURIResolver)InstantiateClassName(name, value, typeof(IUnparsedTextURIResolver));
                        break;
                    case FeatureCode.URI_RESOLVER_CLASS:
                        throw new ArgumentException(name + ": the JAXP URIResolver interface is not supported by this port; use SetResourceResolver(IResourceResolver)");
                    case FeatureCode.USE_XSI_SCHEMA_LOCATION:
                        defaultParseOptions = defaultParseOptions.WithUseXsiSchemaLocation(RequireBoolean(name, value));
                        break;
                    case FeatureCode.VALIDATION_COMMENTS:
                        defaultParseOptions = defaultParseOptions.WithAddCommentsAfterValidationErrors(RequireBoolean(name, value));
                        break;
                    case FeatureCode.VALIDATION_WARNINGS:
                        SetValidationWarnings(RequireBoolean(name, value));
                        break;
                    case FeatureCode.VERSION_WARNING:

                        // no action
                        break;
                    case FeatureCode.XINCLUDE:
                        SetXIncludeAware(RequireBoolean(name, value));
                        break;
                    case FeatureCode.XPATH_VERSION_FOR_XSD:
                        {
                            int val = RequireInteger(name, value);
                            if (val != 20 && val != 30 && val != 31)
                            {
                                throw new ArgumentException("XPath version for XSD must be 20 (XPath 2.0), 30 (XPath 3.0), or 31 (XPath 3.1)");
                            }

                            xpathVersionForXsd = val;
                            break;
                        }

                    case FeatureCode.XPATH_VERSION_FOR_XSLT:
                        {
                            int val = RequireInteger(name, value);
                            if (val != 20 && val != 30 && val != 305 && val != 31 && val != 40)
                            {
                                throw new ArgumentException("XPath version for XSLT must be 20 (XPath 2.0), 30 (XPath 3.0), 31 (XPath 3.1), or 305 (XPath 3.0 with XSLT-defined extensions), or 40 (XPath 4.0 proposal)");
                            }

                            xpathVersionForXslt = val;
                            break;
                        }

                    case FeatureCode.XQUERY_ALLOW_UPDATE:
                        DefaultStaticQueryContext.SetUpdatingEnabled(RequireBoolean(name, value));
                        break;
                    case FeatureCode.XQUERY_CONSTRUCTION_MODE:
                        DefaultStaticQueryContext.ConstructionMode = Validation.GetCode(value.ToString());
                        break;
                    case FeatureCode.XQUERY_DEFAULT_ELEMENT_NAMESPACE:
                        DefaultStaticQueryContext.DefaultElementNamespace = NamespaceUri.Of(value.ToString());
                        break;
                    case FeatureCode.XQUERY_DEFAULT_FUNCTION_NAMESPACE:
                        DefaultStaticQueryContext.DefaultFunctionNamespace = NamespaceUri.Of(value.ToString());
                        break;
                    case FeatureCode.XQUERY_EMPTY_LEAST:
                        DefaultStaticQueryContext.SetEmptyLeast(RequireBoolean(name, value));
                        break;
                    case FeatureCode.XQUERY_INHERIT_NAMESPACES:
                        DefaultStaticQueryContext.SetInheritNamespaces(RequireBoolean(name, value));
                        break;
                    case FeatureCode.XQUERY_PRESERVE_BOUNDARY_SPACE:
                        DefaultStaticQueryContext.SetPreserveBoundarySpace(RequireBoolean(name, value));
                        break;
                    case FeatureCode.XQUERY_PRESERVE_NAMESPACES:
                        DefaultStaticQueryContext.SetPreserveNamespaces(RequireBoolean(name, value));
                        break;
                    case FeatureCode.XQUERY_REQUIRED_CONTEXT_ITEM_TYPE:
                        IndependentContext env = new IndependentContext(this);
                        XPathParser parser = new XPathParser(env);
                        env.SetXPathLanguageLevel(31);
                        try
                        {
                            Values.SequenceType type = parser.ParseSequenceType(value.ToString(), env);
                            if (type.GetCardinality() != StaticProperty.EXACTLY_ONE)
                            {
                                throw new ArgumentException("Context item type must have no occurrence indicator");
                            }

                            DefaultStaticQueryContext.RequiredContextItemType = type.PrimaryType;
                        }
                        catch (XPathException err)
                        {
                            throw new ArgumentException(err.Message, err);
                        }

                        break;
                    case FeatureCode.XQUERY_SCHEMA_AWARE:
                        DefaultStaticQueryContext.SetSchemaAware(RequireBoolean(name, value));
                        break;
                    case FeatureCode.XQUERY_STATIC_ERROR_LISTENER_CLASS:
                        throw new ArgumentException(name + ": the JAXP ErrorListener interface is not supported by this port; use SetErrorReporter(IErrorReporter)");
                    case FeatureCode.XQUERY_VERSION:
                        {
                            int qvn;
                            switch (value.ToString())
                            {
                                case "3.1":
                                    qvn = 31;
                                    break;
                                case "4.0":
                                    qvn = 40;
                                    break;
                                default:
                                    MakeErrorReporter().Report(new XmlProcessingIncident("XQuery version ignored: only \"3.1\" and \"4.0\" are recognized", DAXonErrorCode.SXWN9049).AsWarning());
                                    qvn = 40;
                                    break;
                            }

                            DefaultStaticQueryContext.LanguageVersion = qvn;
                            break;
                        }

                    case FeatureCode.XML_VERSION:
                        string xv = RequireString(name, value);
                        if (!(xv.Equals("1.0") || xv.Equals("1.1")))
                        {
                            throw new ArgumentException("XML_VERSION value must be \"1.0\" or \"1.1\" as a String");
                        }

                        XMLVersion = xv.Equals("1.0") ? XML10 : XML11;
                        break;
                    case FeatureCode.XSD_VERSION:
                        {
                            string xsdVn = RequireString(name, value);
                            if (!(xsdVn.Equals("1.0") || xsdVn.Equals("1.1")))
                            {
                                throw new ArgumentException("XSD_VERSION value must be \"1.0\" or \"1.1\" as a String");
                            }

                            xsdVersion = xsdVn.Equals("1.0") ? XSD10 : XSD11;
                            theConversionRules = null;
                            break;
                        }

                    case FeatureCode.XSLT_ENABLE_ASSERTIONS:
                        DefaultXsltCompilerInfo.SetAssertionsEnabled(RequireBoolean(name, value));
                        break;
                    case FeatureCode.XSLT_INITIAL_MODE:
                        {
                            string s = RequireString(name, value);
                            DefaultXsltCompilerInfo.DefaultInitialMode = StructuredQName.FromClarkName(s);
                            break;
                        }

                    case FeatureCode.XSLT_INITIAL_TEMPLATE:
                        {
                            string s = RequireString(name, value);
                            DefaultXsltCompilerInfo.DefaultInitialTemplate = StructuredQName.FromClarkName(s);
                            break;
                        }

                    case FeatureCode.XSLT_SCHEMA_AWARE:
                        DefaultXsltCompilerInfo.SetSchemaAware(RequireBoolean(name, value));
                        break;
                    case FeatureCode.XSLT_STATIC_ERROR_LISTENER_CLASS:
                        throw new ArgumentException(name + ": the JAXP ErrorListener interface is not supported by this port; use SetErrorReporter(IErrorReporter)");
                    case FeatureCode.XSLT_STATIC_URI_RESOLVER_CLASS:
                        throw new ArgumentException(name + ": the JAXP URIResolver interface is not supported by this port; use SetResourceResolver(IResourceResolver)");
                    case FeatureCode.XSLT_VERSION:
                        {
                            int xsltVersion;
                            switch (value.ToString())
                            {
                                case "3.0":
                                    xsltVersion = 30;
                                    break;
                                case "4.0":
                                    xsltVersion = 40;
                                    break;
                                default:
                                    MakeErrorReporter().Report(new XmlProcessingIncident("XSLT version ignored: only \"3.0\" and \"4.0\" are recognized", DAXonErrorCode.SXWN9020).AsWarning());
                                    xsltVersion = 30;
                                    break;
                            }

                            DefaultXsltCompilerInfo.XsltVersion = xsltVersion;
                            break;
                        }

                    case FeatureCode.RESOURCE_RESOLVER:
                        if (!(value is IResourceResolver))
                        {
                            throw new ArgumentException("RESOURCE_RESOLVER value must be an instance of OutSmart.DAXon.Lib.IResourceResolver");
                        }

                        SetResourceResolver((IResourceResolver)value);
                        break;
                    case FeatureCode.RESOURCE_RESOLVER_CLASS:
                        IResourceResolver rresolver = (IResourceResolver)InstantiateClassName(name, value, typeof(IResourceResolver));
                        SetResourceResolver(rresolver);
                        break;
                    default:
                        throw new ArgumentException("Unknown configuration property " + name);
                }
            }
        }

        public static bool RequireBoolean(string propertyName, object value)
        {
            if (value is bool)
            {
                return (bool)value;
            }
            else if (value is string)
            {
                string s = ((string)value).Trim();
                if (s == "true" || s == "on" || s == "yes" || s == "1")
                {
                    return true;
                }
                else if (s == "false" || s == "off" || s == "no" || s == "0")
                {
                    return false;
                }
                else
                {
                    throw new ArgumentException(propertyName + " must be 'true' or 'false' (or on|off, yes|no, 1|0)");
                }
            }
            else
            {
                throw new ArgumentException(propertyName + " must be a boolean (or a string representing a boolean)");
            }
        }

        protected virtual int RequireInteger(string propertyName, object value)
        {
            if (value is int)
            {
                return (int)value;
            }
            else if (value is string)
            {
                try
                {
                    return int.Parse((string)value);
                }
                catch (FormatException nfe)
                {
                    throw new ArgumentException(propertyName + " must be an integer");
                }
            }
            else
            {
                throw new ArgumentException(propertyName + " must be an integer (or a string representing an integer)");
            }
        }

        protected virtual void InternalSetBooleanProperty(int code, string name, object value)
        {
            bool b = RequireBoolean(name, value);
            if ((uint)code >= (uint)enabledProperties.Length)
            {
                throw new ArgumentException("Unknown feature code " + code + " for " + name);
            }

            enabledProperties[code] = b;
        }

        public virtual bool GetBooleanProperty(Feature<bool> feature)
        {
            return enabledProperties[feature.code];
        }

        public virtual void SetBooleanProperty(string propertyName, bool value)
        {
            SetConfigurationProperty(propertyName, value);
        }

        public virtual void SetBooleanProperty(Feature<bool> feature, bool value)
        {
            SetConfigurationProperty(feature, value);
        }

        protected virtual string RequireString(string propertyName, object value)
        {
            if (value is string)
            {
                return (string)value;
            }
            else
            {
                throw new ArgumentException("The value of " + propertyName + " must be a string");
            }
        }

        protected virtual object InstantiateClassName(string propertyName, object value, System.Type requiredClass)
        {
            if (!(value is string))
            {
                throw new ArgumentException(propertyName + " must be a String");
            }

            try
            {
                object obj = GetInstance((string)value);
                if (!requiredClass.IsAssignableFrom(obj.GetType()))
                {
                    throw new ArgumentException("Error in " + propertyName + ": Class " + value + " does not implement " + requiredClass.FullName);
                }

                return obj;
            }
            catch (XPathException err)
            {
                throw new ArgumentException("Cannot use " + value + " as the value of " + propertyName + ". " + err.Message);
            }
        }

        public virtual object GetConfigurationProperty(string name)
        {
            if (FeatureIndex.Exists(name))
            {
                return GetFeature(FeatureIndex.GetData(name));
            }
            else
            {
                throw new ArgumentException("Unknown configuration property " + name);
            }
        }

        public virtual T GetConfigurationProperty<T>(Feature<T> feature)
        {
            FeatureData data = FeatureIndex.GetData(feature.code);
            return (T)GetFeature(data);
        }

        protected virtual object GetFeature(FeatureData feature)
        {
            int code = feature.code;
            if (booleanFeatures.Contains(code))
            {
                return enabledProperties[code];
            }

            if (stringFeatures.Contains(code))
            {
                string value = stringProperties[code];
                if (value == null)
                {
                    return feature.defaultValue;
                }
                else
                {
                    return value;
                }
            }

            switch (code)
            {
                case FeatureCode.ALLOWED_PROTOCOLS:
                    return protocolRestrictor.ToString();
                case FeatureCode.COLLATION_URI_RESOLVER:
                    return CollationURIResolver;
                case FeatureCode.COLLATION_URI_RESOLVER_CLASS:
                    return CollationURIResolver.GetType().FullName;
                case FeatureCode.CONFIGURATION:
                    return this;
                case FeatureCode.DEFAULT_COLLATION:
                    return defaultCollationName;
                case FeatureCode.DEFAULT_COLLECTION:
                    return DefaultCollection;
                case FeatureCode.DEFAULT_COUNTRY:
                    return DefaultCountry;
                case FeatureCode.DEFAULT_LANGUAGE:
                    return GetDefaultLanguage();
                case FeatureCode.DTD_VALIDATION:
                    return IsValidation();
                case FeatureCode.DTD_VALIDATION_RECOVERABLE:
                    return defaultParseOptions.DTDValidationMode == Validation.LAX;
                case FeatureCode.ERROR_LISTENER_CLASS:

                    // Obsolete
                    return null;
                case FeatureCode.ENTITY_RESOLVER_CLASS:
                    return defaultParseOptions.EntityResolverClass ?? "";

                case FeatureCode.ENVIRONMENT_VARIABLE_RESOLVER:
                    return environmentVariableResolver;
                case FeatureCode.ENVIRONMENT_VARIABLE_RESOLVER_CLASS:
                    return environmentVariableResolver.GetType().FullName;
                case FeatureCode.EXPAND_ATTRIBUTE_DEFAULTS:
                    return IsExpandAttributeDefaults();
                case FeatureCode.LINE_NUMBERING:
                    return IsLineNumbering();
                case FeatureCode.MESSAGE_EMITTER_CLASS:
                    return null;
                case FeatureCode.MODULE_URI_RESOLVER:
                    return GetModuleURIResolver();
                case FeatureCode.MODULE_URI_RESOLVER_CLASS:
                    return GetModuleURIResolver().GetType().FullName;
                case FeatureCode.NAME_POOL:
                    return GetNamePool();
                case FeatureCode.OPTIMIZATION_LEVEL:
                    return optimizerOptions.ToString();
                case FeatureCode.OUTPUT_URI_RESOLVER:
                    return GetOutputURIResolver();
                case FeatureCode.OUTPUT_URI_RESOLVER_CLASS:
                    return GetOutputURIResolver().GetType().FullName;
                case FeatureCode.RECOVERY_POLICY:
                    return 0;
                case FeatureCode.RECOVERY_POLICY_NAME:
                    return "recoverWithWarnings";
                case FeatureCode.REGEX_BACKTRACKING_LIMIT:
                    return regexBacktrackingLimit;
                case FeatureCode.SCHEMA_VALIDATION:
                    return SchemaValidationMode;
                case FeatureCode.SCHEMA_VALIDATION_MODE:
                    return Validation.Describe(SchemaValidationMode);
                case FeatureCode.SERIALIZER_FACTORY_CLASS:
                    return SerializerFactory.GetType().FullName;
                case FeatureCode.SOURCE_PARSER_CLASS:
                    return SourceParserClass;
                case FeatureCode.STRIP_WHITESPACE:
                    ISpaceStrippingRule rule = GetParseOptions().SpaceStrippingRule;
                    if (rule == AllElementsSpaceStrippingRule.GetInstance())
                    {
                        return "all";
                    }
                    else if (rule == null || rule == IgnorableSpaceStrippingRule.GetInstance())
                    {
                        return "ignorable";
                    }
                    else
                    {
                        return "none";
                    }

                case FeatureCode.STYLE_PARSER_CLASS:
                    return StyleParserClass;
                case FeatureCode.TIMING:
                    return IsTiming();
                case FeatureCode.TRACE_LISTENER:
                    return traceListener;
                case FeatureCode.TRACE_LISTENER_CLASS:
                    return traceListenerClass;
                case FeatureCode.TRACE_LISTENER_OUTPUT_FILE:
                    return traceListenerOutput;
                case FeatureCode.TREE_MODEL:
                    return GetTreeModel();
                case FeatureCode.TREE_MODEL_NAME:
                    switch (GetTreeModel())
                    {
                        case Builder.TINY_TREE:
                        default:
                            return "tinyTree";
                        case Builder.TINY_TREE_CONDENSED:
                            return "tinyTreeCondensed";
                        case Builder.LINKED_TREE:
                            return "linkedTree";
                    }

                case FeatureCode.UNPARSED_TEXT_URI_RESOLVER:
                    return UnparsedTextURIResolver;
                case FeatureCode.UNPARSED_TEXT_URI_RESOLVER_CLASS:
                    return UnparsedTextURIResolver.GetType().FullName;
                case FeatureCode.URI_RESOLVER_CLASS:
                    return null;

                case FeatureCode.USE_XSI_SCHEMA_LOCATION:
                    return defaultParseOptions.IsUseXsiSchemaLocation();
                case FeatureCode.VALIDATION_COMMENTS:
                    return defaultParseOptions.IsAddCommentsAfterValidationErrors();
                case FeatureCode.VALIDATION_WARNINGS:
                    return IsValidationWarnings();
                case FeatureCode.VERSION_WARNING:
                    return false;
                case FeatureCode.XINCLUDE:
                    return IsXIncludeAware();
                case FeatureCode.XML_VERSION:
                    return XMLVersion == XML10 ? "1.0" : "1.1";
                case FeatureCode.XQUERY_ALLOW_UPDATE:
                    return DefaultStaticQueryContext.IsUpdatingEnabled();
                case FeatureCode.XQUERY_CONSTRUCTION_MODE:
                    return DefaultStaticQueryContext.ConstructionMode;
                case FeatureCode.XQUERY_DEFAULT_ELEMENT_NAMESPACE:
                    return DefaultStaticQueryContext.DefaultElementNamespace;
                case FeatureCode.XQUERY_DEFAULT_FUNCTION_NAMESPACE:
                    return DefaultStaticQueryContext.DefaultFunctionNamespace;
                case FeatureCode.XQUERY_EMPTY_LEAST:
                    return DefaultStaticQueryContext.IsEmptyLeast();
                case FeatureCode.XQUERY_INHERIT_NAMESPACES:
                    return DefaultStaticQueryContext.IsInheritNamespaces();
                case FeatureCode.XQUERY_PRESERVE_BOUNDARY_SPACE:
                    return DefaultStaticQueryContext.IsPreserveBoundarySpace();
                case FeatureCode.XQUERY_PRESERVE_NAMESPACES:
                    return DefaultStaticQueryContext.IsPreserveNamespaces();
                case FeatureCode.XQUERY_REQUIRED_CONTEXT_ITEM_TYPE:
                    return DefaultStaticQueryContext.RequiredContextItemType;
                case FeatureCode.XQUERY_SCHEMA_AWARE:
                    return DefaultStaticQueryContext.IsSchemaAware();
                case FeatureCode.XQUERY_STATIC_ERROR_LISTENER_CLASS:
                    return null;
                case FeatureCode.XQUERY_VERSION:
                    return DefaultStaticQueryContext.LanguageVersion == 40 ? "4.0" : "3.1";
                case FeatureCode.XPATH_VERSION_FOR_XSD:
                    return xpathVersionForXsd;
                case FeatureCode.XPATH_VERSION_FOR_XSLT:
                    return xpathVersionForXslt;
                case FeatureCode.XSD_VERSION:
                    return xsdVersion == XSD10 ? "1.0" : "1.1";
                case FeatureCode.XSLT_ENABLE_ASSERTIONS:
                    return DefaultXsltCompilerInfo.IsAssertionsEnabled();
                case FeatureCode.XSLT_INITIAL_MODE:
                    return DefaultXsltCompilerInfo.DefaultInitialMode.ClarkName;
                case FeatureCode.XSLT_INITIAL_TEMPLATE:
                    return DefaultXsltCompilerInfo.DefaultInitialTemplate.ClarkName;
                case FeatureCode.XSLT_SCHEMA_AWARE:
                    return DefaultXsltCompilerInfo.IsSchemaAware();
                case FeatureCode.XSLT_STATIC_ERROR_LISTENER_CLASS:
                    return null;
                case FeatureCode.XSLT_STATIC_URI_RESOLVER_CLASS:
                    return null; // TODO: drop this
                case FeatureCode.XSLT_VERSION:
                    {
                        int vn = DefaultXsltCompilerInfo.XsltVersion;
                        return vn == 40 ? "4.0" : "3.0";
                    }

                case FeatureCode.RESOURCE_RESOLVER:
                    return GetResourceResolver();
                case FeatureCode.RESOURCE_RESOLVER_CLASS:
                    return GetResourceResolver().GetType().FullName;
            }

            throw new ArgumentException("Unknown configuration property ");
        }

        public virtual bool IsJITEnabled()
        {
            return false;
        }

        public virtual void Dispose()
        {
            if (traceOutput != null)
            {
                traceOutput.Dispose();
            }
        }

        public virtual IIPackageLoader MakePackageLoader()
        {
            return (IIPackageLoader)new PackageLoaderHE(this);
        }

        public virtual InvalidityReportGenerator CreateValidityReporter()
        {
            throw new NotSupportedException("Schema validation requires Saxon-EE");
        }

        public virtual SimpleMode MakeMode(StructuredQName modeName, CompilerInfo compilerInfo)
        {
            return new SimpleMode(modeName);
        }

        public virtual TemplateRule MakeTemplateRule()
        {
            return new TemplateRule();
        }

        public virtual XPathContextMajor.ThreadManager MakeThreadManager()
        {
            return null;
        }

        public virtual CompilerInfo MakeCompilerInfo()
        {
            return new CompilerInfo(this);
        }

        public virtual IICompilerService MakeCompilerService(HostLanguage hostLanguage)
        {
            return null;
        }

        public interface IApiProvider
        {
        }

        private readonly struct RegexCacheKey : IEquatable<RegexCacheKey>
        {
            private readonly string pattern;
            private readonly string flags;
            private readonly string hostLanguage;
            private readonly int backtrackingLimit;
            public RegexCacheKey(string pattern, string flags, string hostLanguage, int backtrackingLimit)
            {
                this.pattern = pattern;
                this.flags = flags;
                this.hostLanguage = hostLanguage;
                this.backtrackingLimit = backtrackingLimit;
            }
            public bool Equals(RegexCacheKey o)
                => backtrackingLimit == o.backtrackingLimit && pattern == o.pattern && flags == o.flags && hostLanguage == o.hostLanguage;
            public override bool Equals(object o) => o is RegexCacheKey k && Equals(k);
            public override int GetHashCode()
            {
                unchecked
                {
                    int h = pattern == null ? 0 : pattern.GetHashCode();
                    h = h * 31 + (flags == null ? 0 : flags.GetHashCode());
                    h = h * 31 + (hostLanguage == null ? 0 : hostLanguage.GetHashCode());
                    return h * 31 + backtrackingLimit;
                }
            }
        }

        private sealed class RegexCacheEntry
        {
            public readonly IRegularExpression Regex;
            public readonly string[] Warnings;
            public RegexCacheEntry(IRegularExpression regex, string[] warnings)
            {
                Regex = regex;
                Warnings = warnings;
            }
        }

        internal sealed class LicenseFeature
        {
            public const int SCHEMA_VALIDATION = 1;
            public const int ENTERPRISE_XSLT = 2;
            public const int ENTERPRISE_XQUERY = 4;
            public const int PROFESSIONAL_EDITION = 8;
        }
    }
}
