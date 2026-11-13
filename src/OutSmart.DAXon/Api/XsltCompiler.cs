////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Transformation.Packages;
using OutSmart.DAXon.Trees.Linked;
using OutSmart.DAXon.Internal.Net;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Xslt;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Streams;
namespace OutSmart.DAXon.Api
{
    public class XsltCompiler
    {
        private readonly Processor processor;
        private readonly Configuration config;
        private readonly CompilerInfo compilerInfo;

        public virtual string XsltLanguageVersion
        {
            get => compilerInfo.XsltVersion == 40 ? "4.0" : "3.0"; set
            {
                switch (value)
                {
                    case "3":
                    case "3.0":
                        compilerInfo.XsltVersion = 30;
                        break;
                    case "4":
                    case "4.0":
                        compilerInfo.XsltVersion = 40;
                        break;
                    default:
                        throw new ArgumentException("Language version must be 3.0|4.0");
                }
            }
        }

        public virtual CompilerInfo UnderlyingCompilerInfo => compilerInfo;
        public XsltCompiler(Processor processor)
        {
            ArgumentCheck.NotNull(processor, nameof(processor));
            this.processor = processor;
            this.config = processor.UnderlyingConfiguration;
            compilerInfo = new CompilerInfo(config.DefaultXsltCompilerInfo);
            compilerInfo.TargetEdition = config.EditionCode;
        }

        public virtual Processor GetProcessor()
        {
            return processor;
        }

        public virtual void SetResourceResolver(IResourceResolver resolver)
        {
            compilerInfo.ResourceResolver = resolver;
        }

        public virtual void SetParameter(QName name, XdmValue value)
        {
            ArgumentCheck.NotNull(name, nameof(name));
            ArgumentCheck.NotNull(value, nameof(value));
            try
            {
                compilerInfo.SetParameter(name.GetStructuredQName(), ((ISequence)value.UnderlyingValue).Materialize());
            }
            catch (Exception e) when (ApiErrors.IsEngineError(e))
            {
                throw ApiErrors.WrapUnchecked(e);
            }
        }

        public virtual void ClearParameters()
        {
            compilerInfo.ClearParameters();
        }

        public virtual IResourceResolver GetResourceResolver()
        {
            return compilerInfo.ResourceResolver;
        }

        public virtual void SetErrorList(IList<IXmlProcessingError> errorList)
        {
            // null clears it, as SetErrorReporter(null) does: the list is not called on a compile error.
            compilerInfo.ErrorReporter = errorList == null ? null : new DelegateErrorReporter(err => errorList.Add(err));
        }

        public virtual void SetErrorReporter(IErrorReporter reporter)
        {
            compilerInfo.ErrorReporter = reporter;
        }

        public virtual IErrorReporter GetErrorReporter()
        {
            return compilerInfo.ErrorReporter;
        }

        public virtual void SetSchemaAware(bool schemaAware)
        {
            compilerInfo.SetSchemaAware(schemaAware);
        }

        public virtual bool IsSchemaAware()
        {
            return compilerInfo.IsSchemaAware();
        }

        public virtual bool IsRelocatable()
        {
            return compilerInfo.IsRelocatable();
        }

        public virtual void SetRelocatable(bool relocatable)
        {
            compilerInfo.SetRelocatable(relocatable);
        }

        public virtual void SetTargetEdition(string edition)
        {
            switch (edition)
            {
                case "EE":
                case "PE":
                case "HE":
                case "JS":
                case "JS2":
                case "JS3":
                    compilerInfo.TargetEdition = edition;
                    return;
                default:
                    throw new ArgumentException("Unknown Saxon edition " + edition);
            }
        }

        public virtual string GetTargetEdition()
        {
            return compilerInfo.TargetEdition;
        }

        public virtual void DeclareDefaultCollation(string uri)
        {
            IStringCollator c;
            try
            {
                c = GetProcessor().UnderlyingConfiguration.GetCollation(uri);
            }
            catch (XPathException)
            {
                c = null;
            }

            if (c == null)
            {
                throw new InvalidOperationException("Unknown collation " + uri);
            }

            compilerInfo.SetDefaultCollation(uri);
        }

        public virtual string GetDefaultCollation()
        {
            return compilerInfo.GetDefaultCollation();
        }

        public virtual bool IsAssertionsEnabled()
        {
            return compilerInfo.IsAssertionsEnabled();
        }

        public virtual void SetAssertionsEnabled(bool enabled)
        {
            compilerInfo.SetAssertionsEnabled(enabled);
        }

        // No optimizer passes, as the XPath and XQuery compilers do. Saxon sets JIT here, but just-in-time template
        // compilation is an EE feature this edition lacks: with it set every template rule was left without a body.
        public virtual void SetFastCompilation(bool fast)
        {
            if (fast)
            {
                compilerInfo.SetOptimizerOptions(new OptimizerOptions(0));
            }
            else
            {
                compilerInfo.SetOptimizerOptions(GetProcessor().UnderlyingConfiguration.GetOptimizerOptions());
            }
        }

