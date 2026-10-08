////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Transformation;
using System;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Expressions
{
    internal sealed class LocalVariableReference : VariableReference
    {
        int slotNumber = -999;

        public int SlotNumber
        {
            get => slotNumber; set
            {
                this.slotNumber = value;
            }
        }

        public override string ExpressionName => "locVarRef";
        public LocalVariableReference(StructuredQName name) : base(name)
        {
        }

        public LocalVariableReference(ILocalBinding binding) : base(binding)
        {
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            if (binding == null)
            {
                throw new NotSupportedException("Cannot copy a variable reference whose binding is unknown");
            }


            //        if (slotNumber < 0) {
            //            // slot numbers have not yet been allocated. This is messy. For the time being, the safest
            //            // thing is to reuse the existing variable reference, which usually works, rather than copying
            //            // it, which will almost certainly fail. See XSLT streaming test case si-attribute-053
            LocalVariableReference @ref = new LocalVariableReference(VariableName);
            @ref.CopyFrom(this);
            @ref.slotNumber = slotNumber;
            IBinding newBinding = rebindings[binding];
            if (newBinding != null)
            {
                @ref.binding = newBinding;
            }

            @ref.binding.AddReference(@ref, IsInLoop());
            return @ref;
        }

        public void SetBinding(ILocalBinding binding)
        {
            this.binding = binding;
        }

        public new ILocalBinding GetBinding()
        {
            return (ILocalBinding)base.GetBinding();
        }

        public override ISequence EvaluateVariable(IXPathContext c)
        {
            try
            {
                return c.GetStackFrame().slots[slotNumber];
            }
            catch (IndexOutOfRangeException)
            {
                if (slotNumber == -999)
                {
                    if (binding != null)
                    {
                        try
                        {
                            slotNumber = GetBinding().LocalSlotNumber;
                            return c.GetStackFrame().slots[slotNumber];
                        }
                        catch (IndexOutOfRangeException)
                        {
                        }
                    }

                    throw new IndexOutOfRangeException("Local variable $" + DisplayName + " has not been allocated a stack frame slot");
                }
                else
                {
                    int actual = c.GetStackFrame().slots.Length;
                    throw new IndexOutOfRangeException("Local variable $" + DisplayName + " uses slot " + slotNumber + " but " + (actual == 0 ? "no" : "only " + c.GetStackFrame().slots.Length) + " slots" + " are allocated on the stack frame");
                }
            }
        }

        //    }
        public override Elaborator GetElaborator()
        {
            return new LocalVariableReferenceElaborator();
        }

        //    }
        /// <summary>
        /// Elaborator for a local variable reference, for example {@code $var}.
        /// </summary>
        internal sealed class LocalVariableReferenceElaborator : PullElaborator
        {
            public override void SetExpression(Expression expr)
            {
                base.SetExpression(expr);
                if (((LocalVariableReference)expr).SlotNumber < 0)
                {
                    throw new InvalidOperationException("Can't elaborate a local variable reference before slot numbers have been allocated");
                }
            }

            public override ISequenceEvaluator Eagerly()
            {
                LocalVariableReference varRef = (LocalVariableReference)GetExpression();
                int slot = varRef.SlotNumber;
                return new LocalVariableEvaluator(slot);
            }

            public override ISequenceEvaluator Lazily(bool repeatable, bool lazyEvaluationRequired)
            {
                return Eagerly();
            }

            public override IPullEvaluator ElaborateForPull()
            {
                LocalVariableReference varRef = (LocalVariableReference)GetExpression();
                int slot = varRef.SlotNumber;
                return (context) =>
                {
                    try
                    {
                        return context.EvaluateLocalVariable(slot).Iterate();
                    }
                    catch (UncheckedXPathException e)
                    {
                        throw e.GetXPathException().MaybeWithLocation(GetExpression().GetLocation()).MaybeWithContext(context);
                    }
                };
            }

            public override IPushEvaluator ElaborateForPush()
            {
                LocalVariableReference varRef = (LocalVariableReference)GetExpression();
                int slot = varRef.SlotNumber;
                return (@out, context) =>
                {
                    try
                    {
                        ISequenceIterator value = context.EvaluateLocalVariable(slot).Iterate();
                        for (IItem it; (it = value.Next()) != null;)
                        {
                            @out.Append(it);
                        }

                        return null;
                    }
                    catch (UncheckedXPathException e)
                    {
                        throw e.GetXPathException().MaybeWithLocation(GetExpression().GetLocation()).MaybeWithContext(context);
                    }
                    catch (XPathException e)
                    {
                        throw e.MaybeWithLocation(GetExpression().GetLocation()).MaybeWithContext(context);
                    }
                };
            }

            public override IItemEvaluator ElaborateForItem()
            {
                LocalVariableReference varRef = (LocalVariableReference)GetExpression();
                int slot = varRef.SlotNumber;
                return (context) =>
                {
                    try
                    {
                        return context.EvaluateLocalVariable(slot).Head();
                    }
                    catch (UncheckedXPathException e)
                    {
                        throw e.GetXPathException().MaybeWithLocation(GetExpression().GetLocation()).MaybeWithContext(context);
                    }
                    catch (XPathException e)
                    {
                        throw e.MaybeWithLocation(GetExpression().GetLocation()).MaybeWithContext(context);
                    }
                };
            }
        }
    }
}