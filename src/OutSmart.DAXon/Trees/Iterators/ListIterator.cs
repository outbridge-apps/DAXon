////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System.Collections.Generic;
using System.Linq;
namespace OutSmart.DAXon.Trees.Iterators
{
    internal abstract class ListIterator : ISequenceIterator, IFocusIterator, ILastPositionFinder, ILookaheadIterator, IGroundedIterator, IReversibleIterator
    {
        public abstract bool HasNext { get; }
        public virtual bool SupportsHasNext()
        {
            return true;
        }
        public abstract IItem Current();
        public abstract int Position();
        public abstract int GetLength();
        public abstract bool SupportsGetLength();
        public abstract bool IsActuallyGrounded();
        public abstract IGroundedValue GetResidue();
        public abstract ISequenceIterator GetReverseIterator();
        public abstract IItem Next();
        public virtual void Dispose() { }

        // === Auto-generated stubs (StubGenerator Phase 3.1f) ===
        public abstract IGroundedValue Materialize();

        internal class Of<T> : ListIterator, ISequenceIterator, IFocusIterator, ILastPositionFinder, ILookaheadIterator, IGroundedIterator, IReversibleIterator
        {
            private int index = 0;
            protected IList<T> list;

            public Of(IList<T> list)
            {
                index = 0;
                this.list = list;
            }

            public override bool HasNext => index < list.Count;

            public override IItem Next()
            {
                if (index >= list.Count)
                {
                    return null;
                }

                return (IItem)(object)list[index++];
            }

            public override bool SupportsGetLength()
            {
                return true;
            }

            public override int GetLength()
            {
                return list.Count;
            }

            public override IItem Current()
            {
                return (IItem)(object)list[index - 1];
            }

            public override int Position()
            {
                return index;
            }

            public override bool IsActuallyGrounded()
            {
                return true;
            }

            // override (was a hide): calls through the ListIterator BASE type hit the base virtual NIE.
            // Java-faithful zero-copy: ListIterator.materialize() == SequenceExtent.makeSequenceExtent(list),
            // wrapping the backing list by reference in O(1). The prior list.Cast<IItem>().ToList() re-copied
            // the whole sequence on every call, so subsequence(groundedSeq, min>4, len) — which grounds the base
            // via SequenceTool.ToGroundedValue -> Materialize — was O(N) per call (e.g. count(subsequence($E,$i,5))
            // in a loop = O(N) per iteration instead of O(len)). When the backing list is already IList<IItem>
            // (the common case: node/atomic sequences), wrap it directly; the .Cast().ToList() copy remains only
            // as a fallback for the exotic unconstrained-T lists the transpiler left (Java had T extends Item).
            public override IGroundedValue Materialize()
            {
                if (list is IList<IItem> il)
                {
                    return SequenceExtent.MakeSequenceExtent(il);
                }

                return new SequenceExtent.Of<IItem>(list.Cast<IItem>().ToList());
            }

            public override IGroundedValue GetResidue()
            {
                IList<T> l2 = list;
                if (index != 0)
                {
                    l2 = l2.GetRange(index, (l2.Count) - (index));
                }

                if (l2 is IList<IItem> il)
                {
                    return SequenceExtent.MakeSequenceExtent(il);
                }

                return new SequenceExtent.Of<IItem>(l2.Cast<IItem>().ToList());
            }

            public override ISequenceIterator GetReverseIterator()
            {
                return Reverse.ReverseIterator(list);
            }

            // explicit interface re-implementation (Of<T> covariant-hiding fix): the typed members above HIDE the base ListIterator NIE virtuals
            // (no covariant returns in C# 7.3), so without these remaps every interface-typed call on an
            // Of<T> instance threw NotImplementedException (e.g. materializing a literal map/array lookup).
            // (IItem)(object) bridges the unconstrained T (transpiler dropped Java's T extends Item).
            IItem ISequenceIterator.Next() => (IItem)(object)Next();
            IItem IFocusIterator.Current() => (IItem)(object)Current();
            int IFocusIterator.Position() => Position();
            int IFocusIterator.GetLength() => GetLength();
            int ILastPositionFinder.GetLength() => GetLength();
            bool ILastPositionFinder.SupportsGetLength() => SupportsGetLength();
            bool ILookaheadIterator.SupportsHasNext() => SupportsHasNext();
            IGroundedValue IGroundedIterator.GetResidue() => GetResidue();
            IGroundedValue IGroundedIterator.Materialize() => Materialize();
            bool IGroundedIterator.IsActuallyGrounded() => IsActuallyGrounded();
            ISequenceIterator IReversibleIterator.GetReverseIterator() => GetReverseIterator();
        }

        internal sealed class OfAtomic<A> : Of<A>, IAtomicIterator
        {
            public OfAtomic(IList<A> nodes) : base(nodes)
            {
            }

            AtomicValue IAtomicIterator.Next() => (AtomicValue)(object)base.Next();
        }
    }
}
