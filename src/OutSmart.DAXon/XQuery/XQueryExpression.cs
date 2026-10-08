////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Serialization;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using System.IO;
namespace OutSmart.DAXon.XQuery
{
    public class XQueryExpression : ILocation, IExpressionOwner, ITraceableComponent
    {
        private readonly object syncLock = new object();
        protected Expression expression;
        protected SlotManager stackFrameMap;
        protected Executable executable;
        protected QueryModule mainModule;
        protected IPullEvaluator pullEvaluator = null;
        protected IPushEvaluator pushEvaluator = null;

        public virtual string TracingTag => "query";

        public virtual QueryModule MainModule => mainModule;

        public virtual StructuredQName[] ExternalVariableNames
        {
            get
            {
                IList<StructuredQName> list = stackFrameMap.VariableMap;
                StructuredQName[] names = new StructuredQName[stackFrameMap.NumberOfVariables];
                for (int i = 0; i < names.Length; i++)
                {
                    names[i] = list[i];
                }

                return names;
            }
        }
        public XQueryExpression(Expression exp, QueryModule mainModule, bool streaming)
        {
            Executable exec = mainModule.GetExecutable();
            Configuration config = mainModule.GetConfiguration();
            stackFrameMap = config.MakeSlotManager();
            executable = exec;
            this.mainModule = mainModule;
            exp.SetRetainedStaticContext(mainModule.MakeRetainedStaticContext());
            try
            {
                ExpressionVisitor visitor = ExpressionVisitor.Make(mainModule);
                Optimizer optimizer = visitor.ObtainOptimizer();
                visitor.SetOptimizeForStreaming(streaming);
                exp = exp.Simplify();
                exp.CheckForUpdatingSubexpressions();
                GlobalContextRequirement contextReq = exec.GlobalContextRequirement;
                Types.ItemType req = contextReq == null ? AnyItemType.GetInstance() : contextReq.RequiredItemType;
                ContextItemStaticInfo cit = config.MakeContextItemStaticInfo(req, true);
                Expression e2 = exp.TypeCheck(visitor, cit);
                if (e2 != exp)
                {
                    e2.SetRetainedStaticContext(exp.GetRetainedStaticContext());
                    e2.ParentExpression = null;
                    exp = e2;
                }

                if (optimizer.IsOptionSet(OptimizerOptions.MISCELLANEOUS))
                {
                    e2 = exp.Optimize(visitor, cit);
                    if (e2 != exp)
                    {
                        e2.SetRetainedStaticContext(exp.GetRetainedStaticContext());
                        e2.ParentExpression = null;
                        exp = e2;
                    }
                }

                if (optimizer.IsOptionSet(OptimizerOptions.LOOP_LIFTING))
                {
                    e2 = LoopLifter.Process(exp, visitor, cit);
                    if (e2 != exp)
                    {
                        e2.SetRetainedStaticContext(exp.GetRetainedStaticContext());
                        e2.ParentExpression = null;
                        exp = e2;
                    }
                }
            }
            catch (XPathException err)
            {

                mainModule.ReportStaticError(err);
                throw;
            }

            ExpressionTool.AllocateSlots(exp, 0, stackFrameMap);
            ExpressionTool.ComputeEvaluationModesForUserFunctionCalls(exp);
            foreach (GlobalVariable var in GetPackageData().GlobalVariableList)
            {
                Expression top = var.GetBody();
                if (top != null)
                {
                    ExpressionTool.ComputeEvaluationModesForUserFunctionCalls(top);
                }
            }

            expression = exp;
            executable.SetConfiguration(config);
        }

        public virtual Expression GetExpression()
        {
            return expression;
        }

        public virtual Expression GetBody()
        {
            return GetExpression();
        }

        public virtual Expression GetChildExpression()
        {
            return expression;
        }

        public virtual void SetBody(Expression expression)
        {
            SetChildExpression(expression);
        }

        public virtual StructuredQName GetObjectName()
        {
            return null;
        }

        public virtual ILocation GetLocation()
        {
            return this;
        }

