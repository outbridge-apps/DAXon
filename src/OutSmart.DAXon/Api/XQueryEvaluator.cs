////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.XQuery;
using OutSmart.DAXon.Serialization;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Tiny;
using OutSmart.DAXon.Types;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Api
{
    public class XQueryEvaluator : AbstractDestination, IEnumerable<XdmItem>, IChainedDestination
    {
        private readonly Processor processor;
        private readonly XQueryExpression expression;
        private readonly DynamicQueryContext context;
        private Controller controller; // used only when making direct calls to global functions
        private QName calledName;      // the function the last direct call found: a host calls one over and over, and
        private UserFunction called;   // finding it again hashed its name on every call
        private IDestination destination;

        IDestination IChainedDestination.Next => destination;
        private HashSet<XdmNode> updatedDocuments;
        private Builder sourceTreeBuilder;

        // None before an updating query has run.
        public virtual IEnumerator<XdmNode> UpdatedDocuments => (updatedDocuments ?? new HashSet<XdmNode>()).GetEnumerator();

        public virtual DynamicQueryContext UnderlyingQueryContext => context;
        public XQueryEvaluator(Processor processor, XQueryExpression expression)
        {
            ArgumentCheck.NotNull(processor, nameof(processor));
            ArgumentCheck.NotNull(expression, nameof(expression));
            this.processor = processor;
            this.expression = expression;
            this.context = new DynamicQueryContext(expression.GetConfiguration());
        }

        public virtual void SetSchemaValidationMode(ValidationMode mode)
        {
            // ValidationMode is an enum: the Java null-check was always true here.
            context.SchemaValidationMode = mode.GetNumber();
        }

        public virtual ValidationMode GetSchemaValidationMode()
        {
            return ValidationModeExtensions.Get(context.SchemaValidationMode);   // the engine's number, not the enum's
        }

        internal virtual void SetSource(ResolvedResource source)
        {
            if (source.Node != null)
            {
                SetContextItem(new XdmNode(source.Node));
            }
            else
            {
                SetContextItem(processor.NewDocumentBuilder().Build(source));
            }
        }

        public virtual void SetContextItem(XdmItem item)
        {
            if (item != null)
            {
                GlobalContextRequirement gcr = expression.GetExecutable().GlobalContextRequirement;
                if (gcr != null && !gcr.IsExternal())
                {
                    throw new DAXonApiException("The context item for the query is not defined as external");
                }

                context.ContextItem = item.UnderlyingValue;
            }
        }

        public virtual XdmItem GetContextItem()
        {
            IItem item = context.ContextItem;
            if (item == null)
            {
                return null;
            }

            return (XdmItem)XdmValue.Wrap(item);
        }

        public virtual void SetExternalVariable(QName name, XdmValue value)
        {
            ArgumentCheck.NotNull(name, nameof(name));
            try
            {
                context.SetParameter(name.GetStructuredQName(), value == null ? null : ((ISequence)value.UnderlyingValue).Materialize());
            }
            catch (Exception e) when (ApiErrors.IsEngineError(e))
            {
                throw ApiErrors.WrapUnchecked(e);
            }
        }

        public virtual XdmValue GetExternalVariable(QName name)
        {
            ArgumentCheck.NotNull(name, nameof(name));
            IGroundedValue oval = context.GetParameter(name.GetStructuredQName());
            if (oval == null)
            {
                return null;
            }

            return XdmValue.Wrap(oval);
        }

        public virtual void SetResourceResolver(IResourceResolver resolver)
        {
            context.ResourceResolver = resolver;
        }

        public virtual IResourceResolver GetResourceResolver()
        {
            return context.ResourceResolver;
        }

        public virtual void SetUnparsedTextResolver(IUnparsedTextURIResolver resolver)
        {
            context.UnparsedTextURIResolver = resolver;
        }

        public virtual IUnparsedTextURIResolver GetUnparsedTextURIResolver()
        {
            return context.UnparsedTextURIResolver;
        }

        public virtual void SetErrorReporter(IErrorReporter reporter)
        {
            // null clears it (HOSTING, "A null argument"): back to the default, which the run's errors are reported to.
            context.ErrorReporter = reporter ?? context.GetConfiguration().MakeErrorReporter();
        }

        public virtual IErrorReporter GetErrorReporter()
        {
            return context.ErrorReporter;
        }

        public virtual void SetTraceListener(ITraceListener listener)
        {
            context.SetTraceListener(listener);
        }

        public virtual ITraceListener GetTraceListener()
        {
            return context.GetTraceListener();
        }

        public virtual void SetTraceFunctionDestination(Logger stream)
        {
            context.TraceFunctionDestination = stream;
        }

        public virtual Logger GetTraceFunctionDestination()
        {
            return context.TraceFunctionDestination;
        }

        public virtual void SetDestination(IDestination destination)
        {
            this.destination = destination;
        }

        public virtual void Run()
        {
            using RunResources run = RunResources.Enter();
            try
            {
                if (expression.IsUpdateQuery())
                {
                    HashSet<IMutableNodeInfo> docs = expression.RunUpdate(context);
                    updatedDocuments = new HashSet<XdmNode>();
                    foreach (IMutableNodeInfo doc in docs)
                    {
                        updatedDocuments.Add(XdmItem.WrapItem(doc));
                    }
                }
                else
                {
                    if (destination == null)
                    {
                        throw new InvalidOperationException("No destination supplied");
                    }

                    Run(destination);
                }
            }
            catch (Exception e) when (ApiErrors.IsEngineError(e))
            {
                throw ApiErrors.Wrap(e);
            }
        }

        public virtual void Run(IDestination destination)
        {
            ArgumentCheck.NotNull(destination, nameof(destination));
            using RunResources run = RunResources.Enter();
            if (expression.IsUpdateQuery())
            {
                throw new InvalidOperationException("Query is updating");
            }

            try
            {
                DestinationHelper.RunTo(destination, () => expression.Run(context, GetDestinationReceiver(destination), null));
            }
            catch (Exception e) when (ApiErrors.IsEngineError(e))
            {
                throw ApiErrors.Wrap(e);
            }
        }

        public virtual void RunStreamed(ResolvedResource source, IDestination destination)
        {
            ArgumentCheck.NotNull(source, nameof(source));
            ArgumentCheck.NotNull(destination, nameof(destination));
            using RunResources run = RunResources.Enter();
            if (expression.IsUpdateQuery())
            {
                throw new InvalidOperationException("Query is updating; cannot run with streaming");
            }

            Configuration config = context.GetConfiguration();
            if (config.IsTiming())
            {
                string systemId = source.SystemId;
                if (systemId == null)
                {
                    systemId = "";
                }

                config.Logger.Info("Processing streamed input " + systemId);
            }

            try
            {
                DestinationHelper.RunTo(destination, () => expression.RunStreamed(context, source, GetDestinationReceiver(destination), null));
            }
            catch (Exception e) when (ApiErrors.IsEngineError(e))
            {
                throw ApiErrors.Wrap(e);
            }
        }

        public virtual XdmValue Evaluate()
        {
            using RunResources run = RunResources.Enter();
            if (expression.IsUpdateQuery())
            {
                throw new InvalidOperationException("Query is updating");
            }

            try
            {
                ISequenceIterator iter = expression.IIterator(context);
                return XdmValue.Wrap(SequenceTool.ToGroundedValue(iter));
            }
            catch (Exception e) when (ApiErrors.IsEngineError(e))
            {
                throw ApiErrors.Wrap(e);
            }
        }

        public virtual XdmItem EvaluateSingle()
        {
            using RunResources run = RunResources.Enter();
            try
            {
                ISequenceIterator iter = expression.IIterator(context);
                IItem next = iter.Next();
                return next == null ? null : (XdmItem)XdmValue.Wrap(next);
            }
            catch (Exception e) when (ApiErrors.IsEngineError(e))
            {
                throw ApiErrors.Wrap(e);
            }
        }

        [Obsolete("Use Iterator(): the same method, named as s9api names it (iterator).")]
        public XdmSequenceIterator<XdmItem> IIterator()
        {
            return Iterator();
        }

        public XdmSequenceIterator<XdmItem> Iterator()
        {
            if (expression.IsUpdateQuery())
            {
                throw new InvalidOperationException("Query is updating");
            }

            RunResources scope = RunResources.Detached();
            RunResources saved = scope.Activate();
            Core.Controller.DeadlineToken limitsBefore = Core.Controller.ActiveLimits;   // the iterator keeps its own
            try
            {
                return new XdmSequenceIterator<XdmItem>(expression.IIterator(context), scope);
            }
            catch (Exception e) when (ApiErrors.IsEngineError(e))
            {
                scope.CloseAll();
                throw ApiErrors.WrapUnchecked(e);
            }
            finally
            {
                RunResources.Restore(saved);
                Core.Controller.RestoreThreadDeadline(limitsBefore);
            }
        }

        private IReceiver GetDestinationReceiver(IDestination destination)
        {
            Executable exec = expression.GetExecutable();
            PipelineConfiguration pipe = expression.GetConfiguration().MakePipelineConfiguration();
            return destination.GetReceiver(pipe, exec.PrimarySerializationProperties);
        }

        public override IReceiver GetReceiver(PipelineConfiguration pipe, SerializationProperties @params)
        {
            ArgumentCheck.NotNull(pipe, nameof(pipe));
            ArgumentCheck.NotNull(@params, nameof(@params));
            if (destination == null)
            {
                throw new InvalidOperationException("No destination has been supplied");
            }

            Core.Controller.DeadlineToken limitsBefore = Core.Controller.ActiveLimits;   // given back when the run is over
            try
            {
                if (controller == null)
                {
                    controller = expression.NewController(context);
                }
                else
                {
                    context.InitializeController(controller);
                    expression.ArmLimits(controller);
                }
            }
            catch (Exception e) when (ApiErrors.IsEngineError(e))
            {
                Core.Controller.RestoreThreadDeadline(limitsBefore);
                throw ApiErrors.Wrap(e);
            }

            sourceTreeBuilder = controller.MakeBuilder();
            sourceTreeBuilder.SetDurability(Durability.LASTING);
            if (sourceTreeBuilder is TinyBuilder)
            {
                ((TinyBuilder)sourceTreeBuilder).SetStatistics(context.GetConfiguration().GetTreeStatistics().SOURCE_DOCUMENT_STATISTICS);
            }

            IReceiver @out = controller.MakeStripper(sourceTreeBuilder);
            SequenceNormalizer sn = @params.MakeSequenceNormalizer(@out);
            sn.OnClose(() =>
            {
                try
                {
                    NodeInfo doc = sourceTreeBuilder.CurrentRoot;
                    if (doc == null)
                    {
                        throw new DAXonApiException("No source document has been built by the previous pipeline stage");
                    }

                    doc.GetTreeInfo().SpaceStrippingRule = controller.SpaceStrippingRule;
                    SetSource(new ResolvedResource { Node = doc });
                    sourceTreeBuilder = null;
                    Run(destination);   // which closes and notifies the destination
                }
                finally
                {
                    Core.Controller.RestoreThreadDeadline(limitsBefore);
                }
            });
            return sn;
        }

        public override void Close()
        {
        }

        public virtual XdmValue CallFunction(QName function, params XdmValue[] arguments)
        {
            ArgumentCheck.NotNull(function, nameof(function));
            ArgumentCheck.NoNulls(arguments, nameof(arguments));
            using RunResources run = RunResources.Enter();
            UserFunction fn = called != null && called.GetArity() == arguments.Length && function.Equals(calledName)
                ? called
                : expression.MainModule.GetUserDefinedFunction(function.GetNamespaceUri(), function.LocalName, arguments.Length);
            if (fn == null)
            {
                throw new DAXonApiException("No function with name " + function.EQName + " and arity " + arguments.Length + " has been declared in the query");
            }

            called = fn;
            calledName = function;

            Controller.DeadlineToken limits = null;   // given back for the next call: the result is grounded
            try
            {
                if (controller == null)
                {
                    controller = expression.NewController(context);
                }
                else
                {
                    context.InitializeController(controller);
                    expression.ArmLimits(controller);
                }

                // Under a trace the globals are evaluated first, as by Saxon's initializeController (Run does it itself)
                if (controller.GetTraceListener() != null)
                {
                    controller.PreEvaluateGlobals(controller.NewXPathContext());
                }

                limits = controller.Limits;
                Configuration config = processor.UnderlyingConfiguration;
                TypeHierarchy th = config.GetTypeHierarchy();
                ISequence[] vr = SequenceTool.MakeSequenceArray(arguments.Length);
                for (int i = 0; i < arguments.Length; i++)
                {
                    Values.SequenceType type = fn.GetParameterDefinitions()[i].GetRequiredType();
                    IGroundedValue gVal = (IGroundedValue)arguments[i].UnderlyingValue;

                    if (controller.GetConfiguration().HoldsForeignNode(gVal))
                    {
                        throw new XPathException(ApiErrors.ForeignArgument, DAXonErrorCode.SXXP0004);
                    }

                    // As Xslt30Transformer's: the argument trees count in the call's memory limit.
                    controller.ChargeInput(gVal);

                    if (!type.Matches(gVal, th))
                    {
                        int pos = i;
                        Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.FUNCTION, function.GetStructuredQName().DisplayName, pos);
                        gVal = th.ApplyFunctionConversionRules(gVal, type, role, Loc.NONE);
                    }

                    vr[i] = gVal;
                }

                limits?.CheckNow();   // the charged arguments are summed now: the function may allocate too little to look

                ISequence result = fn.Call(vr, controller);
                // grounded here: a lazy result could still read from a file this call closes on return
                result = result.Materialize();
                return XdmValue.Wrap(result);
            }
            catch (Exception e) when (ApiErrors.IsEngineError(e))
            {
                throw ApiErrors.Wrap(e);
            }
            finally
            {
                controller?.ReleaseLimits(limits);
            }
        }
        // s9api XQueryEvaluator is Iterable<XdmItem>: foreach over the evaluator runs the query.
        public IEnumerator<XdmItem> GetEnumerator()
        {
            using (XdmSequenceIterator<XdmItem> it = Iterator())
            {
                while (it.HasNext())
                {
                    yield return it.Next();
                }
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
