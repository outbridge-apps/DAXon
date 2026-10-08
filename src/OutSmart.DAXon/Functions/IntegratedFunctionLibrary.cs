////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
namespace OutSmart.DAXon.Functions
{
    public class IntegratedFunctionLibrary : IFunctionLibrary
    {
        private Dictionary<StructuredQName, ExtensionFunctionDefinition> functions = new Dictionary<StructuredQName, ExtensionFunctionDefinition>();
        public virtual void RegisterFunction(ExtensionFunctionDefinition function)
        {
            functions[function.FunctionQName] = function;
        }

        public virtual Expression Bind(SymbolicName.F functionName, Expression[] staticArgs, Dictionary<StructuredQName, int> keywords, IStaticContext env, IList<string> reasons)
        {
            ExtensionFunctionDefinition defn = functions.GetOrDefault(functionName.ComponentName);
            if (defn == null)
            {
                return null;
            }

            if (keywords != null && keywords.Count > 0)
            {
                reasons.Add("Calls to external Java functions cannot use keyword arguments");
                return null;
            }

            return MakeFunctionCall(defn, staticArgs);
        }

        public static Expression MakeFunctionCall(ExtensionFunctionDefinition defn, Expression[] staticArgs)
        {
            ExtensionFunctionCall f = defn.MakeCallExpression();
            f.Definition = defn;
            IntegratedFunctionCall fc = new IntegratedFunctionCall(defn.FunctionQName, f);
            fc.Arguments = staticArgs;
            return fc;
        }

        public virtual IFunctionItem GetFunctionItem(SymbolicName.F functionName, IStaticContext staticContext)
        {
            ExtensionFunctionDefinition defn = functions.GetOrDefault(functionName.ComponentName);
            if (defn == null)
            {
                return null;
            }

            try
            {
                return defn.AsFunction(functionName.GetArity());
            }
            catch (Exception err)
            {
                throw new XPathException("Failed to create call to extension function " + functionName.ComponentName.DisplayName, (Exception)err);
            }
        }

        public virtual bool IsAvailable(SymbolicName.F functionName, int languageLevel)
        {
            ExtensionFunctionDefinition defn = functions.GetOrDefault(functionName.ComponentName);
            int arity = functionName.GetArity();
            return defn != null && defn.MaximumNumberOfArguments >= arity && defn.MinimumNumberOfArguments <= arity;
        }

        public virtual IFunctionLibrary Copy()
        {
            IntegratedFunctionLibrary lib = new IntegratedFunctionLibrary();

            // Type parameters needed for C#
            lib.functions = CopyHashMap(functions);
            return lib;
        }

        private Dictionary<StructuredQName, ExtensionFunctionDefinition> CopyHashMap(Dictionary<StructuredQName, ExtensionFunctionDefinition> functions)
        {

            // Separate method for C# type inference
            return new Dictionary<StructuredQName, ExtensionFunctionDefinition>(functions);
        }

        // === Auto-generated stubs (StubGenerator Phase 3.1f) ===
    }
}