        public virtual PackageData GetPackageData()
        {
            return mainModule.GetPackageData();
        }

        public virtual Configuration GetConfiguration()
        {
            return mainModule.GetConfiguration();
        }

        public virtual bool UsesContextItem()
        {
            if (ExpressionTool.DependsOnFocus(expression))
            {
                return true;
            }

            IList<GlobalVariable> map = GetPackageData().GlobalVariableList;
            if (map != null)
            {
                foreach (GlobalVariable var in map)
                {
                    Expression select = var.GetBody();
                    if (select != null && ExpressionTool.DependsOnFocus(select))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public virtual bool IsUpdateQuery()
        {
            return false;
        }

        public virtual SlotManager GetStackFrameMap()
        {
            return stackFrameMap;
        }

        public virtual void ExplainPathMap()
        {
        }

        public virtual IList<object> Evaluate(DynamicQueryContext env)
        {
            if (IsUpdateQuery())
            {
                throw new XPathException("Cannot call evaluate() on an updating query");
            }

            List<object> list = new List<object>(100);
            SequenceTool.Supply(IIterator(env), (item) => list.Add(SequenceTool.ConvertToJava(item)));
            return list;
        }

        public virtual object EvaluateSingle(DynamicQueryContext env)
        {
            if (IsUpdateQuery())
            {
                throw new XPathException("Cannot call evaluateSingle() on an updating query");
            }

            ISequenceIterator iter = IIterator(env);
            IItem item = iter.Next();
            if (item == null)
            {
                return null;
            }

            return SequenceTool.ConvertToJava(item);
        }

        public virtual ISequenceIterator IIterator(DynamicQueryContext env)
        {
            if (IsUpdateQuery())
            {
                throw new XPathException("Cannot call iterator() on an updating query");
            }

            if (!env.GetConfiguration().IsCompatible(GetExecutable().GetConfiguration()))
            {
                throw new XPathException("The query must be compiled and executed under the same Configuration", DAXonErrorCode.SXXP0004);
            }

            Controller controller = NewController(env);
            try
            {
                IItem contextItem = controller.GlobalContextItem;
                if (contextItem is NodeInfo && ((NodeInfo)contextItem).GetTreeInfo().IsTyped() && !GetExecutable().IsSchemaAware())
                {
                    throw new XPathException("A typed input document can only be used with a schema-aware query");
                }

                XPathContextMajor context = InitialContext(env, controller);

                // In tracing/debugging mode, evaluate all the global variables first
                if (controller.GetTraceListener() != null)
                {
                    controller.PreEvaluateGlobals(context);
                }

                context.OpenStackFrame(stackFrameMap);
                ISequenceIterator iterator = GetExpressionIterator(context);
                if (iterator is IGroundedIterator && ((IGroundedIterator)iterator).IsActuallyGrounded())
                {
                    return iterator;
                }
                else
                {
                    return new ErrorReportingIterator(iterator, controller.ErrorReporter, GetLocation());
                }
            }
            catch (XPathException err)
            {
                XPathException terr = err;
                while (terr.InnerException is XPathException inner)
                {
                    terr = inner;
                }

                XPathException de = XPathException.MakeXPathException(terr);
                controller.ReportFatalError(de);
                throw de;
            }
        }

        protected virtual ISequenceIterator GetExpressionIterator(IXPathContext context)
        {
            lock (syncLock)
            {
                if (pullEvaluator == null)
                {
                    pullEvaluator = expression.MakeElaborator().ElaborateForPull();
                }
            }

            return pullEvaluator.Iterate(context);
        }

        public virtual void Run(DynamicQueryContext env, IResultTarget result, Properties outputProperties)
        {
            if (IsUpdateQuery())
            {
                throw new XPathException("Cannot call run() on an updating query");
            }

            if (!env.GetConfiguration().IsCompatible(GetExecutable().GetConfiguration()))
            {
                throw new XPathException("The query must be compiled and executed under the same Configuration", DAXonErrorCode.SXXP0004);
            }

            IItem contextItem = env.ContextItem;
            if (contextItem is NodeInfo && ((NodeInfo)contextItem).GetTreeInfo().IsTyped() && !GetExecutable().IsSchemaAware())
            {
                throw new XPathException("A typed input document can only be used with a schema-aware query");
            }

            Controller controller = NewController(env);
            controller.OpenTraceEpisode();
            if (result is IReceiver)
            {
                ((IReceiver)result).GetPipelineConfiguration().SetController(controller);
            }

            Properties actualProperties = ValidateOutputProperties(controller, outputProperties);
            XPathContextMajor context = InitialContext(env, controller);

            // In tracing/debugging mode, evaluate all the global variables first
            ITraceListener tracer = controller.GetTraceListener();
            if (tracer != null)
            {
                controller.PreEvaluateGlobals(context);
            }

            context.OpenStackFrame(stackFrameMap);
            bool mustClose = result is StreamResult && ((StreamResult)result).GetOutputStream() == null;
            IReceiver @out;
            if (result is IReceiver)
            {
                @out = (IReceiver)result;
            }
            else
            {
                SerializerFactory sf = context.GetConfiguration().SerializerFactory;
                PipelineConfiguration pipe = controller.MakePipelineConfiguration();
                pipe.SetHostLanguage(HostLanguage.XQUERY);
                @out = sf.GetReceiver(result, new SerializationProperties(actualProperties), pipe);
            }

            ComplexContentOutputter dest = new ComplexContentOutputter(@out);
            dest.Open();

            // Run the query
            try
            {
                ProcessQuery(dest, context);
            }
            catch (XPathException err)
            {
                controller.ReportFatalError(err);
                throw;
            }
            finally
            {
                try
                {
                    controller.CloseTraceEpisode();
                    dest.Close();
                }
                catch (XPathException e)
                {
                    // ignored, as upstream does (it only printed the stack trace)
                }
            }

            if (result is StreamResult)
            {
                CloseStreamIfNecessary((StreamResult)result, mustClose);
            }
        }

        protected virtual void ProcessQuery(Outputter dest, IXPathContext context)
        {
            try
            {
                lock (syncLock)
                {
                    if (pushEvaluator == null)
                    {
                        pushEvaluator = expression.MakeElaborator().ElaborateForPush();
                    }
                }

                Expression.DispatchTailCall(pushEvaluator.ProcessLeavingTail(dest, context));
            }
            catch (UncheckedXPathException e)
            {
                throw e.GetXPathException();
            }
        }

        protected virtual void CloseStreamIfNecessary(StreamResult result, bool mustClose)
        {
            if (mustClose)
            {
                System.IO.Stream os = result.GetOutputStream();
                if (os != null)
                {
                    try
                    {
                        os.Dispose();
                    }
                    catch (IOException err)
                    {
                        throw new XPathException(err?.Message);
                    }
                }
            }
        }

        public virtual void RunStreamed(DynamicQueryContext dynamicEnv, ResolvedResource source, IResultTarget result, Properties outputProperties)
        {
            throw new XPathException("Streaming requires Saxon-EE");
        }

        protected virtual Properties ValidateOutputProperties(Controller controller, Properties outputProperties)
        {

            // Validate the serialization properties requested
            Properties baseProperties = controller.GetExecutable().PrimarySerializationProperties.GetProperties();
            SerializerFactory sf = controller.GetConfiguration().SerializerFactory;
            if (outputProperties != null)
            {
                foreach (string key in outputProperties.StringPropertyNames())
                {
                    string value = outputProperties.GetProperty(key);
                    try
                    {
                        value = sf.CheckOutputProperty(key, value);
                        baseProperties.SetProperty(key, value);
                    }
                    catch (XPathException dynamicError)
                    {
                        outputProperties.Remove(key);
                        XmlProcessingException err = new XmlProcessingException(dynamicError);
                        err.SetWarning(true);
                        controller.ErrorReporter.Report(err);
                    }
                }
            }

            if (baseProperties.GetProperty("method") == null)
            {

                // XQuery forces the default method to XML, unlike XSLT where it depends on the contents of the result tree
                baseProperties.SetProperty("method", "xml");
            }

            return baseProperties;
        }

        public virtual HashSet<IMutableNodeInfo> RunUpdate(DynamicQueryContext dynamicEnv)
        {
            throw new XPathException("Calling runUpdate() on a non-updating query");
        }

        public virtual void RunUpdate(DynamicQueryContext dynamicEnv, IUpdateAgent agent)
        {
            throw new XPathException("Calling runUpdate() on a non-updating query");
        }

        protected virtual XPathContextMajor InitialContext(DynamicQueryContext dynamicEnv, Controller controller)
        {
            IItem contextItem = controller.GlobalContextItem;
            XPathContextMajor context = controller.NewXPathContext();
            if (contextItem != null)
            {
                ManualIterator single = new ManualIterator(contextItem);
                context.SetCurrentIterator(single);
                controller.GlobalContextItem = contextItem;
            }

            return context;
        }

        public virtual Controller NewController(DynamicQueryContext env)
        {
            Controller controller = new Controller(executable.GetConfiguration(), executable);
            env.InitializeController(controller);
            ArmLimits(controller);
            return controller;
        }

        // Arm the Processor-wide limits for one query call - also on a controller the evaluator keeps for direct function
        // calls, which armed only once before. A module loaded via fn:load-xquery-module inherits the caller's instead.
        internal void ArmLimits(Controller controller)
        {
            if (executable.GetConfiguration().GetProcessor() is OutSmart.DAXon.Api.Processor p)
            {
                controller.SetTimeout(p.TransformTimeout, "Query");
            }
        }

        public virtual void Explain(ExpressionPresenter @out)
        {
            @out.StartElement("query");
            mainModule.GetKeyManager().ExportKeys(@out, null);
            GetExecutable().ExplainGlobalVariables(@out);
            mainModule.ExplainGlobalFunctions(@out);
            @out.StartElement("body");
            expression.Export(@out);
            @out.EndElement();
            @out.EndElement();
            @out.Dispose();
        }

        public virtual Executable GetExecutable()
        {
            return executable;
        }

        public virtual void SetAllowDocumentProjection(bool allowed)
        {
            if (allowed)
            {
                throw new NotSupportedException("Document projection requires Saxon-EE");
            }
        }

        public virtual bool IsDocumentProjectionAllowed()
        {
            return false;
        }

        public virtual string GetPublicId()
        {
            return null;
        }

        public virtual string GetSystemId()
        {
            return mainModule.GetSystemId();
        }

        public virtual int GetLineNumber()
        {
            return -1;
        }

        public virtual int GetColumnNumber()
        {
            return -1;
        }

        public virtual ILocation SaveLocation()
        {
            return this;
        }

        public virtual HostLanguage GetHostLanguage()
        {
            return HostLanguage.XQUERY;
        }

        public virtual void SetChildExpression(Expression expr)
        {
            expression = expr;
        }

        // === Auto-generated stubs (StubGenerator Phase 3.1f) ===
        public virtual void GatherProperties(Action<string, object> consumer) { } // upstream Traceable default: no properties

        private sealed class ErrorReportingIterator : ISequenceIterator
        {
            private readonly ISequenceIterator @base;
            private readonly IErrorReporter reporter;
            private readonly ILocation location;
            public ErrorReportingIterator(ISequenceIterator @base, IErrorReporter reporter, ILocation location)
            {
                this.@base = @base;
                this.reporter = reporter;
                this.location = location;
            }

            public IItem Next()
            {
                try
                {
                    return @base.Next();
                }
                catch (UncheckedXPathException e1)
                {
                    XPathException xe = e1.GetXPathException().MaybeWithLocation(location);
                    XmlProcessingException err = new XmlProcessingException(xe);
                    reporter.Report(err);
                    xe.SetHasBeenReported(true);
                    throw;
                }
            }

            public void Dispose()
            {
                @base.Dispose();
            }
        }
    }
}