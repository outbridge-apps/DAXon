////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Serialization;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Streams;
using OutSmart.DAXon.Values;
using System.IO;
namespace OutSmart.DAXon.Api
{
    public class Xslt30Transformer : AbstractXsltTransformer
    {
        private GlobalParameterSet globalParameterSet;
        private bool primed = false;
        private IItem globalContextItem = null;
        private bool alreadyStripped;

        /*staticParameters*/
        public virtual XdmItem GlobalContextItem
        {
            get => XdmItem.WrapItem(this.globalContextItem); set
            {
                SetGlobalContextItem(value, false);
            }
        }
        public Xslt30Transformer(Processor processor, XsltController controller, GlobalParameterSet staticParameters) : base(processor, controller)
        {
            globalParameterSet = new GlobalParameterSet();
        }

        /*staticParameters*/
        public virtual void SetGlobalContextItem(XdmItem globalContextItem, bool alreadyStripped)
        {
            lock (syncLock)
            {
                if (primed)
                {
                    throw new InvalidOperationException("Stylesheet has already been evaluated");
                }

                this.globalContextItem = globalContextItem == null ? null : globalContextItem.UnderlyingValue;
                this.alreadyStripped = alreadyStripped;
            }
        }

        /*staticParameters*/
        public virtual void SetStylesheetParameters<T>(Dictionary<QName, T> parameters)
        {
            lock (syncLock)
            {
                if (primed)
                {
                    throw new InvalidOperationException("Stylesheet has already been evaluated");
                }

                if (globalParameterSet == null)
                {
                    globalParameterSet = new GlobalParameterSet();
                }

                foreach (KeyValuePair<QName, T> param in parameters)
                {
                    StructuredQName name = param.Key.GetStructuredQName();
                    XdmValue value = (XdmValue)(object)param.Value;
                    try
                    {
                        globalParameterSet.Put(name, (IGroundedValue)(value.UnderlyingValue));
                    }
                    catch (UncheckedXPathException e)
                    {
                        throw new DAXonApiException(e);
                    }
                }
            }
        }