        public virtual bool IsFastCompilation()
        {
            return compilerInfo.GetOptimizerOptions().GetOptions() == 0;
        }

        public virtual void SetCompileWithTracing(bool option)
        {
            if (option)
            {
                compilerInfo.CodeInjector = new XSLTTraceCodeInjector();
                compilerInfo.SetOptimizerOptions(compilerInfo.GetOptimizerOptions().Except(new OptimizerOptions(OptimizerOptions.COMMON_SUBEXPRESSIONS | OptimizerOptions.CONSTANT_FOLDING | OptimizerOptions.INLINE_FUNCTIONS | OptimizerOptions.INLINE_VARIABLES | OptimizerOptions.LOOP_LIFTING | OptimizerOptions.EXTRACT_GLOBALS)));
            }
            else
            {
                compilerInfo.CodeInjector = null;
            }
        }

        public virtual bool IsCompileWithTracing()
        {
            return compilerInfo.IsCompileWithTracing();
        }

        public virtual void SetGenerateByteCode(bool option)
        {
        }

        public virtual bool IsGenerateByteCode()
        {
            return false;
        }

        public virtual void ImportXQueryEnvironment(XQueryCompiler queryCompiler)
        {
            ArgumentCheck.NotNull(queryCompiler, nameof(queryCompiler));
            compilerInfo.SetXQueryLibraries(queryCompiler.UnderlyingStaticContext.CompiledLibraries);
        }

        public virtual ResolvedResource GetAssociatedStylesheet(ResolvedResource source, string media, string title, string charset)
        {
            ArgumentCheck.NotNull(source, nameof(source));
            // Parses the source to its first element: claims the deadline slot as DocumentBuilder does, or a spent
            // token from an earlier run on this thread failed the search at once ("Failed while looking for ...").
            using Controller.ThreadLimits deadline = Controller.ArmScope(config, "Parsing");
            try
            {
                return StylesheetModule.GetAssociatedStylesheet(config, compilerInfo.ResourceResolver, source, media, title, charset);
            }
            catch (Exception e) when (ApiErrors.IsEngineError(e))
            {
                throw ApiErrors.Wrap(e, source.SystemId);
            }
        }

        public virtual XsltPackage CompilePackage(ResolvedResource source)
        {
            ArgumentCheck.NotNull(source, nameof(source));
            // Compile under the Processor's deadline (see Compile(ResolvedResource)).
            using Controller.ThreadLimits deadline = Controller.ArmScope(config, "Compilation");
            try
            {
                Compilation compilation = null;
                if (source.Node is DocumentImpl)
                {
                    ElementImpl elem = ((DocumentImpl)source.Node).DocumentElement;
                    if (elem is StyleElement)
                    {
                        compilation = ((StyleElement)elem).GetCompilation();
                    }
                }

                if (compilation == null)
                {
                    compilation = new Compilation(config, new CompilerInfo(compilerInfo));
                }

                compilation.SetLibraryPackage(true);
                XsltPackage pack = new XsltPackage(this, compilation.CompilePackage(InputSizeLimit.Apply(source, MaxInput, "FODC0002")).GetStylesheetPackage());
                if (compilation.ErrorCount > 0)
                {
                    // The first error's code, its location and every message, as Compile gives them: a host with no
                    // reporter learned only "Package compilation failed: one error reported".
                    throw compilation.MakeCompilationFailure();
                }

                return pack;
            }
            catch (Exception e) when (ApiErrors.IsEngineError(e))
            {
                throw ApiErrors.Wrap(e, source.SystemId);
            }
        }

        public virtual XsltPackage CompilePackage(string file)
        {
            ArgumentCheck.NotNull(file, nameof(file));
            return CompilePackage(new ResolvedResource { SystemId = file });
        }

        private PackageLibrary GetPackageLibrary()
        {
            return compilerInfo.GetPackageLibrary();
        }

        public virtual XsltPackage LoadLibraryPackage(URI location)
        {
            ArgumentCheck.NotNull(location, nameof(location));
            return LoadLibraryPackage(new ResolvedResource { SystemId = location.ToString() });
        }

        public virtual XsltPackage LoadLibraryPackage(ResolvedResource input)
        {
            ArgumentCheck.NotNull(input, nameof(input));
            try
            {
                IIPackageLoader loader = processor.UnderlyingConfiguration.MakePackageLoader();
                if (loader != null)
                {
                    StylesheetPackage pack = loader.LoadPackage(input);
                    return new XsltPackage(this, pack);
                }

                throw new DAXonApiException("A compiled package (SEF) cannot be loaded by this edition: compile the stylesheet from source");
            }
            catch (Exception e) when (ApiErrors.IsEngineError(e))
            {
                throw ApiErrors.Wrap(e);
            }
        }

