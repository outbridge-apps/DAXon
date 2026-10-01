////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Trees.Iterators
{
    internal abstract class ArrayIterator : ISequenceIterator, IFocusIterator, ILastPositionFinder, ILookaheadIterator, IGroundedIterator, IReversibleIterator
    {
        protected int index; // position in array of current item, zero-based
        protected int start; // position of first item to be returned, zero-based
        protected int end; // position of first item that is NOT returned, zero-based
        public abstract bool HasNext { get; }
        public abstract ISequenceIterator MakeSliceIterator(int min, int max);
        public virtual bool IsActuallyGrounded()
        {
            return true;
        }

        public virtual bool SupportsHasNext()
        {
            return true;
        }

        public virtual int Position()
        {
            return index - start;
        }

        public virtual int GetLength()
        {
            return end - start;
        }
        public abstract IItem Current();
        public abstract bool SupportsGetLength();
        public abstract IGroundedValue GetResidue();
        public abstract ISequenceIterator GetReverseIterator();
        public abstract IItem Next();
        public virtual void Dispose() { }

        // === Auto-generated stubs (StubGenerator Phase 3.1f) ===
        public abstract IGroundedValue Materialize();

        internal class Of<T> : ArrayIterator where T : class, IItem
        {
            protected T[] items;

            public override bool HasNext => index < end;

            public virtual int StartPosition => start;

            public virtual int EndPosition => end;

            public Of(T[] items)
            {
                this.items = items;
                start = 0;
                end = items.Length;
                index = 0;
            }

            public Of(T[] items, int start, int end)
            {
                this.items = items;
                this.end = end;
                this.start = start;
                index = start;
            }

            public override ISequenceIterator MakeSliceIterator(int min, int max)
            {
                T[] items = GetArray();
                int currentStart = StartPosition;
                int currentEnd = EndPosition;
                if (min < 1)
                {
                    min = 1;
                }

                int newStart = currentStart + (min - 1);
                if (newStart < currentStart)
                {
                    newStart = currentStart;
                }

                int newEnd = max == int.MaxValue ? currentEnd : newStart + max - min + 1;
                if (newEnd > currentEnd)
                {
                    newEnd = currentEnd;
                }

                if (newEnd <= newStart)
                {
                    return EmptyIterator.GetInstance();
                }

                return new Of<T>(items, newStart, newEnd);
            }

            public override IItem Next()
            {
                if (index >= end)
                {
                    index = end + 1;
                    return null;
                }

                return items[index++];
            }

            public override IItem Current()
            {
                return index > start && index <= end ? items[index - 1] : null;
            }

            public override bool SupportsGetLength()
            {
                return true;
            }

            public override int GetLength()
            {
                return end - start;
            }

            public virtual T[] GetArray()
            {
                return items;
            }

            // override (was a hide): calls through the ArrayIterator BASE type hit the base virtual
            // NIE stub even though this correct impl existed (axes-057 raised-ERR family).
            public override IGroundedValue Materialize()
            {
                // Java: Arrays.asList(items).subList(start, end) — views, not copies
                SequenceExtent.Of<T> seq;
                if (start == 0 && end == items.Length)
                {
                    seq = new SequenceExtent.Of<T>(items);
                }
                else
                {
                    IList<T> sublist = new ArraySegment<T>(items, start, end - start);
                    seq = new SequenceExtent.Of<T>(sublist);
                }

                return seq.Reduce();
            }

            public override IGroundedValue GetResidue()
            {
                SequenceExtent seq;
                if (start == 0 && index == 0 && end == items.Length)
                {
                    seq = new SequenceExtent.Of<T>(items);
                }
                else
                {
                    IList<T> sublist = new ArraySegment<T>(items, start + index, end - (start + index));
                    seq = new SequenceExtent.Of<T>(sublist);
                }

                return seq.Reduce();
            }

            public override ISequenceIterator GetReverseIterator()
            {
                if (start == 0 && end == items.Length)
                {
                    return Reverse.ReverseIterator((IList<T>)items);
                }
                else
                {
                    IList<T> sublist = new ArraySegment<T>(items, start, end - start);
                    return Reverse.ReverseIterator(sublist);
                }
            }
        }

        internal sealed class OfNodes<N> : Of<N>, IAxisIterator where N : class, NodeInfo
        {
            public OfNodes(N[] nodes) : base(nodes)
            {
            }

            NodeInfo IAxisIterator.Next() => (NodeInfo)base.Next();
        }
    }
}