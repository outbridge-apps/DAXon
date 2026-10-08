////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.XQuery;
using OutSmart.DAXon.Serialization;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using System.Collections.Generic;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Expressions.Instructions
{
    public class Executable
    {
        // the Configuration options
        private Configuration config;
        // the top level package
        private PackageData topLevelPackage;
        // the set of packages making up this Executable
        private readonly IList<PackageData> packages = new List<PackageData>();
        // default output properties (for the unnamed output format)
        private Properties defaultOutputProperties;
        // table of character maps indexed by StructuredQName
        private CharacterMapIndex characterMapIndex;
        // hash table of query library modules indexed by module namespace
        private Dictionary<NamespaceUri, IList<QueryModule>> queryLibraryModules;
        // hash set of query module location hints that have been processed
        private HashSet<string> queryLocationHintsProcessed;
        // list of functions available in the static context
        private FunctionLibraryList functionLibrary;
        // flag to indicate whether the principal language is for example XSLT or XQuery
        private HostLanguage hostLanguage = HostLanguage.XSLT;
        // a list of required parameters, identified by the structured QName of their names
        private readonly Dictionary<StructuredQName, GlobalParam> globalParams = new Dictionary<StructuredQName, GlobalParam>();
        // Hash table of named (and unnamed) output declarations. This is assembled only
        // if there is a need for it: that @is, if there is a call on xsl:result-document
        // with a format attribute computed at run-time. The key is a StructuredQName object,
        // the value is a Properties object
        private Dictionary<StructuredQName, Properties> outputDeclarations;
        // a boolean, true if the executable represents a stylesheet that uses xsl:result-document
        private bool _createsSecondaryResult;
        // a boolean, indicates that the executable is schema-aware. This will true by default only
        // if it statically imports a schema. If the executable is not schema-aware, then
        // all input documents must be untyped.
        protected bool schemaAware;
        // Requirements for the initial context item
        private GlobalContextRequirement globalContextRequirement;

        public virtual PackageData TopLevelPackage
        {
            get => topLevelPackage; set
            {
                this.topLevelPackage = value;
            }
        }

        public virtual IEnumerable<PackageData> Packages => packages;

        public virtual FunctionLibraryList FunctionLibrary
        {
            get => functionLibrary; set
            {

                this.functionLibrary = value;
            }
        }

        public virtual SerializationProperties PrimarySerializationProperties
        {
            get
            {
                if (defaultOutputProperties == null)
                {
                    defaultOutputProperties = new Properties();
                }

                Properties props = defaultOutputProperties;
                return new SerializationProperties(props, GetCharacterMapIndex());
            }
        }

        public virtual IList<QueryModule> QueryLibraryModules
        {
            get
            {
                if (queryLibraryModules == null)
                {
                    return new List<QueryModule>();
                }
                else
                {
                    List<QueryModule> modules = new List<QueryModule>();
                    foreach (IList<QueryModule> queryModules in queryLibraryModules.Values)
                    {
                        modules.AddRange(queryModules);
                    }

                    return modules;
                }
            }
        }

        public virtual Dictionary<StructuredQName, GlobalParam> GlobalParameters => globalParams;

        public virtual GlobalContextRequirement GlobalContextRequirement
        {
            get => globalContextRequirement; set
            {
                globalContextRequirement = value;
            }
        }
        public Executable(Configuration config)
        {
            SetConfiguration(config);
        }

        public virtual void SetConfiguration(Configuration config)
        {
            this.config = config;
        }

        public virtual Configuration GetConfiguration()
        {
            return config;
        }

        public virtual void AddPackage(PackageData data)
        {
            packages.Add(data);
        }

        public virtual void SetHostLanguage(HostLanguage language)
        {
            hostLanguage = language;
        }

        public virtual HostLanguage GetHostLanguage()
        {
            return hostLanguage;
        }

        public virtual void SetCharacterMapIndex(CharacterMapIndex cmi)
        {
            characterMapIndex = cmi;
        }

        public virtual CharacterMapIndex GetCharacterMapIndex()
        {
            if (characterMapIndex == null)
            {
                characterMapIndex = new CharacterMapIndex();
            }

            return characterMapIndex;
        }

        public virtual void SetDefaultOutputProperties(Properties properties)
        {
            defaultOutputProperties = properties;
        }

        public virtual void SetOutputProperties(StructuredQName qName, Properties properties)
        {
            if (outputDeclarations == null)
            {
                outputDeclarations = new Dictionary<StructuredQName, Properties>(5);
            }

            outputDeclarations[qName] = properties;
        }

        public virtual Properties GetOutputProperties()
        {
            return new Properties(defaultOutputProperties);
        }

        public virtual Properties GetOutputProperties(StructuredQName qName)
        {
            if (outputDeclarations == null)
            {
                return null;
            }
            else
            {
                return outputDeclarations.GetOrDefault(qName);
            }
        }

        public virtual void AddQueryLibraryModule(QueryModule module)
        {
            if (queryLibraryModules == null)
            {
                queryLibraryModules = new Dictionary<NamespaceUri, IList<QueryModule>>(5);
            }

            NamespaceUri uri = module.ModuleNamespace;
            IList<QueryModule> existing = queryLibraryModules.GetOrDefault(uri);
            if (existing == null)
            {
                existing = new List<QueryModule>(5);
                existing.Add(module);
                queryLibraryModules[uri] = existing;
            }
            else if (!existing.Contains(module))
            {
                existing.Add(module);
            }
        }

        public virtual IList<QueryModule> GetQueryLibraryModules(NamespaceUri @namespace)
        {
            if (queryLibraryModules == null)
            {
                return null;
            }

            return queryLibraryModules.GetOrDefault(@namespace);
        }

        public virtual QueryModule GetQueryModuleWithSystemId(string systemId, QueryModule topModule)
        {
            if (systemId.Equals(topModule.GetSystemId()))
            {
                return topModule;
            }

            foreach (QueryModule sqc in QueryLibraryModules)
            {
                string uri = sqc.GetSystemId();
                if (uri != null && uri.Equals(systemId))
                {
                    return sqc;
                }
            }

            return null;
        }

        public virtual void AddQueryLocationHintProcessed(string uri)
        {
            if (queryLocationHintsProcessed == null)
            {
                queryLocationHintsProcessed = new HashSet<string>();
            }

            queryLocationHintsProcessed.Add(uri);
        }

        public virtual bool IsQueryLocationHintProcessed(string uri)
        {
            return queryLocationHintsProcessed != null && queryLocationHintsProcessed.Contains(uri);
        }

        public virtual void FixupQueryModules(QueryModule main)
        {

            // Bind any previously unbound variables (forwards references)
            main.BindUnboundVariables();
            if (queryLibraryModules != null)
            {
                foreach (IList<QueryModule> queryModules in queryLibraryModules.Values)
                {
                    foreach (QueryModule env in queryModules)
                    {
                        env.BindUnboundVariables();
                    }
                }
            }

            IList<GlobalVariable> varDefinitions = main.FixupGlobalVariables(main.GlobalStackFrameMap);
            main.BindUnboundFunctionCalls();
            if (queryLibraryModules != null)
            {
                foreach (IList<QueryModule> queryModules in queryLibraryModules.Values)
                {
                    foreach (QueryModule env in queryModules)
                    {
                        env.BindUnboundFunctionCalls();
                    }
                }
            }


            // Note: the checks for circularities between variables and functions have to happen
            // before functions are compiled and optimized, as the optimization can involve function
            // inlining which eliminates the circularities (tests K-InternalVariablesWith-17, errata8-002)
            main.CheckForCircularities(varDefinitions, main.GlobalFunctionLibrary);
            main.FixupGlobalFunctions();

            //                QueryModule module = (QueryModule) miter.next();
            //                module.lookForModuleCycles(new Stack<QueryModule>(), 1);
            main.TypeCheckGlobalVariables(varDefinitions);
            main.OptimizeGlobalFunctions();
        }

        public virtual void ExplainGlobalVariables(ExpressionPresenter presenter)
        {
            presenter.StartElement("globalVariables");
            foreach (PackageData pack in Packages)
            {
                foreach (GlobalVariable var in pack.GlobalVariableList)
                {
                    var.Export(presenter);
                }
            }

            presenter.EndElement();
        }

        public virtual void RegisterGlobalParameter(GlobalParam param)
        {
            globalParams[param.GetVariableQName()] = param;
        }

        public virtual GlobalParam GetGlobalParameter(StructuredQName name)
        {
            return globalParams.GetOrDefault(name);
        }

        public virtual void CheckSuppliedParameters(GlobalParameterSet @params)
        {
        }

        public virtual void SetCreatesSecondaryResult(bool flag)
        {
            _createsSecondaryResult = flag;
        }

        public virtual bool CreatesSecondaryResult()
        {
            return _createsSecondaryResult;
        }

        public virtual IItem CheckInitialContextItem(IItem contextItem, IXPathContext context)
        {
            if (globalContextRequirement == null)
            {
                return contextItem;
            }

            if (contextItem != null && globalContextRequirement.IsAbsentFocus())
            {
                throw new XPathException("The global context item is required to be absent", "XPDY0002");
            }

            TypeHierarchy th = config.GetTypeHierarchy();
            if (contextItem == null)
            {
                if (!globalContextRequirement.IsMayBeOmitted())
                {

                    // Bug 30173 allocates an error code
                    throw new XPathException("A global context item is required, but none has been supplied", "XTDE3086");
                }

                if (globalContextRequirement.DefaultValue != null)
                {

                    // XQuery only
                    try
                    {
                        contextItem = globalContextRequirement.DefaultValue.EvaluateItem(context);
                    }
                    catch (XPathException e)
                    {

                        // XPDY0002 here means there is no context item, which means the default value
                        // of the context item depends on the context item: a circularity.
                        if (e.HasErrorCode("XPDY0002"))
                        {
                            if (e.Message.Contains("last()") || e.Message.Contains("position()"))
                            {
                            }
                            else
                            {
                                e.SetErrorCode("XQDY0054");
                            }
                        }

                        throw;
                    }

                    if (contextItem == null)
                    {
                        throw new XPathException("The context item cannot be initialized to an empty sequence", "XPTY0004");
                    }

                    foreach (Types.ItemType type in globalContextRequirement.RequiredItemTypes)
                    {
                        if (!type.Matches(contextItem, th))
                        {
                            RoleDiagnostic role = new RoleDiagnostic(RoleDiagnostic.MISC, "defaulted global context item", 0);
                            string s = role.ComposeErrorMessage(type, contextItem, th);
                            throw new XPathException(s, "XPTY0004");
                        }
                    }
                }
            }
            else
            {
                foreach (Types.ItemType type in globalContextRequirement.RequiredItemTypes)
                {
                    if (!type.Matches(contextItem, config.GetTypeHierarchy()))
                    {
                        RoleDiagnostic role = new RoleDiagnostic(RoleDiagnostic.MISC, "supplied global context item", 0);
                        string s = role.ComposeErrorMessage(type, contextItem, th);
                        throw new XPathException(s, GetHostLanguage() == HostLanguage.XSLT ? "XTTE0590" : "XPTY0004");
                    }
                }
            }

            return contextItem;
        }

        public virtual void SetSchemaAware(bool aware)
        {

            //        if (aware) {
            //            config.checkLicensedFeature(Configuration.LicenseFeature.SCHEMA_VALIDATION,
            //        }
            schemaAware = aware;
        }

        public virtual bool IsSchemaAware()
        {
            return schemaAware;
        }
    }
}