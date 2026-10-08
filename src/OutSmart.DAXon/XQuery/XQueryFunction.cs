////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Functions.Registry;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Tracing;
using System;
using System.Collections.Generic;
using System.Linq;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Transformation;
namespace OutSmart.DAXon.XQuery
{
    /// <summary>
    /// A user-defined function in an XQuery module
    /// </summary>
    public class XQueryFunction : IDeclaration, ILocation, IFunctionDefinition
    {
        private StructuredQName functionName;
        private readonly IList<UserFunctionParameter> parameters;
        private Values.SequenceType resultType;
        private Expression body = null;
        private IList<IUserFunctionResolvable> references = new List<IUserFunctionResolvable>(10);
        private ILocation location;
        private UserFunction compiledFunction = null;
        private bool memoFunction;
        private INamespaceResolver namespaceResolver;
        private QueryModule staticContext;
        private bool updating = false;
        private AnnotationList annotations = AnnotationList.EMPTY;
        private int mandatoryParams = 0;

        public virtual Expression Body
        {
            get => body; set
            {
                this.body = value;
            }
        }

        public virtual string DisplayName => functionName.DisplayName;

        public virtual SymbolicName IdentificationKey => new SymbolicName.F(functionName, parameters.Count);

        public virtual Values.SequenceType ResultType
        {
            get => resultType; set
            {
                this.resultType = value;
            }
        }

        public virtual Values.SequenceType[] ArgumentTypes
        {
            get
            {
                Values.SequenceType[] types = new Values.SequenceType[parameters.Count];
                for (int i = 0; i < parameters.Count; i++)
                {
                    types[i] = parameters[i].GetRequiredType();
                }

                return types;
            }
        }

        public virtual int NumberOfParameters => parameters.Count;

        public virtual AnnotationList Annotations
        {
            get => annotations; set
            {
                this.annotations = value;
                if (compiledFunction != null)
                {
                    compiledFunction.SetAnnotations(value);
                }

                if (value.Includes(Annotation.UPDATING))
                {
                    SetUpdating(true);
                }
            }
        }
        public XQueryFunction()
        {
            parameters = new List<UserFunctionParameter>(8);
        }

        public virtual PackageData GetPackageData()
        {
            return staticContext.GetPackageData();
        }

        public virtual void SetFunctionName(StructuredQName name)
        {
            functionName = name;
        }

        public virtual void AddParameter(UserFunctionParameter param)
        {
            parameters.Add(param);
            if (param.DefaultValueExpression == null)
            {
                mandatoryParams++;
            }
        }

        public virtual void SetLocation(ILocation location)
        {
            this.location = location;
        }

        public virtual StructuredQName GetFunctionName()
        {
            return functionName;
        }

        public static SymbolicName GetIdentificationKey(StructuredQName qName, int arity)
        {
            return new SymbolicName.F(qName, arity);
        }

        public virtual void SetStaticContext(QueryModule env)
        {
            staticContext = env;
        }

        public virtual IStaticContext GetStaticContext()
        {
            return staticContext;
        }

        public virtual UserFunctionParameter[] GetParameterDefinitions()
        {
            UserFunctionParameter[] @params = new UserFunctionParameter[parameters.Count];
            return parameters.ToArray();
        }

        public virtual int GetPositionOfParameter(StructuredQName name)
        {
            int pos = 0;
            foreach (UserFunctionParameter p in parameters)
            {
                if (p.GetVariableQName().Equals(name))
                {
                    return pos;
                }

                pos++;
            }

            return -1;
        }

        public virtual StructuredQName GetParameterName(int i)
        {
            return parameters[i].GetVariableQName();
        }

        public virtual Expression GetDefaultValueExpression(int i)
        {
            return parameters[i].DefaultValueExpression;
        }

        public virtual int GetMinimumArity()
        {
            return mandatoryParams;
        }

        public virtual void RegisterReference(IUserFunctionResolvable ufc)
        {
            references.Add(ufc);
        }

        public virtual void SetMemoFunction(bool isMemoFunction)
        {
            memoFunction = isMemoFunction;
        }

        public virtual bool IsMemoFunction()
        {
            return memoFunction;
        }

        public virtual void SetUpdating(bool isUpdating)
        {
            this.updating = isUpdating;
        }

        public virtual bool IsUpdating()
        {
            return updating;
        }

        public virtual bool HasAnnotation(StructuredQName name)
        {
            return annotations.Includes(name);
        }

