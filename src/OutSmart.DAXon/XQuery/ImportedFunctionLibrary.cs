////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using System.Collections.Generic;
namespace OutSmart.DAXon.XQuery
{
    public class ImportedFunctionLibrary : IFunctionLibrary, IXQueryFunctionBinder
    {
        private QueryModule importingModule;
        private readonly XQueryFunctionLibrary baseLibrary;
        private readonly HashSet<NamespaceUri> namespaces = new HashSet<NamespaceUri>();
        public ImportedFunctionLibrary(QueryModule importingModule, XQueryFunctionLibrary baseLibrary)
        {
            this.importingModule = importingModule;
            this.baseLibrary = baseLibrary;
        }

        public virtual void AddImportedNamespace(NamespaceUri @namespace)
        {
            namespaces.Add(@namespace);
        }

        public virtual Expression Bind(SymbolicName.F symbolicName, Expression[] staticArgs, Dictionary<StructuredQName, int> keywords, IStaticContext env, IList<string> reasons)
        {
            StructuredQName functionName = symbolicName.ComponentName;
            NamespaceUri uri = functionName.GetNamespaceUri();
            RetainedStaticContext rsc = new RetainedStaticContext(env);
            foreach (Expression arg in staticArgs)
            {
                if (arg.LocalRetainedStaticContext == null)
                {
                    arg.SetRetainedStaticContext(rsc);
                }
            }

            if (namespaces.Contains(uri))
            {
                return baseLibrary.Bind(symbolicName, staticArgs, keywords, env, reasons);
            }
            else
            {
                return null;
            }
        }

        public virtual XQueryFunction GetDeclaration(StructuredQName functionName, int staticArgs)
        {
            NamespaceUri uri = functionName.GetNamespaceUri();
            if (namespaces.Contains(uri))
            {
                return baseLibrary.GetDeclaration(functionName, staticArgs);
            }
            else
            {
                return null;
            }
        }

        public virtual bool BindUnboundFunctionCall(UserFunctionCall call, IList<string> reasons)
        {
            return baseLibrary.BindUnboundFunctionCall(call, reasons);
        }

        public virtual IFunctionLibrary Copy()
        {
            ImportedFunctionLibrary lib = new ImportedFunctionLibrary(importingModule, baseLibrary);
            foreach (NamespaceUri ns in namespaces)
            {
                lib.AddImportedNamespace(ns);
            }

            return lib;
        }

        public virtual void SetImportingModule(QueryModule importingModule)
        {
            this.importingModule = importingModule;
        }

        public virtual IFunctionItem GetFunctionItem(SymbolicName.F functionName, IStaticContext staticContext)
        {
            if (namespaces.Contains(functionName.ComponentName.GetNamespaceUri()))
            {
                return baseLibrary.GetFunctionItem(functionName, staticContext);
            }
            else
            {
                return null;
            }
        }

        public virtual bool IsAvailable(SymbolicName.F functionName, int languageLevel)
        {
            return namespaces.Contains(functionName.ComponentName.GetNamespaceUri()) && baseLibrary.IsAvailable(functionName, languageLevel);
        }

        // === Auto-generated stubs (StubGenerator Phase 3.1f) ===
    }
}