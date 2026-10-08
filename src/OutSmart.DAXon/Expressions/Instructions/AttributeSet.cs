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
        private IPushEvaluator bodyEvaluator;

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
            lock (syncLock)
            {
                if (bodyEvaluator == null)
                {
                    bodyEvaluator = GetBody().MakeElaborator().ElaborateForPush();
                }
            }

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
                Expression.DispatchTailCall(bodyEvaluator.ProcessLeavingTail(output, context));
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

        public override void Export(ExpressionPresenter presenter)
        {
            presenter.StartElement("attributeSet");
            presenter.EmitAttribute("name", GetObjectName());
            presenter.EmitAttribute("line", GetLineNumber() + "");
            presenter.EmitAttribute("module", GetSystemId());
            presenter.EmitAttribute("slots", GetStackFrameMap().NumberOfVariables + "");
            presenter.EmitAttribute("binds", DeclaringComponent.ComponentBindings.Count + "");
            if (IsDeclaredStreamable())
            {
                presenter.EmitAttribute("flags", "s");
            }

            GetBody().Export(presenter);
            presenter.EndElement();
        }
    }
}