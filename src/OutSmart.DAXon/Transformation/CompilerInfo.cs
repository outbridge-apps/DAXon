////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.XQuery;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Transformation.Packages;
using System.Collections.Generic;
using OutSmart.DAXon.Lib;
namespace OutSmart.DAXon.Transformation
{
    //@CSharpInjectMembers(code = {
    //        "    public void setErrorReporter(Action<OutSmart.DAXon.Api.IXmlProcessingError> reporter) {"
    //      + "        setErrorReporter(new Saxon.Impl.Helpers.ErrorReportingAction(reporter));"
    //      + "    }"
    //})
    public class CompilerInfo
    {
        private Configuration config;
        private IResourceResolver resourceResolver;
        private IErrorReporter errorReporter = new StandardErrorReporter();
        private ICodeInjector codeInjector;
        private int recoveryPolicy = Mode.RECOVER_WITH_WARNINGS;
        private bool schemaAware;
        private StructuredQName defaultInitialMode;
        private StructuredQName defaultInitialTemplate;
        private GlobalParameterSet suppliedParameters = new GlobalParameterSet();
        private string defaultCollation;
        private PackageLibrary packageLibrary;
        private bool assertionsEnabled;
        private string targetEdition = "HE";
        private bool relocatable;
        private IEnumerable<QueryLibrary> queryLibraries;
        private OptimizerOptions optimizerOptions;
        private NamespaceUri defaultNamespaceForElementsAndTypes = NamespaceUri.NULL;
        private UnprefixedElementMatchingPolicy unprefixedElementMatchingPolicy = UnprefixedElementMatchingPolicy.DEFAULT_NAMESPACE;
        private int languageVersion = 30;
        private IFunctionLibrary stubFunctionLibrary;
        private IOutputURIResolver outputURIResolver = StandardOutputResolver.GetInstance();

        public virtual GlobalParameterSet Parameters => suppliedParameters;

        public virtual string TargetEdition
        {
            get => targetEdition; set
            {
                this.targetEdition = value;
            }
        }

        public virtual IResourceResolver ResourceResolver
        {
            get => resourceResolver; set
            {
                resourceResolver = value;
            }
        }

        public virtual IOutputURIResolver OutputURIResolver
        {
            get => outputURIResolver; set
            {
                this.outputURIResolver = value;
            }
        }

        public virtual IErrorReporter ErrorReporter
        {
            get => this.errorReporter; set
            {
                this.errorReporter = value;
            }
        }

        public virtual ICodeInjector CodeInjector
        {
            get => codeInjector; set
            {
                codeInjector = value;
            }
        }

        public virtual StructuredQName DefaultInitialTemplate
        {
            get => defaultInitialTemplate; set
            {
                defaultInitialTemplate = value;
            }
        }

        public virtual StructuredQName DefaultInitialMode
        {
            get => defaultInitialMode; set
            {
                defaultInitialMode = value;
            }
        }

        public virtual int XsltVersion
        {
            get => languageVersion; set
            {
                this.languageVersion = value;
            }
        }

        public virtual NamespaceUri DefaultElementNamespace
        {
            get => defaultNamespaceForElementsAndTypes; set
            {
                this.defaultNamespaceForElementsAndTypes = value;
            }
        }

        public virtual IEnumerable<QueryLibrary> QueryLibraries => queryLibraries;

        public virtual IFunctionLibrary StubFunctionLibrary => stubFunctionLibrary;
        public CompilerInfo(Configuration config)
        {
            this.config = config;
            errorReporter = config.MakeErrorReporter();
            packageLibrary = new PackageLibrary(this);
            optimizerOptions = config.GetOptimizerOptions();
        }

        public CompilerInfo(CompilerInfo info)
        {
            CopyFrom(info);
        }