        public virtual bool IsPrivate()
        {
            return HasAnnotation(Annotation.PRIVATE);
        }

        public virtual void Compile()
        {
            Configuration config = staticContext.GetConfiguration();
            try
            {

                // If a query function is imported into several modules, then the compile()
                // method will be called once for each importing module. If the compiled
                // function already exists, then this is a repeat call, and the only thing
                // needed is to fix up references to the function from within the importing
                // module.
                if (compiledFunction == null)
                {
                    SlotManager map = config.MakeSlotManager();
                    UserFunctionParameter[] @params = GetParameterDefinitions();
                    for (int i = 0; i < @params.Length; i++)
                    {
                        @params[i].SetSlotNumber(i);
                        map.AllocateSlotNumber(@params[i].GetVariableQName(), @params[i]);
                    }


                    // type-check the body of the function
                    RetainedStaticContext rsc = null;
                    try
                    {
                        rsc = GetStaticContext().MakeRetainedStaticContext();
                        body.SetRetainedStaticContext(rsc);
                        ExpressionVisitor visitor = ExpressionVisitor.Make(staticContext);
                        body = body.Simplify().TypeCheck(visitor, ContextItemStaticInfo.ABSENT);

                        Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.FUNCTION_RESULT, functionName.DisplayName, 0);
                        body = config.GetTypeChecker(false).StaticTypeCheck(body, resultType, role, visitor);
                    }
                    catch (XPathException e)
                    {
                        e.MaybeSetLocation(this);
                        if (e.IsReportableStatically())
                        {
                            throw;
                        }
                        else
                        {
                            Expression newBody = new ErrorExpression(new XmlProcessingException(e));
                            ExpressionTool.CopyLocationInfo(body, newBody);
                            body = newBody;
                        }
                    }

                    compiledFunction = config.NewUserFunction(memoFunction, FunctionStreamability.UNCLASSIFIED);
                    compiledFunction.SetRetainedStaticContext(rsc);
                    compiledFunction.SetPackageData(staticContext.GetPackageData());
                    compiledFunction.SetBody(body);
                    compiledFunction.SetFunctionName(functionName);
                    compiledFunction.SetParameterDefinitions(@params);
                    compiledFunction.ResultType = ResultType;
                    compiledFunction.SetLineNumber(location.GetLineNumber());
                    compiledFunction.SetColumnNumber(location.GetColumnNumber());
                    compiledFunction.SetSystemId(location.GetSystemId());
                    compiledFunction.SetStackFrameMap(map);
                    compiledFunction.SetUpdating(updating);
                    compiledFunction.SetAnnotations(annotations);
                    if (staticContext.UserQueryContext.IsCompileWithTracing())
                    {
                        namespaceResolver = staticContext.GetNamespaceResolver();
                        staticContext.CodeInjector.Process(compiledFunction);
                        body = compiledFunction.GetBody();
                    }
                }


                // bind all references to this function to the UserFunction object
                FixupReferences();
            }
            catch (XPathException e)
            {
                e.MaybeSetLocation(this);
                throw;
            }
        }

        public virtual void Optimize()
        {
            body.CheckForUpdatingSubexpressions();
            if (updating)
            {
                if (ExpressionTool.IsNotAllowedInUpdatingContext(body))
                {
                    XPathException err = new XPathException("The body of an updating function must be an updating expression", "XUST0002");
                    err.SetLocator(body.GetLocation());
                    throw err;
                }
            }
            else
            {
                if (body.IsUpdatingExpression())
                {
                    XPathException err = new XPathException("The body of a non-updating function must be a non-updating expression", "XUST0001");
                    err.SetLocator(body.GetLocation());
                    throw err;
                }
            }

            ExpressionVisitor visitor = ExpressionVisitor.Make(staticContext);
            Configuration config = staticContext.GetConfiguration();
            Optimizer opt = visitor.ObtainOptimizer();
            int arity = parameters.Count;
            if (opt.IsOptionSet(OptimizerOptions.MISCELLANEOUS))
            {
                body = body.Optimize(visitor, ContextItemStaticInfo.ABSENT);
            }

            body.ParentExpression = null;
            if (opt.IsOptionSet(OptimizerOptions.LOOP_LIFTING))
            {
                body = LoopLifter.Process(body, visitor, ContextItemStaticInfo.ABSENT);
            }

            if (opt.IsOptionSet(OptimizerOptions.EXTRACT_GLOBALS))
            {
                IGlobalVariableManager manager = new AnonymousGlobalVariableManager(this);

                // Try to extract new global variables from the body of the function
                Expression b2 = opt.PromoteExpressionsToGlobal(body, manager, visitor);
                if (b2 != null)
                {
                    body = body.Optimize(visitor, ContextItemStaticInfo.ABSENT);
                }
            }


            // mark tail calls within the function body
            if (opt.GetOptimizerOptions().IsSet(OptimizerOptions.TAIL_CALLS) && !updating)
            {
                int tailCalls = ExpressionTool.MarkTailFunctionCalls(body, functionName, arity);
                if (tailCalls != 0)
                {
                    compiledFunction.SetBody(body);
                    compiledFunction.SetTailRecursive(tailCalls > 0, tailCalls > 1);
                    body = new TailCallLoop(compiledFunction, body);
                }
            }

            compiledFunction.SetBody(body);

            ExpressionTool.AllocateSlots(body, arity, compiledFunction.GetStackFrameMap());
        }

