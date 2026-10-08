////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using System;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Expressions
{
    /// <summary>
    /// A reference to a global variable
    /// </summary>
    internal sealed class GlobalVariableReference : VariableReference, IComponentInvocation
    {
        int bindingSlot = -1;

        public int BindingSlot
        {
            get => bindingSlot; set
            {
                if (bindingSlot != -1)
                {
                    throw new InvalidOperationException("Duplicate binding slot assignment");
                }

                bindingSlot = value;
            }
        }

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

        public override string ExpressionName => "gVarRef";
        public GlobalVariableReference(StructuredQName name) : base(name)
        {
        }

        public GlobalVariableReference(GlobalVariable var) : base(var)
        {
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            if (binding == null)
            {

                throw new NotSupportedException("Cannot copy a variable reference whose binding is unknown");
            }

            GlobalVariableReference @ref = new GlobalVariableReference(VariableName);
            @ref.CopyFrom(this);
            return @ref;
        }

        public SymbolicName GetSymbolicName()
        {
            return new SymbolicName(StandardNames.XSL_VARIABLE, VariableName);
        }

        public Component GetTarget()
        {
            return ((GlobalVariable)binding).DeclaringComponent;
        }

        // Must override the base slot-unaware path: xsl:original references have binding==null and
        // resolve only through the component binding slot (upstream returns covariant GroundedValue).
        public override ISequence EvaluateVariable(IXPathContext c)
        {
            if (bindingSlot >= 0)
            {
                if (c.GetCurrentComponent() == null)
                {
                    throw new InvalidOperationException("No current component");
                }

                Component target = c.GetTargetComponent(bindingSlot);
                if (target.IsHiddenAbstractComponent())
                {
                    throw new XPathException("Cannot evaluate an abstract variable (" + VariableName.DisplayName + ") with no overriding declaration", "XTDE3052").WithLocation(GetLocation());
                }

                GlobalVariable p = (GlobalVariable)target.GetActor();
                return p.EvaluateVariable(c, target);
            }
            else
            {

                // code for references to final/private variables, also used in XQuery
                GlobalVariable b = (GlobalVariable)binding;
                return b.EvaluateVariable(c, b.DeclaringComponent);
            }
        }

        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("gVarRef", this);
            @out.EmitAttribute("name", VariableName);
            @out.EmitAttribute("bSlot", "" + BindingSlot);
            @out.EndElement();
        }

        public override Elaborator GetElaborator()
        {
            return new GlobalVariableReferenceElaborator();
        }

        /// <summary>
        /// Elaborator for a global variable reference, for example {@code $globalVar}.
        /// </summary>
        internal sealed class GlobalVariableReferenceElaborator : PullElaborator, ISequenceEvaluator
        {
            public ISequence Evaluate(IXPathContext context)
            {
                GlobalVariableReference varRef = (GlobalVariableReference)GetExpression();
                return varRef.EvaluateVariable(context);
            }

            public override ISequenceEvaluator Eagerly()
            {
                return this;
            }

            public override ISequenceEvaluator Lazily(bool repeatable, bool lazyEvaluationRequired)
            {
                return this;
            }

            public override IPullEvaluator ElaborateForPull()
            {
                GlobalVariableReference varRef = (GlobalVariableReference)GetExpression();
                return (context) => varRef.EvaluateVariable(context).Iterate();
            }

            public override IPushEvaluator ElaborateForPush()
            {
                GlobalVariableReference varRef = (GlobalVariableReference)GetExpression();
                return (@out, context) =>
                {
                    ISequenceIterator value = varRef.EvaluateVariable(context).Iterate();
                    for (IItem it; (it = value.Next()) != null;)
                    {
                        @out.Append(it);
                    }

                    return null;
                };
            }

            public override IItemEvaluator ElaborateForItem()
            {
                GlobalVariableReference varRef = (GlobalVariableReference)GetExpression();
                return (context) => varRef.EvaluateVariable(context).Head();
            }
        }
    }
}
