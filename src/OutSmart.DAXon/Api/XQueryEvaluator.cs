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
    public class XQueryEvaluator : AbstractDestination, IEnumerable<XdmItem>
    {
        private readonly Processor processor;
        private readonly XQueryExpression expression;
        private readonly DynamicQueryContext context;
        private Controller controller; // used only when making direct calls to global functions
        private IDestination destination;
        private HashSet<XdmNode> updatedDocuments;
        private Builder sourceTreeBuilder;

        public virtual IEnumerator<XdmNode> UpdatedDocuments => updatedDocuments.GetEnumerator();

        public virtual DynamicQueryContext UnderlyingQueryContext => context;
        public XQueryEvaluator(Processor processor, XQueryExpression expression)
        {
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
            return (ValidationMode)context.SchemaValidationMode;
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
            try
            {
                context.SetParameter(name.GetStructuredQName(), value == null ? null : ((ISequence)value.UnderlyingValue).Materialize());
            }
            catch (XPathException e)
            {
                throw new DAXonApiUncheckedException(e);
            }
            catch (RecursionDepthError e)
            {
                throw new DAXonApiUncheckedException(e.ToXPathException());
            }
        }

        public virtual XdmValue GetExternalVariable(QName name)
        {
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
            context.ErrorReporter = reporter;
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

                    Run(destination); //                Result receiver;
                    //                if (destination instanceof Serializer) {
                    //                    //context.set
                    //                } else {
                    //                }
                    //                expression.run(context, receiver, null);
                    //                destination.close();
                }
            }
            catch (XPathException e)
            {
                throw new DAXonApiException(e);
            }
            catch (UncheckedXPathException e)
            {
                throw new DAXonApiException(e.GetXPathException());
            }
        }

        public virtual void Run(IDestination destination)
        {
            using RunResources run = RunResources.Enter();
            if (expression.IsUpdateQuery())
            {
                throw new InvalidOperationException("Query is updating");
            }

            bool closed = false;
            try
            {
                IReceiver @out = GetDestinationReceiver(destination);
                expression.Run(context, @out, null);
                destination.CloseAndNotify();
                closed = true;
            }
            catch (XPathException e)
            {
                throw new DAXonApiException(e);
            }
            finally
            {
                if (!closed)
                {
                    DestinationHelper.ReleaseUnclosed(destination);
                }
            }
        }

        public virtual void RunStreamed(ResolvedResource source, IDestination destination)
        {
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

            bool closed = false;
            try
            {
                IReceiver receiver = GetDestinationReceiver(destination);
                expression.RunStreamed(context, source, receiver, null);
                destination.CloseAndNotify();
                closed = true;
            }
            catch (XPathException e)
            {
                throw new DAXonApiException(e);
            }
            finally
            {
                if (!closed)
                {
                    DestinationHelper.ReleaseUnclosed(destination);
                }
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
            catch (UncheckedXPathException e)
            {
                throw new DAXonApiException(e.GetXPathException());
            }
            catch (XPathException e)
            {
                throw new DAXonApiException(e);
            }
            catch (RecursionDepthError e)
            {
                throw new DAXonApiException(e.ToXPathException());
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
            catch (UncheckedXPathException e)
            {
                throw new DAXonApiException(e.GetXPathException());
            }
            catch (XPathException e)
            {
                throw new DAXonApiException(e);
            }
            catch (RecursionDepthError e)
            {
                throw new DAXonApiException(e.ToXPathException());
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
            catch (XPathException e)
            {
                scope.CloseAll();
                throw new DAXonApiUncheckedException(e);
            }
            catch (RecursionDepthError e)
            {
                scope.CloseAll();
                throw new DAXonApiUncheckedException(e.ToXPathException());
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
            IReceiver @out = destination.GetReceiver(pipe, exec.PrimarySerializationProperties);
            if (Configuration.IsAssertionsEnabled())
            {
                return new RegularSequenceChecker(@out, true);
            }
            else
            {
                return @out;
            }
        }

        public override IReceiver GetReceiver(PipelineConfiguration pipe, SerializationProperties @params)
        {
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
            catch (XPathException e)
            {
                Core.Controller.RestoreThreadDeadline(limitsBefore);
                throw new DAXonApiException(e);
            }
            catch (RecursionDepthError e)
            {
                Core.Controller.RestoreThreadDeadline(limitsBefore);
                throw new DAXonApiException(e.ToXPathException());
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
                    Run(destination);
                    destination.CloseAndNotify();
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
            using RunResources run = RunResources.Enter();
            UserFunction fn = expression.MainModule.GetUserDefinedFunction(function.GetNamespaceUri(), function.LocalName, arguments.Length);
            if (fn == null)
            {
                throw new DAXonApiException("No function with name " + function.EQName + " and arity " + arguments.Length + " has been declared in the query");
            }

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

                Configuration config = processor.UnderlyingConfiguration;
                TypeHierarchy th = config.GetTypeHierarchy();
                ISequence[] vr = SequenceTool.MakeSequenceArray(arguments.Length);
                for (int i = 0; i < arguments.Length; i++)
                {
                    Values.SequenceType type = fn.GetParameterDefinitions()[i].GetRequiredType();
                    IGroundedValue gVal = (IGroundedValue)arguments[i].UnderlyingValue;
                    if (!type.Matches(gVal, th))
                    {
                        int pos = i;
                        Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.FUNCTION, function.GetStructuredQName().DisplayName, pos);
                        gVal = th.ApplyFunctionConversionRules(gVal, type, role, Loc.NONE);
                    }

                    vr[i] = gVal;
                }

                ISequence result = fn.Call(vr, controller);
                // grounded here: a lazy result could still read from a file this call closes on return
                result = result.Materialize();
                return XdmValue.Wrap(result);
            }
            catch (XPathException e)
            {
                throw new DAXonApiException(e);
            }
            catch (RecursionDepthError e)
            {
                throw new DAXonApiException(e.ToXPathException());
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
