////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Functions.Registry;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
namespace OutSmart.DAXon.XPath
{
    public abstract class AbstractStaticContext : IStaticContext
    {
        private string baseURI;
        private Configuration config;
        private PackageData packageData;
        private ILocation containingLocation = Loc.NONE;
        private string defaultCollationName;
        private FunctionLibraryList libraryList = new FunctionLibraryList();
        private NamespaceUri defaultFunctionNamespace = NamespaceUri.FN;
        private NamespaceUri defaultElementNamespace = NamespaceUri.NULL;
        private bool backwardsCompatible;
        private int xpathLanguageLevel = 31;
        private readonly Dictionary<StructuredQName, Types.ItemType> typeAliases = new Dictionary<StructuredQName, Types.ItemType>();
        private UnprefixedElementMatchingPolicy unprefixedElementPolicy = UnprefixedElementMatchingPolicy.DEFAULT_NAMESPACE;
        private IWarningHandler warningHandler;

        public virtual string StaticBaseURI => baseURI == null ? "" : baseURI;
        protected virtual void SetConfiguration(Configuration config)
        {
            this.config = config;
            this.defaultCollationName = config.GetDefaultCollationName();
            warningHandler = (message, code, locator) =>
            {
                XmlProcessingIncident incident = new XmlProcessingIncident(message, code, locator).AsWarning();
                config.MakeErrorReporter().Report(incident);
            };
        }

        public virtual Configuration GetConfiguration()
        {
            return config;
        }

        public virtual void SetPackageData(PackageData packageData)
        {
            this.packageData = packageData;
        }

        public virtual PackageData GetPackageData()
        {
            return packageData;
        }

        public virtual void SetSchemaAware(bool aware)
        {
            GetPackageData().SetSchemaAware(aware);
        }

        public virtual RetainedStaticContext MakeRetainedStaticContext()
        {
            return new RetainedStaticContext(this);
        }

        protected void SetDefaultFunctionLibrary()
        {
            FunctionLibraryList lib = new FunctionLibraryList();
            lib.AddFunctionLibrary(XPath31FunctionSet.GetInstance());
            lib.AddFunctionLibrary(GetConfiguration().GetBuiltInExtensionLibraryList(31));
            lib.AddFunctionLibrary(new ConstructorFunctionLibrary(GetConfiguration()));
            lib.AddFunctionLibrary(config.GetIntegratedFunctionLibrary());
            config.AddExtensionBinders(lib);
            SetFunctionLibrary(lib);
        }

        public void SetDefaultFunctionLibrary(int version)
        {
            FunctionLibraryList lib = new FunctionLibraryList();
            lib.AddFunctionLibrary(config.GetXPathFunctionSet(version));
            lib.AddFunctionLibrary(config.GetBuiltInExtensionLibraryList(version));
            lib.AddFunctionLibrary(new ConstructorFunctionLibrary(config));
            lib.AddFunctionLibrary(config.GetIntegratedFunctionLibrary());
            config.AddExtensionBinders(lib);
            SetFunctionLibrary(lib);
        }

        protected void AddFunctionLibrary(IFunctionLibrary library)
        {
            libraryList.AddFunctionLibrary(library);
        }

        public virtual IXPathContext MakeEarlyEvaluationContext()
        {
            return new EarlyEvaluationContext(GetConfiguration());
        }

        public virtual ILocation GetContainingLocation()
        {
            return containingLocation;
        }

        public virtual void SetContainingLocation(ILocation location)
        {
            containingLocation = location;
        }

        public virtual void SetBaseURI(string baseURI)
        {
            this.baseURI = baseURI;
        }

        public virtual IFunctionLibrary GetFunctionLibrary()
        {
            return libraryList;
        }

        public virtual void SetFunctionLibrary(FunctionLibraryList lib)
        {
            libraryList = lib;
        }

        public virtual void SetDefaultCollationName(string collationName)
        {
            defaultCollationName = collationName;
        }

        public virtual string GetDefaultCollationName()
        {
            return defaultCollationName;
        }

