////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Transformation.Rules;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Expressions.Instructions
{
    /// <summary>
    /// An xsl:apply-imports element in the stylesheet.
    /// </summary>
    internal sealed class ApplyImports : ApplyNextMatchingTemplate, IITemplateCall
    {

        public override int InstructionNameCode => StandardNames.XSL_APPLY_IMPORTS;

        public override string StreamerName => "ApplyImports";
        public ApplyImports()
        {
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            ApplyImports ai2 = new ApplyImports();
            ai2.SetActualParams(WithParam.Copy(ai2, GetActualParams(), rebindings));
            ai2.SetTunnelParams(WithParam.Copy(ai2, GetTunnelParams(), rebindings));
            ExpressionTool.CopyLocationInfo(this, ai2);
            return ai2;
        }

        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("applyImports", this);
            @out.EmitAttribute("flags", "i"); // used to mean "allow any item" i.e. non-nodes
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
            return new ApplyImportsElaborator();
        }

        private sealed class ApplyImportsElaborator : PushElaborator
        {
            public override IPushEvaluator ElaborateForPush()
            {
                ApplyImports expr = (ApplyImports)GetExpression();
                ILocation loc = expr.GetLocation();
                return (output, context) =>
                {
                    Controller controller = context.GetController();

                    // handle parameters if any
                    ParameterSet @params = AssembleParams(context, expr.GetActualParams());
                    ParameterSet tunnels = AssembleTunnelParams(context, expr.GetTunnelParams());
                    Rule currentTemplateRule = context.GetCurrentTemplateRule();
                    if (currentTemplateRule == null)
                    {
                        throw new XPathException("There is no current template rule").WithXPathContext(context).WithErrorCode("XTDE0560").WithLocation(loc);
                    }

                    int min = currentTemplateRule.MinImportPrecedence;
                    int max = currentTemplateRule.Precedence - 1;
                    Component.M modeComponent = context.GetCurrentMode();
                    if (modeComponent == null)
                    {
                        throw new InvalidOperationException("Current mode is null");
                    }

                    IItem currentItem = context.GetCurrentIterator().Current();
                    Mode mode = modeComponent.GetActor();
                    Rule rule = mode.GetRule(currentItem, min, max, context);
                    if (rule == null)
                    {

                        // use the default action for the node
                        mode.GetBuiltInRuleSet().Process(currentItem, @params, tunnels, output, context, loc);
                    }
                    else
                    {
                        XPathContextMajor c2 = context.NewContext();
                        TemplateRule nh = (TemplateRule)rule.GetAction();
                        nh.Initialize();
                        c2.Origin = expr;
                        c2.SetLocalParameters(@params);
                        c2.SetTunnelParameters(tunnels);
                        c2.OpenStackFrame(nh.StackFrameMap);
                        c2.SetCurrentTemplateRule(rule);
                        c2.SetCurrentComponent(modeComponent);
                        c2.SetCurrentMergeGroupIterator(null);
                        if (mode.IsModeTracing())
                        {
                            TemplateRuleTraceListener tracer = ((XsltController)controller).TemplateRuleTraceListener;
                            tracer.Enter("apply-imports", loc, currentItem, nh);
                            nh.Apply(output, c2);
                            tracer.Leave();
                        }
                        else
                        {
                            nh.Apply(output, c2);
                        }
                    }

                    return null; // we could use tail recursion, but we don't
                };
            }
        }
    }
}