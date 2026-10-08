////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal.Collections;
using System.Collections.Generic;
using OutSmart.DAXon.Expressions.Elaboration;
namespace OutSmart.DAXon.Expressions.Instructions
{
    internal sealed class ComponentTracer : Instruction
    {
        private Operand baseOp;
        private Dictionary<string, object> properties = new Dictionary<string, object>(10);
        private ITraceableComponent component;

        public Expression Child => baseOp.GetChildExpression();

        public override string ExpressionName => "trace";

        public override string StreamerName => "TraceExpr";

        public override int ImplementationMethod => Child.ImplementationMethod;

        public override int Dependencies => Child.Dependencies;

        public override int NetCost => 0;

        public override int InstructionNameCode
        {
            get
            {
                if (Child is Instruction)
                {
                    return ((Instruction)Child).InstructionNameCode;
                }
                else
                {
                    return -1;
                }
            }
        }
        public ComponentTracer(ITraceableComponent component)
        {
            this.component = component;
            baseOp = new Operand(this, component.GetBody(), OperandRole.SAME_FOCUS_ACTION);
            component.GatherProperties((k, v) => properties.PutAndGetPrevious(k, v));
        }

        private ComponentTracer()
        {
        }

        public Expression GetBody()
        {
            return baseOp.GetChildExpression();
        }

        public void SetProperty(string name, object value)
        {
            properties[name] = value;
        }

        public override IEnumerable<Operand> Operands()
        {
            return baseOp;
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            ComponentTracer t = new ComponentTracer();
            t.component = component;
            t.properties = new Dictionary<string, object>(properties);
            Expression newBody = Child.Copy(rebindings); // Bug 4642
            t.baseOp = new Operand(t, newBody, OperandRole.SAME_FOCUS_ACTION);
            t.AdoptChildExpression(newBody);
            t.SetLocation(GetLocation()); // Bug 3034
            return t;
        }

        public override bool IsUpdatingExpression()
        {
            return Child.IsUpdatingExpression();
        }

        public override bool IsVacuousExpression()
        {
            return Child.IsVacuousExpression();
        }

        public override void CheckForUpdatingSubexpressions()
        {
            Child.CheckForUpdatingSubexpressions();
        }

        public override ItemType GetItemType()
        {
            return Child.GetItemType();
        }

        public override int GetCardinality()
        {
            return Child.GetCardinality();
        }

        public override bool MayCreateNewNodes()
        {
            return !Child.HasSpecialProperty(StaticProperty.NO_NODES_NEWLY_CREATED);
        }

        public override IItem EvaluateItem(IXPathContext context)
        {
            return MakeElaborator().ElaborateForItem().Eval(context);
        }

        public override ISequenceIterator Iterate(IXPathContext context)
        {
            return MakeElaborator().ElaborateForPull().Iterate(context);
        }

        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("componentTracer");
            Child.Export(@out);
            @out.EndElement();
        }

        public override string ToShortString()
        {
            return Child.ToShortString();
        }

        public override Elaborator GetElaborator()
        {
            return new ComponentTracerElaborator();
        }

        private sealed class ComponentTracerElaborator : PullElaborator
        {
            public override IUpdateEvaluator ElaborateForUpdate()
            {
                ComponentTracer expr = (ComponentTracer)GetExpression();
                IUpdateEvaluator baseEval = expr.GetBody().MakeElaborator().ElaborateForUpdate();
                return (context, pul) =>
                {
                    Controller controller = context.GetController();
                    if (controller.IsTracing())
                    {
                        ITraceListener listener = controller.GetTraceListener();
                        listener.Enter(expr, expr.properties, context);
                        baseEval.RegisterUpdates(context, pul);
                        listener.Leave(expr);
                    }
                    else
                    {
                        baseEval.RegisterUpdates(context, pul);
                    }
                };
            }

            public override IPullEvaluator ElaborateForPull()
            {
                ComponentTracer expr = (ComponentTracer)GetExpression();
                IPullEvaluator baseEval = expr.GetBody().MakeElaborator().ElaborateForPull();
                return (context) =>
                {
                    Controller controller = context.GetController();
                    if (controller.IsTracing())
                    {
                        ITraceListener listener = controller.GetTraceListener();
                        listener.Enter(expr.component, expr.properties, context);
                        ISequenceIterator result = baseEval.Iterate(context);
                        IGroundedValue extent;
                        try
                        {
                            extent = SequenceTool.ToGroundedValue(result);
                        }
                        catch (UncheckedXPathException e)
                        {
                            throw e.GetXPathException();
                        }

                        listener.Leave(expr.component);
                        return extent.Iterate();
                    }
                    else
                    {
                        return baseEval.Iterate(context);
                    }
                };
            }

            public override IPushEvaluator ElaborateForPush()
            {
                ComponentTracer expr = (ComponentTracer)GetExpression();
                IPushEvaluator baseEval = expr.GetBody().MakeElaborator().ElaborateForPush();
                return (output, context) =>
                {
                    Controller controller = context.GetController();
                    if (controller.IsTracing())
                    {
                        ITraceListener listener = controller.GetTraceListener();
                        listener.Enter(expr.component, expr.properties, context);
                        ITailCall tc = baseEval.ProcessLeavingTail(output, context);
                        DispatchTailCall(tc);
                        listener.Leave(expr.component);
                    }
                    else
                    {
                        DispatchTailCall(baseEval.ProcessLeavingTail(output, context));
                    }

                    return null;
                };
            }

            public override IItemEvaluator ElaborateForItem()
            {
                ComponentTracer expr = (ComponentTracer)GetExpression();
                IItemEvaluator baseEval = expr.GetBody().MakeElaborator().ElaborateForItem();
                return (context) =>
                {
                    Controller controller = context.GetController();
                    if (controller.IsTracing())
                    {
                        ITraceListener listener = controller.GetTraceListener();
                        listener.Enter(expr.component, expr.properties, context);
                        IItem result = baseEval.Eval(context);
                        listener.Leave(expr.component);
                        return result;
                    }
                    else
                    {
                        return baseEval.Eval(context);
                    }
                };
            }
        }
    }
}