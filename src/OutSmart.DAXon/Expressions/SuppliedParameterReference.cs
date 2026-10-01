////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Expressions
{
    internal sealed class SuppliedParameterReference : Expression
    {
        private readonly int slotNumber;
        private SequenceType type;

        public int SlotNumber => slotNumber;

        public override int IntrinsicDependencies => StaticProperty.DEPENDS_ON_LOCAL_VARIABLES;

        public override int ImplementationMethod => EVALUATE_METHOD | ITERATE_METHOD;

        public override string ExpressionName => "supplied";
        public SuppliedParameterReference(int slot)
        {
            slotNumber = slot;
        }

        public void SetSuppliedType(SequenceType type)
        {
            this.type = type;
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            return this;
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {
            return this;
        }

        public override ItemType GetItemType()
        {
            if (type != null)
            {
                return type.PrimaryType;
            }
            else
            {
                return AnyItemType.GetInstance();
            }
        }

        protected override int ComputeCardinality()
        {
            if (type != null)
            {
                return type.GetCardinality();
            }
            else
            {
                return StaticProperty.ALLOWS_ZERO_OR_MORE;
            }
        }

        public override bool SupportsLazyEvaluation()
        {
            return false;
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            SuppliedParameterReference exp = new SuppliedParameterReference(slotNumber);
            ExpressionTool.CopyLocationInfo(this, exp);
            return exp;
        }

        public ISequence EvaluateVariable(IXPathContext c)
        {
            if (slotNumber == -1)
            {
                return c.GetStackFrame().PopDynamicValue();
            }

            try
            {
                return c.EvaluateLocalVariable(slotNumber);
            }
            catch (InvalidOperationException e)
            {
                new StandardDiagnostics().LogStackTrace(c, c.GetConfiguration().Logger, 2);
                throw new InvalidOperationException(e.Message + ". No value has been set for parameter " + slotNumber);
            }
        }

        public override ISequenceIterator Iterate(IXPathContext context)
        {
            return EvaluateVariable(context).Iterate();
        }

        public override IItem EvaluateItem(IXPathContext context)
        {
            return EvaluateVariable(context).Head();
        }

        public override void Export(ExpressionPresenter destination)
        {
            destination.StartElement("supplied", this);
            destination.EmitAttribute("slot", slotNumber + "");
            if (type != null)
            {
                destination.EmitAttribute("sType", AlphaCode.FromSequenceType(type));
            }

            destination.EndElement();
        }

        public override string ToString()
        {
            return "suppliedParam(" + slotNumber + ")";
        }

        public override Elaborator GetElaborator()
        {
            return new SuppliedParameterReferenceElaborator();
        }

        internal sealed class SuppliedParameterReferenceElaborator : PullElaborator
        {
            public override ISequenceEvaluator Eagerly()
            {
                SuppliedParameterReference varRef = (SuppliedParameterReference)GetExpression();
                int slot = varRef.SlotNumber;
                return new LocalVariableEvaluator(slot);
            }

            public override ISequenceEvaluator Lazily(bool repeatable, bool lazyEvaluationRequired)
            {
                return Eagerly();
            }

            public override IPullEvaluator ElaborateForPull()
            {
                SuppliedParameterReference varRef = (SuppliedParameterReference)GetExpression();
                int slot = varRef.SlotNumber;
                return (context) => context.EvaluateLocalVariable(slot).Iterate();
            }

            public override IPushEvaluator ElaborateForPush()
            {
                SuppliedParameterReference varRef = (SuppliedParameterReference)GetExpression();
                int slot = varRef.SlotNumber;
                return (@out, context) =>
                {
                    ISequenceIterator value = context.EvaluateLocalVariable(slot).Iterate();
                    IItem it;
                    while ((it = value.Next()) != null)
                    {
                        @out.Append(it);
                    }

                    return null;
                };
            }

            public override IItemEvaluator ElaborateForItem()
            {
                SuppliedParameterReference varRef = (SuppliedParameterReference)GetExpression();
                int slot = varRef.SlotNumber;
                return (context) => context.EvaluateLocalVariable(slot).Head();
            }
        }
    }
}
