////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Accumulators;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Types;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation.Rules;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Transformation
{
    public abstract class Mode : Actor
    {
        public const int RECOVER_WITH_WARNINGS = 1;
        public static readonly StructuredQName OMNI_MODE_NAME = new StructuredQName("saxon", NamespaceUri.SAXON, "_omniMode");
        public static readonly StructuredQName UNNAMED_MODE_NAME = new StructuredQName("xsl", NamespaceUri.XSLT, "unnamed");
        public static readonly StructuredQName DEFAULT_MODE_NAME = new StructuredQName("xsl", NamespaceUri.XSLT, "default");
        protected StructuredQName modeName;
        private bool streamable;
        private RecoveryPolicy recoveryPolicy = RecoveryPolicy.RECOVER_WITH_WARNINGS;
        public bool mustBeTyped;
        public bool mustBeUntyped;
        public bool hasRules;
        public bool bindingSlotsAllocated;
        bool modeTracing;
        Values.SequenceType defaultResultType;
        bool enclosingMode;
        private HashSet<Accumulator> accumulators;

        public virtual StructuredQName ModeName => modeName;

        public abstract SimpleMode ActivePart { get; }
        public abstract int MaxPrecedence { get; }
        public abstract int MaxRank { get; }

        public virtual HashSet<Accumulator> Accumulators
        {
            get
            {
                if (accumulators == null)
                {
                    return new HashSet<Accumulator>();
                }
                else
                {
                    return accumulators;
                }
            }
            set
            {
                this.accumulators = value;
            }
        }

        public virtual RecoveryPolicy RecoveryPolicy
        {
            get => recoveryPolicy; set
            {
                recoveryPolicy = value;
            }
        }

        public virtual Values.SequenceType DefaultResultType
        {
            get => defaultResultType; set
            {
                defaultResultType = value;
            }
        }
        public Mode(StructuredQName modeName)
        {
            this.modeName = modeName;
        }

        public Component.M GetDeclaringComponent()
        {
            return (Component.M)base.DeclaringComponent;
        }

        public abstract IBuiltInRuleSet GetBuiltInRuleSet();
        public virtual bool IsUnnamedMode()
        {
            return modeName.Equals(UNNAMED_MODE_NAME);
        }
        public abstract void ComputeRankings(int start);
        public virtual string GetModeTitle(bool initialCaps)
        {
            if (initialCaps)
            {
                return IsUnnamedMode() ? "The unnamed mode" : "Mode " + ModeName.DisplayName;
            }
            else
            {
                return IsUnnamedMode() ? "the unnamed mode" : "mode " + ModeName.DisplayName;
            }
        }

        public virtual void SetModeTracing(bool tracing)
        {
            this.modeTracing = tracing;
        }

        public virtual bool IsModeTracing()
        {
            return modeTracing;
        }

        public override SymbolicName GetSymbolicName()
        {
            return new SymbolicName(StandardNames.XSL_MODE, ModeName);
        }

        public virtual StructuredQName GetObjectName()
        {
            return ModeName;
        }

        public abstract bool IsEmpty();

        // True only when NO real template rule is registered (a declared-but-ruleless mode);
        // gates the bulk shallow-copy fast path in ApplyTemplates. Conservative default.
        public virtual bool HasNoTemplateRules => false;
        public virtual void SetEnclosingMode(bool enclosing)
        {
            this.enclosingMode = enclosing;
        }

        public virtual bool IsEnclosingMode()
        {
            return enclosingMode;
        }

        public virtual void SetHasRules(bool hasRules)
        {
            this.hasRules = hasRules;
        }

        public virtual void SetStreamable(bool streamable)
        {
            this.streamable = streamable;
        }

        public virtual bool IsDeclaredStreamable()
        {
            return streamable;
        }

        public abstract HashSet<NamespaceUri> GetExplicitNamespaces(NamePool pool);

        public abstract void ProcessRules(IRuleAction action);
        public virtual IXPathContext MakeNewContext(IXPathContext context)
        {
            XPathContextMajor c2 = context.NewContext();
            c2.Origin = context.GetController(); // WHY?
            c2.OpenStackFrame(GetStackFrameSlotsNeeded());
            if (!(context.GetCurrentComponent().GetActor() is Accumulator))
            {
                c2.SetCurrentComponent(context.GetCurrentMode()); // bug 3706
            }

            return c2;
        }

        // WHY?
        public abstract Rule GetRule(IItem item, IXPathContext context);
        public abstract Rule GetRule(IItem item, IXPathContext context, Func<Rule, bool> filter);
        public virtual Rule GetRule(IItem item, int min, int max, IXPathContext context)
        {
            return GetRule(item, context, (r) =>
            {
                int p = r.Precedence;
                return p >= min && p <= max;
            });
        }

        public virtual Rule GetNextMatchRule(IItem item, Rule currentRule, IXPathContext context)
        {
            return GetRule(item, context, (r) =>
            {
                int comp = r.CompareRank(currentRule);
                if (comp < 0)
                {

                    // the rule has lower precedence or priority than the current rule
                    return true;
                }
                else if (comp == 0)
                {
                    int seqComp = r.Sequence.CompareTo(currentRule.Sequence);
                    if (seqComp < 0)
                    {

                        // the rule is before the current rule in declaration order
                        return true;
                    }
                    else if (seqComp == 0)
                    {

                        // we have two branches of the same union pattern; examine the parent pattern to see which is first
                        return r.PartNumber < currentRule.PartNumber;
                    }
                }

                return false;
            });
        }

        public abstract void ExportTemplateRules(ExpressionPresenter @out);
        public abstract void ExplainTemplateRules(ExpressionPresenter @out);
        public virtual ITailCall ApplyTemplates(ParameterSet parameters, ParameterSet tunnelParameters, NodeInfo separator, Outputter output, XPathContextMajor context, ILocation locationId)
        {
            // Every apply-templates dispatch (user rules AND built-in-rule descent over deep
            // input trees) funnels through here — one probe bounds the recursion depth
            // (RecursionDepthError, described as SXLM0001 by the nearest recursion site).
            StackGuard.Probe();
            Controller controller = context.GetController();
            ISequenceIterator iterator = context.GetCurrentIterator();

            // Fast path: an empty mode (no template rules at all) with the XSLT 3.0 shallow-copy
            // built-in behaves exactly like deep copy -- the built-in recursion can never reach a
            // user rule. Bulk-copy each item, skipping the per-node rule search and per-element
            // context/iterator churn (this is the production identity-transform pattern).
            if (separator == null && !mustBeTyped && !mustBeUntyped && !modeTracing
                && !controller.IsTracing() && HasNoTemplateRules
                && GetBuiltInRuleSet().GetType() == typeof(Rules.ShallowCopyRuleSet))
            {
                int copyOptions = CopyOptions.ALL_NAMESPACES
                    | (controller.GetExecutable().IsSchemaAware() ? CopyOptions.TYPE_ANNOTATIONS : 0);
                IItem it;
                while ((it = iterator.Next()) != null)
                {
                    controller.CheckTimeoutPerStep();
                    if (it is NodeInfo node)
                    {
                        // Leaf kinds are emitted exactly as ShallowCopyRuleSet does (Copy cannot
                        // handle a parentless attribute/namespace); Copy is used only for the
                        // recursive kinds, replacing the per-node rule dispatch with a bulk copy.
                        switch (node.GetNodeKind())
                        {
                            case Types.Type.TEXT:
                                output.Characters(node.UnicodeStringValue, locationId, ReceiverOption.NONE);
                                break;
                            case Types.Type.ATTRIBUTE:
                                output.Attribute(NameOfNode.MakeName(node), (ISimpleType)node.GetSchemaType(), node.GetStringValue(), locationId, ReceiverOption.NONE);
                                break;
                            case Types.Type.COMMENT:
                                output.Comment(node.UnicodeStringValue, locationId, ReceiverOption.NONE);
                                break;
                            case Types.Type.PROCESSING_INSTRUCTION:
                                output.ProcessingInstruction(node.GetLocalPart(), node.UnicodeStringValue, locationId, ReceiverOption.NONE);
                                break;
                            case Types.Type.NAMESPACE:
                                output.Namespace(node.GetLocalPart(), NamespaceUri.Of(node.GetStringValue()), ReceiverOption.NONE);
                                break;
                            default:
                                if (output.GetSystemId() == null)
                                {
                                    output.SetSystemId(node.GetBaseURI());
                                }

                                node.Copy(output, copyOptions, locationId);
                                break;
                        }
                    }
                    else
                    {
                        output.Append(it, locationId, ReceiverOption.NONE);
                    }
                }

                return null;
            }

            ITailCall tc = null;
            ITraceListener traceListener = null;
            if (controller.IsTracing())
            {
                traceListener = controller.GetTraceListener();
            }


            // Iterate over this sequence
            bool lookahead = iterator is ILookaheadIterator && ((ILookaheadIterator)iterator).SupportsHasNext();
            TemplateRule previousTemplate = null;
            bool first = true;
            while (true)
            {
                // Per STEP, not per item: one matched node's template body can cost anything.
                controller.CheckTimeoutPerStep();

                // process any tail calls returned from previous nodes. We need to do this before changing
                // the context. If we have a ILookaheadIterator, we can tell whether we're positioned at the
                // end without changing the current position, and we can then return the last tail call to
                // the caller and execute it further down the stack, reducing the risk of running out of stack
                // space. In other cases, we need to execute the outstanding tail calls before moving the iterator
                if (tc != null)
                {
                    if (lookahead && !((ILookaheadIterator)iterator).HasNext)
                    {
                        break;
                    }

                    do
                    {
                        tc = tc.ProcessLeavingTail();
                    }
                    while (tc != null);
                }

                IItem item = iterator.Next();
                if (item == null)
                {
                    break;
                }

                if (separator != null)
                {
                    if (first)
                    {
                        first = false;
                    }
                    else
                    {
                        output.Append(separator);
                    }
                }

                if (mustBeTyped)
                {
                    CheckMustBeTyped(item);
                }
                else if (mustBeUntyped)
                {
                    CheckMustByUntyped(item);
                }


                // find the template rule for this node
                if (traceListener != null)
                {
                    traceListener.StartRuleSearch();
                }

                Rule rule = GetRule(item, context);
                if (traceListener != null)
                {
                    HandleTraceListener(rule, item, traceListener);
                }

                TemplateRuleTraceListener ruleTraceListener = null;
                if (modeTracing)
                {
                    ruleTraceListener = HandleRuleTraceListener(ruleTraceListener, controller, locationId, item, rule);
                }

                if (rule == null)
                {

                    // Use the default action for the node
                    // No need to open a new stack frame
                    GetBuiltInRuleSet().Process(item, parameters, tunnelParameters, output, context, locationId);
                }
                else if (traceListener == null && !modeTracing
                    && rule.GetAction() is TemplateRule emptyTemplate && Expressions.Literal.IsEmptySequence(emptyTemplate.GetBody()))
                {
                    // An empty template body (match="text()" suppression and friends) executes to
                    // nothing — skip the frame/current-rule ceremony entirely. The reuse frame
                    // stays the PREVIOUS template's, which is exactly what the next node matching
                    // that template expects; an empty body has no tail call.
                    tc = null;
                }
                else
                {
                    tc = HandleRuleNotNull(rule, traceListener, context, item, ref previousTemplate, parameters, tunnelParameters, output);
                }

                if (modeTracing)
                {
                    ruleTraceListener.Leave();
                }
            }


            // return the ITailCall returned from the last node processed
            return tc;
        }

        // WHY?
        private void CheckMustBeTyped(IItem item)
        {
            if (item is NodeInfo)
            {
                int kind = ((NodeInfo)item).GetNodeKind();
                if (kind == Types.Type.ELEMENT || kind == Types.Type.ATTRIBUTE)
                {
                    ISchemaType annotation = ((NodeInfo)item).GetSchemaType();
                    if (annotation == Untyped.INSTANCE || annotation == BuiltInAtomicType.UNTYPED_ATOMIC)
                    {
                        throw new XPathException(GetModeTitle(true) + " requires typed nodes, but the input is untyped", "XTTE3100");
                    }
                }
            }
        }

        private void CheckMustByUntyped(IItem item)
        {
            if (item is NodeInfo)
            {
                int kind = ((NodeInfo)item).GetNodeKind();
                if (kind == Types.Type.ELEMENT || kind == Types.Type.ATTRIBUTE)
                {
                    ISchemaType annotation = ((NodeInfo)item).GetSchemaType();
                    if (!(annotation == Untyped.INSTANCE || annotation == BuiltInAtomicType.UNTYPED_ATOMIC))
                    {
                        throw new XPathException(GetModeTitle(true) + " requires untyped nodes, but the input is typed", "XTTE3110");
                    }
                }
            }
        }

        // Returns the tail call directly and updates previousTemplate via ref (was: new object[2]
        // {tc, previousTemplate} per node — a ~4.7M-alloc/transform transpile artifact on the hottest
        // apply-templates loop). Pure refactor: identical control flow, no boxing.
        private ITailCall HandleRuleNotNull(Rule rule, ITraceListener traceListener, XPathContextMajor context, IItem item, ref TemplateRule previousTemplate, ParameterSet parameters, ParameterSet tunnelParameters, Outputter output)
        {
            TemplateRule template = (TemplateRule)rule.GetAction();
            if (template != previousTemplate)
            {

                // Reuse the previous stackframe unless it's a different template rule
                previousTemplate = template;
                template.Initialize();
                context.OpenStackFrame(template.StackFrameMap);
                context.SetLocalParameters(parameters);
                context.SetTunnelParameters(tunnelParameters);
                context.SetCurrentMergeGroupIterator(null);
            }

            context.SetCurrentTemplateRule(rule);
            ITailCall tc;
            if (traceListener != null)
            {
                traceListener.StartCurrentItem(item);
                tc = template.ApplyLeavingTail(output, context);
                if (tc != null)
                {

                    // disable tail call optimization while tracing
                    do
                    {
                        tc = tc.ProcessLeavingTail();
                    }
                    while (tc != null);
                }

                traceListener.EndCurrentItem(item);
            }
            else
            {
                tc = template.ApplyLeavingTail(output, context);
            }

            return tc;
        }

        // WHY?
        private TemplateRuleTraceListener HandleRuleTraceListener(TemplateRuleTraceListener ruleTraceListener, Controller controller, ILocation locationId, IItem item, Rule rule)
        {
            ruleTraceListener = ((XsltController)controller).TemplateRuleTraceListener;
            if (ruleTraceListener == null)
            {
                ruleTraceListener = new TemplateRuleTraceListener(controller.GetConfiguration().Logger);
                ((XsltController)controller).TemplateRuleTraceListener = ruleTraceListener;
            }

            ruleTraceListener.Enter("apply-templates", locationId, item, rule == null ? null : (TemplateRule)rule.GetAction());
            return ruleTraceListener;
        }

        private void HandleTraceListener(Rule rule, IItem item, ITraceListener traceListener)
        {
            if (rule == null)
            {
                traceListener.EndRuleSearch(GetBuiltInRuleSet(), this, item);
            }
            else
            {
                traceListener.EndRuleSearch(rule, this, item);
            }
        }

        public abstract int GetStackFrameSlotsNeeded();
        public virtual string GetCodeForBuiltInRuleSet(IBuiltInRuleSet builtInRuleSet)
        {
            if (builtInRuleSet is ShallowCopyAllRuleSet)
            {
                return "CA";
            }
            else if (builtInRuleSet is ShallowCopyRuleSet)
            {
                return "SC";
            }
            else if (builtInRuleSet is ShallowSkipRuleSet)
            {
                return "SS";
            }
            else if (builtInRuleSet is DeepCopyRuleSet)
            {
                return "DC";
            }
            else if (builtInRuleSet is DeepSkipRuleSet)
            {
                return "DS";
            }
            else if (builtInRuleSet is FailRuleSet)
            {
                return "FF";
            }
            else if (builtInRuleSet is TextOnlyCopyRuleSet)
            {
                return "TC";
            }
            else if (builtInRuleSet is RuleSetWithWarnings)
            {
                return GetCodeForBuiltInRuleSet(((RuleSetWithWarnings)builtInRuleSet).BaseRuleSet) + "+W";
            }
            else
            {
                return "???";
            }
        }

        public virtual IBuiltInRuleSet GetBuiltInRuleSetForCode(string code)
        {
            IBuiltInRuleSet @base;
            if (code.StartsWith("SC", StringComparison.Ordinal))
            {
                @base = ShallowCopyRuleSet.GetInstance();
            }
            else if (code.StartsWith("SS", StringComparison.Ordinal))
            {
                @base = ShallowSkipRuleSet.GetInstance();
            }
            else if (code.StartsWith("DC", StringComparison.Ordinal))
            {
                @base = DeepCopyRuleSet.GetInstance();
            }
            else if (code.StartsWith("DS", StringComparison.Ordinal))
            {
                @base = DeepSkipRuleSet.GetInstance();
            }
            else if (code.StartsWith("FF", StringComparison.Ordinal))
            {
                @base = FailRuleSet.GetInstance();
            }
            else if (code.StartsWith("TC", StringComparison.Ordinal))
            {
                @base = TextOnlyCopyRuleSet.GetInstance();
            }
            else if (code.StartsWith("CA", StringComparison.Ordinal))
            {
                @base = ShallowCopyAllRuleSet.GetInstance();
            }
            else
            {
                throw new ArgumentException(code);
            }

            if (code.EndsWith("+W", StringComparison.Ordinal))
            {
                @base = new RuleSetWithWarnings(@base);
            }

            return @base;
        }

        public override void Export(ExpressionPresenter presenter)
        {
            int s = presenter.StartElement("mode");
            if (!IsUnnamedMode())
            {
                presenter.EmitAttribute("name", ModeName);
            }

            presenter.EmitAttribute("onNo", GetCodeForBuiltInRuleSet(GetBuiltInRuleSet()));
            string flags = "";
            if (IsDeclaredStreamable())
            {
                flags += "s";
            }

            if (IsUnnamedMode())
            {
                flags += "d";
            }

            if (mustBeTyped)
            {
                flags += "t";
            }

            if (mustBeUntyped)
            {
                flags += "u";
            }

            if (recoveryPolicy == RecoveryPolicy.DO_NOT_RECOVER)
            {
                flags += "F";
            }
            else if (recoveryPolicy == RecoveryPolicy.RECOVER_WITH_WARNINGS)
            {
                flags += "W";
            }

            if (!hasRules)
            {
                flags += "e";
            }

            if (!(flags.Length == 0))
            {
                presenter.EmitAttribute("flags", flags);
            }

            ExportUseAccumulators(presenter);
            presenter.EmitAttribute("patternSlots", GetStackFrameSlotsNeeded() + "");
            ExportTemplateRules(presenter);
            int e = presenter.EndElement();
            if (s != e)
            {
                throw new InvalidOperationException("Export tree unbalanced for mode " + ModeName);
            }
        }

        protected virtual void ExportUseAccumulators(ExpressionPresenter presenter)
        {
        }

        public virtual bool IsMustBeTyped()
        {
            return mustBeTyped;
        }

        public virtual void Explain(ExpressionPresenter presenter)
        {
            int s = presenter.StartElement("mode");
            if (!IsUnnamedMode())
            {
                presenter.EmitAttribute("name", ModeName);
            }

            presenter.EmitAttribute("onNo", GetCodeForBuiltInRuleSet(GetBuiltInRuleSet()));
            string flags = "";
            if (IsDeclaredStreamable())
            {
                flags += "s";
            }

            if (IsUnnamedMode())
            {
                flags += "d";
            }

            if (mustBeTyped)
            {
                flags += "t";
            }

            if (mustBeUntyped)
            {
                flags += "u";
            }

            if (recoveryPolicy == RecoveryPolicy.DO_NOT_RECOVER)
            {
                flags += "F";
            }
            else if (recoveryPolicy == RecoveryPolicy.RECOVER_WITH_WARNINGS)
            {
                flags += "W";
            }

            if (!(flags.Length == 0))
            {
                presenter.EmitAttribute("flags", flags);
            }

            presenter.EmitAttribute("patternSlots", GetStackFrameSlotsNeeded() + "");
            ExplainTemplateRules(presenter);
            int e = presenter.EndElement();
            if (s != e)
            {
                throw new InvalidOperationException("tree unbalanced");
            }
        }

        // WHY?
        // WHY?
        /// <summary>
        /// Interface for helper classes used to process all the rules in the Mode
        /// </summary>
        // IRuleAction interface->delegate for lambda assignability.
        public delegate void IRuleAction(Rule r);
    }
}
