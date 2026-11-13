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
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Streams;
using System.IO;
namespace OutSmart.DAXon.Api
{
    public class Xslt30Transformer : AbstractXsltTransformer
    {
        private GlobalParameterSet globalParameterSet;
        private bool primed;
        private IItem globalContextItem;
        private bool alreadyStripped;

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

                ArgumentCheck.NotNull(parameters, nameof(parameters));
                foreach (KeyValuePair<QName, T> param in parameters)
                {
                    StructuredQName name = param.Key.GetStructuredQName();
                    XdmValue value = ArgumentCheck.ParameterValue(param.Key, param.Value, nameof(parameters));
                    try
                    {
                        globalParameterSet.Put(name, (IGroundedValue)(value.UnderlyingValue));
                    }
                    catch (Exception e) when (ApiErrors.IsEngineError(e))
                    {
                        throw ApiErrors.Wrap(e);
                    }
                }
            }
        }

        private void Prime()
        {
            if (!primed)
            {
                if (globalParameterSet == null)
                {
                    globalParameterSet = new GlobalParameterSet();
                }

                controller.SetGlobalContextItem(globalContextItem, alreadyStripped);
                controller.InitializeController(globalParameterSet);
            }

            // Arm the Processor-wide cooperative deadline relative to the start of this run.
            controller.SetTimeout(processor.TransformTimeout);
            RestoreBaseOutputURI();
            primed = true;
        }

        // The trees in a function's arguments count against the run's memory limit, as its context item does.
        private void ChargeArguments(XdmValue[] arguments)
        {
            if (arguments != null)
            {
                foreach (XdmValue argument in arguments)
                {
                    controller.ChargeInput((ISequence)argument?.UnderlyingValue);
                }
            }
        }

        public virtual void SetInitialTemplateParameters<T>(Dictionary<QName, T> parameters, bool tunnel)
        {
            lock (syncLock)
            {
                ArgumentCheck.NotNull(parameters, nameof(parameters));
                Dictionary<StructuredQName, ISequence> templateParams = new Dictionary<StructuredQName, ISequence>();
                foreach (KeyValuePair<QName, T> entry in parameters)
                {
                    QName key = entry.Key;
                    XdmValue value = ArgumentCheck.ParameterValue(key, entry.Value, nameof(parameters));
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
                ArgumentCheck.NotNull(destination, nameof(destination));
                if (input == null)
                {
                    // No source is no initial match selection, as in s9api; a null selection is a bad argument there too.
                    throw new DAXonApiException(new XPathException("No initial match selection supplied", "XTDE0044"));
                }

                Run(destination, systemId, () =>
                {
                    Prime();
                    IReceiver sOut = GetDestinationReceiver(controller, destination);
                    long length = Resources.ActiveStreamSource.RemainingLength(input);
                    Stream capped = InputSizeLimit.Apply(input, length, InputSizeLimit.MaxFor(processor.UnderlyingConfiguration), systemId, "FODC0002");
                    Configuration config = processor.UnderlyingConfiguration;
                    try
                    {
                        using (System.Xml.XmlReader reader = XmlReaderToReceiver.CreateXmlReader(null, capped, systemId, null, XmlReaderToReceiver.DtdUseFor(config.GetParseOptions()), config, 0, GetErrorReporter()))
                        {
                            ApplyTemplatesToXmlReader(reader, systemId, sOut, length);
                        }
                    }
                    catch (Exception e) when (DAXonApiException.IsIO(e))
                    {
                        // the input failed to read (output errors are the emitters' XPathExceptions): as DocumentBuilder
                        throw ApiErrors.ErrorOf(e, systemId);
                    }
                });
            }
        }

        public virtual void ApplyTemplates(XdmValue selection, IDestination destination)
        {
            using RunResources run = RunResources.Enter();
            lock (syncLock)
            {
                ArgumentCheck.NotNull(selection, nameof(selection));
                ArgumentCheck.NotNull(destination, nameof(destination));
                Run(destination, null, () =>
                {
                    Prime();
                    controller.ChargeInput((ISequence)(selection.UnderlyingValue));
                    IReceiver sOut = GetDestinationReceiver(controller, destination);
                    if (baseOutputUriWasSet)
                    {
                        sOut.SetSystemId(GetBaseOutputURI());
                    }

                    controller.ApplyTemplates((ISequence)(selection.UnderlyingValue), sOut);
                });
            }
        }

        public virtual XdmValue ApplyTemplates(XdmValue selection)
        {
            using RunResources run = RunResources.Enter();
            lock (syncLock)
            {
                ArgumentCheck.NotNull(selection, nameof(selection));
                RawDestination raw = new RawDestination();
                ApplyTemplates(selection, raw);
                return raw.GetXdmValue();
            }
        }

        public virtual void CallTemplate(QName templateName, IDestination destination)
        {
            using RunResources run = RunResources.Enter();
            lock (syncLock)
            {
                ArgumentCheck.NotNull(destination, nameof(destination));
                StructuredQName name = (templateName ?? new QName("xsl", NamespaceConstant.XSLT, "initial-template")).GetStructuredQName();
                Run(destination, null, () =>
                {
                    Prime();
                    IReceiver sOut = GetDestinationReceiver(controller, destination);
                    if (baseOutputUriWasSet)
                    {
                        sOut.SetSystemId(GetBaseOutputURI());
                    }

                    controller.CallTemplate(name, sOut);
                });
            }
        }

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

        public virtual XdmValue CallFunction(QName function, XdmValue[] arguments)
        {
            using RunResources run = RunResources.Enter();
            lock (syncLock)
            {
                ArgumentCheck.NotNull(function, nameof(function));
                ArgumentCheck.NoNulls(arguments, nameof(arguments));
                return Run(null, null, () =>
                {
                    Prime();
                    ChargeArguments(arguments);
                    Component f = GetFunctionComponent(function, arguments);
                    UserFunction uf = (UserFunction)f.GetActor();
                    ISequence[] vr = TypeCheckFunctionArguments(uf, arguments);
                    ISequence result = uf.Call(FunctionContext(f), vr);
                    return XdmValue.Wrap(result.Materialize());
                });
            }
        }

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

        // The context of a stylesheet function the host calls.
        private XPathContextMajor FunctionContext(Component f)
        {
            XPathContextMajor context = controller.NewXPathContext();
            context.SetCurrentComponent(f);
            context.TemporaryOutputState = StandardNames.XSL_FUNCTION;
            context.CurrentOutputUri = null;
            return context;
        }

        private ISequence[] TypeCheckFunctionArguments(UserFunction uf, XdmValue[] arguments)
        {
            Configuration config = processor.UnderlyingConfiguration;
            UserFunctionParameter[] @params = uf.GetParameterDefinitions();
            IGroundedValue[] vr = new IGroundedValue[arguments.Length];
            for (int i = 0; i < arguments.Length; i++)
            {
                Values.SequenceType type = @params[i].GetRequiredType();
                vr[i] = (IGroundedValue)arguments[i].UnderlyingValue;
                if (controller.GetConfiguration().HoldsForeignNode(vr[i]))
                {
                    throw new XPathException(ApiErrors.ForeignArgument, DAXonErrorCode.SXXP0004);
                }

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

        public virtual void CallFunction(QName function, XdmValue[] arguments, IDestination destination)
        {
            ArgumentCheck.NotNull(function, nameof(function));
            ArgumentCheck.NoNulls(arguments, nameof(arguments));
            ArgumentCheck.NotNull(destination, nameof(destination));
            using RunResources run = RunResources.Enter();
            lock (syncLock)
            {
                Run(destination, null, () =>
                {
                    Prime();
                    ChargeArguments(arguments);
                    Component f = GetFunctionComponent(function, arguments);
                    UserFunction uf = (UserFunction)f.GetActor();
                    ISequence[] vr = TypeCheckFunctionArguments(uf, arguments);
                    XPathContextMajor context = FunctionContext(f);
                    SerializationProperties @params = controller.GetExecutable().PrimarySerializationProperties;
                    using (IReceiver receiver = destination.GetReceiver(controller.MakePipelineConfiguration(), @params))
                    {
                        receiver.Open();
                        uf.Process(context, vr, new ComplexContentOutputter(receiver));
                        receiver.Close();
                    }
                });
            }
        }

        public virtual IDestination AsDocumentDestination(IDestination finalDestination)
        {
            return new AnonymousAbstractDestination(this, finalDestination);
        }

        public virtual Serializer NewSerializer()
        {
            Serializer serializer = processor.NewSerializer();
            serializer.SetOutputProperties(controller.GetExecutable().PrimarySerializationProperties);
            return serializer;
        }

        public virtual Serializer NewSerializer(string file)
        {
            Serializer serializer = processor.NewSerializer(file);
            serializer.SetOutputProperties(controller.GetExecutable().PrimarySerializationProperties);
            try
            {
                SetBaseOutputURI(OutSmart.DAXon.Internal.Net.FileUris.Of(Path.GetFullPath(file)));
            }
            catch (Exception e) when (e is ArgumentException || e is NotSupportedException || e is IOException || e is FormatException || e is System.Security.SecurityException)
            {
                // As in Serializer.SetOutputFile: the name is refused when the file is opened, as SXRD0004
            }

            return serializer;
        }

        public virtual Serializer NewSerializer(TextWriter writer)
        {
            Serializer serializer = NewSerializer();
            serializer.SetOutputWriter(writer);
            return serializer;
        }

        public virtual Serializer NewSerializer(System.IO.Stream stream)
        {
            Serializer serializer = NewSerializer();
            serializer.SetOutputStream(stream);
            return serializer;
        }

        private sealed class AnonymousAbstractDestination : AbstractDestination, IChainedDestination
        {

            IDestination IChainedDestination.Next => finalDestination;

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
                // Closed before anything was pushed into it: there is no run to finish.
                if (receiver == null)
                {
                    return;
                }

                try
                {
                    receiver.Close();
                }
                catch (Exception e) when (ApiErrors.IsEngineError(e))
                {
                    throw ApiErrors.Wrap(e);
                }
            }
        }
    }
}