        public virtual XsltExecutable LoadExecutablePackage(URI location)
        {
            ArgumentCheck.NotNull(location, nameof(location));
            return LoadLibraryPackage(location).Link();
        }

        public virtual XsltExecutable LoadExecutablePackage(ResolvedResource source)
        {
            ArgumentCheck.NotNull(source, nameof(source));
            return LoadLibraryPackage(source).Link();
        }

        public virtual void ImportPackage(XsltPackage thePackage)
        {
            ArgumentCheck.NotNull(thePackage, nameof(thePackage));
            if (thePackage.GetProcessor() != processor)
            {
                throw new DAXonApiException("The imported package and the XsltCompiler must belong to the same Processor");
            }

            compilerInfo.GetPackageLibrary().AddPackage(thePackage.UnderlyingPreparedPackage);
        }

        public virtual void ImportPackage(XsltPackage thePackage, string packageName, string version)
        {
            ArgumentCheck.NotNull(thePackage, nameof(thePackage));
            try
            {
                if (thePackage.GetProcessor() != processor)
                {
                    throw new DAXonApiException("The imported package and the XsltCompiler must belong to the same Processor");
                }

                PackageDetails details = new PackageDetails();
                if (packageName == null)
                {
                    packageName = thePackage.Name;
                }

                if (version == null)
                {
                    version = thePackage.GetVersion();
                }

                details.nameAndVersion = new VersionedPackageName(packageName, version);
                details.loadedPackage = thePackage.UnderlyingPreparedPackage;
                compilerInfo.GetPackageLibrary().AddPackage(details);
            }
            catch (Exception e) when (ApiErrors.IsEngineError(e))
            {
                throw ApiErrors.Wrap(e);
            }
        }

        // versionRange as xsl:use-package's package-version: null is any version ("*").
        public virtual XsltPackage ObtainPackage(string packageName, string versionRange)
        {
            if (packageName == null)
            {
                throw new ArgumentNullException(nameof(packageName));
            }

            try
            {
                PackageVersionRanges pvr = new PackageVersionRanges(versionRange ?? "*");
                PackageDetails details = GetPackageLibrary().FindPackage(packageName, pvr);
                if (details != null)
                {
                    if (details.loadedPackage != null)
                    {
                        return new XsltPackage(this, details.loadedPackage);
                    }
                    else if (details.sourceLocation != null)
                    {
                        XsltPackage pack = CompilePackage(details.sourceLocation);
                        details.loadedPackage = pack.UnderlyingPreparedPackage;
                        return pack;
                    }
                }

                return null;
            }
            catch (Exception e) when (ApiErrors.IsEngineError(e))
            {
                throw ApiErrors.Wrap(e);
            }
        }

        public virtual XsltPackage ObtainPackageWithAlias(string alias)
        {
            PackageDetails details = GetPackageLibrary().FindDetailsForAlias(alias);
            if (details == null)
            {
                throw new DAXonApiException("No package with alias " + alias + " found in package library");
            }

            // The library may compile the package from source here: under the Processor's deadline, as
            // every compile entry is, so its static expressions share one budget (see Compile(ResolvedResource)).
            using Controller.ThreadLimits deadline = Controller.ArmScope(config, "Compilation");
            try
            {
                IList<VersionedPackageName> packageNames = new List<VersionedPackageName>();
                StylesheetPackage pack = GetPackageLibrary().ObtainLoadedPackage(details, packageNames);
                return new XsltPackage(this, pack);
            }
            catch (Exception e) when (ApiErrors.IsEngineError(e))
            {
                throw ApiErrors.Wrap(e);
            }
        }

        // Public in the Java original (compile(Source)); needed by callers that obtain a
        // ResolvedResource from GetAssociatedStylesheet (xml-stylesheet PI processing).
        public virtual XsltExecutable Compile(ResolvedResource source)
        {
            ArgumentCheck.NotNull(source, nameof(source));
            // Compile under the Processor's deadline: constant folding of hostile stylesheet text is
            // otherwise unbounded work before any run-time deadline exists (see ArmThreadDeadline).
            using Controller.ThreadLimits deadline = Controller.ArmScope(config, "Compilation");
            try
            {
                CompilerInfo ci2 = new CompilerInfo(compilerInfo);
                PreparedStylesheet pss = Compilation.CompileSingletonPackage(config, ci2, InputSizeLimit.Apply(source, MaxInput, "FODC0002"));
                return new XsltExecutable(processor, pss);
            }
            catch (Exception e) when (ApiErrors.IsEngineError(e))
            {
                throw ApiErrors.Wrap(e, source.SystemId);
            }
        }

