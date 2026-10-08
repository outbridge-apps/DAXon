////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Accumulators;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Serialization;
using OutSmart.DAXon.Xslt;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation.Rules;
using OutSmart.DAXon.Internal.Net;
using OutSmart.DAXon.Internal.Collections;
using System.Collections.Generic;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Transformation;

namespace OutSmart.DAXon.Core
{
    public class PreparedStylesheet : Executable
    {
        private Dictionary<URI, PreparedStylesheet> nextStylesheetCache;
        private RuleManager ruleManager;
        private Dictionary<StructuredQName, NamedTemplate> namedTemplateTable;
        private Dictionary<SymbolicName, Component> componentIndex;
        private readonly StructuredQName defaultInitialTemplate;
        private readonly StructuredQName defaultInitialMode;
        private readonly GlobalParameterSet compileTimeParams;
        private readonly IOutputURIResolver outputURIResolver;

        public virtual GlobalParameterSet CompileTimeParams => compileTimeParams;

        public virtual StructuredQName DefaultInitialTemplateName => defaultInitialTemplate;

        public virtual SerializationProperties DeclaredSerializationProperties
        {
            get
            {
                SerializationProperties details = PrimarySerializationProperties;
                return new SerializationProperties(new Properties(details.GetProperties()), GetCharacterMapIndex());
            }
        }
        public PreparedStylesheet(Compilation compilation) : base(compilation.GetConfiguration())
        {
            CompilerInfo compilerInfo = compilation.GetCompilerInfo();
            SetHostLanguage(HostLanguage.XSLT);
            if (compilerInfo.IsSchemaAware())
            {
                int localLic = compilation.GetPackageData().LocalLicenseId;
                GetConfiguration().CheckLicensedFeature(Configuration.LicenseFeature.ENTERPRISE_XSLT, "schema-aware XSLT", localLic);
                schemaAware = true;
            }

            defaultInitialMode = compilerInfo.DefaultInitialMode;
            defaultInitialTemplate = compilerInfo.DefaultInitialTemplate;
            compileTimeParams = compilation.Parameters;
            outputURIResolver = compilerInfo.OutputURIResolver;
        }

        public virtual XsltController NewController()
        {
            Configuration config = GetConfiguration();
            XsltController c = new XsltController(config, this);
            c.SetOutputURIResolver(outputURIResolver);
            if (defaultInitialMode != null)
            {
                try
                {
                    c.SetInitialMode(defaultInitialMode);
                }
                catch (XPathException e)
                {
                }
            }

            return c;
        }

        public override void CheckSuppliedParameters(GlobalParameterSet @params)
        {
            foreach (KeyValuePair<StructuredQName, GlobalParam> entry in GlobalParameters)
            {
                if (entry.Value.IsRequiredParam())
                {
                    StructuredQName req = entry.Key;
                    if (CompileTimeParams[req] == null && (@params == null || @params[req] == null))
                    {
                        throw new XPathException("No value supplied for required parameter " + req.DisplayName).WithErrorCode(GetHostLanguage() == HostLanguage.XQUERY ? "XPDY0002" : "XTDE0050");
                    }
                }
            }

            foreach (StructuredQName name in @params.Keys)
            {
                GlobalParam decl = GetGlobalParameter(name);
                if (decl != null && decl.IsStatic())
                {
                    throw new XPathException("Parameter $" + name.DisplayName + " cannot be supplied dynamically because it is declared as static");
                }

                if (compileTimeParams.ContainsKey(name))
                {
                    throw new XPathException("Parameter $" + name.DisplayName + " cannot be supplied dynamically because a value was already supplied at compile time");
                }
            }

            foreach (StructuredQName name in compileTimeParams.Keys)
            {
                @params.Put(name, compileTimeParams[name]);
            }
        }

        public StylesheetPackage GetTopLevelPackage()
        {
            return (StylesheetPackage)base.TopLevelPackage;
        }

        public virtual void SetRuleManager(RuleManager rm)
        {
            ruleManager = rm;
        }

        public virtual RuleManager GetRuleManager()
        {
            return ruleManager;
        }

        public virtual void PutNamedTemplate(StructuredQName templateName, NamedTemplate template)
        {
            if (namedTemplateTable == null)
            {
                namedTemplateTable = new Dictionary<StructuredQName, NamedTemplate>(32);
            }

            namedTemplateTable[templateName] = template;
        }

