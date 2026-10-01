////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Transformation.Rules;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Expressions.Instructions
{
    /// <summary>
    /// An xsl:next-match element in the stylesheet
    /// </summary>
    internal sealed class NextMatch : ApplyNextMatchingTemplate
    {
        bool useTailRecursion;

        public override int InstructionNameCode => StandardNames.XSL_NEXT_MATCH;

        public override string StreamerName => "NextMatch";
        public NextMatch(bool useTailRecursion) : base()
        {
            this.useTailRecursion = useTailRecursion;
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            NextMatch nm2 = new NextMatch(useTailRecursion);
            nm2.SetActualParams(WithParam.Copy(nm2, GetActualParams(), rebindings));
            nm2.SetTunnelParams(WithParam.Copy(nm2, GetTunnelParams(), rebindings));
            ExpressionTool.CopyLocationInfo(this, nm2);
            return nm2;
        }

        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("nextMatch", this);
            string flags = "i";
            if (useTailRecursion)
            {
                flags = "t";
            }

            @out.EmitAttribute("flags", flags);
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
            return new NextMatchElaborator();
        }

        private sealed class NextMatchPackage : ITailCall
        {
            private readonly NextMatch instruction;
            private readonly Rule rule;
            private readonly ParameterSet @params;
            private readonly ParameterSet tunnelParams;
            private readonly Outputter output;
            private readonly IXPathContext evaluationContext;
            public NextMatchPackage(NextMatch instruction, Rule rule, ParameterSet @params, ParameterSet tunnelParams, Outputter output, IXPathContext evaluationContext)
            {
                this.instruction = instruction;
                this.rule = rule;
                this.@params = @params;
                this.tunnelParams = tunnelParams;
                this.output = output;
                this.evaluationContext = evaluationContext;
            }

            public ITailCall ProcessLeavingTail()
            {
                TemplateRule nh = (TemplateRule)rule.GetAction();
                nh.Initialize();
                XPathContextMajor c2 = evaluationContext.NewContext();
                c2.Origin = instruction;

                c2.SetLocalParameters(@params);
                c2.SetTunnelParameters(tunnelParams);
                c2.OpenStackFrame(nh.StackFrameMap);
                c2.SetCurrentTemplateRule(rule);
                c2.SetCurrentComponent(evaluationContext.GetCurrentComponent());
                c2.SetCurrentMergeGroupIterator(null);

                Mode mode = evaluationContext.GetCurrentMode().GetActor();
                if (mode.IsModeTracing())
                {
                    TemplateRuleTraceListener tracer = ((XsltController)evaluationContext.GetController()).TemplateRuleTraceListener;
                    tracer.Enter("next-match", instruction.GetLocation(), evaluationContext.GetContextItem(), nh);
                    ITailCall tc = nh.ApplyLeavingTail(output, c2);
                    tracer.Leave();
                    return tc;
                }
                else
                {
                    return nh.ApplyLeavingTail(output, c2);
                }
            }
        }

        private sealed class NextMatchElaborator : PushElaborator
        {
            public override IPushEvaluator ElaborateForPush()
            {
                NextMatch expr = (NextMatch)GetExpression();
                ILocation loc = expr.GetLocation();
                return (output, context) =>
                {
                    Controller controller = context.GetController();

                    // handle parameters if any
                    ParameterSet @params = AssembleParams(context, expr.GetActualParams());
                    ParameterSet tunnels = AssembleTunnelParams(context, expr.GetTunnelParams());
                    Rule currentRule = context.GetCurrentTemplateRule();
                    if (currentRule == null)
                    {
                        throw new XPathException("There is no current template rule", "XTDE0560").WithXPathContext(context).WithLocation(loc);
                    }

                    Component.M modeComponent = context.GetCurrentMode();
                    if (modeComponent == null)
                    {
                        throw new InvalidOperationException("Current mode is null");
                    }

                    Mode mode = modeComponent.GetActor();
                    IItem currentItem = context.GetCurrentIterator().Current();
                    XPathContextMajor c1 = context.NewContext();
                    c1.SetCurrentMode(modeComponent);
                    c1.Origin = expr;
                    c1.SetCurrentComponent(modeComponent);
                    PipelineConfiguration pipe = output.GetPipelineConfiguration();
                    pipe.XPathContext = c1;
                    Rule rule;
                    try
                    {
                        rule = mode.GetNextMatchRule(currentItem, currentRule, c1);
                    }
                    catch (XPathException e)
                    {
                        throw e.WithLocation(this.GetExpression().GetLocation());
                    }

                    if (rule == null)
                    {

                        // use the default action for the node
                        mode.GetBuiltInRuleSet().Process(currentItem, @params, tunnels, output, context, loc);
                    }
                    else if (expr.useTailRecursion)
                    {

                        // clear all the local variables: they are no longer needed
                        ArrayTools.Fill(context.GetStackFrame().StackFrameValues, null);
                        ((XPathContextMajor)context).SetCurrentComponent(modeComponent); // bug 2818
                        return new NextMatchPackage(expr, rule, @params, tunnels, output, context);
                    }
                    else
                    {
                        TemplateRule nh = (TemplateRule)rule.GetAction();
                        nh.Initialize();
                        XPathContextMajor c2 = context.NewContext();
                        c2.Origin = expr;

                        c2.OpenStackFrame(nh.StackFrameMap);
                        c2.SetLocalParameters(@params);
                        c2.SetTunnelParameters(tunnels);
                        c2.SetCurrentTemplateRule(rule);
                        c2.SetCurrentComponent(modeComponent); // needed in the case where next-match is called from a named template
                        c2.SetCurrentMergeGroupIterator(null);
                        if (mode.IsModeTracing())
                        {
                            TemplateRuleTraceListener tracer = ((XsltController)controller).TemplateRuleTraceListener;
                            tracer.Enter("next-match", loc, currentItem, nh);
                            nh.Apply(output, c2);
                            tracer.Leave();
                        }
                        else
                        {
                            nh.Apply(output, c2);
                        }
                    }

                    return null;
                };
            }
        }
    }
}