        // module.
        public virtual void FixupReferences()
        {
            foreach (IUserFunctionResolvable ufc in references)
            {
                ufc.SetFunction(compiledFunction);
            }
        }

        // module.
        public virtual void CheckReferences(ExpressionVisitor visitor)
        {
            foreach (IUserFunctionResolvable ufr in references)
            {
                if (ufr is UserFunctionCall)
                {
                    UserFunctionCall ufc = (UserFunctionCall)ufr;
                    ufc.CheckFunctionCall(compiledFunction, visitor); //ufc.computeArgumentEvaluationModes();
                }
            }


            // clear the list of references, so that more can be added in another module
            references = new List<IUserFunctionResolvable>(0);
        }

        // module.
        public virtual void Explain(ExpressionPresenter @out)
        {
            @out.StartElement("declareFunction");
            @out.EmitAttribute("name", functionName.DisplayName);
            @out.EmitAttribute("arity", "" + NumberOfParameters);
            if (compiledFunction == null)
            {
                @out.EmitAttribute("unreferenced", "true");
            }
            else
            {
                if (compiledFunction.IsMemoFunction())
                {
                    @out.EmitAttribute("memo", "true");
                }

                @out.EmitAttribute("tailRecursive", compiledFunction.IsTailRecursive() ? "true" : "false");
                body.Export(@out);
            }

            @out.EndElement();
        }

        // module.
        public virtual UserFunction GetUserFunction()
        {
            return compiledFunction;
        }

        // module.
        public virtual StructuredQName GetObjectName()
        {
            return functionName;
        }

        // module.
        public virtual string GetSystemId()
        {
            return location.GetSystemId();
        }

        // module.
        public virtual int GetLineNumber()
        {
            return location.GetLineNumber();
        }

        // module.
        public virtual string GetPublicId()
        {
            return null;
        }

        // module.
        public virtual int GetColumnNumber()
        {
            return -1;
        }

        // module.
        public virtual ILocation SaveLocation()
        {
            return this;
        }

        // module.
        public virtual INamespaceResolver GetNamespaceResolver()
        {
            return namespaceResolver;
        }

        // module.
        public virtual object GetProperty(string name)
        {
            if (name == "name")
            {
                return functionName.DisplayName;
            }
            else if (name == "as")
            {
                return resultType.ToString();
            }
            else
            {
                return null;
            }
        }

        // module.
        public virtual IEnumerator<string> GetProperties()
        {
            yield return "name";
            yield return "as";
        }

        // module.
        public virtual HostLanguage GetHostLanguage()
        {
            return HostLanguage.XQUERY;
        }

        private sealed class AnonymousGlobalVariableManager : IGlobalVariableManager
        {

            private readonly XQueryFunction parent;
            public AnonymousGlobalVariableManager(XQueryFunction parent)
            {
                this.parent = parent;
            }
            public void AddGlobalVariable(GlobalVariable variable)
            {
                PackageData pd = parent.staticContext.GetPackageData();
                variable.SetPackageData(pd);
                SlotManager sm = pd.GlobalSlotManager;
                int slot = sm.AllocateSlotNumber(variable.GetVariableQName(), null);
                variable.Compile(parent.staticContext.GetExecutable(), slot);
                pd.AddGlobalVariable(variable);
            }

            public GlobalVariable GetEquivalentVariable(Expression select)
            {
                return null;
            }
        }
    }
}