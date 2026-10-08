////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Accumulators;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Serialization;
using OutSmart.DAXon.Xslt;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Trees.Wrappers;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Transformation
{
    public class XsltController : Controller
    {
        private readonly Dictionary<StructuredQName, int> messageCounters = new Dictionary<StructuredQName, int>();
        private bool assertionsEnabled = true;
        private IResultDocumentResolver resultDocumentResolver;
        private HashSet<DocumentKey> allOutputDestinations;
        private Component.M initialMode = null;
        private Dictionary<StructuredQName, ISequence> initialTemplateParams;
        private Dictionary<StructuredQName, ISequence> initialTemplateTunnelParams;
        private readonly Dictionary<long, Stack<AttributeSet>> attributeSetEvaluationStacks = new Dictionary<long, Stack<AttributeSet>>();
        private AccumulatorManager accumulatorManager = new AccumulatorManager();
        private PrincipalOutputGatekeeper gatekeeper = null;
        private IDestination principalDestination;
        private TemplateRuleTraceListener templateRuleTraceListener = null;
        private Action<Message> messageHandler;

        public virtual StructuredQName InitialModeName => initialMode == null ? null : initialMode.GetActor().ModeName;

        public virtual Action<Message> MessageHandler
        {
            get => messageHandler; set
            {
                messageHandler = value;
            }
        }

        public virtual IReceiver MessageEmitter
        {
            get => null; set
            {
            }
        }

        public virtual Dictionary<StructuredQName, int> MessageCounters
        {
            get
            {
                lock (messageCounters)
                {
                    return new Dictionary<StructuredQName, int>(messageCounters);
                }
            }
        }

        public virtual IResultDocumentResolver ResultDocumentResolver
        {
            get => resultDocumentResolver; set
            {
                this.resultDocumentResolver = value;
            }
        }

        public virtual IDestination PrincipalDestination
        {
            get => principalDestination; set
            {
                principalDestination = value;
            }
        }

        public virtual TemplateRuleTraceListener TemplateRuleTraceListener
        {
            get => templateRuleTraceListener; set
            {
                this.templateRuleTraceListener = value;
            }
        }

        public virtual PrincipalOutputGatekeeper Gatekeeper => gatekeeper;

        public virtual Stack<AttributeSet> AttributeSetEvaluationStack
        {
            get
            {
                lock (syncLock)
                {
                    long thread = Environment.CurrentManagedThreadId;

                    return attributeSetEvaluationStacks.ComputeIfAbsent(thread, (k) => new Stack<AttributeSet>());
                }
            }
        }
        public XsltController(Configuration config, PreparedStylesheet pss) : base(config, pss)
        {
            InitMessageHandler(config);
        }

        private void InitMessageHandler(Configuration config)
        {
            messageHandler = new StandardMessageHandler(config);
        }

        public override void Reset()
        {
            base.Reset();
            Configuration config = GetConfiguration();
            validationMode = config.SchemaValidationMode;
            traceListener = null;
            ITraceListener tracer;
            try
            {
                tracer = config.MakeTraceListener();
            }
            catch (XPathException err)
            {
                throw new InvalidOperationException(err.Message);
            }

            if (tracer != null)
            {
                AddTraceListener(tracer);
            }

            Model = config.GetParseOptions().Model;
            globalContextItem = null;
            initialMode = null;
            ClearPerTransformationData();
        }

        protected internal override void ReleaseRunState()
        {
            lock (syncLock)
            {
                base.ReleaseRunState();

                // Both of these were reset only in Reset(), i.e. only from the constructor, so they
                // lived as long as the Xslt30Transformer rather than as long as the run. The
                // accumulator manager is the expensive one: its two maps key on ITreeInfo with STRONG
                // references and the class has no Remove/Clear at all, and SetApplicableAccumulators
                // runs for EVERY source document whether or not the stylesheet declares an
                // accumulator - so a reused transformer pinned every tree it ever saw.
                accumulatorManager = new AccumulatorManager();

                // The attribute-set frame stack is keyed by managed thread id and was never removed
                // except when a stack happened to empty; an aborted expansion left the entry behind.
                attributeSetEvaluationStacks.Clear();
            }
        }

        protected override void ClearPerTransformationData()
        {
            lock (syncLock)
            {
                base.ClearPerTransformationData();
                principalResult = null;
                allOutputDestinations = null;
                if (messageCounters != null)
                {
                    messageCounters.Clear();
                }
            }
        }

        public virtual void SetInitialMode(StructuredQName expandedModeName)
        {
            if (expandedModeName == null || expandedModeName.Equals(Mode.UNNAMED_MODE_NAME))
            {
                Mode initial = ((PreparedStylesheet)executable).GetRuleManager().ObtainMode(Mode.UNNAMED_MODE_NAME, true);
                initialMode = initial.GetDeclaringComponent();
            }
            else
            {
                StylesheetPackage topLevelPackage = (StylesheetPackage)executable.TopLevelPackage;
                if (expandedModeName.Equals(Mode.DEFAULT_MODE_NAME))
                {
                    StructuredQName defaultModeName = topLevelPackage.DefaultMode;
                    if (!expandedModeName.Equals(defaultModeName))
                    {
                        SetInitialMode(defaultModeName);
                    }
                }
                else
                {
                    bool declaredModes = topLevelPackage.IsDeclaredModes();
                    SymbolicName sn = new SymbolicName(StandardNames.XSL_MODE, expandedModeName);
                    Component.M c = (Component.M)topLevelPackage.GetComponent(sn);
                    if (c == null)
                    {
                        throw new XPathException("Requested initial mode " + expandedModeName + " is not defined in the stylesheet", "XTDE0045");
                    }

                    if (!((PreparedStylesheet)executable).IsEligibleInitialMode(c))
                    {
                        throw new XPathException("Requested initial mode " + expandedModeName + " is private in the top-level package", "XTDE0045");
                    }

                    initialMode = c;
                    if (!declaredModes && initialMode.GetActor().IsEmpty() && !expandedModeName.Equals(topLevelPackage.DefaultMode))
                    {
                        throw new XPathException("Requested initial mode " + expandedModeName + " contains no template rules", "XTDE0045");
                    }
                }
            }
        }

        public virtual Mode GetInitialMode()
        {
            if (initialMode == null)
            {
                StylesheetPackage top = (StylesheetPackage)executable.TopLevelPackage;
                StructuredQName defaultMode = top.DefaultMode;
                if (defaultMode == null)
                {
                    defaultMode = Mode.UNNAMED_MODE_NAME;
                }

                Component.M c = (Component.M)top.GetComponent(new SymbolicName(StandardNames.XSL_MODE, defaultMode));
                initialMode = c;
                return c.GetActor();
            }
            else
            {
                return initialMode.GetActor();
            }
        }

        public virtual AccumulatorManager GetAccumulatorManager()
        {
            return accumulatorManager;
        }

        public virtual bool CheckUniqueOutputDestination(DocumentKey uri)
        {
            lock (syncLock)
            {
                if (uri == null)
                {
                    return true; // happens when writing say to an anonymous System.IO.StringWriter
                }

                if (allOutputDestinations == null)
                {
                    allOutputDestinations = new HashSet<DocumentKey>();
                }

                return !allOutputDestinations.Contains(uri);
            }
        }

        public virtual void AddUnavailableOutputDestination(DocumentKey uri)
        {
            lock (syncLock)
            {
                if (allOutputDestinations == null)
                {
                    allOutputDestinations = new HashSet<DocumentKey>();
                }

                allOutputDestinations.Add(uri);
            }
        }

        public virtual void RemoveUnavailableOutputDestination(DocumentKey uri)
        {
            lock (syncLock)
            {
                if (allOutputDestinations != null)
                {
                    allOutputDestinations.Remove(uri);
                }
            }
        }

        public virtual bool IsUnusedOutputDestination(DocumentKey uri)
        {
            lock (syncLock)
            {
                return allOutputDestinations == null || !allOutputDestinations.Contains(uri);
            }
        }

        public virtual void SetInitialTemplateParameters(Dictionary<StructuredQName, ISequence> @params, bool tunnel)
        {
            if (tunnel)
            {
                this.initialTemplateTunnelParams = @params;
            }
            else
            {
                this.initialTemplateParams = @params;
            }
        }

        public virtual Dictionary<StructuredQName, ISequence> GetInitialTemplateParameters(bool tunnel)
        {
            return tunnel ? initialTemplateTunnelParams : initialTemplateParams;
        }

        // The initial template's parameters count against the memory limit too.
        internal override void ChargeRunInputs()
        {
            base.ChargeRunInputs();
            foreach (Dictionary<StructuredQName, ISequence> parameters in new[] { initialTemplateParams, initialTemplateTunnelParams })
            {
                if (parameters != null)
                {
                    foreach (ISequence value in parameters.Values)
                    {
                        ChargeInput(value);
                    }
                }
            }
        }

        public virtual void SetMessageFactory(Func<IReceiver> messageReceiverFactory)
        {
        }

        public virtual void SetMessageReceiverClassName(string name)
        {
        }

        public virtual IReceiver MakeMessageReceiver()
        {
            return null;
        }

        public virtual void IncrementMessageCounter(StructuredQName code)
        {
            lock (messageCounters)
            {
                int n = messageCounters.GetOrDefault(code, 0);
                messageCounters[code] = n + 1;
            }
        }

        public virtual IOutputURIResolver GetOutputURIResolver()
        {
            if (resultDocumentResolver is OutputURIResolverWrapper)
            {
                return ((OutputURIResolverWrapper)resultDocumentResolver).GetOutputURIResolver();
            }
            else
            {
                return GetConfiguration().GetOutputURIResolver();
            }
        }

        public virtual void SetOutputURIResolver(IOutputURIResolver resolver)
        {
            IOutputURIResolver our = resolver == null ? GetConfiguration().GetOutputURIResolver() : resolver;
            ResultDocumentResolver = new OutputURIResolverWrapper(our);
        }

        public virtual bool IsAssertionsEnabled()
        {
            return assertionsEnabled;
        }

        public virtual void SetAssertionsEnabled(bool enabled)
        {
            this.assertionsEnabled = enabled;
        }

        public override void PreEvaluateGlobals(IXPathContext context)
        {
        }

        public virtual void ApplyTemplates(ISequence source, IReceiver @out)
        {
            CheckReadiness();
            try
            {
                ComplexContentOutputter dest = PrepareOutputReceiver(@out);
                XPathContextMajor initialContext = NewXPathContext();
                initialContext.CreateThreadManager();
                initialContext.Origin = this;
                Mode mode = GetInitialMode();
                if (mode == null)
                {
                    throw new XPathException("Requested initial mode " + (initialMode == null ? "#unnamed" : initialMode.GetActor().ModeName.DisplayName) + " does not exist", "XTDE0045");
                }

                if (!((PreparedStylesheet)executable).IsEligibleInitialMode(initialMode))
                {
                    throw new XPathException("Requested initial mode " + (mode.ModeName.DisplayName) + " is not public or final", "XTDE0045");
                }

                WarningIfStreamable(mode);

                // Process the source document by applying template rules to the initial context node
                ParameterSet ordinaryParams = null;
                if (initialTemplateParams != null)
                {
                    ordinaryParams = new ParameterSet(initialTemplateParams);
                }

                ParameterSet tunnelParams = null;
                if (initialTemplateTunnelParams != null)
                {
                    tunnelParams = new ParameterSet(initialTemplateTunnelParams);
                }

                ISequenceIterator iter = source.Iterate();
                IMappingFunction preprocessor = GetInputPreprocessor(mode);
                iter = new Expressions.MappingIterator(iter, preprocessor);
                initialContext.TrackFocus(iter);
                initialContext.SetCurrentMode(initialMode);
                initialContext.SetCurrentComponent(initialMode);
                ITailCall tc = mode.ApplyTemplates(ordinaryParams, tunnelParams, null, dest, initialContext, Loc.NONE);
                while (tc != null)
                {
                    tc = tc.ProcessLeavingTail();
                }

                initialContext.WaitForChildThreads();
                dest.Close();
            }
            catch (TerminationException err)
            {

                if (!err.HasBeenReported())
                {
                    ReportFatalError(err);
                }

                throw;
            }
            catch (UncheckedXPathException err)
            {
                HandleXPathException(err.GetXPathException());
            }
            catch (XPathException err)
            {
                HandleXPathException(err);
            }
            finally
            {
                inUse = false;
                principalResultURI = null;
                ReleaseRunState();
            }
        }

        private ComplexContentOutputter PrepareOutputReceiver(IReceiver @out)
        {
            principalResult = @out;
            if (principalResultURI == null)
            {
                principalResultURI = @out.GetSystemId();
            }

            if (GetExecutable().CreatesSecondaryResult())
            {

                // This is for the case where the stylesheet writes no output to the primary destination,
                // and then calls xsl:result-document with a null or empty href, in which case the xsl:result-document
                // output is sent to the primary output destination, but with different serialization properties.
                @out = this.gatekeeper = new PrincipalOutputGatekeeper(this, @out);
            }


            //@out = new RegularSequenceChecker(@out); // uncomment for debugging
            //@out = new TracingFilter(@out); // uncomment for debugging
            //NamespaceReducer nr = new NamespaceReducer(@out);
            ComplexContentOutputter cco = new ComplexContentOutputter(@out);
            cco.SetSystemId(@out.GetSystemId());
            cco.Open();
            return cco;
        }

        private IMappingFunction GetInputPreprocessor(Mode finalMode)
        {
            return SequenceMapper.Of((item) =>
            {
                if (item is NodeInfo)
                {
                    NodeInfo node = (NodeInfo)item;
                    if (node.GetConfiguration() == null)
                    {

                        // must be a non-standard document implementation
                        throw new XPathException("The supplied source document must be associated with a Configuration");
                    }

                    if (!node.GetConfiguration().IsCompatible(executable.GetConfiguration()))
                    {
                        throw new XPathException("Source document and stylesheet must use the same or compatible Configurations", DAXonErrorCode.SXXP0004);
                    }

                    if (node.GetTreeInfo().IsTyped() && !executable.IsSchemaAware())
                    {
                        throw new XPathException("Cannot use a schema-validated source document unless the stylesheet is schema-aware");
                    }

                    if (IsStylesheetStrippingTypeAnnotations() && node != globalContextItem)
                    {
                        ITreeInfo docInfo = node.GetTreeInfo();
                        if (docInfo.IsTyped())
                        {
                            TypeStrippedDocument strippedDoc = new TypeStrippedDocument(docInfo);
                            node = strippedDoc.Wrap(node);
                        }
                    }

                    // Whether the tree still needs stripping is decided by the tree itself - it is
                    // either a stripped wrapper or its TreeInfo already carries this rule. An
                    // identity test against globalContextItem used to stand in for that, which
                    // held only while callers were honest: SetGlobalContextItem(node, true) stores
                    // the node unstripped, so the guard matched and xsl:strip-space was lost for
                    // the whole transform. The type-annotation test above keeps its identity check
                    // - typed trees need a schema-aware processor, so it has no reachable trigger.
                    ISpaceStrippingRule spaceStrippingRule = SpaceStrippingRule;
                    if (IsStylesheetContainingStripSpace() && IsStripSourceTree() && !(node is SpaceStrippedNode) && node.GetTreeInfo().SpaceStrippingRule != spaceStrippingRule
                        && !(node.GetTreeInfo() is Trees.Tiny.TinyTree noWs && !noWs.ContainsWhitespaceText()))
                    {
                        SpaceStrippedDocument strippedDoc = new SpaceStrippedDocument(node.GetTreeInfo(), spaceStrippingRule);

                        // Edge case: the item might itself be a whitespace text node that is stripped
                        if (!SpaceStrippedNode.IsPreservedNode(node, strippedDoc, node.GetParent()))
                        {
                            return EmptyIterator.GetInstance();
                        }

                        node = strippedDoc.Wrap(node);
                    }

                    if (GetAccumulatorManager() != null)
                    {
                        GetAccumulatorManager().SetApplicableAccumulators(node.GetTreeInfo(), finalMode.Accumulators);
                    }

                    return SingletonIterator.MakeIterator(node);
                }
                else
                {
                    return SingletonIterator.MakeIterator(item);
                }
            });
        }

        private void WarningIfStreamable(Mode mode)
        {
            if (mode.IsDeclaredStreamable())
            {
                Warning((initialMode == null ? "" : GetInitialMode().GetModeTitle(true)) + " is streamable, but the input is not supplied as a stream", DAXonErrorCode.SXWN9045, Loc.NONE);
            }
        }

        public virtual void CallTemplate(StructuredQName initialTemplateName, IReceiver @out)
        {
            CheckReadiness();
            try
            {
                ComplexContentOutputter dest = PrepareOutputReceiver(@out);
                XPathContextMajor initialContext = NewXPathContext();
                initialContext.CreateThreadManager();
                initialContext.Origin = this;
                if (globalContextItem != null)
                {
                    initialContext.SetCurrentIterator(new ManualIterator(globalContextItem));
                }


                // Process the source document by invoking the initial named template
                ParameterSet ordinaryParams = null;
                if (initialTemplateParams != null)
                {
                    ordinaryParams = new ParameterSet(initialTemplateParams);
                }

                ParameterSet tunnelParams = null;
                if (initialTemplateTunnelParams != null)
                {
                    tunnelParams = new ParameterSet(initialTemplateTunnelParams);
                }

                StylesheetPackage pack = (StylesheetPackage)executable.TopLevelPackage;
                Component initialComponent = pack.GetComponent(new SymbolicName(StandardNames.XSL_TEMPLATE, initialTemplateName));
                if (initialComponent == null)
                {
                    throw new XPathException("Template " + initialTemplateName.DisplayName + " does not exist", "XTDE0040");
                }

                if (!pack.IsImplicitPackage() && !(initialComponent.GetVisibility() == Visibility.PUBLIC || initialComponent.GetVisibility() == Visibility.FINAL))
                {
                    throw new XPathException("Template " + initialTemplateName.DisplayName + " is " + Err.DescribeVisibility(initialComponent.GetVisibility()), "XTDE0040");
                }

                NamedTemplate t = (NamedTemplate)initialComponent.GetActor();
                XPathContextMajor c2 = initialContext.NewContext();
                initialContext.Origin = this;
                c2.SetCurrentComponent(initialComponent);
                c2.OpenStackFrame(t.GetStackFrameMap());
                c2.SetLocalParameters(ordinaryParams);
                c2.SetTunnelParameters(tunnelParams);
                ITailCall tc = t.Expand(dest, c2);
                while (tc != null)
                {
                    tc = tc.ProcessLeavingTail();
                }

                initialContext.WaitForChildThreads();
                dest.Close();
            }
            catch (UncheckedXPathException err)
            {
                HandleXPathException(err.GetXPathException());
            }
            catch (XPathException err)
            {
                HandleXPathException(err);
            }
            finally
            {
                inUse = false;
                ReleaseRunState();
            }
        }

        public virtual void ApplyStreamingTemplates(IActiveSource source, IReceiver @out)
        {
            CheckReadiness();
            ComplexContentOutputter dest = PrepareOutputReceiver(@out);
            try
            {
                int validationMode = SchemaValidationMode;
                IActiveSource underSource = source;

                Configuration config = GetConfiguration();
                IActiveSource s2 = underSource;

                if (!initialMode.GetActor().IsDeclaredStreamable())
                {
                    throw new ArgumentException("Initial mode is not streamable");
                }

                XPathContextMajor initialContext = NewXPathContext();
                initialContext.CreateThreadManager();
                initialContext.Origin = this;

                // Process the source document by applying template rules to the initial context node
                ParameterSet ordinaryParams = null;
                if (initialTemplateParams != null)
                {
                    ordinaryParams = new ParameterSet(initialTemplateParams);
                }

                ParameterSet tunnelParams = null;
                if (initialTemplateTunnelParams != null)
                {
                    tunnelParams = new ParameterSet(initialTemplateTunnelParams);
                }

                IReceiver despatcher = config.MakeStreamingTransformer(initialMode.GetActor(), ordinaryParams, tunnelParams, dest, initialContext);
                if (config.IsStripsAllWhiteSpace() || IsStylesheetContainingStripSpace())
                {
                    despatcher = MakeStripper(despatcher);
                }

                PipelineConfiguration pipe = despatcher.GetPipelineConfiguration();
                pipe.SetParseOptions(pipe.GetParseOptions().WithSchemaValidationMode(this.validationMode));
                bool verbose = GetConfiguration().IsTiming();
                if (verbose)
                {
                    GetConfiguration().Logger.Info("Streaming " + source.GetSystemId());
                }

                try
                {
                    if (s2 != null)
                    {
                        Sender.Send(s2, despatcher, null);
                    }
                    else
                    {
                        Sender.Send(underSource, despatcher, null);
                    }
                }
                catch (QuitParsingException)
                {
                    if (verbose)
                    {
                        GetConfiguration().Logger.Info("Streaming " + source.GetSystemId() + " : early exit");
                    }
                }

                initialContext.WaitForChildThreads();
                dest.Close();
            }
            catch (TerminationException err)
            {

                if (!err.HasBeenReported())
                {
                    ReportFatalError(err);
                }

                throw;
            }
            catch (UncheckedXPathException err)
            {
                HandleXPathException(err.GetXPathException());
            }
            catch (XPathException err)
            {
                HandleXPathException(err);
            }
            finally
            {
                inUse = false;
                if (traceListener != null)
                {
                    traceListener.Close();
                }

                ReleaseRunState();
            }
        }

        public virtual IReceiver GetStreamingReceiver(Mode mode, IReceiver result)
        {

            CheckReadiness();

            // Determine whether we need to close the output stream at the end. We
            // do this if the Result object is a StreamResult and is supplied as a
            // system ID, not as a System.IO.TextWriter or System.IO.Stream
            ComplexContentOutputter dest = PrepareOutputReceiver(result);
            XPathContextMajor initialContext = NewXPathContext();
            initialContext.Origin = this;

            globalContextItem = null;

            // Process the source document by applying template rules to the initial context node
            if (!mode.IsDeclaredStreamable())
            {
                throw new XPathException("mode supplied to getStreamingReceiver() must be streamable");
            }

            Configuration config = GetConfiguration();
            IReceiver despatcher = config.MakeStreamingTransformer(mode, null, null, dest, initialContext);
            if (despatcher == null)
            {
                throw new XPathException("Streaming requires Saxon-EE");
            }

            if (config.IsStripsAllWhiteSpace() || IsStylesheetContainingStripSpace())
            {
                despatcher = MakeStripper(despatcher);
            }

            despatcher.SetPipelineConfiguration(MakePipelineConfiguration());
            Outputter finalResult = dest;
            return new AnonymousProxyReceiver(this, despatcher, finalResult);
        }

        public virtual void ReleaseAttributeSetEvaluationStack()
        {
            lock (syncLock)
            {
                long thread = Environment.CurrentManagedThreadId;
                attributeSetEvaluationStacks.Remove(thread);
            }
        }

        private sealed class AnonymousProxyReceiver : ProxyReceiver
        {

            private readonly XsltController parent;
            private readonly Outputter finalResult;
            public AnonymousProxyReceiver(XsltController parent, IReceiver despatcher, Outputter finalResult) : base(despatcher)
            {
                this.parent = parent;
                this.finalResult = finalResult;
            }
            public override void Close()
            {
                if (parent.traceListener != null)
                {
                    parent.traceListener.Close();
                }

                finalResult.Close();
                parent.inUse = false;
                parent.ReleaseRunState();
            }
        }
    }
}