////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Flwor;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Types;
using System.Collections.Generic;
using SequenceType = OutSmart.DAXon.Values.SequenceType;

namespace OutSmart.DAXon.Patterns
{
    // Faithful port of net/sf/saxon/pattern/PatternThatSetsCurrent.java (Saxon 12.9). Was a hollow
    // wrong-namespace stub in style/ (GetCurrentBinding => NIE) — every match pattern whose predicate
    // calls current() died at compile time (match-049/099/216/240* family).
    // Wraps another pattern and binds fn:current to a local variable holding the matched item, so
    // predicates can refer to current() while the wrapped pattern shifts the context.
    internal sealed class PatternThatSetsCurrent : Pattern
    {
        private readonly LocalVariableBinding binding;
        private Pattern wrappedPattern;

        public ILocalBinding CurrentBinding => binding;

        public override int Fingerprint => wrappedPattern.Fingerprint;

        public PatternThatSetsCurrent(Pattern wrappedPattern)
            : this(wrappedPattern, new LocalVariableBinding(Current.FN_CURRENT, SequenceType.SINGLE_ITEM))
        {
        }

        public PatternThatSetsCurrent(Pattern wrappedPattern, LocalVariableBinding binding)
        {
            this.wrappedPattern = wrappedPattern;
            this.binding = binding;
            binding.SetRequiredType(SequenceType.MakeSequenceType(wrappedPattern.GetItemType(), StaticProperty.EXACTLY_ONE));
            AdoptChildExpression(wrappedPattern);
            SetPriority(wrappedPattern.DefaultPriority);
        }

        public override IEnumerable<Operand> Operands()
        {
            return new Operand(this, wrappedPattern, OperandRole.SINGLE_ATOMIC);
        }

        public override bool HasVariableBinding(IBinding binding)
        {
            return binding == this.binding;
        }

        public override int AllocateSlots(SlotManager slotManager, int nextFree)
        {
            slotManager.AllocateSlotNumber(Current.FN_CURRENT, null);
            binding.SetSlotNumber(nextFree++);
            return wrappedPattern.AllocateSlots(slotManager, nextFree);
        }

        public override bool Matches(IItem item, IXPathContext context)
        {
            context.SetLocalVariable(binding.LocalSlotNumber, item);
            return wrappedPattern.Matches(item, context);
        }

        public override ItemType GetItemType()
        {
            return wrappedPattern.GetItemType();
        }

        public override Expression Simplify()
        {
            wrappedPattern = (Pattern)wrappedPattern.Simplify();
            return this;
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {
            wrappedPattern = (Pattern)wrappedPattern.TypeCheck(visitor, contextItemType);
            return this;
        }

        public override UType GetUType()
        {
            return wrappedPattern.GetUType();
        }

        public override string Reconstruct()
        {
            return wrappedPattern.ToString();
        }

        public override bool IsMotionless()
        {
            return wrappedPattern.IsMotionless();
        }

        public override bool MatchesBeneathAnchor(NodeInfo node, NodeInfo anchor, IXPathContext context)
        {
            return wrappedPattern.MatchesBeneathAnchor(node, anchor, context);
        }

        public override Pattern ConvertToTypedPattern(string val)
        {
            Pattern w2 = wrappedPattern.ConvertToTypedPattern(val);
            return w2 == wrappedPattern ? this : new PatternThatSetsCurrent(w2);
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            LocalVariableBinding newCurrent = new LocalVariableBinding(Current.FN_CURRENT, SequenceType.SINGLE_ITEM);
            rebindings.Put(binding, newCurrent);
            PatternThatSetsCurrent n = new PatternThatSetsCurrent((Pattern)wrappedPattern.Copy(rebindings), newCurrent);
            ExpressionTool.CopyLocationInfo(this, n);
            n.OriginalText = OriginalText;
            return n;
        }

        public override void Export(ExpressionPresenter presenter)
        {
            presenter.StartElement("p.withCurrent");
            wrappedPattern.Export(presenter);
            presenter.EndElement();
        }
    }
}
