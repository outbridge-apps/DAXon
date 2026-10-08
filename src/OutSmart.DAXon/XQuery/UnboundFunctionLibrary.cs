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
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using System;
using System.Collections.Generic;
using System.Text;
namespace OutSmart.DAXon.XQuery
{
    public class UnboundFunctionLibrary : IFunctionLibrary
    {
        private IList<IUserFunctionResolvable> unboundFunctionReferences = new List<IUserFunctionResolvable>(20);
        private IList<QueryModule> correspondingQueryModule = new List<QueryModule>(20);
        private readonly IList<IList<string>> correspondingReasons = new List<IList<string>>();
        private bool resolving = false;
        public UnboundFunctionLibrary()
        {
        }

        public virtual Expression Bind(SymbolicName.F functionName, Expression[] arguments, Dictionary<StructuredQName, int> keywords, IStaticContext env, IList<string> reasons)
        {
            if (resolving)
            {
                return null;
            }

            if (reasons.Count > 0 && reasons[0].StartsWith("Cannot call the private XQuery function", StringComparison.Ordinal))
            {

                // The function call matched a private function in another module; don't attempt a late binding
                return null;
            }

            UnboundFunctionCallDetails details = new UnboundFunctionCallDetails(functionName, arguments, keywords, env);
            UserFunctionCall ufc = new UserFunctionCall(details);
            unboundFunctionReferences.Add(ufc);
            correspondingQueryModule.Add((QueryModule)env);
            correspondingReasons.Add(reasons);
            return ufc;
        }

        public virtual IFunctionItem GetFunctionItem(SymbolicName.F functionName, IStaticContext staticContext)
        {
            if (resolving)
            {
                return null;
            }

            XQueryFunctionLibrary.UnresolvedCallable uc = new XQueryFunctionLibrary.UnresolvedCallable(functionName);
            unboundFunctionReferences.Add(uc);
            correspondingQueryModule.Add((QueryModule)staticContext);
            correspondingReasons.Add(new List<string>());
            CallableFunction fi = new CallableFunction(functionName, uc, AnyFunctionType.GetInstance());

            return fi;
        }

        public virtual bool IsAvailable(SymbolicName.F functionName, int languageLevel)
        {
            return false; // function-available() is not used in XQuery
        }

        public virtual void BindUnboundFunctionReferences(IXQueryFunctionBinder lib, Configuration config)
        {
            resolving = true;
            for (int i = 0; i < unboundFunctionReferences.Count; i++)
            {
                IUserFunctionResolvable @ref = unboundFunctionReferences[i];
                if (@ref is UserFunctionCall)
                {
                    UserFunctionCall ufc = (UserFunctionCall)@ref;
                    QueryModule containingModule = correspondingQueryModule[i];
                    if (containingModule == null)
                    {

                        // means we must have already been here
                        continue;
                    }

                    correspondingQueryModule[i] = null; // for garbage collection purposes

                    // The original UserFunctionCall is effectively a dummy: we weren't able to find a function
                    // definition at the time. So we try again.
                    UnboundFunctionCallDetails details = ufc.UnboundCallDetails;
                    bool success = containingModule.LocalFunctionLibrary.BindUnboundFunctionCall(ufc, new List<string>());
                    if (!success)
                    {
                        foreach (QueryModule imp in containingModule.ImportedModules)
                        {
                            success = imp.LocalFunctionLibrary.BindUnboundFunctionCall(ufc, new List<string>());
                            if (success)
                            {
                                break;
                            }
                        }
                    }

                    if (success)
                    {
                    }
                    else
                    {
                        StringBuilder sb = new StringBuilder("Cannot find a " + details.arguments.Length + "-argument function named " + details.functionName.ComponentName.EQName + "()");
                        IList<string> reasons = correspondingReasons[i];
                        foreach (string reason in reasons)
                        {
                            sb.Append(". ").Append(reason);
                        }

                        if (reasons.Count == 0)
                        {
                            string supplementary = XPathParser.GetMissingFunctionExplanation(details.functionName.ComponentName, config);
                            if (supplementary != null)
                            {
                                sb.Append(". ").Append(supplementary);
                            }
                        }

                        XPathException err = new XPathException(sb.ToString(), "XPST0017", ufc.GetLocation());
                        err.SetIsStaticError(true);
                        throw err;
                    }
                }
                else if (@ref is XQueryFunctionLibrary.UnresolvedCallable)
                {
                    XQueryFunctionLibrary.UnresolvedCallable uc = (XQueryFunctionLibrary.UnresolvedCallable)@ref;
                    StructuredQName q = uc.GetFunctionName();
                    int arity = uc.GetArity();
                    QueryModule containingModule = correspondingQueryModule[i];
                    if (containingModule == null)
                    {

                        // means we must have already been here
                        continue;
                    }

                    correspondingQueryModule[i] = null;
                    bool found = false;
                    XQueryFunction fd = containingModule.LocalFunctionLibrary.GetDeclaration(q, arity);
                    if (fd != null)
                    {
                        fd.RegisterReference(uc);
                        found = true;
                    }
                    else
                    {
                        foreach (QueryModule imp in containingModule.ImportedModules)
                        {
                            fd = imp.LocalFunctionLibrary.GetDeclaration(q, arity);
                            if (fd != null)
                            {
                                fd.RegisterReference(uc);
                                found = true;
                                break;
                            }
                        }
                    }

                    if (!found)
                    {
                        string msg = "Cannot find a " + arity + "-argument function named " + q.EQName + "()";
                        if (!config.GetBooleanProperty(Feature<bool>.ALLOW_EXTERNAL_FUNCTIONS))
                        {
                            msg += ". Note: external function calls have been disabled";
                        }

                        throw new XPathException(msg).WithErrorCode("XPST0017").AsStaticError();
                    }
                }
            }
        }

        // all done
        public virtual IFunctionLibrary Copy()
        {
            UnboundFunctionLibrary qfl = new UnboundFunctionLibrary();
            qfl.unboundFunctionReferences = new List<IUserFunctionResolvable>(unboundFunctionReferences);
            qfl.correspondingQueryModule = new List<QueryModule>(correspondingQueryModule);
            qfl.resolving = resolving;
            return qfl;
        }

        // === Auto-generated stubs (StubGenerator Phase 3.1f) ===

        public class UnboundFunctionCallDetails
        {
            public SymbolicName.F functionName;
            public Expression[] arguments;
            public Dictionary<StructuredQName, int> keywords;
            public IStaticContext env;
            public UnboundFunctionCallDetails(SymbolicName.F functionName, Expression[] arguments, Dictionary<StructuredQName, int> keywords, IStaticContext env)
            {
                this.functionName = functionName;
                this.arguments = arguments;
                this.keywords = keywords;
                this.env = env;
            }
        }
    }
}