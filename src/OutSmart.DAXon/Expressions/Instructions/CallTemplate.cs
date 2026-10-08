////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Text;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Expressions.Instructions
{
    /// <summary>
    /// Instruction representing an xsl:call-template element in the stylesheet.
    /// </summary>
    internal sealed class CallTemplate : Instruction, IITemplateCall, IComponentInvocation
    {
        private readonly NamedTemplate template; // Null only for saxon:call-template
        private readonly StructuredQName calledTemplateName; // the name of the called template
        private WithParam[] actualParams = WithParam.EMPTY_ARRAY;
        private WithParam[] tunnelParams = WithParam.EMPTY_ARRAY;
        private bool useTailRecursion;
        private int bindingSlot = -1;
        private readonly bool isWithinDeclaredStreamableConstruct;

        public Component FixedTarget
        {
            get
            {
                Component c = GetTarget();
                Visibility v = c.GetVisibility();
                if (v == Visibility.PRIVATE || v == Visibility.FINAL)
                {
                    return c;
                }
                else
                {
                    return null;
                }
            }
        }

        public override int InstructionNameCode => StandardNames.XSL_CALL_TEMPLATE;

        public int BindingSlot
        {
            get => bindingSlot; set
            {
                bindingSlot = value;
            }
        }

        public override int IntrinsicDependencies => StaticProperty.DEPENDS_ON_XSLT_CONTEXT | StaticProperty.DEPENDS_ON_FOCUS;

        public override string StreamerName => "CallTemplate";
        public CallTemplate(NamedTemplate template, StructuredQName calledTemplateName, bool useTailRecursion, bool inStreamable)
        {
            this.template = template;
            this.calledTemplateName = calledTemplateName;
            this.useTailRecursion = useTailRecursion;
            this.isWithinDeclaredStreamableConstruct = inStreamable;
        }

        public void SetActualParameters(WithParam[] actualParams, WithParam[] tunnelParams)
        {
            this.actualParams = actualParams;
            this.tunnelParams = tunnelParams;
            foreach (WithParam actualParam in actualParams)
            {
                AdoptChildExpression(actualParam.GetSelectExpression());
            }

            foreach (WithParam tunnelParam in tunnelParams)
            {
                AdoptChildExpression(tunnelParam.GetSelectExpression());
            }
        }

        public void SetTailRecursive(bool tailRecursive)
        {
            this.useTailRecursion = tailRecursive;
        }

        public SymbolicName GetSymbolicName()
        {
            return calledTemplateName == null ? null : new SymbolicName(StandardNames.XSL_TEMPLATE, calledTemplateName);
        }

        public Component GetTarget()
        {
            return template.DeclaringComponent;
        }

        public WithParam[] GetActualParams()
        {
            return actualParams;
        }

        public WithParam[] GetTunnelParams()
        {
            return tunnelParams;
        }

        public bool UsesTailRecursion()
        {
            return useTailRecursion;
        }

        public override Expression Simplify()
        {
            WithParam.Simplify(actualParams);
            WithParam.Simplify(tunnelParams);
            return this;
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            WithParam.TypeCheck(actualParams, visitor, contextInfo);
            WithParam.TypeCheck(tunnelParams, visitor, contextInfo);

            // For non-tunnel parameters, see if the supplied value is type-safe against the declared
            // type of the value, and if so, avoid the dynamic type check
            // Can't do this check unless the target template has been compiled.
            bool backwards = visitor.StaticContext.IsInBackwardsCompatibleMode();
            TypeChecker tc = visitor.GetConfiguration().GetTypeChecker(backwards);
            for (int p = 0; p < actualParams.Length; p++)
            {
                WithParam wp = actualParams[p];
                NamedTemplate.LocalParamInfo lp = template.GetLocalParamInfo(wp.VariableQName);
                if (lp != null)
                {
                    int pos = p;
                    SequenceType req = lp.requiredType;
                    Func<RoleDiagnostic> role = () =>
                    {
                        RoleDiagnostic role0 = new RoleDiagnostic(RoleDiagnostic.PARAM, wp.VariableQName.DisplayName, pos);
                        role0.ErrorCode = "XTTE0590";
                        return role0;
                    };
                    Expression select = tc.StaticTypeCheck(wp.GetSelectExpression(), req, role, visitor);
                    wp.SetSelectExpression(this, select);
                    wp.SetTypeChecked(true);
                }
            }

            return this;
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {
            WithParam.Optimize(visitor, actualParams, contextItemType);
            WithParam.Optimize(visitor, tunnelParams, contextItemType);
            return this;
        }

        protected override int ComputeCardinality()
        {
            if (template == null)
            {
                return StaticProperty.ALLOWS_ZERO_OR_MORE;
            }
            else
            {
                return template.RequiredType.GetCardinality();
            }
        }

        public override ItemType GetItemType()
        {
            if (template == null)
            {
                return AnyItemType.GetInstance();
            }
            else
            {
                return template.RequiredType.PrimaryType;
            }
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            CallTemplate ct = new CallTemplate(template, calledTemplateName, useTailRecursion, isWithinDeclaredStreamableConstruct);
            ExpressionTool.CopyLocationInfo(this, ct);
            ct.actualParams = WithParam.Copy(ct, actualParams, rebindings);
            ct.tunnelParams = WithParam.Copy(ct, tunnelParams, rebindings);
            return ct;
        }

        public override bool MayCreateNewNodes()
        {
            return true;
        }

        public override IEnumerable<Operand> Operands()
        {
            List<Operand> list = new List<Operand>(10);
            WithParam.GatherOperands(this, actualParams, list);
            WithParam.GatherOperands(this, tunnelParams, list);
            return list;
        }

        public override void Process(Outputter output, IXPathContext context)
        {
            NamedTemplate t;
            Component target = FixedTarget;
            if (bindingSlot >= 0)
            {
                target = context.GetTargetComponent(bindingSlot);
                if (target.IsHiddenAbstractComponent())
                {
                    throw new XPathException("Cannot call an abstract template (" + calledTemplateName.DisplayName + ") with no implementation", "XTDE3052").WithLocation(GetLocation());
                }
            }

            t = (NamedTemplate)target.GetActor();
            XPathContextMajor c2 = context.NewContext();
            c2.SetCurrentComponent(target);
            c2.Origin = this;
            c2.OpenStackFrame(t.GetStackFrameMap());
            c2.SetLocalParameters(AssembleParams(context, actualParams));
            c2.SetTunnelParameters(AssembleTunnelParams(context, tunnelParams));
            if (isWithinDeclaredStreamableConstruct)
            {
                c2.SetCurrentGroupIterator(null);
            }

            c2.SetCurrentMergeGroupIterator(null);
            try
            {
                ITailCall tc = t.Expand(output, c2);
                DispatchTailCall(tc);
            }
            catch (RecursionDepthError e) when (!e.Described)
            {
                // Filtered: one such catch per recursion level; only the innermost describes.
                throw e.Describe("Too many nested template or function calls. The stylesheet may be looping.", DAXonErrorCode.SXLM0001, GetLocation());
            }
        }

        public override StructuredQName GetObjectName()
        {
            return template == null ? null : template.TemplateName;
        }

        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("callT", this);
            string flags = "";
            if (template != null && template.TemplateName != null)
            {
                @out.EmitAttribute("name", template.TemplateName);
            }

            @out.EmitAttribute("bSlot", "" + BindingSlot);
            if (isWithinDeclaredStreamableConstruct)
            {
                flags += "d";
            }

            if (useTailRecursion)
            {
                flags += "t";
            }

            if (!(flags.Length == 0))
            {
                @out.EmitAttribute("flags", flags);
            }

            if (actualParams.Length > 0)
            {
                WithParam.ExportParameters(actualParams, @out, false);
            }

            if (tunnelParams.Length > 0)
            {
                WithParam.ExportParameters(tunnelParams, @out, true);
            }

            @out.EndElement();
        }

        public override string ToString()
        {

            // fallback implementation
            StringBuilder buff = new StringBuilder(64);
            buff.Append("CallTemplate#");
            if (template.GetObjectName() != null)
            {
                buff.Append(template.GetObjectName().DisplayName);
            }

            bool first = true;
            foreach (WithParam p in GetActualParams())
            {
                buff.Append(first ? "(" : ", ");
                buff.Append(p.VariableQName.DisplayName);
                buff.Append('=');
                buff.Append(p.GetSelectExpression().ToString());
                first = false;
            }

            if (!first)
            {
                buff.Append(')');
            }

            return buff.ToString();
        }

        public override string ToShortString()
        {

            // fallback implementation
            return "CallTemplate#" + template.GetObjectName().DisplayName;
        }

        public override Elaborator GetElaborator()
        {
            return new CallTemplateElaborator();
        }

        internal sealed class CallTemplatePackage : ITailCall
        {
            private readonly Component targetComponent;
            private readonly ParameterSet @params;
            private readonly ParameterSet tunnelParams;
            private readonly CallTemplate instruction;
            private readonly Outputter output;
            private readonly IXPathContext evaluationContext;
            public CallTemplatePackage(Component targetComponent, ParameterSet @params, ParameterSet tunnelParams, CallTemplate instruction, Outputter output, IXPathContext evaluationContext)
            {
                this.targetComponent = targetComponent;
                if (!(targetComponent.GetActor() is NamedTemplate))
                {
                    throw new InvalidCastException("Target of call-template must be a named template");
                }

                this.@params = @params;
                this.tunnelParams = tunnelParams;
                this.instruction = instruction;
                this.output = output;
                this.evaluationContext = evaluationContext;
            }

            public ITailCall ProcessLeavingTail()
            {

                // TODO: the idea of tail call optimization is to reuse the caller's stack frame rather than
                //  creating a new one. We're doing this for the Java stack, but not for the context stack where
                //  local variables are held. It should be possible to avoid creating a new context, and instead
                //  to update the existing one in situ. Experimented with this June 2022 (MHK) and it looks possible
                //  in principle, but I hit trouble getting the current component right.
                // One tail call is one step of unbounded cost, and the trampoline driving them
                // never grows the stack — so StackGuard.Probe in Expand is blind to an
                // infinitely tail-recursive template and only the deadline can stop it. Checked
                // here and not in Expand because the trampoline runs at constant depth: an
                // XPathException raised deep inside a NON-tail recursion grows the stack at every
                // converting rethrow while unwinding, and overflows it (see StackGuard.Margin).
                evaluationContext.GetController().CheckTimeoutPerStep();
                NamedTemplate template = (NamedTemplate)targetComponent.GetActor();
                XPathContextMajor c2 = evaluationContext.NewContext();
                c2.SetCurrentComponent(targetComponent);
                c2.Origin = instruction;
                c2.SetLocalParameters(@params);
                c2.SetTunnelParameters(tunnelParams);
                c2.OpenStackFrame(template.GetStackFrameMap());
                c2.SetCurrentMergeGroupIterator(null);

                // Drop the link to the caller, so it can be garbage-collected
                c2.SetCaller(evaluationContext.MajorContext.GetCaller());

                return template.Expand(output, c2);
            }
        }

        internal sealed class CallTemplateElaborator : PushElaborator
        {
            public override IPushEvaluator ElaborateForPush()
            {
                CallTemplate expr = (CallTemplate)GetExpression();
                int bindingSlot = expr.BindingSlot;
                if (expr.useTailRecursion)
                {
                    return (output, context) =>
                    {
                        Component targetComponent;
                        if (bindingSlot >= 0)
                        {
                            targetComponent = context.GetTargetComponent(bindingSlot);
                        }
                        else
                        {
                            targetComponent = expr.FixedTarget;
                        }

                        if (targetComponent == null)
                        {
                            throw new XPathException("Internal Saxon error: No binding available for call-template instruction", DAXonErrorCode.SXPK0001, expr.GetLocation());
                        }

                        if (targetComponent.IsHiddenAbstractComponent())
                        {
                            throw new XPathException("Cannot call an abstract template (" + expr.calledTemplateName.DisplayName + ") with no implementation", "XTDE3052", expr.GetLocation());
                        }


                        // handle parameters if any
                        ParameterSet @params = AssembleParams(context, expr.actualParams);
                        ParameterSet tunnels = AssembleTunnelParams(context, expr.tunnelParams);

                        // Call the named template. Actually, don't call it; rather construct a call package
                        // and return it to the caller, who will then process this package.
                        if (@params == null)
                        {

                            // bug 490967
                            @params = ParameterSet.EMPTY_PARAMETER_SET;
                        }


                        // clear all the local variables: they are no longer needed
                        ArrayTools.Fill(context.GetStackFrame().StackFrameValues, null);
                        return new CallTemplatePackage(targetComponent, @params, tunnels, expr, output, context);
                    };
                }
                else
                {
                    return (output, context) =>
                    {
                        NamedTemplate t;
                        Component target = expr.FixedTarget;
                        if (bindingSlot >= 0)
                        {
                            target = context.GetTargetComponent(bindingSlot);
                            if (target.IsHiddenAbstractComponent())
                            {
                                throw new XPathException("Cannot call an abstract template (" + expr.calledTemplateName.DisplayName + ") with no implementation", "XTDE3052").WithLocation(expr.GetLocation());
                            }
                        }

                        t = (NamedTemplate)target.GetActor();
                        XPathContextMajor c2 = context.NewContext();
                        c2.SetCurrentComponent(target);
                        c2.Origin = expr;
                        c2.OpenStackFrame(t.GetStackFrameMap());
                        c2.SetLocalParameters(AssembleParams(context, expr.actualParams));
                        c2.SetTunnelParameters(AssembleTunnelParams(context, expr.tunnelParams));
                        if (expr.isWithinDeclaredStreamableConstruct)
                        {
                            c2.SetCurrentGroupIterator(null);
                        }

                        c2.SetCurrentMergeGroupIterator(null);
                        try
                        {
                            ITailCall tc = t.Expand(output, c2);
                            DispatchTailCall(tc);
                        }
                        catch (RecursionDepthError e) when (!e.Described)
                        {
                            // Filtered: one such catch per recursion level; only the innermost describes.
                            throw e.Describe("Too many nested template or function calls. The stylesheet may be looping.", DAXonErrorCode.SXLM0001, expr.GetLocation());
                        }

                        return null;
                    };
                }
            }
        }
    }
}