        public virtual void SetWarningHandler(Action<string, ILocation> handler)
        {
            warningHandler = (message, code, loc) => handler(message,loc);
        }

        public virtual void SetWarningHandler(IWarningHandler handler)
        {
            warningHandler = handler;
        }

        public virtual IWarningHandler GetWarningHandler()
        {
            return warningHandler;
        }

        public virtual void IssueWarning(string s, string errorCode, ILocation locator)
        {
            GetWarningHandler().Invoke(s, errorCode, locator);
        }

        public virtual string GetSystemId()
        {
            return "";
        }

        public virtual NamespaceUri GetDefaultElementNamespace()
        {
            return defaultElementNamespace;
        }

        public virtual void SetDefaultElementNamespace(NamespaceUri uri)
        {
            defaultElementNamespace = uri;
        }

        public virtual void SetDefaultFunctionNamespace(NamespaceUri uri)
        {
            defaultFunctionNamespace = uri;
        }

        public virtual NamespaceUri GetDefaultFunctionNamespace()
        {
            return defaultFunctionNamespace;
        }

        public virtual void SetXPathLanguageLevel(int level)
        {
            if (level == 40)
            {
                config.CheckLicensedFeature(Configuration.LicenseFeature.PROFESSIONAL_EDITION, "XPath 4.0", -1);
            }

            xpathLanguageLevel = level;
            if (packageData.HostLanguageVersion != level)
            {
                packageData.SetHostLanguage(packageData.GetHostLanguage(), level);
            }
        }

        public virtual int GetXPathVersion()
        {
            return xpathLanguageLevel;
        }

        public virtual void SetBackwardsCompatibilityMode(bool option)
        {
            backwardsCompatible = option;
        }

        public virtual bool IsInBackwardsCompatibleMode()
        {
            return backwardsCompatible;
        }

        public virtual void SetDecimalFormatManager(DecimalFormatManager manager)
        {
            GetPackageData().SetDecimalFormatManager(manager);
        }

        public virtual Types.ItemType GetRequiredContextItemType()
        {
            return AnyItemType.GetInstance();
        }

        public virtual DecimalFormatManager GetDecimalFormatManager()
        {
            DecimalFormatManager manager = GetPackageData().GetDecimalFormatManager();
            if (manager == null)
            {
                manager = new DecimalFormatManager(HostLanguage.XPATH, xpathLanguageLevel);
                GetPackageData().SetDecimalFormatManager(manager);
            }

            return manager;
        }

        public virtual KeyManager GetKeyManager()
        {
            return GetPackageData().GetKeyManager();
        }

        public virtual void SetTypeAlias(StructuredQName name, Types.ItemType type)
        {
            typeAliases[name] = type;
        }

        public virtual Types.ItemType ResolveTypeAlias(StructuredQName typeName)
        {
            return typeAliases.GetOrDefault(typeName);
        }

        public virtual void SetUnprefixedElementMatchingPolicy(UnprefixedElementMatchingPolicy policy)
        {
            this.unprefixedElementPolicy = policy;
        }

        public virtual UnprefixedElementMatchingPolicy GetUnprefixedElementMatchingPolicy()
        {
            return unprefixedElementPolicy;
        }
        public abstract Expression BindVariable(StructuredQName arg0);
        public abstract bool IsImportedSchema(NamespaceUri arg0);
        public abstract HashSet<NamespaceUri> GetImportedSchemaNamespaces();
        public abstract INamespaceResolver GetNamespaceResolver();

        // === Auto-generated stubs (StubGenerator Phase 3.1f) ===
        // Upstream returns getConfiguration().getOptimizerOptions(); the NIE stub broke static-parameter
        // evaluation in fn:transform (xsl:param static='yes').
        public virtual OptimizerOptions GetOptimizerOptions() => config.GetOptimizerOptions();

        /// <summary>
        /// Interface defining a callback for handling warnings
        /// </summary>
        // IWarningHandler interface->delegate.
        public delegate void IWarningHandler(string message, string errorCode, ILocation location);
    }
}
