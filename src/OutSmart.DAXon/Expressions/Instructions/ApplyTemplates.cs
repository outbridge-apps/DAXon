////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation.Rules;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Trees.Utilities;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Expressions.Instructions
{
    /// <summary>
    /// An instruction representing an xsl:apply-templates element in the stylesheet
    /// </summary>
    internal sealed class ApplyTemplates : Instruction, IITemplateCall, IComponentInvocation
    {
        private Operand selectOp;
        private Operand separatorOp;
        private WithParam[] actualParams;
        private WithParam[] tunnelParams;
        protected bool useCurrentMode = false;
        protected bool _useTailRecursion = false;
        protected Mode mode;
        protected bool implicitSelect;
        protected bool inStreamableConstruct = false;
        protected RuleManager ruleManager;
        private int bindingSlot = -1; // for binding the mode

        public Expression SeparatorExpression
        {
            get => separatorOp == null ? null : separatorOp.GetChildExpression(); set
            {
                separatorOp = new Operand(this, value, OperandRole.SINGLE_ATOMIC);
            }
        }

        public override int InstructionNameCode => StandardNames.XSL_APPLY_TEMPLATES;

        public override int ImplementationMethod => base.ImplementationMethod | Expression.WATCH_METHOD;

        public override int IntrinsicDependencies => base.IntrinsicDependencies | (useCurrentMode ? StaticProperty.DEPENDS_ON_CURRENT_ITEM : 0);

        public Component FixedTarget => mode.GetDeclaringComponent();

        public Expression Select
        {
            get => selectOp.GetChildExpression(); set
            {
                selectOp.SetChildExpression(value);
            }
        }

        public int BindingSlot
        {
            get => bindingSlot; set
            {
                bindingSlot = value;
            }
        }

        public override string StreamerName => "ApplyTemplates";

        public ApplyTemplates(Expression select, bool useCurrentMode, bool useTailRecursion, bool implicitSelect, bool inStreamableConstruct, Mode mode, RuleManager ruleManager)
        {
            selectOp = new Operand(this, select, OperandRole.SINGLE_ATOMIC);
            Init(select, useCurrentMode, useTailRecursion, mode);
            this.implicitSelect = implicitSelect;
            this.inStreamableConstruct = inStreamableConstruct;
            this.ruleManager = ruleManager;
        }

        protected void Init(Expression select, bool useCurrentMode, bool useTailRecursion, Mode mode)
        {
            this.Select = select;
            this.useCurrentMode = useCurrentMode;
            this._useTailRecursion = useTailRecursion;
            this.mode = mode;
            AdoptChildExpression(select);
        }

        public WithParam[] GetActualParams()
        {
            return actualParams;
        }

        public WithParam[] GetTunnelParams()
        {
            return tunnelParams;
        }

        public void SetActualParams(WithParam[] @params)
        {
            actualParams = @params;
        }

        public void SetTunnelParams(WithParam[] @params)
        {
            tunnelParams = @params;
        }

        public override IEnumerable<Operand> Operands()
        {
            IList<Operand> operanda = new List<Operand>();
            operanda.Add(selectOp);
            if (separatorOp != null)
            {
                operanda.Add(separatorOp);
            }

            WithParam.GatherOperands(this, GetActualParams(), operanda);
            WithParam.GatherOperands(this, GetTunnelParams(), operanda);
            return operanda;
        }

        public override Expression Simplify()
        {
            WithParam.Simplify(GetActualParams());
            WithParam.Simplify(GetTunnelParams());
            Select = Select.Simplify();
            return this;
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            WithParam.TypeCheck(actualParams, visitor, contextInfo);
            WithParam.TypeCheck(tunnelParams, visitor, contextInfo);
            try
            {
                selectOp.TypeCheck(visitor, contextInfo);
            }
            catch (XPathException e)
            {
                if (implicitSelect)
                {
                    if (e.HasErrorCode("XPTY0020", "XPTY0019"))
                    {
                        throw new XPathException("Cannot apply-templates to child nodes when the context item is an atomic value").WithErrorCode("XTTE0510").AsTypeError();
                    }
                    else if (e.HasErrorCode("XPDY0002"))
                    {
                        throw new XPathException("Cannot apply-templates to child nodes when the context item is absent").WithErrorCode("XTTE0510").AsTypeError();
                    }
                }

                throw;
            }

            AdoptChildExpression(Select);
            if (Literal.IsEmptySequence(Select))
            {
                return Select;
            }

            return this;
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            WithParam.Optimize(visitor, actualParams, contextInfo);
            WithParam.Optimize(visitor, tunnelParams, contextInfo);
            selectOp.TypeCheck(visitor, contextInfo); // More info available second time around
            selectOp.Optimize(visitor, contextInfo);
            if (Literal.IsEmptySequence(Select))
            {
                return Select;
            }

            return this;
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            ApplyTemplates a2 = new ApplyTemplates(Select.Copy(rebindings), useCurrentMode, _useTailRecursion, implicitSelect, inStreamableConstruct, mode, ruleManager);
            a2.SetActualParams(WithParam.Copy(a2, GetActualParams(), rebindings));
            a2.SetTunnelParams(WithParam.Copy(a2, GetTunnelParams(), rebindings));
            ExpressionTool.CopyLocationInfo(this, a2);
            a2.ruleManager = ruleManager;
            if (separatorOp != null)
            {
                a2.SeparatorExpression = SeparatorExpression.Copy(rebindings);
            }

            return a2;
        }

        public override bool MayCreateNewNodes()
        {
            return true;
        }

        public Component.M GetTargetMode(IXPathContext context)
        {
            Component.M targetMode;
            if (useCurrentMode)
            {
                targetMode = context.GetCurrentMode();
            }
            else
            {
                if (bindingSlot >= 0)
                {
                    try
                    {
                        targetMode = (Component.M)context.GetTargetComponent(bindingSlot);
                        if (targetMode.GetVisibility() == Visibility.ABSTRACT)
                        {
                            throw new InvalidOperationException("Modes cannot be abstract");
                        }
                    }
                    catch (InvalidCastException e)
                    {
                        throw new InvalidOperationException("In apply-templates at " + GetLocation().GetSystemId() + "#" + GetLocation().GetLineNumber() + " target component for slot " + bindingSlot + " is " + context.GetTargetComponent(bindingSlot).GetActor().GetSymbolicName());
                    }
                }
                else
                {

                    // fallback
                    targetMode = mode.GetDeclaringComponent();
                }
            }

            return targetMode;
        }

        public bool UseTailRecursion()
        {
            return _useTailRecursion;
        }

        public SymbolicName GetSymbolicName()
        {
            return mode == null ? null : mode.GetSymbolicName();
        }

        public override PathMap.PathMapNodeSet AddToPathMap(PathMap pathMap, PathMap.PathMapNodeSet pathMapNodeSet)
        {

            // This logic is assuming the mode is streamable (so that called templates can't return streamed nodes)
            PathMap.PathMapNodeSet result = base.AddToPathMap(pathMap, pathMapNodeSet);
            result.SetReturnable(false);
            return new PathMap.PathMapNodeSet(pathMap.MakeNewRoot(this));
        }

        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("applyT", this);
            if (mode != null && !mode.IsUnnamedMode())
            {
                @out.EmitAttribute("mode", mode.ModeName);
            }

            string flags = "";
            if (useCurrentMode)
            {
                flags = "c";
            }

            if (_useTailRecursion)
            {
                flags += "t";
            }

            if (implicitSelect)
            {
                flags += "i";
            }

            if (!(flags.Length == 0))
            {
                @out.EmitAttribute("flags", flags);
            }

            @out.EmitAttribute("bSlot", "" + BindingSlot);
            @out.SetChildRole("select");
            Select.Export(@out);
            if (separatorOp != null)
            {
                @out.SetChildRole("separator");
                SeparatorExpression.Export(@out);
            }

            if (GetActualParams().Length != 0)
            {
                WithParam.ExportParameters(GetActualParams(), @out, false);
            }

            if (GetTunnelParams().Length != 0)
            {
                WithParam.ExportParameters(GetTunnelParams(), @out, true);
            }

            @out.EndElement();
        }

        public override Elaborator GetElaborator()
        {
            return new ApplyTemplatesElaborator();
        }

        protected sealed class ApplyTemplatesPackage : ITailCall
        {
            private readonly ISequence selectedItems;
            private readonly Component.M targetMode;
            private readonly ParameterSet @params;
            private readonly ParameterSet tunnelParams;
            private readonly NodeInfo separator;
            private readonly XPathContextMajor evaluationContext;
            private readonly Outputter output;
            private readonly ILocation locationId;
            public ApplyTemplatesPackage(ISequence selectedItems, Component.M targetMode, ParameterSet @params, ParameterSet tunnelParams, NodeInfo separator, Outputter output, XPathContextMajor context, ILocation locationId)
            {
                this.selectedItems = selectedItems;
                this.targetMode = targetMode;
                this.@params = @params;
                this.tunnelParams = tunnelParams;
                this.separator = separator;
                this.output = output;
                evaluationContext = context;
                this.locationId = locationId;
            }

            public ITailCall ProcessLeavingTail()
            {
                evaluationContext.TrackFocus(selectedItems.Iterate());
                evaluationContext.SetCurrentMode(targetMode);
                evaluationContext.SetCurrentComponent(targetMode);
                return targetMode.GetActor().ApplyTemplates(@params, tunnelParams, separator, output, evaluationContext, locationId);
            }
        }

        internal sealed class ApplyTemplatesElaborator : PushElaborator
        {
            private NodeInfo MakeSeparator(IUnicodeStringEvaluator sep, IXPathContext context)
            {
                NodeInfo separator;
                UnicodeString sepValue = sep.Eval(context);
                Orphan orphan = new Orphan(context.GetConfiguration());
                orphan.SetNodeKind(Types.Type.TEXT);
                orphan.SetStringValue(sepValue);
                separator = orphan;
                return separator;
            }

            public override IPushEvaluator ElaborateForPush()
            {
                ApplyTemplates expr = (ApplyTemplates)GetExpression();
                IUnicodeStringEvaluator sep = expr.separatorOp == null ? null : expr.SeparatorExpression.MakeElaborator().ElaborateForUnicodeString(true);
                if (expr.UseTailRecursion())
                {
                    ISequenceEvaluator select = expr.Select.MakeElaborator().Lazily(false, false);
                    return (output, context) =>
                    {
                        Component.M targetMode = expr.GetTargetMode(context);
                        NodeInfo separator = null;
                        if (sep != null)
                        {
                            separator = MakeSeparator(sep, context);
                        }


                        // handle parameters if any
                        ParameterSet @params = AssembleParams(context, expr.GetActualParams());
                        ParameterSet tunnels = AssembleTunnelParams(context, expr.GetTunnelParams());
                        XPathContextMajor context2 = context.NewContext();
                        context2.Origin = expr;

                        // Allow context object of caller to be garbage-collected (only affects diagnostics)
                        context2.SetCaller(context.GetCaller());
                        return new ApplyTemplatesPackage(select.Evaluate(context), targetMode, @params, tunnels, separator, output, context2, expr.GetLocation());
                    };
                }
                else
                {
                    IPullEvaluator select = expr.Select.MakeElaborator().ElaborateForPull();
                    return (output, context) =>
                    {
                        Component.M targetMode = expr.GetTargetMode(context);
                        Mode thisMode = targetMode.GetActor();
                        NodeInfo separator = null;
                        if (sep != null)
                        {
                            separator = MakeSeparator(sep, context);
                        }


                        // handle parameters if any
                        ParameterSet @params = AssembleParams(context, expr.GetActualParams());
                        ParameterSet tunnels = AssembleTunnelParams(context, expr.GetTunnelParams());

                        // Get an iterator to iterate through the selected nodes in original order
                        ISequenceIterator iter = select.Iterate(context);

                        // Quick exit if the iterator is empty
                        if (iter is EmptyIterator)
                        {
                            return null;
                        }


                        // process the selected nodes now
                        XPathContextMajor c2 = context.NewContext();
                        c2.TrackFocus(iter);
                        c2.SetCurrentMode(targetMode);
                        c2.Origin = expr;
                        c2.SetCurrentComponent(targetMode);
                        if (expr.inStreamableConstruct)
                        {
                            c2.SetCurrentGroupIterator(null);
                        }

                        PipelineConfiguration pipe = output.GetPipelineConfiguration();
                        pipe.XPathContext = c2;
                        try
                        {
                            ITailCall tc = thisMode.ApplyTemplates(@params, tunnels, separator, output, c2, expr.GetLocation());
                            DispatchTailCall(tc);
                        }
                        catch (RecursionDepthError e) when (!e.Described)
                        {
                            // Filtered: this catch sits at EVERY level of the apply-templates
                            // recursion, so only the innermost may run a handler (see RecursionDepthError).
                            throw e.Describe("Too many nested apply-templates calls. The stylesheet may be looping.", DAXonErrorCode.SXLM0001, expr.GetLocation());
                        }

                        pipe.XPathContext = context;
                        return null;
                    };
                }
            }
        }
    }
}