        /*staticParameters*/
        private void Prime()
        {
            if (!primed)
            {
                if (globalParameterSet == null)
                {
                    globalParameterSet = new GlobalParameterSet();
                }

                try
                {
                    controller.SetGlobalContextItem(globalContextItem, alreadyStripped);
                    controller.InitializeController(globalParameterSet);
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

            // Arm the Processor-wide cooperative deadline relative to the start of this run.
            controller.SetTimeout(processor.TransformTimeout);
            primed = true;
        }

        /*staticParameters*/
        public virtual void SetInitialTemplateParameters<T>(Dictionary<QName, T> parameters, bool tunnel)
        {
            lock (syncLock)
            {
                Dictionary<StructuredQName, ISequence> templateParams = new Dictionary<StructuredQName, ISequence>();
                foreach (KeyValuePair<QName, T> entry in parameters)
                {
                    QName key = entry.Key;
                    XdmValue value = (XdmValue)(object)entry.Value;
                    templateParams[key.GetStructuredQName()] = (ISequence)(value.UnderlyingValue);
                }

                controller.SetInitialTemplateParameters(templateParams, tunnel);
            }
        }

        // .NET-native input overload (P5): apply templates to a document read directly from a Stream with an
        // explicit system identifier — the caller no longer constructs a JAXP Source.
        public virtual void ApplyTemplates(Stream input, string systemId, IDestination destination)
        {
            using RunResources run = RunResources.Enter();
            lock (syncLock)
            {
                if (destination == null)
                    throw new NullReferenceException();
                if (input == null)
                    throw new NullReferenceException("input");
                controller.OpenTraceEpisode();
                Prime();
                bool closed = false;
                try
                {
                    IReceiver sOut = GetDestinationReceiver(controller, destination);
                    long length = Resources.ActiveStreamSource.RemainingLength(input);
                    using (System.Xml.XmlReader reader = XmlReaderToReceiver.CreateXmlReader(null, input, systemId, null, false, false, processor.UnderlyingConfiguration))
                    {
                        ApplyTemplatesToXmlReader(reader, systemId, sOut, length);
                    }

                    destination.CloseAndNotify();
                    closed = true;
                }
                catch (XPathException e)
                {
                    if (!e.HasBeenReported())
                    {
                        GetErrorReporter().Report(new XmlProcessingException(e));
                    }

                    throw new DAXonApiException(e);
                }
                catch (RecursionDepthError e)
                {
                    throw new DAXonApiException(e.ToXPathException());
                }
                catch (System.Xml.XmlException e)
                {
                    DAXonApiException err = DAXonApiException.FromXmlParser(e, systemId);
                    GetErrorReporter().Report(new XmlProcessingException((XPathException)err.InnerException));
                    throw err;
                }
                finally
                {
                    controller.CloseTraceEpisode();
                    if (!closed)
                    {
                        DestinationHelper.ReleaseUnclosed(destination);
                    }
                }
            }
        }

        /*staticParameters*/
        public virtual void ApplyTemplates(XdmValue selection, IDestination destination)
        {
            using RunResources run = RunResources.Enter();
            lock (syncLock)
            {
                if (selection == null)
                    throw new NullReferenceException();
                if (destination == null)
                    throw new NullReferenceException();
                controller.OpenTraceEpisode();
                Prime();
                bool closed = false;
                try
                {
                    IReceiver sOut = GetDestinationReceiver(controller, destination);
                    if (baseOutputUriWasSet)
                    {
                        sOut.SetSystemId(GetBaseOutputURI());
                    }

                    controller.ApplyTemplates((ISequence)(selection.UnderlyingValue), sOut);
                    destination.CloseAndNotify();
                    closed = true;
                }
                catch (XPathException e)
                {
                    if (!e.HasBeenReported())
                    {
                        GetErrorReporter().Report(new XmlProcessingException(e));
                    }

                    throw new DAXonApiException(e);
                }
                catch (RecursionDepthError e)
                {
                    throw new DAXonApiException(e.ToXPathException());
                }
                finally
                {
                    controller.CloseTraceEpisode();
                    if (!closed)
                    {
                        DestinationHelper.ReleaseUnclosed(destination);
                    }
                }
            }
        }

        /*staticParameters*/
        public virtual XdmValue ApplyTemplates(XdmValue selection)
        {
            using RunResources run = RunResources.Enter();
            lock (syncLock)
            {
                if (selection == null)
                    throw new NullReferenceException();
                RawDestination raw = new RawDestination();
                ApplyTemplates(selection, raw);
                return raw.GetXdmValue();
            }
        }

        /*staticParameters*/
        public virtual void CallTemplate(QName templateName, IDestination destination)
        {
            using RunResources run = RunResources.Enter();
            lock (syncLock)
            {
                if (destination == null)
                    throw new NullReferenceException();
                controller.OpenTraceEpisode();
                Prime();
                if (templateName == null)
                {
                    templateName = new QName("xsl", NamespaceConstant.XSLT, "initial-template");
                }

                bool closed = false;
                try
                {
                    IReceiver sOut = GetDestinationReceiver(controller, destination);
                    if (baseOutputUriWasSet)
                    {
                        sOut.SetSystemId(GetBaseOutputURI());
                    }

                    controller.CallTemplate(templateName.GetStructuredQName(), sOut);
                    destination.CloseAndNotify();
                    closed = true;
                }
                catch (XPathException e)
                {
                    // The failing path used to CloseAndNotify here, which fired the host's OnClose
                    // listeners for a run that produced no result; the finally now releases the
                    // destination without notifying, as every other entry point does.
                    if (!e.HasBeenReported())
                    {
                        GetErrorReporter().Report(new XmlProcessingException(e));
                    }

                    throw new DAXonApiException(e);
                }
                catch (RecursionDepthError e)
                {
                    throw new DAXonApiException(e.ToXPathException());
                }
                finally
                {
                    controller.CloseTraceEpisode();
                    if (!closed)
                    {
                        DestinationHelper.ReleaseUnclosed(destination);
                    }
                }
            }
        }

        /*staticParameters*/
        public virtual XdmValue CallTemplate(QName templateName)
        {
            using RunResources run = RunResources.Enter();
            lock (syncLock)
            {
                RawDestination dest = new RawDestination();
                CallTemplate(templateName, dest);
                return dest.GetXdmValue();
            }
        }

        /*staticParameters*/
        public virtual XdmValue CallFunction(QName function, XdmValue[] arguments)
        {
            using RunResources run = RunResources.Enter();
            lock (syncLock)
            {
                if (function == null)
                    throw new NullReferenceException();
                if (arguments == null)
                    throw new NullReferenceException();
                controller.OpenTraceEpisode();
                Prime();
                try
                {
                    Component f = GetFunctionComponent(function, arguments);
                    UserFunction uf = (UserFunction)f.GetActor();
                    ISequence[] vr = TypeCheckFunctionArguments(uf, arguments);
                    XPathContextMajor context = controller.NewXPathContext();
                    context.SetCurrentComponent(f);
                    context.TemporaryOutputState = StandardNames.XSL_FUNCTION;
                    context.CurrentOutputUri = null;
                    ISequence result = uf.Call(context, vr);
                    result = result.Materialize();
                    return XdmValue.Wrap(result);
                }
                catch (XPathException e)
                {
                    if (!e.HasBeenReported())
                    {
                        GetErrorReporter().Report(new XmlProcessingException(e));
                    }

                    throw new DAXonApiException(e);
                }
                catch (RecursionDepthError e)
                {
                    throw new DAXonApiException(e.ToXPathException());
                }
                finally
                {
                    controller.CloseTraceEpisode();
                }
            }
        }

        /*staticParameters*/
        private Component GetFunctionComponent(QName function, XdmValue[] arguments)
        {
            lock (syncLock)
            {
                SymbolicName fName = new SymbolicName.F(function.GetStructuredQName(), arguments.Length);
                PreparedStylesheet pss = (PreparedStylesheet)controller.GetExecutable();
                Component f = pss.GetComponent(fName);
                if (f == null)
                {
                    throw new XPathException("No public function with name " + function.ClarkName + " and arity " + arguments.Length + " has been declared in the stylesheet", "XTDE0041");
                }
                else if (f.GetVisibility() != Visibility.FINAL && f.GetVisibility() != Visibility.PUBLIC)
                {
                    throw new XPathException("Cannot invoke " + fName + " externally, because it is not public", "XTDE0041");
                }

                return f;
            }
        }

        /*staticParameters*/
        private ISequence[] TypeCheckFunctionArguments(UserFunction uf, XdmValue[] arguments)
        {
            Configuration config = processor.UnderlyingConfiguration;
            UserFunctionParameter[] @params = uf.GetParameterDefinitions();
            IGroundedValue[] vr = new IGroundedValue[arguments.Length];
            for (int i = 0; i < arguments.Length; i++)
            {
                Values.SequenceType type = @params[i].GetRequiredType();
                vr[i] = (IGroundedValue)arguments[i].UnderlyingValue;
                if (!type.Matches(vr[i], config.GetTypeHierarchy()))
                {
                    int pos = i;
                    Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.FUNCTION, uf.GetFunctionName().DisplayName, pos);
                    ISequence converted = config.GetTypeHierarchy().ApplyFunctionConversionRules(vr[i], type, role, Loc.NONE);
                    vr[i] = converted.Materialize();
                }
            }

            return vr;
        }

        /*staticParameters*/
        public virtual void CallFunction(QName function, XdmValue[] arguments, IDestination destination)
        {
            using RunResources run = RunResources.Enter();
            lock (syncLock)
            {
                controller.OpenTraceEpisode();
                Prime();
                bool ran = false;
                try
                {
                    Component f = GetFunctionComponent(function, arguments);
                    UserFunction uf = (UserFunction)f.GetActor();
                    ISequence[] vr = TypeCheckFunctionArguments(uf, arguments);
                    XPathContextMajor context = controller.NewXPathContext();
                    context.SetCurrentComponent(f);
                    context.TemporaryOutputState = StandardNames.XSL_FUNCTION;
                    context.CurrentOutputUri = null;
                    SerializationProperties @params = controller.GetExecutable().PrimarySerializationProperties;
                    using (IReceiver receiver = destination.GetReceiver(controller.MakePipelineConfiguration(), @params))
                    {
                        receiver.Open();
                        uf.Process(context, vr, new ComplexContentOutputter(receiver));
                        receiver.Close();
                    }

                    ran = true;
                }
                catch (XPathException e)
                {
                    GetErrorReporter().Report(new XmlProcessingException(e));
                    throw new DAXonApiException(e);
                }
                catch (RecursionDepthError e)
                {
                    throw new DAXonApiException(e.ToXPathException());
                }
                finally
                {
                    controller.CloseTraceEpisode();

                    // CloseAndNotify below is unreachable once the catch rethrows, so the
                    // destination has to be released here or its output file stays open.
                    if (!ran)
                    {
                        DestinationHelper.ReleaseUnclosed(destination);
                    }
                }

                destination.CloseAndNotify();
            }
        }

        /*staticParameters*/
        public virtual IDestination AsDocumentDestination(IDestination finalDestination)
        {
            return new AnonymousAbstractDestination(this, finalDestination);
        }

        /*staticParameters*/
        public virtual Serializer NewSerializer()
        {
            Serializer serializer = processor.NewSerializer();
            serializer.SetOutputProperties(controller.GetExecutable().PrimarySerializationProperties);
            return serializer;
        }

        /*staticParameters*/
        public virtual Serializer NewSerializer(string file)
        {
            Serializer serializer = processor.NewSerializer(file);
            serializer.SetOutputProperties(controller.GetExecutable().PrimarySerializationProperties);
            SetBaseOutputURI(new Uri(Path.GetFullPath(file)).AbsoluteUri);
            return serializer;
        }

        /*staticParameters*/
        public virtual Serializer NewSerializer(TextWriter writer)
        {
            Serializer serializer = NewSerializer();
            serializer.SetOutputWriter(writer);
            return serializer;
        }

        /*staticParameters*/
        public virtual Serializer NewSerializer(System.IO.Stream stream)
        {
            Serializer serializer = NewSerializer();
            serializer.SetOutputStream(stream);
            return serializer;
        }

        private sealed class AnonymousAbstractDestination : AbstractDestination
        {

            private readonly Xslt30Transformer parent;
            private readonly IDestination finalDestination;
            private IReceiver receiver;
            public AnonymousAbstractDestination(Xslt30Transformer parent, IDestination finalDestination)
            {
                this.parent = parent;
                this.finalDestination = finalDestination;
            }
            public override IReceiver GetReceiver(PipelineConfiguration pipe, SerializationProperties @params)
            {
                IReceiver rt = parent.GetReceivingTransformer(parent.controller, parent.globalParameterSet, finalDestination);
                rt = new SequenceNormalizerWithSpaceSeparator(rt);
                rt.SetPipelineConfiguration(pipe);
                return receiver = rt;
            }

            public override void Close()
            {
                try
                {
                    receiver.Close();
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
    }
}
