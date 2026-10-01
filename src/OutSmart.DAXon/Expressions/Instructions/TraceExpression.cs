////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Expressions.Instructions
{
    /// <summary>
    /// A wrapper expression used to trace expressions in XSLT and XQuery.
    /// </summary>
    internal sealed class TraceExpression : Instruction
    {
        private readonly Operand baseOp;
        private Dictionary<string, object> properties = new Dictionary<string, object>(10);

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
        public TraceExpression(Expression child)
        {
            baseOp = new Operand(this, child, OperandRole.SAME_FOCUS_ACTION);
            AdoptChildExpression(child);
            child.GatherProperties((k, v) => properties.PutAndGetPrevious(k, v));
        }

        public Expression GetBody()
        {
            return baseOp.GetChildExpression();
        }

        public override IEnumerable<Operand> Operands()
        {
            return baseOp;
        }

        public void SetProperty(string name, object value)
        {
            properties[name] = value;
        }

        public override object GetProperty(string name)
        {
            return properties.GetOrDefault(name);
        }

        public override IEnumerator<string> GetProperties()
        {
            return properties.Keys.GetEnumerator();
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            TraceExpression t = new TraceExpression(Child.Copy(rebindings));
            t.SetLocation(GetLocation()); // Bug 3034
            t.properties = properties;
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

        public override bool Equals(object other)
        {
            return other is TraceExpression && Child.Equals(((TraceExpression)other).Child);
        }

        protected override int ComputeHashCode()
        {
            return 0x64646464 ^ Child.GetHashCode();
        }

        public override IItem EvaluateItem(IXPathContext context)
        {
            return MakeElaborator().ElaborateForItem().Eval(context);
        }

        public override ISequenceIterator Iterate(IXPathContext context)
        {
            return MakeElaborator().ElaborateForPull().Iterate(context);
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {

            // See bug 6415
            Expression t = base.Optimize(visitor, contextInfo);
            if (t != this)
            {
                return t;
            }

            if (Child is TraceExpression)
            {
                return Child;
            }

            return this;
        }

        public override void Export(ExpressionPresenter @out)
        {
            Child.Export(@out); // Following code was written for diagnostics, to show the tree with the trace instructions
            //        @out.startElement("traceExp");
            //        @out.endElement();
        }

        public override string ToShortString()
        {
            return Child.ToShortString();
        }

        public override Elaborator GetElaborator()
        {
            return new TraceExpressionElaborator();
        }

        private sealed class TraceExpressionElaborator : FallbackElaborator
        {
            public override IStringEvaluator ElaborateForString(bool zeroLengthWhenAbsent)
            {
                TraceExpression expr = (TraceExpression)GetExpression();
                Expression body = expr.GetBody();
                IStringEvaluator baseEval = expr.GetBody().MakeElaborator().ElaborateForString(zeroLengthWhenAbsent);
                return (context) =>
                {
                    Controller controller = context.GetController();
                    if (controller.IsTracing())
                    {
                        ITraceListener listener = controller.GetTraceListener();
                        listener.Enter(body, expr.properties, context);
                        string result = baseEval.Eval(context);
                        listener.Leave(body);
                        return result;
                    }
                    else
                    {
                        return baseEval.Eval(context);
                    }
                };
            }

            public override IUpdateEvaluator ElaborateForUpdate()
            {
                TraceExpression expr = (TraceExpression)GetExpression();
                Expression body = expr.GetBody();
                IUpdateEvaluator baseEval = expr.GetBody().MakeElaborator().ElaborateForUpdate();
                return (context, pul) =>
                {
                    Controller controller = context.GetController();
                    if (controller.IsTracing())
                    {
                        ITraceListener listener = controller.GetTraceListener();
                        listener.Enter(body, expr.properties, context);
                        baseEval.RegisterUpdates(context, pul);
                        listener.Leave(body);
                    }
                    else
                    {
                        baseEval.RegisterUpdates(context, pul);
                    }
                };
            }

            //        public ISequenceEvaluator eagerly() {
            //            TraceExpression expr = (TraceExpression) getExpression();
            //            return context -> {
            //                assert controller != null;
            //
            //                    listener.enter(expr, expr.properties, context);
            //                    IGroundedValue result = (IGroundedValue)baseEval.evaluate(context);
            //                    listener.leave(expr);
            //                    return result;
            //                } else {
            //                    return (IGroundedValue) baseEval.evaluate(context);
            //                }
            //            };
            //
            //        public ISequenceEvaluator lazily(boolean repeatable) {
            //            return eagerly();
            public override IPullEvaluator ElaborateForPull()
            {
                TraceExpression expr = (TraceExpression)GetExpression();
                Expression body = expr.GetBody();
                IPullEvaluator baseEval = expr.GetBody().MakeElaborator().ElaborateForPull();
                return (context) =>
                {
                    Controller controller = context.GetController();
                    if (controller.IsTracing())
                    {
                        ITraceListener listener = controller.GetTraceListener();
                        listener.Enter(body, expr.properties, context);
                        ISequenceIterator result = baseEval.Iterate(context);
                        listener.Leave(body);
                        return result;
                    }
                    else
                    {
                        return baseEval.Iterate(context);
                    }
                };
            }

            public override IPushEvaluator ElaborateForPush()
            {
                TraceExpression expr = (TraceExpression)GetExpression();
                Expression body = expr.GetBody();
                IPushEvaluator baseEval = body.MakeElaborator().ElaborateForPush();
                return (output, context) =>
                {
                    Controller controller = context.GetController();
                    if (controller.IsTracing())
                    {
                        ITraceListener listener = controller.GetTraceListener();
                        listener.Enter(body, expr.properties, context);
                        ITailCall tc = baseEval.ProcessLeavingTail(output, context);
                        DispatchTailCall(tc);
                        listener.Leave(body);
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
                TraceExpression expr = (TraceExpression)GetExpression();
                Expression body = expr.GetBody();
                IItemEvaluator baseEval = expr.GetBody().MakeElaborator().ElaborateForItem();
                return (context) =>
                {
                    Controller controller = context.GetController();
                    if (controller.IsTracing())
                    {
                        ITraceListener listener = controller.GetTraceListener();
                        listener.Enter(body, expr.properties, context);
                        IItem result = baseEval.Eval(context);
                        listener.Leave(body);
                        return result;
                    }
                    else
                    {
                        return baseEval.Eval(context);
                    }
                };
            }

            public override IBooleanEvaluator ElaborateForBoolean()
            {
                TraceExpression expr = (TraceExpression)GetExpression();
                Expression body = expr.GetBody();
                IBooleanEvaluator baseEval = expr.GetBody().MakeElaborator().ElaborateForBoolean();
                return (context) =>
                {
                    Controller controller = context.GetController();
                    if (controller.IsTracing())
                    {
                        ITraceListener listener = controller.GetTraceListener();
                        listener.Enter(body, expr.properties, context);
                        bool result = baseEval.Eval(context);
                        listener.Leave(body);
                        return result;
                    }
                    else
                    {
                        return baseEval.Eval(context);
                    }
                };
            }

            public override IUnicodeStringEvaluator ElaborateForUnicodeString(bool zeroLengthWhenAbsent)
            {
                TraceExpression expr = (TraceExpression)GetExpression();
                Expression body = expr.GetBody();
                IUnicodeStringEvaluator baseEval = expr.GetBody().MakeElaborator().ElaborateForUnicodeString(zeroLengthWhenAbsent);
                return (context) =>
                {
                    Controller controller = context.GetController();
                    if (controller.IsTracing())
                    {
                        ITraceListener listener = controller.GetTraceListener();
                        listener.Enter(body, expr.properties, context);
                        UnicodeString result = baseEval.Eval(context);
                        listener.Leave(body);
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