////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Transformation.Packages;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// Represents an XSLT compilation episode, compiling a single package.
    /// </summary>
    public class Compilation
    {
        // diagnostic switch to control output of timing information
        public static bool TIMING = false;
        private readonly Configuration config;
        private readonly CompilerInfo compilerInfo;
        private PrincipalStylesheetModule principalStylesheetModule;
        private int errorCount = 0;
        // Round C1: the reported diagnostics are also kept here so MakeCompilationFailure can
        // hand them to the caller. Capped - a stylesheet can report thousands, and the exception
        // is a diagnostic, not a transcript; errorCount stays the true total.
        private const int MaxRetainedErrors = 20;
        private readonly IList<IXmlProcessingError> reportedErrors = new List<IXmlProcessingError>();
        private bool schemaAware;
        private readonly QNameParser qNameParser;
        private readonly Dictionary<StructuredQName, ValueAndPrecedence> staticVariables = new Dictionary<StructuredQName, ValueAndPrecedence>();
        private readonly Dictionary<DocumentKey, ITreeInfo> stylesheetModules = new Dictionary<DocumentKey, ITreeInfo>();
        private readonly Stack<DocumentKey> importStack = new Stack<DocumentKey>(); // handles both include and import
        private PackageData packageData;
        private bool preScan = true;
        private bool createsSecondaryResultDocuments = false;
        private bool libraryPackage = false;
        private VersionedPackageName expectedNameAndVersion = null;
        private readonly IList<UsePack> packageDependencies = new List<UsePack>();
        private IList<VersionedPackageName> usingPackages = new List<VersionedPackageName>();
        private GlobalParameterSet suppliedParameters;
        private bool fallbackToNonStreaming = false;
        private HashSet<StructuredQName> referencedModes = new HashSet<StructuredQName>();
        public Timer timer = null;

        public virtual int ErrorCount => errorCount;

        public virtual Dictionary<DocumentKey, ITreeInfo> StylesheetModules => stylesheetModules;

        public virtual Stack<DocumentKey> ImportStack => importStack;

        public virtual GlobalParameterSet Parameters => suppliedParameters;

        public virtual HashSet<StructuredQName> AllKnownModeNames => referencedModes;

        public Compilation(Configuration config, CompilerInfo info)
            : this(config, info, false)
        {
        }

        // nestedInEpisode: a package compiled mid-compile (PackageLibrary.ObtainLoadedPackage) is
        // part of the CALLING compilation's episode, like an imported query module - it must keep
        // counting on the shared reporter, not reset it (round 12).
        internal Compilation(Configuration config, CompilerInfo info, bool nestedInEpisode)
        {
            this.config = config;
            this.compilerInfo = info;

            // The error/warning budgets belong to this compilation, not to the Processor. The
            // reporter is shared by every compiler made from one Configuration, so without this
            // the counts accumulated for the process's life and a late compile aborted on its
            // first error with "Too many errors reported" (round 10).
            if (!nestedInEpisode)
            {
                (info.ErrorReporter as StandardErrorReporter)?.StartCompilationEpisode();
            }
            schemaAware = info.IsSchemaAware();
            preScan = info.IsJustInTimeCompilation();
            suppliedParameters = compilerInfo.Parameters;
            referencedModes.Add(Mode.UNNAMED_MODE_NAME);
            qNameParser = new QNameParser(null).WithAcceptEQName(true).WithErrorOnBadSyntax("XTSE0020").WithErrorOnUnresolvedPrefix("XTSE0280");
            if (TIMING)
            {
                timer = new Timer();
            }
        }

        public static PreparedStylesheet CompileSingletonPackage(Configuration config, CompilerInfo compilerInfo, ResolvedResource source)
        {
            using RunResources run = RunResources.Enter();
            try
            {
                Compilation compilation = new Compilation(config, compilerInfo);
                return StylesheetModule.LoadStylesheet(source, compilation);
            }
            catch (XPathException err)
            {
                if (!err.HasBeenReported())
                {
                    compilerInfo.ErrorReporter.Report(new XmlProcessingException(err));
                }

                throw;
            }
        }

        // Source-free compile (P5): compile a stylesheet from a System.Xml.XmlReader with an explicit system id.
        public static PreparedStylesheet CompileSingletonPackage(Configuration config, CompilerInfo compilerInfo, System.Xml.XmlReader reader, string systemId)
        {
            using RunResources run = RunResources.Enter();
            try
            {
                Compilation compilation = new Compilation(config, compilerInfo);
                return StylesheetModule.LoadStylesheet(reader, systemId, compilation);
            }
            catch (XPathException err)
            {
                if (!err.HasBeenReported())
                {
                    compilerInfo.ErrorReporter.Report(new XmlProcessingException(err));
                }

                throw;
            }
        }

        public virtual void SetUsingPackages(IList<VersionedPackageName> users)
        {
            this.usingPackages = users;
        }

        public virtual void SetPackageData(PackageData pack)
        {
            this.packageData = pack;
        }

        public virtual void SetMinimalPackageData()
        {
            if (GetPackageData() == null)
            {

                // Create a temporary PackageData for use during use-when processing
                PackageData pd = new PackageData(GetConfiguration());
                pd.SetHostLanguage(HostLanguage.XSLT, compilerInfo.XsltVersion);
                pd.TargetEdition = compilerInfo.TargetEdition;
                pd.SetSchemaAware(schemaAware);
                packageData = pd;
            }
        }

        public virtual void SetExpectedNameAndVersion(VersionedPackageName vpn)
        {
            this.expectedNameAndVersion = vpn;
        }

        public virtual void RegisterPackageDependency(UsePack use)
        {
            packageDependencies.Add(use);
        }

        public virtual void SatisfyPackageDependencies(XSLPackage thisPackage)
        {

            //            throw new XPathException("Name and version of package in XSLT source [" + thisPackage.getNameAndVersion() +
            //            "] do not match name and version in configuration file [" + expectedNameAndVersion + "]");
            //        }
            PackageLibrary library = compilerInfo.GetPackageLibrary();
            library.GetCompilerInfo().TargetEdition = compilerInfo.TargetEdition;
            XPathException lastError = null;
            foreach (UsePack use in packageDependencies)
            {
                PackageDetails details = library.FindPackage(use.packageName, use.ranges);
                if (details == null)
                {
                    throw new XPathException("Cannot find package " + use.packageName + " (version " + use.ranges + ")", "XTSE3000", use.location);
                }

                if (details.loadedPackage != null)
                {
                    StylesheetPackage used = details.loadedPackage;
                    VersionedPackageName existing = new VersionedPackageName(used.PackageName, used.GetPackageVersion());
                    if (usingPackages.Contains(existing))
                    {

                        // Report a cycle of package dependencies
                        StringBuilder buffer = new StringBuilder(1024);
                        foreach (VersionedPackageName n in usingPackages)
                        {
                            buffer.Append(n.packageName);
                            buffer.Append(", ");
                        }

                        buffer.Append("and ");
                        buffer.Append(thisPackage.Name);
                        throw new XPathException("There is a cycle of package dependencies involving " + buffer, "XTSE3005");
                    }
                }

                try
                {
                    IList<VersionedPackageName> disallowed = new List<VersionedPackageName>(usingPackages);
                    disallowed.Add(details.nameAndVersion);
                    library.ObtainLoadedPackage(details, disallowed);
                }
                catch (XPathException err)
                {
                    err.MaybeSetErrorCode("XTSE3000");
                    if (!err.HasBeenReported())
                    {
                        ReportError(err);
                    }

                    lastError = err;
                }
            }

            if (lastError != null)
            {
                throw lastError;
            }
        }

        public virtual PrincipalStylesheetModule CompilePackage(ResolvedResource source)
        {
            using RunResources run = RunResources.Enter();
            SetMinimalPackageData();
            NodeInfo document;
            NodeInfo outermost = null;
            NodeInfo root = source.Node;
            if (root != null)
            {
                if (root.GetNodeKind() == Types.Type.DOCUMENT)
                {
                    document = root;
                    outermost = document.IterateAxis(AxisInfo.CHILD, NodeKindTest.ELEMENT).Next();
                }
                else if (root.GetNodeKind() == Types.Type.ELEMENT)
                {
                    document = root.Root;
                    outermost = root;
                }
            }

            if (!(outermost is XSLPackage))
            {
                document = StylesheetModule.LoadStylesheetModule(source, true, this, NestedIntegerValue.TWO);
                outermost = document.IterateAxis(AxisInfo.CHILD, NodeKindTest.ELEMENT).Next();
            }

            if (outermost == null)
            {
                throw new XPathException("No stylesheet element found at " + source.SystemId, "XPST0010");
            }

            if (outermost is LiteralResultElement)
            {
                document = ((LiteralResultElement)outermost).MakeStylesheet(true);
                outermost = document.IterateAxis(AxisInfo.CHILD, NodeKindTest.ELEMENT).Next();
            }

            XSLPackage xslpackage;
            try
            {
                if (outermost is XSLPackage)
                {
                    xslpackage = (XSLPackage)outermost;
                }
                else
                {
                    throw new XPathException("Outermost element must be xsl:package, xsl:stylesheet, or xsl:transform (found " + outermost.DisplayName + ")", "XPST0010").WithLocation(outermost);
                }
            }
            catch (XPathException e)
            {
                if (!e.HasBeenReported())
                {
                    GetCompilerInfo().ErrorReporter.Report(new XmlProcessingException(e));
                }

                throw;
            }

            if (Compilation.TIMING)
            {
                timer.Report("Built stylesheet documents");
            }

            CompilerInfo info = GetCompilerInfo();
            StyleNodeFactory factory = GetStyleNodeFactory(true);
            PrincipalStylesheetModule psm = factory.NewPrincipalModule(xslpackage);
            StylesheetPackage pack = psm.GetStylesheetPackage();
            pack.SetLanguageVersion(xslpackage.GetVersion());
            pack.SetPackageVersion(xslpackage.GetPackageVersion());
            pack.PackageName = xslpackage.Name;
            pack.SetSchemaAware(info.IsSchemaAware() || IsSchemaAware());
            pack.SetLanguageVersion(info.XsltVersion);
            pack.CreateFunctionLibrary();
            if (compilerInfo.StubFunctionLibrary != null)
            {
                pack.GetFunctionLibrary().AddFunctionLibrary(compilerInfo.StubFunctionLibrary);
            }

            psm.GetRuleManager().SetCompilerInfo(info);
            SetPrincipalStylesheetModule(psm);
            packageData = null;
            SatisfyPackageDependencies(xslpackage);
            if (TIMING)
            {
                timer.Report("Preparing package");
            }

            try
            {
                psm.Preprocess(this);
            }
            catch (XPathException e)
            {
                info.ErrorReporter.Report(new XmlProcessingException(e));
                throw;
            }

            if (ErrorCount == 0)
            {
                try
                {
                    psm.Fixup();
                }
                catch (XPathException e)
                {
                    ReportError(e);
                }
            }

            if (TIMING)
            {
                timer.Report("Fixup");
            }


            // Compile groups of like-named attribute sets into a single attributeSet object
            if (ErrorCount == 0)
            {
                try
                {
                    psm.CombineAttributeSets(this);
                }
                catch (XPathException e)
                {
                    ReportError(e);
                }
            }

            if (TIMING)
            {
                timer.Report("Combine attribute sets");
            }


            // Compile the stylesheet package
            if (ErrorCount == 0)
            {
                try
                {
                    psm.Compile(this);
                }
                catch (XPathException e)
                {
                    ReportError(e);
                }
            }

            if (ErrorCount == 0)
            {
                try
                {
                    psm.Complete();
                }
                catch (XPathException e)
                {
                    ReportError(e);
                }
            }

            if (TIMING)
            {
                timer.Report("Completion");
            }

            psm.GetStylesheetPackage().SetCreatesSecondaryResultDocuments(createsSecondaryResultDocuments);
            if (IsFallbackToNonStreaming())
            {
                psm.GetStylesheetPackage().SetFallbackToNonStreaming();
            }

            if (TIMING)
            {
                timer.Report("Streaming fallback");
            }

            return psm;
        }

        public virtual Configuration GetConfiguration()
        {
            return config;
        }

        public virtual CompilerInfo GetCompilerInfo()
        {
            return compilerInfo;
        }

        public virtual PackageData GetPackageData()
        {
            if (packageData != null)
            {
                return packageData;
            }

            return principalStylesheetModule == null ? null : principalStylesheetModule.GetStylesheetPackage();
        }

        public virtual bool IsSchemaAware()
        {
            return schemaAware;
        }

        public virtual void SetSchemaAware(bool schemaAware)
        {
            this.schemaAware = schemaAware;
            GetPackageData().SetSchemaAware(schemaAware);
        }

        public virtual StyleNodeFactory GetStyleNodeFactory(bool topLevel)
        {
            StyleNodeFactory factory = GetConfiguration().MakeStyleNodeFactory(this);
            factory.SetTopLevelModule(topLevel);
            return factory;
        }

        private void SetPrincipalStylesheetModule(PrincipalStylesheetModule module)
        {
            this.principalStylesheetModule = module;
        }

        public virtual PrincipalStylesheetModule GetPrincipalStylesheetModule()
        {
            return principalStylesheetModule;
        }

        public virtual void ReportError(IXmlProcessingError err)
        {
            Retain(err);
            IErrorReporter reporter = compilerInfo.ErrorReporter;
            if (reporter != null)
            {
                reporter.Report(err);
            }

            errorCount++;
            if (err.TerminationMessage != null)
            {
                throw new XmlProcessingAbort(err.TerminationMessage);
            }
        }

        public virtual void ReportError(XPathException err)
        {
            err.SetHostLanguage(HostLanguage.XSLT);
            IErrorReporter el = compilerInfo.ErrorReporter;
            if (el == null)
            {
                el = GetConfiguration().MakeErrorReporter();
            }

            if (!err.HasBeenReported())
            {
                errorCount++;
                XmlProcessingException error = new XmlProcessingException(err);
                Retain(error);
                try
                {
                    el.Report(error);
                    err.SetHasBeenReported(true);
                }
                catch (Exception)
                {
                    // A reporter is a diagnostic sink: if the host's own one throws, that must
                    // not replace the error being reported.
                }
            }
            else
            {
                // Already emitted by whoever first caught it - the XPath parser reports through
                // its own reporter and marks the exception. Retain it anyway: the failure thrown
                // at the end of the compile is the caller's only channel.
                Retain(new XmlProcessingException(err));
                if (errorCount == 0)
                {
                    errorCount++;
                }
            }
        }

        private void Retain(IXmlProcessingError err)
        {
            if (reportedErrors.Count >= MaxRetainedErrors)
            {
                return;
            }

            // One failure can arrive twice - reported once, then again as it unwinds. Keep the first.
            XPathException cause = (err as XmlProcessingException)?.GetXPathException();
            if (cause != null)
            {
                foreach (IXmlProcessingError seen in reportedErrors)
                {
                    if ((seen as XmlProcessingException)?.GetXPathException() == cause)
                    {
                        return;
                    }
                }
            }

            reportedErrors.Add(err);
        }

        /// <summary>
        /// The failure to throw when the compile reported errors. Carries the diagnostics -
        /// message, code, line, column, module - so a host that installs no reporter still gets
        /// a usable error instead of the bare fact that compilation failed (round C1).
        /// </summary>
        public virtual XPathException MakeCompilationFailure()
        {
            if (reportedErrors.Count == 0)
            {
                return new XPathException("Errors were reported during stylesheet compilation");
            }

            StandardErrorReporter formatter = new StandardErrorReporter();
            StringBuilder text = new StringBuilder();
            foreach (IXmlProcessingError e in reportedErrors)
            {
                if (text.Length > 0)
                {
                    text.Append('\n');
                }

                text.Append(formatter.DescribeError(e));
            }

            if (errorCount > reportedErrors.Count)
            {
                text.Append("\n...and ").Append(errorCount - reportedErrors.Count).Append(" further error(s)");
            }

            XsltCompilationFailure failure = new XsltCompilationFailure(
                text.ToString(), new ReadOnlyCollection<IXmlProcessingError>(reportedErrors), errorCount);
            IXmlProcessingError first = reportedErrors[0];
            QName code = first.GetErrorCode();
            if (code != null)
            {
                failure.WithErrorCode(code.GetStructuredQName());
            }

            if (first.GetLocation() != null)
            {
                failure.SetLocator(first.GetLocation());
            }

            // Already emitted through the reporter; this object exists to reach the caller.
            failure.SetHasBeenReported(true);
            return failure;
        }

        public virtual void ReportWarning(XPathException err)
        {
            err.SetHostLanguage(HostLanguage.XSLT);
            IErrorReporter reporter = compilerInfo.ErrorReporter;
            if (reporter == null)
            {
                reporter = GetConfiguration().MakeErrorReporter();
            }

            if (reporter != null)
            {
                XmlProcessingException error = new XmlProcessingException(err);
                error.SetWarning(true);
                reporter.Report(error);
            }
        }

        public virtual void ReportWarning(string message, string errorCode, ILocation location)
        {
            XmlProcessingIncident error = new XmlProcessingIncident(message, errorCode, location).AsWarning();
            error.SetHostLanguage(HostLanguage.XSLT);
            compilerInfo.ErrorReporter.Report(error);
        }

        public virtual void DeclareStaticVariable(StructuredQName name, IGroundedValue value, NestedIntegerValue precedence, bool isParam)
        {
            ValueAndPrecedence vp = staticVariables.GetOrDefault(name);
            if (vp != null)
            {
                if (vp.precedence.CompareTo(precedence) < 0)
                {

                    // new value must be compatible with the old, see spec bug 24478
                    if (!ValuesAreCompatible(value, vp.value))
                    {
                        throw new XPathException("Incompatible values assigned for static variable " + name.DisplayName, "XTSE3450");
                    }

                    if (vp.isParam != isParam)
                    {
                        throw new XPathException("Static variable " + name.DisplayName + " cannot be redeclared as a param", "XTSE3450");
                    }
                }
                else
                {
                    return; // ignore the new value
                }
            }

            staticVariables[name] = new ValueAndPrecedence(value, precedence, isParam);
        }

        private bool ValuesAreCompatible(IGroundedValue val0, IGroundedValue val1)
        {
            if (val0.GetLength() != val1.GetLength())
            {
                return false;
            }

            if (val0.GetLength() == 1)
            {
                IItem i0 = val0.Head();
                IItem i1 = val1.Head();
                if (i0 is AtomicValue)
                {
                    return i1 is AtomicValue && ((AtomicValue)i0).IsIdentical((AtomicValue)i1);
                }
                else if (i0 is NodeInfo)
                {
                    return i1 is NodeInfo && i0.Equals(i1);
                }
                else
                {
                    return i0 == i1;
                }
            }
            else
            {
                for (int i = 0; i < val0.GetLength(); i++)
                {
                    if (!ValuesAreCompatible(val0.ItemAt(i), val1.ItemAt(i)))
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public virtual IGroundedValue GetStaticVariable(StructuredQName name)
        {
            ValueAndPrecedence vp = staticVariables.GetOrDefault(name);
            return vp == null ? null : vp.value;
        }

        public virtual NestedIntegerValue GetStaticVariablePrecedence(StructuredQName name)
        {
            ValueAndPrecedence vp = staticVariables.GetOrDefault(name);
            return vp == null ? null : vp.precedence;
        }

        public virtual QNameParser GetQNameParser()
        {
            return qNameParser;
        }

        public virtual bool IsPreScan()
        {
            return preScan;
        }

        public virtual void SetPreScan(bool preScan)
        {
            this.preScan = preScan;
        }

        public virtual bool IsCreatesSecondaryResultDocuments()
        {
            return createsSecondaryResultDocuments;
        }

        public virtual void SetCreatesSecondaryResultDocuments(bool createsSecondaryResultDocuments)
        {
            this.createsSecondaryResultDocuments = createsSecondaryResultDocuments;
        }

        public virtual bool IsLibraryPackage()
        {
            return libraryPackage;
        }

        public virtual void SetLibraryPackage(bool libraryPackage)
        {
            this.libraryPackage = libraryPackage;
        }

        public virtual void SetParameter(StructuredQName name, IGroundedValue seq)
        {
            suppliedParameters.Put(name, seq);
        }

        public virtual void ClearParameters()
        {
            suppliedParameters = new GlobalParameterSet();
        }

        public virtual bool IsFallbackToNonStreaming()
        {
            return fallbackToNonStreaming;
        }

        public virtual void SetFallbackToNonStreaming(bool fallbackToNonStreaming)
        {
            this.fallbackToNonStreaming = fallbackToNonStreaming;
        }
        private sealed class ValueAndPrecedence
        {

            public IGroundedValue value;
            public NestedIntegerValue precedence;
            public bool isParam;
            public ValueAndPrecedence(IGroundedValue v, NestedIntegerValue p, bool isParam)
            {
                this.value = v;
                this.precedence = p;
                this.isParam = isParam;
            }
        }
    }
}