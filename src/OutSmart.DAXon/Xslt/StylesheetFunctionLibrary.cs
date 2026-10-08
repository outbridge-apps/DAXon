////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Internal.Collections;
using System.Collections.Generic;
namespace OutSmart.DAXon.Xslt
{
    internal sealed class StylesheetFunctionLibrary : IFunctionLibrary
    {
        private readonly StylesheetPackage pack;
        private readonly bool overrideExtensionFunction;
        private Dictionary<StructuredQName, IList<Component>> functionIndex;
        public StylesheetFunctionLibrary(StylesheetPackage sheet, bool overrideExtensionFunction)
        {
            this.pack = sheet;
            this.overrideExtensionFunction = overrideExtensionFunction;
        }

        public bool IsOverrideExtensionFunction()
        {
            return overrideExtensionFunction;
        }

        public StylesheetPackage GetStylesheetPackage()
        {
            return pack;
        }

        public Expression Bind(SymbolicName.F functionName, Expression[] staticArgs, Dictionary<StructuredQName, int> keywords, IStaticContext env, IList<string> reasons)
        {
            Component c = GetFunction(functionName.ComponentName, staticArgs.Length);
            if (c == null)
            {
                return null;
            }

            UserFunction fn = (UserFunction)c.GetActor();
            fn.IncrementReferenceCount();
            if (fn.IsOverrideExtensionFunction() != this.overrideExtensionFunction)
            {
                return null;
            }

            UserFunctionCall fc = new UserFunctionCall();
            fc.SetFunction(fn);
            fc.SetFunctionName(fn.GetFunctionName());
            int maxArity = fn.GetArity();
            if (staticArgs.Length == maxArity && (keywords == null || keywords.Count == 0))
            {
                fc.Arguments = staticArgs;
            }
            else
            {
                Expression[] expandedArgs = UserFunction.MakeExpandedArgumentArray(staticArgs, keywords, fn);
                fc.Arguments = expandedArgs;
            }

            if (env is ExpressionContext)
            {

                // compile-time binding of a static function call in XSLT
                PrincipalStylesheetModule psm = ((ExpressionContext)env).GetStyleElement().GetCompilation().GetPrincipalStylesheetModule();
                ExpressionVisitor visitor = ExpressionVisitor.Make(env);
                psm.AddFixupAction(() =>
                {
                    if (fc.GetFunction() == null)
                    {
                        Component target = psm.GetComponent(fc.GetSymbolicName());
                        UserFunction fn1 = (UserFunction)target.GetActor();
                        if (fn1 != null)
                        {
                            fc.AllocateArgumentEvaluators();
                            fc.SetStaticType(fn1.ResultType);
                        }
                        else
                        {
                            XPathException err = new XPathException("There is no available function named " + fc.DisplayName + " with " + fc.GetArity() + " arguments", "XPST0017");
                            err.SetLocator(fc.GetLocation());
                            throw err;
                        }
                    }
                });
            }
            else
            {
            }

            return fc;
        }

        // must be a call within xsl:evaluate
        private void BuildFunctionIndex()
        {
            Dictionary<SymbolicName, Component> allComponents = pack.ComponentIndex;
            functionIndex = new Dictionary<StructuredQName, IList<Component>>();
            foreach (KeyValuePair<SymbolicName, Component> entry in allComponents)
            {
                if (entry.Value.ComponentKind == StandardNames.XSL_FUNCTION)
                {
                    StructuredQName functionName = entry.Key.ComponentName;
                    if (functionIndex.ContainsKey(functionName))
                    {
                        functionIndex.GetOrDefault(functionName).Add(entry.Value);
                    }
                    else
                    {
                        IList<Component> functionList = new List<Component>();
                        functionList.Add(entry.Value);
                        functionIndex[functionName] = functionList;
                    }
                }
            }
        }

        private Component GetFunction(StructuredQName name, int actualArgs)
        {
            if (functionIndex == null)
            {
                BuildFunctionIndex();
            }

            IList<Component> candidates = functionIndex.GetOrDefault(name);
            if (candidates == null)
            {
                return null;
            }

            foreach (Component c in candidates)
            {
                UserFunction fn = (UserFunction)c.GetActor();
                if (fn.GetMinimumArity() <= actualArgs && fn.GetArity() >= actualArgs)
                {
                    return c;
                }
            }

            return null;
        }

        public IFunctionItem GetFunctionItem(SymbolicName.F functionName, IStaticContext staticContext)
        {
            return pack.GetFunction(functionName);
        }

        public bool IsAvailable(SymbolicName.F functionName, int languageLevel)
        {
            return pack.GetFunction(functionName) != null;
        }

        public IFunctionLibrary Copy()
        {
            return this;
        }

        // === Auto-generated stubs (StubGenerator Phase 3.1f) ===
    }
}
