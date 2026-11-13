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

        // Asked at run time too, when an instruction's static properties are recomputed: intersect and except chains are
        // left-deep, one level per operand (unions are built balanced, VennExpression.UnionOfAlternatives).
        public override int Dependencies
        {
            get
            {
                OutSmart.DAXon.Internal.StackGuard.Probe();
                return p1.Dependencies | p2.Dependencies;
            }
        }

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

        // Equal: the same operator over the same operands, for | and intersect in any order and grouping ((a|b)|c is
        // c|(b|a)), for except as it stands. Upstream flattens through any operator, so (a except b)|c equalled a|b|c;
        // .NET's HashSet.Equals compared the two sets by reference, so no two were ever equal.
        public override bool Equals(object other)
        {
            if (!(other is VennPattern venn) || venn.GetType() != GetType())
            {
                return false;
            }

            return this is ExceptPattern ? p1.Equals(venn.p1) && p2.Equals(venn.p2) : ChainOperands().SetEquals(venn.ChainOperands());
        }

        // The operands of this chain of one operator: a | (b | c) gives a, b and c.
        private HashSet<Pattern> ChainOperands()
        {
            var operands = new HashSet<Pattern>();
            var todo = new Stack<Pattern>();
            todo.Push(p2);
            todo.Push(p1);
            while (todo.Count > 0)
            {
                Pattern p = todo.Pop();
                if (p is VennPattern venn && venn.GetType() == GetType())
                {
                    todo.Push(venn.p2);
                    todo.Push(venn.p1);
                }
                else
                {
                    operands.Add(p);
                }
            }

            return operands;
        }

        /// <summary>
        /// Hashcode supporting equals()
        /// </summary>
        protected override int ComputeHashCode()
        {
            if (this is ExceptPattern)
            {
                return 0x7bd7dfa6 ^ p1.GetHashCode() ^ (31 * p2.GetHashCode());
            }

            int h = GetType() == typeof(UnionPattern) ? 0x3bd723a6 : 0x6bd723a6;
            foreach (Pattern p in ChainOperands())
            {
                h ^= p.GetHashCode();
            }

            return h;
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