        public virtual void CopyFrom(CompilerInfo info)
        {
            config = info.config;
            resourceResolver = info.resourceResolver;
            errorReporter = info.errorReporter;
            codeInjector = info.codeInjector;
            recoveryPolicy = info.recoveryPolicy;
            schemaAware = info.schemaAware;
            defaultInitialMode = info.defaultInitialMode;
            defaultInitialTemplate = info.defaultInitialTemplate;
            suppliedParameters = new GlobalParameterSet(info.suppliedParameters);
            defaultCollation = info.defaultCollation;
            assertionsEnabled = info.assertionsEnabled;
            targetEdition = info.targetEdition;
            packageLibrary = new PackageLibrary(info.packageLibrary);
            relocatable = info.relocatable;
            optimizerOptions = info.optimizerOptions;
            queryLibraries = info.queryLibraries;
            defaultNamespaceForElementsAndTypes = info.defaultNamespaceForElementsAndTypes;
            unprefixedElementMatchingPolicy = info.unprefixedElementMatchingPolicy;
            languageVersion = info.languageVersion;
            stubFunctionLibrary = info.stubFunctionLibrary;
            outputURIResolver = info.outputURIResolver;
        }

        public virtual Configuration GetConfiguration()
        {
            return config;
        }

        public virtual void SetJustInTimeCompilation(bool jit)
        {
            if (jit)
            {
                optimizerOptions = optimizerOptions.Union(new OptimizerOptions(OptimizerOptions.JIT));
            }
            else
            {
                optimizerOptions = optimizerOptions.Except(new OptimizerOptions(OptimizerOptions.JIT));
            }
        }

        public virtual bool IsJustInTimeCompilation()
        {
            return optimizerOptions.IsSet(OptimizerOptions.JIT);
        }

        public virtual void SetParameter(StructuredQName name, IGroundedValue seq)
        {
            suppliedParameters.Put(name, seq);
        }

        public virtual void ClearParameters()
        {
            suppliedParameters.Clear();
        }

        public virtual bool IsRelocatable()
        {
            return relocatable;
        }

        public virtual void SetRelocatable(bool relocatable)
        {
            this.relocatable = relocatable;
        }

        public virtual void SetPackageLibrary(PackageLibrary library)
        {
            packageLibrary = library;
        }

        public virtual PackageLibrary GetPackageLibrary()
        {
            return packageLibrary;
        }

        public virtual bool IsAssertionsEnabled()
        {
            return assertionsEnabled;
        }

        public virtual void SetAssertionsEnabled(bool enabled)
        {
            this.assertionsEnabled = enabled;
        }

        public virtual void SetOptimizerOptions(OptimizerOptions options)
        {
            this.optimizerOptions = options;
        }

        public virtual OptimizerOptions GetOptimizerOptions()
        {
            return this.optimizerOptions;
        }

        public virtual void SetDefaultCollation(string collation)
        {
            this.defaultCollation = collation;
        }

        public virtual string GetDefaultCollation()
        {
            return this.defaultCollation;
        }

        public virtual bool IsCompileWithTracing()
        {
            return codeInjector != null;
        }

        public virtual void SetSchemaAware(bool schemaAware)
        {
            this.schemaAware = schemaAware;
        }

        public virtual bool IsSchemaAware()
        {
            return schemaAware;
        }

        public virtual UnprefixedElementMatchingPolicy GetUnprefixedElementMatchingPolicy()
        {
            return unprefixedElementMatchingPolicy;
        }

        public virtual void SetUnprefixedElementMatchingPolicy(UnprefixedElementMatchingPolicy unprefixedElementMatchingPolicy)
        {
            this.unprefixedElementMatchingPolicy = unprefixedElementMatchingPolicy;
        }

        public virtual void SetXQueryLibraries(IEnumerable<QueryLibrary> libraries)
        {
            this.queryLibraries = libraries;
        }

        public virtual void ImportStubFunctionLibrary(ResolvedResource jsonSignatures)
        {
            stubFunctionLibrary = config.LoadStubFunctionLibrary(jsonSignatures);
        }
    }
}