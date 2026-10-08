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
using OutSmart.DAXon.Types;
using System.Collections.Generic;
using OutSmart.DAXon.Expressions;
namespace OutSmart.DAXon.Patterns
{
    internal abstract class VennPattern : Pattern
    {
        public Pattern p1, p2;

        public override int Dependencies => p1.Dependencies | p2.Dependencies;

        public virtual Pattern LHS => p1;

        public virtual Pattern RHS => p2;

        /// <summary>
        /// Hashcode supporting equals()
        /// </summary>
        protected abstract string OperatorName { get; }
        public VennPattern(Pattern p1, Pattern p2)
        {
            this.p1 = p1;
            this.p2 = p2;
            AdoptChildExpression(p1);
            AdoptChildExpression(p2);
        }

        public override IEnumerable<Operand> Operands()
        {
            return OperandList(new Operand(this, p1, OperandRole.SAME_FOCUS_ACTION), new Operand(this, p2, OperandRole.SAME_FOCUS_ACTION));
        }

        public override Expression Simplify()
        {
            p1 = (Pattern)p1.Simplify();
            p2 = (Pattern)p2.Simplify();
            return this;
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {
            MustBeNodePattern(p1);
            p1 = (Pattern)p1.TypeCheck(visitor, contextItemType);
            MustBeNodePattern(p2);
            p2 = (Pattern)p2.TypeCheck(visitor, contextItemType);
            return this;
        }

        private void MustBeNodePattern(Pattern p)
        {
            if (p is NodeTestPattern)
            {
                ItemType it = p.GetItemType();
                if (!(it is NodeTest))
                {
                    XPathException err = new XPathException("The operands of a union, intersect, or except pattern " + "must be patterns that match nodes", "XPTY0004");
                    err.SetIsTypeError(true);
                    throw err;
                }
            }
        }

        public override void BindCurrent(ILocalBinding binding)
        {
            p1.BindCurrent(binding);
            p2.BindCurrent(binding);
        }

        public override bool IsMotionless()
        {
            return p1.IsMotionless() && p2.IsMotionless();
        }

        public override int AllocateSlots(SlotManager slotManager, int nextFree)
        {
            nextFree = p1.AllocateSlots(slotManager, nextFree);
            nextFree = p2.AllocateSlots(slotManager, nextFree);
            return nextFree;
        }

        public virtual void GatherComponentPatterns(HashSet<Pattern> set)
        {
            if (p1 is VennPattern)
            {
                ((VennPattern)p1).GatherComponentPatterns(set);
            }
            else
            {
                set.Add(p1);
            }

            if (p2 is VennPattern)
            {
                ((VennPattern)p2).GatherComponentPatterns(set);
            }
            else
            {
                set.Add(p2);
            }
        }

        public override bool MatchesCurrentGroup()
        {
            return p1.MatchesCurrentGroup() || p2.MatchesCurrentGroup();
        }

        public override bool Equals(object other)
        {
            if (other is VennPattern)
            {
                HashSet<Pattern> s0 = new HashSet<Pattern>();
                GatherComponentPatterns(s0);
                HashSet<Pattern> s1 = new HashSet<Pattern>();
                ((VennPattern)other).GatherComponentPatterns(s1);
                return s0.Equals(s1);
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Hashcode supporting equals()
        /// </summary>
        protected override int ComputeHashCode()
        {
            return 0x6bd723a6 ^ p1.GetHashCode() ^ p2.GetHashCode();
        }
        /// <summary>
        /// Get the original pattern text
        /// </summary>
        public override string Reconstruct()
        {
            return p1 + " " + OperatorName + " " + p2;
        }

        /// <summary>
        /// Get the original pattern text
        /// </summary>
        public override void Export(ExpressionPresenter presenter)
        {
            presenter.StartElement("p.venn");
            presenter.EmitAttribute("op", OperatorName);
            p1.Export(presenter);
            p2.Export(presenter);
            presenter.EndElement();
        }
    }
}
