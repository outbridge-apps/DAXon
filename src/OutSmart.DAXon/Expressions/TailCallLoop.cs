////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;

using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using System;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Expressions
{
    public sealed class TailCallLoop : UnaryExpression
    {
        readonly UserFunction containingFunction;

        public UserFunction ContainingFunction => containingFunction;

        public override int ImplementationMethod => BaseExpression.ImplementationMethod;

        /*TailCallLoop e2 = new TailCallLoop(containingFunction);
        e2.setBaseExpression(getBaseExpression().copy());
        return e2;*/
        public override string ExpressionName => "tailCallLoop";
        public TailCallLoop(UserFunction function, Expression body) : base(body)
        {
            containingFunction = function;
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            GetOperand().TypeCheck(visitor, contextInfo);
            return this;
        }

        protected override OperandRole GetOperandRole()
        {
            return OperandRole.SAME_FOCUS_ACTION;
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            throw new NotSupportedException("TailCallLoop.copy()"); /*TailCallLoop e2 = new TailCallLoop(containingFunction);
        e2.setBaseExpression(getBaseExpression().copy());
        return e2;*/
        }

        /*TailCallLoop e2 = new TailCallLoop(containingFunction);
        e2.setBaseExpression(getBaseExpression().copy());
        return e2;*/
        /// <summary>
        /// Iterate over the sequence of values
        /// </summary>
        public override ISequenceIterator Iterate(IXPathContext context)
        {
            return MakeElaborator().ElaborateForPull().Iterate(context);
        }

        /*TailCallLoop e2 = new TailCallLoop(containingFunction);
        e2.setBaseExpression(getBaseExpression().copy());
        return e2;*/
        public override IItem EvaluateItem(IXPathContext context)
        {
            return MakeElaborator().ElaborateForItem().Eval(context);
        }

        /*TailCallLoop e2 = new TailCallLoop(containingFunction);
        e2.setBaseExpression(getBaseExpression().copy());
        return e2;*/
        private UserFunction EstablishTargetFunction(ITailCallInfo tail, XPathContextMajor cm)
        {
            if (tail is TailCallFunction)
            {
                return ((TailCallFunction)tail).function;
            }
            else if (tail is TailCallComponent)
            {
                Component targetComponent = ((TailCallComponent)tail).component;
                cm.SetCurrentComponent(targetComponent);
                return (UserFunction)targetComponent.GetActor();
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        /*TailCallLoop e2 = new TailCallLoop(containingFunction);
        e2.setBaseExpression(getBaseExpression().copy());
        return e2;*/
        public override void Process(Outputter output, IXPathContext context)
        {
            MakeElaborator().ElaborateForPush().ProcessLeavingTail(output, context);
        }

        /*TailCallLoop e2 = new TailCallLoop(containingFunction);
        e2.setBaseExpression(getBaseExpression().copy());
        return e2;*/
        private ISequence TailCallDifferentFunction(UserFunction userFunction, XPathContextMajor cm)
        {
            cm.ResetStackFrameMap(userFunction.GetStackFrameMap(), userFunction.GetArity());
            try
            {
                return userFunction.BodyEvaluator.Evaluate(cm);
            }
            catch (XPathException err)
            {
                throw err.MaybeWithLocation(GetLocation()).MaybeWithContext(cm);
            }
        }

        /*TailCallLoop e2 = new TailCallLoop(containingFunction);
        e2.setBaseExpression(getBaseExpression().copy());
        return e2;*/
        public override ItemType GetItemType()
        {
            return BaseExpression.GetItemType();
        }

        /*TailCallLoop e2 = new TailCallLoop(containingFunction);
        e2.setBaseExpression(getBaseExpression().copy());
        return e2;*/
        public override Elaborator GetElaborator()
        {
            return new TailCallLoopElaborator();
        }

        /*TailCallLoop e2 = new TailCallLoop(containingFunction);
        e2.setBaseExpression(getBaseExpression().copy());
        return e2;*/
        public interface ITailCallInfo
        {
        }

        /*TailCallLoop e2 = new TailCallLoop(containingFunction);
        e2.setBaseExpression(getBaseExpression().copy());
        return e2;*/
        internal sealed class TailCallComponent : ITailCallInfo
        {
            public Component component;
            public UserFunction function;
        }

        /*TailCallLoop e2 = new TailCallLoop(containingFunction);
        e2.setBaseExpression(getBaseExpression().copy());
        return e2;*/
        internal sealed class TailCallFunction : ITailCallInfo
        {
            public UserFunction function;
        }

        /*TailCallLoop e2 = new TailCallLoop(containingFunction);
        e2.setBaseExpression(getBaseExpression().copy());
        return e2;*/
        private sealed class TailCallLoopElaborator : PullElaborator
        {
            public override IPullEvaluator ElaborateForPull()
            {
                TailCallLoop expr = (TailCallLoop)GetExpression();
                IPullEvaluator contentEval = expr.BaseExpression.MakeElaborator().ElaborateForPull();
                return (context) =>
                {
                    try
                    {
                        XPathContextMajor cm = (XPathContextMajor)context;
                        Controller controller = cm.GetController();
                        while (true)
                        {
                            controller.CheckTimeoutPerStep();
                            ISequenceIterator iter = contentEval.Iterate(context);
                            IGroundedValue extent = SequenceTool.ToGroundedValue(iter);
                            ITailCallInfo tail = cm.TailCallInfo;
                            if (tail == null)
                            {
                                return extent.Iterate();
                            }
                            else
                            {
                                UserFunction target = expr.EstablishTargetFunction(tail, cm);
                                if (target != expr.containingFunction)
                                {
                                    return expr.TailCallDifferentFunction(target, cm).Iterate();
                                } // otherwise, loop round to execute the tail call
                            }
                        }
                    }
                    catch (UncheckedXPathException e)
                    {
                        throw e.GetXPathException().MaybeWithContext(context).MaybeWithLocation(expr.GetLocation());
                    }
                };
            }

            public override IPushEvaluator ElaborateForPush()
            {
                TailCallLoop expr = (TailCallLoop)GetExpression();
                IPushEvaluator contentPush = expr.BaseExpression.MakeElaborator().ElaborateForPush();
                return (output, context) =>
                {
                    XPathContextMajor cm = (XPathContextMajor)context;
                    Controller controller = cm.GetController();
                    while (true)
                    {
                        controller.CheckTimeoutPerStep();
                        ITailCall tc = contentPush.ProcessLeavingTail(output, context);
                        ITailCallInfo tail = cm.TailCallInfo;
                        if (tail == null)
                        {
                            return null;
                        }
                        else
                        {
                            UserFunction target = expr.EstablishTargetFunction(tail, cm);
                            if (target != expr.containingFunction)
                            {
                                SequenceTool.Process(expr.TailCallDifferentFunction(target, cm), output, expr.GetLocation());
                                return null;
                            } // otherwise, loop round to execute the tail call
                        }
                    }
                };
            }

            public override IItemEvaluator ElaborateForItem()
            {
                TailCallLoop expr = (TailCallLoop)GetExpression();
                IItemEvaluator contentEval = expr.BaseExpression.MakeElaborator().ElaborateForItem();
                return (context) =>
                {
                    XPathContextMajor cm = (XPathContextMajor)context;
                    Controller controller = cm.GetController();
                    while (true)
                    {
                        controller.CheckTimeoutPerStep();
                        IItem item = contentEval.Eval(context);
                        ITailCallInfo tail = cm.TailCallInfo;
                        if (tail == null)
                        {
                            return item;
                        }
                        else
                        {
                            UserFunction target = expr.EstablishTargetFunction(tail, cm);
                            if (target != expr.containingFunction)
                            {
                                return expr.TailCallDifferentFunction(target, cm).Head();
                            } // otherwise, loop round to execute the tail call
                        }
                    }
                };
            }
        }
    }
}