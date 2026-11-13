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
using System.Collections.Generic;
namespace OutSmart.DAXon.Expressions.Instructions
{
    /// <summary>
    /// The compiled form of an xsl:attribute-set element in the stylesheet.
    /// </summary>
    public class AttributeSet : Actor
    {
        StructuredQName attributeSetName;
        private bool declaredStreamable;
        private volatile IPushEvaluator bodyEvaluator;   // elaborated once, under the lock (Elaborated); read without it

        // Saxon takes the lock on every call
        private IPushEvaluator Elaborated()
        {
            lock (syncLock)
            {
                return bodyEvaluator ??= GetBody().MakeElaborator().ElaborateForPush();
            }
        }

        public virtual int FocusDependencies => body.Dependencies & StaticProperty.DEPENDS_ON_FOCUS;
        public AttributeSet()
        {
        }

        public override SymbolicName GetSymbolicName()
        {
            return new SymbolicName(StandardNames.XSL_ATTRIBUTE_SET, attributeSetName);
        }

        public virtual void SetName(StructuredQName attributeSetName)
        {
            this.attributeSetName = attributeSetName;
        }

        public virtual void SetDeclaredStreamable(bool value)
        {
            this.declaredStreamable = value;
        }

        public virtual bool IsDeclaredStreamable()
        {
            return this.declaredStreamable;
        }

        public override void SetStackFrameMap(SlotManager stackFrameMap)
        {
            if (stackFrameMap != null)
            {
                base.SetStackFrameMap(stackFrameMap);
            }
        }

        public virtual void Expand(Outputter output, IXPathContext context)
        {
            IPushEvaluator body = bodyEvaluator ?? Elaborated();

            // A use-attribute-sets chain expands by recursion through UseAttributeSet (3000 links killed a 256 KB thread),
            // and sets that share members expand once per path, exponentially many: the stack and the clock bound both.
            try
            {
                OutSmart.DAXon.Internal.StackGuard.Probe();
            }
            catch (OutSmart.DAXon.Internal.RecursionDepthError e) when (!e.Described)
            {
                throw e.Describe("Too many nested attribute sets (use-attribute-sets) for the thread's stack.", DAXonErrorCode.SXLM0001, this);
            }

            OutSmart.DAXon.Core.Controller.CheckActiveTimeout();
            Stack<AttributeSet> stack = ((XsltController)context.GetController()).AttributeSetEvaluationStack;
            if (stack.Contains(this))
            {
                throw new XPathException("Attribute set " + GetObjectName().EQName + " invokes itself recursively", "XTDE0640");
            }

            // The pop MUST be in a finally: the body can raise a dynamic error that xsl:try catches,
            // and a frame left on the stack makes every later use of this attribute set in the same
            // run report XTDE0640 "invokes itself recursively" - a wrong answer, not a leak.
            stack.Push(this);
            try
            {
                Expression.DispatchTailCall(body.ProcessLeavingTail(output, context));
            }
            finally
            {
                stack.Pop();
                if (stack.Count == 0)
                {
                    ((XsltController)context.GetController()).ReleaseAttributeSetEvaluationStack();
                }
            }
        }

        public virtual StructuredQName GetObjectName()
        {
            return attributeSetName;
        }
    }
}