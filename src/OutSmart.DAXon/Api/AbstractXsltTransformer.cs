////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Serialization;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Tiny;
using OutSmart.DAXon.Internal.Net;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Streams;
namespace OutSmart.DAXon.Api
{
    /// <summary>
    /// A class that exists to contain common code shared between XsltTransformer and Xslt30Transformer
    /// </summary>
    //@CSharpInjectMembers(code = {
    //        "    public void setErrorReporter(Action<OutSmart.DAXon.Api.IXmlProcessingError> reporter) {"
    //                + "        setErrorReporter(new Saxon.Impl.Helpers.ErrorReportingAction(reporter));"
    //                + "    }"
    //})
    public abstract class AbstractXsltTransformer
    {
        protected readonly object syncLock = new object();
        protected Processor processor;
        protected XsltController controller;
        protected bool baseOutputUriWasSet = false;
        private IMessageListener2 messageListener2;

        public virtual IMessageListener2 MessageListener2 => messageListener2;

        public virtual QName InitialMode
        {
            get
            {
                StructuredQName mode = controller.InitialModeName;
                if (mode == null)
                {
                    return null;
                }
                else
                {
                    return new QName(mode);
                }
            }
            set
            {
                try
                {
                    controller.SetInitialMode(value == null ? null : value.GetStructuredQName());
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
        }

        public virtual XsltController UnderlyingController => controller;
        public AbstractXsltTransformer(Processor processor, XsltController controller)
        {
            this.processor = processor;
            this.controller = controller;
        }

        public virtual void SetBaseOutputURI(string uri)
        {
            lock (syncLock)
            {
                controller.BaseOutputURI = uri;
                baseOutputUriWasSet = uri != null;
            }
        }

        public virtual string GetBaseOutputURI()
        {
            return controller.BaseOutputURI;
        }

        public virtual void SetResourceResolver(IResourceResolver resolver)
        {
            controller.ResourceResolver = resolver;
        }

        public virtual IResourceResolver GetResourceResolver()
        {
            return controller.ResourceResolver;
        }

        public virtual void SetUnparsedTextResolver(IUnparsedTextURIResolver resolver)
        {
            controller.UnparsedTextURIResolver = resolver;
        }

        public virtual IUnparsedTextURIResolver GetUnparsedTextURIResolver()
        {
            return controller.UnparsedTextURIResolver;
        }

        public virtual void SetErrorReporter(IErrorReporter reporter)
        {
            controller.ErrorReporter = reporter;
        }

        public virtual IErrorReporter GetErrorReporter()
        {
            return controller.ErrorReporter;
        }

        public virtual void SetResultDocumentHandler(Func<URI, IDestination> handler)
        {
            controller.ResultDocumentResolver = new AnonymousIResultDocumentResolver(this, handler);
        }

        public virtual void SetMessageListener(IMessageListener2 listener)
        {
            lock (syncLock)
            {
                messageListener2 = listener;
                SetMessageHandler((message) => listener.Message(message.Content, message.GetErrorCode(), message.IsTerminate(), message.GetLocation()));
            }
        }

        public virtual void SetMessageHandler(Action<Message> messageHandler)
        {
            controller.MessageHandler = messageHandler;
        }

        public virtual void SetAssertionsEnabled(bool enabled)
        {
            controller.SetAssertionsEnabled(enabled);
        }

        public virtual bool IsAssertionsEnabled()
        {
            return controller.IsAssertionsEnabled();
        }

        public virtual void SetTraceListener(ITraceListener listener)
        {
            controller.SetTraceListener(listener);
        }

        public virtual ITraceListener GetTraceListener()
        {
            return controller.GetTraceListener();
        }

        public virtual void SetTraceFunctionDestination(Logger stream)
        {
            controller.TraceFunctionDestination = stream;
        }

        public virtual Logger GetTraceFunctionDestination()
        {
            return controller.TraceFunctionDestination;
        }

        protected virtual void ApplyTemplatesToSource(IActiveSource source, IReceiver @out)
        {
            if (source == null)
                throw new NullReferenceException();
            if (@out == null)
                throw new NullReferenceException();
            if (controller.GetInitialMode().IsDeclaredStreamable())
            {
                controller.ApplyStreamingTemplates(source, @out);
            }
            else
            {
                NodeInfo node;
                if (source is NodeInfo)
                {
                    node = (NodeInfo)source;
                }
                else
                {
                    node = controller.MakeSourceTree(source, controller.SchemaValidationMode);
                }

                controller.ApplyTemplates(node, @out);
            }
        }

        // Source-free apply-templates (P5): build the source tree from a System.Xml.XmlReader (with the
        // stylesheet's strip-space applied) and apply templates to it. A streamable initial mode yields the
        // same result over the full tree, so this path always builds the tree — no JAXP Source is constructed.
        protected virtual void ApplyTemplatesToXmlReader(System.Xml.XmlReader reader, string systemId, IReceiver @out)
        {
            if (reader == null)
                throw new NullReferenceException();
            if (@out == null)
                throw new NullReferenceException();
            NodeInfo node = controller.MakeSourceTree(reader, systemId, controller.SchemaValidationMode);
            controller.ApplyTemplates(node, @out);
        }

        public virtual void SetSchemaValidationMode(ValidationMode mode)
        {
            // ValidationMode is an enum: the Java null-check was always true here.
            controller.SchemaValidationMode = mode.GetNumber();
        }

        public virtual ValidationMode GetSchemaValidationMode()
        {
            return (ValidationMode)controller.SchemaValidationMode;
        }

        public virtual IReceiver GetDestinationReceiver(XsltController controller, IDestination destination)
        {
            IReceiver receiver;
            controller.PrincipalDestination = destination;
            PipelineConfiguration pipe = controller.MakePipelineConfiguration();
            SerializationProperties @params = controller.GetExecutable().PrimarySerializationProperties;
            receiver = destination.GetReceiver(pipe, @params);
            if (Configuration.IsAssertionsEnabled())
            {
                receiver = new RegularSequenceChecker(receiver, true);
            }


            //receiver = new TracingFilter(receiver);
            receiver.GetPipelineConfiguration().SetController(controller);
            if (baseOutputUriWasSet)
            {
                try
                {
                    if (destination.DestinationBaseURI == null)
                    {
                        destination.DestinationBaseURI = new URI(controller.BaseOutputURI);
                    }
                }
                catch (URISyntaxException e)
                {
                }
            }
            else if (destination.DestinationBaseURI != null)
            {
                controller.BaseOutputURI = destination.DestinationBaseURI.ToASCIIString();
            }

            receiver.SetSystemId(controller.BaseOutputURI);
            return receiver;
        }

        protected virtual IReceiver GetReceivingTransformer(XsltController controller, GlobalParameterSet parameters, IDestination finalDestination)
        {
            Configuration config = controller.GetConfiguration();
            if (controller.GetInitialMode().IsDeclaredStreamable())
            {
                IReceiver sOut = GetDestinationReceiver(controller, finalDestination);
                try
                {
                    controller.InitializeController(parameters);
                    controller.SetTimeout(processor.TransformTimeout);
                    return controller.GetStreamingReceiver(controller.GetInitialMode(), sOut);
                }
                catch (XPathException e)
                {
                    throw new DAXonApiException(e);
                }
            }
            else
            {
                // The caller streams the source into the returned receiver BEFORE Dispose() arms
                // the run's deadline - arm now so that parse runs under this run's fresh budget,
                // not a spent token left on the thread by an earlier run. Dispose() re-arms for
                // the transform phase itself.
                controller.SetTimeout(processor.TransformTimeout);
                Builder sourceTreeBuilder = controller.MakeBuilder();
                sourceTreeBuilder.SetDurability(Durability.LASTING);
                if (sourceTreeBuilder is TinyBuilder)
                {
                    ((TinyBuilder)sourceTreeBuilder).SetStatistics(config.GetTreeStatistics().SOURCE_DOCUMENT_STATISTICS);
                }

                IReceiver stripper = controller.MakeStripper(sourceTreeBuilder);
                if (controller.IsStylesheetStrippingTypeAnnotations())
                {
                    stripper = controller.GetConfiguration().GetAnnotationStripper(stripper);
                }

                return MakeTreeReceiver(controller, parameters, finalDestination, sourceTreeBuilder, stripper);
            }
        }

        private TreeReceiver MakeTreeReceiver(XsltController controller, GlobalParameterSet parameters, IDestination finalDestination, Builder sourceTreeBuilder, IReceiver stripper)
        {
            return new AnonymousTreeReceiver(this, stripper, controller, parameters, finalDestination, sourceTreeBuilder);
        }

        private sealed class AnonymousIResultDocumentResolver : IResultDocumentResolver
        {

            private readonly AbstractXsltTransformer parent;
            private readonly Func<URI, IDestination> handler;
            public AnonymousIResultDocumentResolver(AbstractXsltTransformer parent, Func<URI, IDestination> handler)
            {
                this.parent = parent;
                this.handler = handler;
            }
            public IReceiver Resolve(IXPathContext context, string href, string baseUri, SerializationProperties properties)
            {
                try
                {
                    URI abs = ResolveURI.MakeAbsolute(href, baseUri);
                    IDestination destination;
                    try
                    {
                        destination = handler(abs);
                    }
                    catch (DAXonApiUncheckedException e)
                    {
                        XPathException xe = XPathException.MakeXPathException(e);
                        xe.MaybeSetErrorCode(DAXonErrorCode.SXRD0001);
                        throw xe;
                    }

                    try
                    {
                        PipelineConfiguration pipe = context.GetController().MakePipelineConfiguration();
                        return destination.GetReceiver(pipe, properties);
                    }
                    catch (DAXonApiException e)
                    {
                        throw XPathException.MakeXPathException(e);
                    }
                }
                catch (URISyntaxException e)
                {
                    throw XPathException.MakeXPathException(e);
                }
            }
        }

        private sealed class AnonymousTreeReceiver : TreeReceiver
        {

            private readonly AbstractXsltTransformer parent;
            private readonly XsltController controller;
            private readonly GlobalParameterSet parameters;
            private readonly IDestination finalDestination;
            private readonly Builder sourceTreeBuilder;
            bool closed = false;
            public AnonymousTreeReceiver(AbstractXsltTransformer parent, IReceiver stripper, XsltController controller, GlobalParameterSet parameters, IDestination finalDestination, Builder sourceTreeBuilder) : base(stripper)
            {
                this.parent = parent;
                this.controller = controller;
                this.parameters = parameters;
                this.finalDestination = finalDestination;
                this.sourceTreeBuilder = sourceTreeBuilder;
            }
            public override void Close()
            {
                if (!closed)
                {
                    try
                    {
                        NodeInfo doc = sourceTreeBuilder.CurrentRoot;
                        if (doc != null)
                        {
                            doc.GetTreeInfo().SpaceStrippingRule = controller.SpaceStrippingRule;
                            IReceiver result = parent.GetDestinationReceiver(controller, finalDestination);
                            try
                            {
                                controller.GlobalContextItem = doc;
                                controller.InitializeController(parameters);
                                controller.SetTimeout(parent.processor.TransformTimeout);
                                controller.ApplyTemplates(doc, result);
                            }
                            catch (XPathException e)
                            {
                                throw new DAXonApiException(e);
                            }
                        }
                    }
                    catch (DAXonApiException e)
                    {
                        throw XPathException.MakeXPathException(e);
                    }

                    closed = true;
                }
            }
        }
    }
}