        // .NET-native input overloads (P5): compile a stylesheet directly from a Stream/TextReader with an
        // explicit system identifier — the caller no longer constructs a JAXP Source.
        public virtual XsltExecutable Compile(System.IO.Stream input, string systemId)
        {
            ArgumentCheck.NotNull(input, nameof(input));
            return CompileFromXmlReader(null, input, systemId);
        }

        public virtual XsltExecutable Compile(System.IO.TextReader input, string systemId)
        {
            ArgumentCheck.NotNull(input, nameof(input));
            return CompileFromXmlReader(input, null, systemId);
        }

        // Round B1: a stylesheet arriving directly on the API is subject to the Processor's cap
        // just like one fetched by the resolver (xsl:include/import already were).
        private long MaxInput => InputSizeLimit.MaxFor(config);

        // Saxonica .NET-API compat: compile with just the reader (BaseUri property, else a pseudo-URI). Absolute, as
        // XPathCompiler.BaseURI and DocumentBuilder.BaseUri: a relative one failed the compile raw.
        public virtual Uri BaseUri
        {
            get => baseUri;
            set
            {
                if (value != null && !value.IsAbsoluteUri)
                {
                    throw new ArgumentException("Supplied base URI must be absolute", nameof(value));
                }

                baseUri = value;
            }
        }

        private Uri baseUri;

        public virtual XsltExecutable Compile(System.IO.TextReader input)
            => Compile(input, BaseUri != null ? BaseUri.AbsoluteUri : "urn:stylesheet");

        // Source-free compile: parse the stylesheet via XmlReaderToReceiver (no JAXP StreamSource). External
        // stylesheet entities / DTD resolve through the config ResourceResolver, wrapped as a native XmlResolver.
        private XsltExecutable CompileFromXmlReader(System.IO.TextReader charStream, System.IO.Stream byteStream, string systemId)
        {
            // Opened by system id (Compile(path)), the stylesheet itself is the resolver's first fetch: the host's
            // input, capped like a stream and not gated as an external entity - as Build(path) treats a document.
            string principal = charStream == null && byteStream == null ? systemId : null;
            System.Xml.XmlResolver resolver = new ResourceResolverXmlResolver(config.GetResourceResolver(), config, principal);
            // Compile under the Processor's deadline (see Compile(ResolvedResource)).
            using Controller.ThreadLimits deadline = Controller.ArmScope(config, "Compilation");
            try
            {
                charStream = InputSizeLimit.Apply(charStream, MaxInput, systemId, "FODC0002");
                byteStream = InputSizeLimit.Apply(byteStream, MaxInput, systemId, "FODC0002");
                CompilerInfo ci2 = new CompilerInfo(compilerInfo);
                using (System.Xml.XmlReader reader = OutSmart.DAXon.Events.XmlReaderToReceiver.CreateXmlReader(charStream, byteStream, systemId, resolver))
                {
                    PreparedStylesheet pss = Compilation.CompileSingletonPackage(config, ci2, reader, systemId);
                    return new XsltExecutable(processor, pss);
                }
            }
            catch (Exception e) when (ApiErrors.IsInputError(e))
            {
                throw ApiErrors.Wrap(e, systemId);
            }
        }

        public virtual XsltExecutable Compile(string file)
        {
            ArgumentCheck.NotNull(file, nameof(file));
            // P5: compile via the native XmlReader path (a bare systemId opens through XmlReader.Create), no JAXP Source.
            return CompileFromXmlReader(null, null, file);
        }

        // Just-in-time template compilation is a Saxon-EE feature: off is the only setting.
        public virtual void SetJustInTimeCompilation(bool jit)
        {
            if (jit)
            {
                throw new NotSupportedException("XSLT just-in-time compilation requires a Saxon-EE license");
            }
        }

        public virtual bool IsJustInTimeCompilation()
        {
            return false;
        }

        public virtual string GetDefaultElementNamespace()
        {
            return compilerInfo.DefaultElementNamespace.ToString();
        }

        public virtual void SetDefaultElementNamespace(string defaultNS)
        {
            compilerInfo.DefaultElementNamespace = NamespaceUri.Of(defaultNS);
        }

        public virtual UnprefixedElementMatchingPolicy GetUnprefixedElementMatchingPolicy()
        {
            return compilerInfo.GetUnprefixedElementMatchingPolicy();
        }

        public virtual void SetUnprefixedElementMatchingPolicy(UnprefixedElementMatchingPolicy unprefixedElementMatchingPolicy)
        {
            ArgumentCheck.Defined(unprefixedElementMatchingPolicy, nameof(unprefixedElementMatchingPolicy));
            compilerInfo.SetUnprefixedElementMatchingPolicy(unprefixedElementMatchingPolicy);
        }
    }
}