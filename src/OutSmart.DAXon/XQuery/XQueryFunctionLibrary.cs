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
using OutSmart.DAXon.Functions.HigherOrder;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System.Collections.Generic;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.XQuery
{
    public class XQueryFunctionLibrary : IFunctionLibrary, IXQueryFunctionBinder
    {
        private Configuration config;
        private Dictionary<SymbolicName, XQueryFunction> functions = new Dictionary<SymbolicName, XQueryFunction>(20);
        private Dictionary<StructuredQName, IList<XQueryFunction>> functionsByName = new Dictionary<StructuredQName, IList<XQueryFunction>>(20);

        public virtual IEnumerable<XQueryFunction> FunctionDefinitions => functions.Values;
        public XQueryFunctionLibrary(Configuration config)
        {
            this.config = config;
        }

        public virtual void SetConfiguration(Configuration config)
        {
            this.config = config;
        }

        public virtual Configuration GetConfiguration()
        {
            return config;
        }

        public virtual void DeclareFunction(XQueryFunction function)
        {
            SymbolicName keyObj = function.IdentificationKey;

            // Test if the arity range of this function overlaps the arity range of another function
            StructuredQName functionName = function.GetFunctionName();
            IList<XQueryFunction> existingFunctions = functionsByName.ComputeIfAbsent(functionName, (k) => new List<XQueryFunction>(2));
            foreach (XQueryFunction existing in existingFunctions)
            {
                if (existing == function)
                {
                    return;
                }

                if (HasOverlappingArity(function, existing))
                {
                    throw new XPathException("Conflicting definition of function " + function.DisplayName + " (see line " + existing.GetLineNumber() + " in " + existing.GetSystemId() + ')').WithErrorCode("XQST0034").AsStaticError().WithLocation(function);
                }
            }

            functions[keyObj] = function;
            existingFunctions.Add(function);
        }

        private static bool HasOverlappingArity(XQueryFunction f1, XQueryFunction f2)
        {

            // From https://stackoverflow.com/questions/3269434,
            // [x1:x2] overlaps [y1:y2] === x1 <= y2 && y1 <= x2
            return f1.GetMinimumArity() <= f2.NumberOfParameters && f2.GetMinimumArity() <= f1.NumberOfParameters;
        }

        public virtual IFunctionItem GetFunctionItem(SymbolicName.F functionName, IStaticContext staticContext)
        {
            XQueryFunction fd = GetDeclaration(functionName.ComponentName, functionName.GetArity());
            if (fd != null)
            {
                if (fd.IsPrivate() && !fd.GetSystemId().Equals(staticContext.StaticBaseURI))
                {
                    throw new XPathException("Cannot call the private function " + functionName.ComponentName.DisplayName + " from outside its module", "XPST0017");
                }

                UserFunction fn = fd.GetUserFunction();

                //            IFunctionItemType type = new SpecificFunctionType(
                if (fn == null)
                {

                    // not yet compiled: create a dummy
                    UserFunction uf = new UserFunction();
                    uf.SetFunctionName(functionName.ComponentName);
                    uf.ResultType = fd.ResultType;
                    uf.SetParameterDefinitions(fd.GetParameterDefinitions());
                    UserFunctionReference @ref = new UserFunctionReference(uf, functionName);
                    fd.RegisterReference(@ref);
                    return new UnresolvedXQueryFunctionItem(fd, functionName, @ref);
                }
                else if (functionName.GetArity() == fd.NumberOfParameters)
                {

                    // all arguments supplied
                    return fn;
                }
                else
                {

                    // return a reference to a reduced-arity version in which some of the arguments are defaulted
                    ICallable callable = new ReducedArityCallable(fd, fn);
                    SequenceType[] argTypes = new SequenceType[functionName.GetArity()];
                    for (int i = 0; i < functionName.GetArity(); i++)
                    {
                        argTypes[i] = fd.ArgumentTypes[i];
                    }

                    SpecificFunctionType functionType = new SpecificFunctionType(argTypes, fd.ResultType);
                    return new CallableFunction(functionName, callable, functionType);
                }
            }
            else
            {
                return null;
            }
        }

        public virtual bool IsAvailable(SymbolicName.F functionName, int languageLevel)
        {
            return functions.GetOrDefault(functionName) != null;
        }

        //}
        public virtual Expression Bind(SymbolicName.F functionName, Expression[] arguments, Dictionary<StructuredQName, int> keywords, IStaticContext env, IList<string> reasons)
        {
            XQueryFunction fd = GetDeclaration(functionName.ComponentName, arguments.Length);
            if (fd != null)
            {
                if (fd.IsPrivate() && fd.GetStaticContext() != env)
                {
                    reasons.Add("Cannot call the private XQuery function " + functionName.ComponentName.DisplayName + " from outside its module");
                    return null;
                }

                UserFunctionCall ufc = new UserFunctionCall();
                ufc.SetFunctionName(fd.GetFunctionName());
                int maxArity = fd.NumberOfParameters;
                if (arguments.Length == maxArity && (keywords == null || keywords.Count == 0))
                {
                    ufc.Arguments = arguments;
                }
                else
                {
                    Expression[] expandedArgs = UserFunction.MakeExpandedArgumentArray(arguments, keywords, fd);
                    ufc.Arguments = expandedArgs;
                    foreach (Expression e in expandedArgs)
                    {
                        ufc.AdoptChildExpression(e);
                    }
                }

                ufc.SetStaticType(fd.ResultType);
                UserFunction fn = fd.GetUserFunction();
                if (fn == null)
                {

                    // not yet compiled
                    fd.RegisterReference(ufc);
                }
                else
                {
                    ufc.SetFunction(fn);
                }

                return ufc;
            }
            else
            {
                return null;
            }
        }

        //}
        public virtual XQueryFunction GetDeclaration(StructuredQName functionName, int staticArgs)
        {
            IList<XQueryFunction> homonyms = functionsByName.GetOrDefault(functionName);
            if (homonyms != null)
            {
                foreach (XQueryFunction f in homonyms)
                {
                    if (f.GetMinimumArity() <= staticArgs && f.NumberOfParameters >= staticArgs)
                    {
                        return f;
                    }
                }
            }

            return null;
        }

        public virtual bool BindUnboundFunctionCall(UserFunctionCall ufc, IList<string> reasons)
        {
            UnboundFunctionLibrary.UnboundFunctionCallDetails details = ufc.UnboundCallDetails;
            StructuredQName functionName = details.functionName.ComponentName;
            Expression[] arguments = details.arguments;
            Dictionary<StructuredQName, int> keywords = details.keywords;
            XQueryFunction fd = GetDeclaration(functionName, arguments.Length);
            if (fd != null)
            {
                if (fd.IsPrivate() && fd.GetStaticContext() != details.env)
                {
                    reasons.Add("Cannot call the private XQuery function " + functionName.DisplayName + " from outside its module");
                    return false;
                }

                ufc.SetFunctionName(fd.GetFunctionName());
                int maxArity = fd.NumberOfParameters;
                if (arguments.Length == maxArity && (details.keywords == null || details.keywords.Count == 0))
                {
                    ufc.Arguments = arguments;
                }
                else
                {

                    // 4.0: handle keyword arguments and default arguments
                    Expression[] expandedArgs = ArrayTools.CopyOf(arguments, maxArity);

                    // If there are keyword arguments, reposition them to the correct position in the argument sequence
                    if (keywords != null)
                    {
                        int positionalArgs = arguments.Length - keywords.Count;
                        foreach (KeyValuePair<StructuredQName, int> entry in keywords)
                        {
                            StructuredQName key = entry.Key;
                            int argPos = entry.Value;
                            int paramPos = fd.GetPositionOfParameter(key);
                            if (paramPos < 0)
                            {
                                throw new UncheckedXPathException("Keyword " + key + " does not match the name of any declared parameter", "XPST0142");
                            }

                            if (paramPos < positionalArgs)
                            {
                                throw new UncheckedXPathException("Parameter " + key + " is supplied both by position and by keyword", "XPST0141");
                            }

                            Expression supplied = arguments[paramPos];
                            expandedArgs[argPos] = null;
                            expandedArgs[paramPos] = supplied;
                        }
                    }

                    for (int a = 0; a < maxArity; a++)
                    {
                        if (expandedArgs[a] == null)
                        {
                            Expression expr = fd.GetParameterDefinitions()[a].DefaultValueExpression;
                            expandedArgs[a] = expr.Copy(new RebindingMap());
                        }
                    }

                    ufc.Arguments = expandedArgs;
                }

                ufc.SetStaticType(fd.ResultType);
                UserFunction fn = fd.GetUserFunction();
                if (fn == null)
                {

                    // not yet compiled
                    fd.RegisterReference(ufc);
                }
                else
                {
                    ufc.SetFunction(fn);
                }

                return true;
            }
            else
            {
                return false;
            }
        }

        //}
        public virtual XQueryFunction GetDeclarationByKey(SymbolicName functionKey)
        {
            return functions.GetOrDefault(functionKey);
        }

        public virtual void FixupGlobalFunctions(QueryModule env)
        {
            ExpressionVisitor visitor = ExpressionVisitor.Make(env);
            foreach (XQueryFunction fn in functions.Values)
            {
                fn.Compile();
            }

            foreach (XQueryFunction fn in functions.Values)
            {
                fn.CheckReferences(visitor);
            }
        }

        public virtual void OptimizeGlobalFunctions(QueryModule topModule)
        {
            foreach (XQueryFunction fn in functions.Values)
            {
                if (((QueryModule)fn.GetStaticContext()).TopLevelModule == topModule)
                {
                    fn.Optimize();
                }
            }
        }

        public virtual void ExplainGlobalFunctions(ExpressionPresenter @out)
        {
            foreach (XQueryFunction fn in functions.Values)
            {
                fn.Explain(@out);
            }
        }

        public virtual UserFunction GetUserDefinedFunction(NamespaceUri uri, string localName, int arity)
        {
            SymbolicName functionKey = new SymbolicName.F(new StructuredQName("", uri, localName), arity);
            XQueryFunction fd = functions.GetOrDefault(functionKey);
            if (fd == null)
            {
                return null;
            }

            return fd.GetUserFunction();
        }

        public virtual IFunctionLibrary Copy()
        {
            XQueryFunctionLibrary qfl = new XQueryFunctionLibrary(config);
            qfl.functions = new Dictionary<SymbolicName, XQueryFunction>(functions);
            return qfl;
        }

        internal sealed class UnresolvedCallable : IUserFunctionResolvable, ICallable
        {
            SymbolicName.F symbolicName;
            UserFunction function;
            public UnresolvedCallable(SymbolicName.F symbolicName)
            {
                this.symbolicName = symbolicName;
            }

            public StructuredQName GetFunctionName()
            {
                return symbolicName.ComponentName;
            }

            public int GetArity()
            {
                return symbolicName.GetArity();
            }

            //}
            public ISequence Call(IXPathContext context, ISequence[] arguments)
            {
                if (function == null)
                {
                    throw new XPathException("Forwards reference to XQuery function has not been resolved");
                }

                ISequence[] args = new ISequence[arguments.Length];
                for (int i = 0; i < arguments.Length; i++)
                {
                    args[i] = arguments[i].Materialize(); // TODO: is the copy needed?
                }

                return function.Call(context.NewCleanContext(), args);
            }

            //}
            public void SetFunction(UserFunction function)
            {
                this.function = function;
            }

            public UserFunction GetFunction()
            {
                return function;
            }
        }

        private sealed class ReducedArityCallable : ICallable
        {
            private readonly XQueryFunction declaredFunction;
            private readonly UserFunction userFunction;
            public ReducedArityCallable(XQueryFunction fd, UserFunction fn)
            {
                this.declaredFunction = fd;
                this.userFunction = fn;
            }

            public ISequence Call(IXPathContext context, ISequence[] arguments)
            {
                ISequence[] extendedArguments = ArrayTools.CopyOf(arguments, userFunction.GetArity());
                for (int i = arguments.Length; i < userFunction.GetArity(); i++)
                {

                    // Evaluate the default value expression for the omitted argument
                    extendedArguments[i] = declaredFunction.GetParameterDefinitions()[i].DefaultValueExpression.MakeElaborator().Eagerly().Evaluate(context);
                }

                return userFunction.Call(context, extendedArguments);
            }
        }
    }
}