        public virtual void SetComponentIndex(Dictionary<SymbolicName, Component> index)
        {
            componentIndex = index;
        }

        public virtual Component GetComponent(SymbolicName name)
        {
            return componentIndex.GetOrDefault(name);
        }

        public virtual bool IsEligibleInitialMode(Component.M component)
        {
            if (component == null)
            {
                return false;
            }


            // Rules 1 and 4
            if (component.GetVisibility() == Visibility.PUBLIC || component.GetVisibility() == Visibility.FINAL)
            {
                return true;
            }


            // Rule 2
            if (component.GetActor().IsUnnamedMode())
            {
                return true;
            }


            // Rule 3
            StylesheetPackage top = GetTopLevelPackage();
            if (component.GetActor().ModeName.Equals(top.DefaultMode))
            {
                return true;
            }


            // Rule 5 (but see also bug 30405)
            if (!top.IsDeclaredModes() && !component.GetActor().IsEmpty() && (component.GetVisibilityProvenance() == VisibilityProvenance.DEFAULTED || component.GetVisibility() != Visibility.PRIVATE))
            {
                return true;
            }

            return false;
        }

        // Rule 2
        // Rule 3
        public virtual void ExplainNamedTemplates(ExpressionPresenter presenter)
        {
            presenter.StartElement("namedTemplates");
            if (namedTemplateTable != null)
            {
                foreach (NamedTemplate t in namedTemplateTable.Values)
                {
                    presenter.StartElement("template");
                    presenter.EmitAttribute("name", t.TemplateName.DisplayName);
                    presenter.EmitAttribute("line", t.GetLineNumber() + "");
                    presenter.EmitAttribute("module", t.GetSystemId());
                    if (t.GetBody() != null)
                    {
                        t.GetBody().Export(presenter);
                    }

                    presenter.EndElement();
                }
            }

            presenter.EndElement();
        }

        public virtual PreparedStylesheet GetCachedStylesheet(string href, string baseURI)
        {
            URI abs = null;
            try
            {
                abs = ResolveURI.MakeAbsolute(href, baseURI);
            }
            catch (URISyntaxException err)
            {
            }

            PreparedStylesheet result = null;
            if (abs != null && nextStylesheetCache != null)
            {
                result = nextStylesheetCache.GetOrDefault(abs);
            }

            return result;
        }

        // Rule 2
        // Rule 3
        //
        public virtual void PutCachedStylesheet(string href, string baseURI, PreparedStylesheet pss)
        {
            URI abs = null;
            try
            {
                abs = ResolveURI.MakeAbsolute(href, baseURI);
            }
            catch (URISyntaxException err)
            {
            }

            if (abs != null)
            {
                if (nextStylesheetCache == null)
                {
                    nextStylesheetCache = new Dictionary<URI, PreparedStylesheet>(4);
                }

                nextStylesheetCache[abs] = pss;
            }
        }

        // Rule 2
        // Rule 3
        //
        //
        public virtual void Explain(ExpressionPresenter presenter)
        {
            presenter.StartElement("stylesheet");
            presenter.Namespace("fn", NamespaceUri.FN);
            presenter.Namespace("xs", NamespaceUri.SCHEMA);
            ExplainGlobalVariables(presenter);
            ruleManager.ExplainTemplateRules(presenter);
            ExplainNamedTemplates(presenter);
            presenter.StartElement("accumulators");
            foreach (Accumulator acc in GetTopLevelPackage().AccumulatorRegistry.AllAccumulators)
            {
                acc.Export(presenter);
            }

            presenter.EndElement();
            FunctionLibraryList libList = FunctionLibrary;
            IList<IFunctionLibrary> libraryList = libList.LibraryList;
            presenter.StartElement("functions");
            foreach (IFunctionLibrary lib in libraryList)
            {
                if (lib is ExecutableFunctionLibrary)
                {
                    foreach (UserFunction func in ((ExecutableFunctionLibrary)lib).AllFunctions)
                    {
                        func.Export(presenter);
                    }
                }
            }

            presenter.EndElement();
            presenter.EndElement();
            presenter.Dispose();
        }
    }
}
