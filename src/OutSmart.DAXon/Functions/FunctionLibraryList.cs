////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.XQuery;
using OutSmart.DAXon.Transformation;
using System.Collections.Generic;
namespace OutSmart.DAXon.Functions
{
    public class FunctionLibraryList : IFunctionLibrary, IXQueryFunctionBinder
    {
        internal IList<IFunctionLibrary> libraryList = new List<IFunctionLibrary>(8);   // LibraryList is the public spelling

        // PHASE7_FLL_INDEXER
        public IFunctionLibrary this[int n] { get { return Get(n); } }

        public virtual IList<IFunctionLibrary> LibraryList => libraryList;
        public FunctionLibraryList()
        {
        }

        public virtual int AddFunctionLibrary(IFunctionLibrary lib)
        {
            libraryList.Add(lib);
            return libraryList.Count - 1;
        }

        public virtual IFunctionLibrary Get(int n)
        {
            return libraryList[n];
        }

        public virtual IFunctionItem GetFunctionItem(SymbolicName.F functionName, IStaticContext staticContext)
        {
            foreach (IFunctionLibrary lib in libraryList)
            {
                IFunctionItem fi = lib.GetFunctionItem(functionName, staticContext);
                if (fi != null)
                {
                    return fi;
                }
            }

            return null;
        }

        public virtual bool IsAvailable(SymbolicName.F functionName, int languageLevel)
        {
            foreach (IFunctionLibrary lib in libraryList)
            {
                if (lib.IsAvailable(functionName, languageLevel))
                {
                    return true;
                }
            }

            return false;
        }

        public virtual Expression Bind(SymbolicName.F functionName, Expression[] staticArgs, Dictionary<StructuredQName, int> keywords, IStaticContext env, IList<string> reasons)
        {
            bool debug = env.GetConfiguration().GetBooleanProperty(Feature<bool>.TRACE_EXTERNAL_FUNCTIONS) && !NamespaceUri.IsReserved(functionName.ComponentName.GetNamespaceUri());
            Logger err = env.GetConfiguration().Logger;
            if (debug)
            {
                err.Info("Looking for function " + functionName.ComponentName.EQName + "#" + functionName.GetArity());
            }

            foreach (IFunctionLibrary lib in libraryList)
            {
                if (debug)
                {
                    err.Info("Trying " + lib.GetType().FullName);
                }

                Expression func = lib.Bind(functionName, staticArgs, keywords, env, reasons);
                if (func != null)
                {
                    return func;
                }
            }

            if (debug)
            {
                err.Info("Function " + functionName.ComponentName.EQName + " not found!");
            }

            return null;
        }

        public virtual XQueryFunction GetDeclaration(StructuredQName functionName, int staticArgs)
        {
            foreach (IFunctionLibrary lib in libraryList)
            {
                if (lib is IXQueryFunctionBinder)
                {
                    XQueryFunction func = ((IXQueryFunctionBinder)lib).GetDeclaration(functionName, staticArgs);
                    if (func != null)
                    {
                        return func;
                    }
                }
            }

            return null;
        }

        public virtual bool BindUnboundFunctionCall(UserFunctionCall call, IList<string> reasons)
        {
            foreach (IFunctionLibrary lib in libraryList)
            {
                if (lib is IXQueryFunctionBinder)
                {
                    bool found = ((IXQueryFunctionBinder)lib).BindUnboundFunctionCall(call, reasons);
                    if (found)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public virtual IFunctionLibrary Copy()
        {
            FunctionLibraryList fll = new FunctionLibraryList();
            fll.libraryList = EmptyFunctionLibraryList(libraryList.Count);
            for (int i = 0; i < libraryList.Count; i++)
            {
                fll.libraryList.Add(libraryList[i].Copy());
            }

            return fll;
        }

        private static List<IFunctionLibrary> EmptyFunctionLibraryList(int allocated)
        {

            // Separate method for C# type inference
            return new List<IFunctionLibrary>(allocated);
        }

        // === Auto-generated stubs (StubGenerator Phase 3.1f) ===
    }
}