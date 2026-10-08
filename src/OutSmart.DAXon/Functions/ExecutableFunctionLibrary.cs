////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Internal.Collections;
using System.Collections.Generic;
namespace OutSmart.DAXon.Functions
{
    public class ExecutableFunctionLibrary : IFunctionLibrary
    {
        private readonly Configuration config;
        private Dictionary<SymbolicName, UserFunction> functions = new Dictionary<SymbolicName, UserFunction>(20);

        public virtual IEnumerable<UserFunction> AllFunctions => functions.Values;
        public ExecutableFunctionLibrary(Configuration config)
        {
            this.config = config;
        }

        public virtual void AddFunction(UserFunction fn)
        {
            functions[fn.GetSymbolicName()] = fn;
        }

        public virtual Expression Bind(SymbolicName.F functionName, Expression[] staticArgs, Dictionary<StructuredQName, int> keywords, IStaticContext env, IList<string> reasons)
        {
            UserFunction fn = functions.GetOrDefault(functionName);
            if (fn == null)
            {
                return null;
            }

            UserFunctionCall fc = new UserFunctionCall();
            fc.SetFunctionName(functionName.ComponentName);
            fc.Arguments = staticArgs;
            fc.SetFunction(fn);
            fc.SetStaticType(fn.ResultType);
            return fc;
        }

        public virtual IFunctionItem GetFunctionItem(SymbolicName.F functionName, IStaticContext staticContext)
        {
            UserFunction fn = functions.GetOrDefault(functionName);
            if (fn != null && fn.IsUpdating())
            {
                throw new XPathException("Cannot bind a function item to an updating function");
            }

            return fn;
        }

        public virtual bool IsAvailable(SymbolicName.F functionName, int languageLevel)
        {
            return functions.GetOrDefault(functionName) != null;
        }

        public virtual IFunctionLibrary Copy()
        {
            ExecutableFunctionLibrary efl = new ExecutableFunctionLibrary(config);
            efl.functions = new Dictionary<SymbolicName, UserFunction>(functions);
            return efl;
        }

        // === Auto-generated stubs (StubGenerator Phase 3.1f) ===
    }
}