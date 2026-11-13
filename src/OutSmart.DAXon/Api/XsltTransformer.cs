////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Serialization;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Internal.Net;
using System;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Api
{
    public class XsltTransformer : AbstractXsltTransformer, IDestination, IChainedDestination
    {
        private QName initialTemplateName;
        private GlobalParameterSet parameters;
        private IActiveSource initialSource;
        private IDestination destination;

        IDestination IChainedDestination.Next => destination;
        private readonly DestinationHelper destinationHelper;
        private URI destinationBaseUri;

        public URI DestinationBaseURI
        {
            get => destinationBaseUri; set
            {
                this.destinationBaseUri = value;
            }
        }

        public virtual QName InitialTemplate
        {
            get => initialTemplateName; set
            {
                initialTemplateName = value;
            }
        }

        public virtual XdmNode InitialContextNode
        {
            get
            {
                if (initialSource is NodeInfo)
                {
                    return (XdmNode)XdmValue.Wrap((NodeInfo)initialSource);
                }
                else if (initialSource is NodeSource)
                {
                    NodeInfo n = ((NodeSource)initialSource).Node;
                    return (XdmNode)XdmValue.Wrap(n);
                }
                else
                {
                    return null;
                }
            }
            set
            {
                lock (syncLock)
                {
                    try
                    {
                        if (value == null)
                        {
                            initialSource = null;
                            controller.GlobalContextItem = null;
                        }
                        else
                        {
                            initialSource = value.UnderlyingNode;
                            controller.GlobalContextItem = value.UnderlyingNode.Root;
                        }
                    }
                    catch (Exception e) when (ApiErrors.IsEngineError(e))
                    {
                        throw ApiErrors.WrapUnchecked(e);
                    }
                }
            }
        }

        public virtual IDestination Destination
        {
            get => destination; set
            {
                this.destination = value;
            }
        }
        public XsltTransformer(Processor processor, XsltController controller, GlobalParameterSet staticParameters) : base(processor, controller)
        {
            parameters = new GlobalParameterSet();
            destinationHelper = new DestinationHelper(this);
        }

        public virtual void OnClose(IAction listener)
        {
            destinationHelper.OnClose(listener);
        }

        public void CloseAndNotify()
        {
            destinationHelper.CloseAndNotify();
        }

        public virtual void SetParameter(QName name, XdmValue value)
        {
            ArgumentCheck.NotNull(name, nameof(name));
            lock (syncLock)
            {
                try
                {
                    parameters.Put(name.GetStructuredQName(), value == null ? null : ((ISequence)value.UnderlyingValue).Materialize());
                }
                catch (Exception e) when (ApiErrors.IsEngineError(e))
                {
                    throw ApiErrors.WrapUnchecked(e);
                }
            }
        }

        public virtual void ClearParameters()
        {
            lock (syncLock)
            {
                parameters = new GlobalParameterSet();
            }
        }

        public virtual XdmValue GetParameter(QName name)
        {
            ArgumentCheck.NotNull(name, nameof(name));
            lock (syncLock)
            {
                ISequence oval = parameters[name.GetStructuredQName()];
                return oval == null ? null : XdmValue.Wrap(oval);
            }
        }

        public virtual void Transform()
        {
            using RunResources run = RunResources.Enter();
            lock (syncLock)
            {
                if (destination == null)
                {
                    throw new InvalidOperationException("No destination has been supplied");
                }

                RestoreBaseOutputURI();
                Run(destination, null, () =>
                {
                    IActiveSource initialSelection = initialSource;
                    bool reset = false;
                    try
                    {
                        // Arm the Processor-wide cooperative deadline relative to the start of this run.
                        // BEFORE the source parse below: the parse loop honours the thread's active
                        // deadline, so it must see this run's fresh budget, not a spent token left on
                        // the thread by an earlier run.
                        controller.SetTimeout(processor.TransformTimeout);
                        IReceiver @out = GetDestinationReceiver(controller, destination);
                        GlobalContextRequirement gcr = controller.GetExecutable().GlobalContextRequirement;
                        if ((gcr == null || !gcr.IsAbsentFocus()) && initialSelection != null)
                        {
                            if (initialSelection is NodeInfo)
                            {
                                reset = MaybeSetGlobalContextItem((NodeInfo)initialSelection);
                            }
                            else
                            {
                                NodeInfo node = controller.MakeSourceTree(initialSelection, GetSchemaValidationMode().GetNumber());
                                reset = MaybeSetGlobalContextItem(node);
                                initialSelection = node;
                            }
                        }

                        if (baseOutputUriWasSet)
                        {
                            @out.SetSystemId(GetBaseOutputURI());
                        }

                        controller.InitializeController(parameters);
                        controller.ChargeRunInputs();   // installed after the limits were armed
                        if (initialTemplateName != null)
                        {
                            controller.CallTemplate(initialTemplateName.GetStructuredQName(), @out);
                        }
                        else if (initialSelection != null)
                        {
                            ApplyTemplatesToSource(initialSelection, @out);
                        }
                        else
                        {
                            QName entryPoint = new QName("xsl", NamespaceConstant.XSLT, "initial-template");
                            controller.CallTemplate(entryPoint.GetStructuredQName(), @out);
                        }
                    }
                    finally
                    {
                        if (reset)
                        {
                            controller.ClearGlobalContextItem();
                        }
                    }
                });
            }
        }

        private bool MaybeSetGlobalContextItem(IItem item)
        {
            if (controller.GlobalContextItem == null)
            {
                // Not alreadyStripped: MakeSourceTree hands a NodeInfo straight back, so nothing
                // on this path has applied xsl:strip-space yet. No probe covers this - the branch
                // needs a null global context item, and the only setter that leaves it null is
                // SetSource, which has no callers. Corrected so it stays right if that changes.
                controller.SetGlobalContextItem(item, false);
                return true;
            }
            else
            {
                return false;
            }
        }

        public IReceiver GetReceiver(PipelineConfiguration pipe, SerializationProperties @params)
        {
            ArgumentCheck.NotNull(pipe, nameof(pipe));
            ArgumentCheck.NotNull(@params, nameof(@params));
            if (destination == null)
            {
                throw new InvalidOperationException("No destination has been supplied");
            }

            IReceiver rt = GetReceivingTransformer(controller, parameters, destination);
            rt = new SequenceNormalizerWithSpaceSeparator(rt);
            rt.SetPipelineConfiguration(pipe);
            return rt;
        }

        public void Close()
        {
        }

        public void Dispose()
        {
        }
    }
}

