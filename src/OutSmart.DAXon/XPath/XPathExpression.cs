////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using System;
using System.Collections.Generic;
namespace OutSmart.DAXon.XPath
{
    public class XPathExpression
    {
        private readonly IStaticContext env;
        private readonly Expression expression;
        private SlotManager stackFrameMap;
        private readonly Executable executable;
        private int numberOfExternalVariables;

        public virtual Expression InternalExpression => expression;
        public XPathExpression(IStaticContext env, Expression exp, Executable exec)
        {
            expression = exp;
            this.env = env;
            this.executable = exec;
        }

        public virtual Executable GetExecutable()
        {
            return executable;
        }

        public virtual void SetStackFrameMap(SlotManager map, int numberOfExternalVariables)
        {
            stackFrameMap = map;
            this.numberOfExternalVariables = numberOfExternalVariables;
        }

        public virtual XPathDynamicContext CreateDynamicContext()
        {
            XPathContextMajor context = new XPathContextMajor(null, executable);
            context.OpenStackFrame(stackFrameMap);
            ArmDeadline(context);
            return new XPathDynamicContext(env.GetRequiredContextItemType(), context, stackFrameMap);
        }

        public virtual XPathDynamicContext CreateDynamicContext(IItem contextItem)
        {
            CheckContextItemType(contextItem);
            XPathContextMajor context = new XPathContextMajor(contextItem, executable);
            context.OpenStackFrame(stackFrameMap);
            ArmDeadline(context);
            return new XPathDynamicContext(env.GetRequiredContextItemType(), context, stackFrameMap);
        }

        // Arm the Processor-wide cooperative deadline for this XPath run - and, no less important,
        // claim the thread's active-deadline slot: a stale deadline left behind by a previous
        // (finished) transformation on this thread must not spuriously abort an unrelated
        // evaluation. A configuration with no Processor claims the slot with no limit.
        private void ArmDeadline(XPathContextMajor context)
        {
            if (executable.GetConfiguration().GetProcessor() is OutSmart.DAXon.Api.Processor p)
            {
                context.GetController().SetTimeout(p.TransformTimeout, "XPath evaluation");
            }
            else
            {
                context.GetController().SetTimeout(TimeSpan.Zero);
            }
        }

        // Each evaluation of a selector is a call of its own: the limits run from its start on the thread that evaluates,
        // and the trees in its context item and variables count against the memory limit. A selector used to arm once,
        // when it was made, so every later evaluation shared the first one's deadline.
        internal void ArmEvaluation(XPathDynamicContext dynamicContext)
        {
            XPathContextMajor context = (XPathContextMajor)dynamicContext.XPathContextObject;
            ArmDeadline(context);
            Controller controller = context.GetController();
            controller.ChargeInput(context.GetContextItem());
            ISequence[] slots = context.GetStackFrame().StackFrameValues;
            for (int i = 0; i < numberOfExternalVariables && i < slots.Length; i++)
            {
                controller.ChargeInput(slots[i]);   // the variables the host set; the slots after them are the last evaluation's
            }
        }

        // A reused selector evaluates in one stack frame and one controller: what an evaluation bound to its local
        // variables stayed reachable until the next one, and the documents doc() loaded for as long as the selector.
        internal void EndEvaluation(XPathDynamicContext dynamicContext)
        {
            XPathContextMajor context = (XPathContextMajor)dynamicContext.XPathContextObject;
            ISequence[] slots = context.GetStackFrame().StackFrameValues;
            for (int i = numberOfExternalVariables; i < slots.Length; i++)
            {
                slots[i] = null;
            }

            Controller controller = context.GetController();
            int pooled = controller.GetDocumentPool().Count;
            if (pooled == 0)
            {
                return;
            }

            // The document of the context item the host set stays: doc() of its URI is that node, evaluation after evaluation.
            // Its key is made only when the pool holds more (a key normalizes its URI: a microsecond and a kilobyte).
            NodeInfo host = context.GetContextItem() as NodeInfo;
            ITreeInfo hostTree = host != null && host.GetSystemId() != null ? host.GetTreeInfo() : null;
            bool hostPooled = hostTree != null && controller.GetDocumentPool().Contains(hostTree);
            if (pooled > (hostPooled ? 1 : 0))
            {
                controller.ReleaseRunState();
                if (hostPooled)
                {
                    controller.GetDocumentPool().Add(hostTree, new DocumentKey(host.GetSystemId()));
                }
            }
        }

        public virtual XPathDynamicContext CreateDynamicContext(Controller controller, IItem contextItem)
        {
            CheckContextItemType(contextItem);
            if (controller == null)
            {
                return CreateDynamicContext(contextItem);
            }
            else
            {
                XPathContextMajor context = controller.NewXPathContext();
                context.OpenStackFrame(stackFrameMap);
                XPathDynamicContext dc = new XPathDynamicContext(env.GetRequiredContextItemType(), context, stackFrameMap);
                if (contextItem != null)
                {
                    dc.ContextItem = contextItem;
                }

                return dc;
            }
        }

        private void CheckContextItemType(IItem contextItem)
        {
            if (contextItem != null)
            {
                ItemType type = env.GetRequiredContextItemType();
                TypeHierarchy th = env.GetConfiguration().GetTypeHierarchy();
                if (!type.Matches(contextItem, th))
                {
                    throw new XPathException("Supplied context item does not match required context item type " + type);
                }
            }
        }

        public virtual ISequenceIterator Iterate(XPathDynamicContext context)
        {
            context.CheckExternalVariables(stackFrameMap, numberOfExternalVariables);
            return expression.MakeElaborator().ElaborateForPull().Iterate(context.XPathContextObject);
        }

        public virtual IList<IItem> Evaluate(XPathDynamicContext context)
        {
            IList<IItem> list = new List<IItem>(20);

            // Don't replace with list.add - C# doesn't like it because list.add() returns boolean
            SequenceTool.Supply(expression.Iterate(context.XPathContextObject), (item) => list.Add(item));
            return list;
        }

        public virtual IItem EvaluateSingle(XPathDynamicContext context)
        {
            ISequenceIterator iter = expression.Iterate(context.XPathContextObject);
            IItem result = iter.Next();
            iter.Dispose();
            return result;
        }

        public virtual bool EffectiveBooleanValue(XPathDynamicContext context)
        {
            return expression.MakeElaborator().ElaborateForBoolean().Eval(context.XPathContextObject);
        }
    }
}