////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;

using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
namespace OutSmart.DAXon.Model
{
    internal sealed class MemoSequence : ISequence
    {
        private readonly ISequenceIterator inputIterator;
        private IItem[] reservoir = null;
        // Above Chunk items the reservoir becomes IItem[][] (64KB chunks): a single flat pointer
        // array of 300k+ refs lives on the LOH and background GC re-marks it wholesale — the
        // dominant cost of materializing big variables. Small sequences keep the flat array path.
        private const int Chunk = 8192;
        private IItem[][] chunks = null;
        private int used;
        private LearningEvaluator learningEvaluator;
        private int serialNumber;

        private State state = State.UNREAD;
        public MemoSequence(ISequenceIterator iterator)
        {
            this.inputIterator = iterator;
        }

        public void SetLearningEvaluator(LearningEvaluator caller, int serialNumber)
        {
            this.learningEvaluator = caller;
            this.serialNumber = serialNumber;
        }

        public IItem Head()
        {
            return Iterate().Next();
        }

        public ISequenceIterator Iterate()
        {
            lock (this)
            {
                switch (state)
                {
                    case State.UNREAD:
                        state = State.BUSY;
                        if (inputIterator is EmptyIterator)
                        {
                            state = State.EMPTY;
                            return inputIterator;
                        }

                        reservoir = new IItem[50];
                        used = 0;
                        state = State.MAYBE_MORE;
                        return new ProgressiveIterator(this);
                    case State.MAYBE_MORE:
                        return new ProgressiveIterator(this);
                    case State.ALL_READ:
                        switch (used)
                        {
                            case 0:
                                state = State.EMPTY;
                                return EmptyIterator.GetInstance();
                            case 1:
                                return SingletonIterator.MakeIterator(Get(0));
                            default:
                                // Java: new ArrayIterator.Of<>(reservoir, 0, used) — a view over the
                                // reservoir. The ToList().SubList() veneer copied the whole reservoir
                                // TWICE on every read of a fully-evaluated variable.
                                return chunks != null
                                    ? (ISequenceIterator)new ListIterator.Of<IItem>(new ChunkedItemList(chunks, 0, used))
                                    : new ArrayIterator.Of<IItem>(reservoir, 0, used);
                        }

                    case State.BUSY:

                        // recursive entry: can happen if there is a circularity involving variable and function definitions
                        // Can also happen if variable evaluation is attempted in a debugger, hence the cautious message
                        XPathException de = new XPathException("Attempt to access a variable while it is being evaluated", "XTDE0640");
                        throw new UncheckedXPathException(de);
                    case State.EMPTY:
                        return EmptyIterator.GetInstance();
                    case State.ERROR:
                        XPathException e2 = new XPathException("Attempting to read a local variable when an error in that variable has already been reported", "XTDE0640");
                        throw new UncheckedXPathException(e2);
                    default:
                        throw new InvalidOperationException("Unknown iterator state");
                }
            }
        }

        public IItem ItemAt(int n)
        {
            lock (this)
            {
                if (n < 0)
                {
                    return null;
                }

                if ((reservoir != null || chunks != null) && n < used)
                {
                    return Get(n);
                }

                if (state == State.ALL_READ || state == State.EMPTY)
                {
                    return null;
                }

                if (state == State.ERROR)
                {
                    throw new XPathException("Attempting to read a local variable when an error in that variable has already been reported", "XTDE0640");
                }

                if (state == State.UNREAD)
                {
                    IItem item = inputIterator.Next();
                    if (item == null)
                    {
                        state = State.EMPTY;
                        return null;
                    }
                    else
                    {
                        state = State.MAYBE_MORE;
                        reservoir = new IItem[50];
                        Append(item);
                        if (n == 0)
                        {
                            return item;
                        }
                    }
                }


                // We have read some items from the input sequence but not enough. Read as many more as are needed.
                int diff = n - used + 1;
                try
                {
                    while (diff-- > 0)
                    {
                        IItem i = inputIterator.Next();
                        if (i == null)
                        {
                            state = State.ALL_READ;
                            Condense();
                            return null;
                        }

                        Append(i);
                        state = State.MAYBE_MORE;
                    }
                }
                catch (UncheckedXPathException e)
                {
                    state = State.ERROR;
                    throw e.GetXPathException();
                }


                return Get(n);
            }
        }

        private static readonly Func<object, long> Sizer = o => ((MemoSequence)o).HeldBytes();

        // The items read so far: their references, and their own bytes as the first chunk tells.
        private long HeldBytes()
        {
            IItem[][] c = chunks;
            int n = used;
            return c == null ? 0 : 24 + 8L * n + OutSmart.DAXon.Core.MemoryLedger.MembersBytes(c[0], Chunk) / Chunk * n;
        }

        private IItem Get(int i)
        {
            IItem[] r = reservoir;
            return r != null ? r[i] : chunks[i >> 13][i & (Chunk - 1)];
        }

        private void Append(IItem item)
        {
            if (chunks == null)
            {
                if (used < reservoir.Length)
                {
                    reservoir[used++] = item;
                    return;
                }

                if (reservoir.Length < Chunk)
                {
                    Array.Resize(ref reservoir, Math.Min(reservoir.Length * 2, Chunk));
                    reservoir[used++] = item;
                    return;
                }

                // the flat part is exactly one full chunk: promote it without copying items
                chunks = new IItem[8][];
                chunks[0] = reservoir;
                reservoir = null;
                OutSmart.DAXon.Core.MemoryLedger.Hold(this, Sizer);   // summed as it grows
            }

            int ci = used >> 13;
            if (ci == chunks.Length)
            {
                Array.Resize(ref chunks, ci * 2);
            }

            IItem[] c = chunks[ci];
            if (c == null)
            {
                chunks[ci] = c = new IItem[Chunk];
            }

            c[used & (Chunk - 1)] = item;
            used++;
        }

        /// <summary>
        /// Release unused space in the reservoir (provided the amount of unused space is worth reclaiming)
        /// </summary>
        private void Condense()
        {
            if (chunks != null)
            {
                int lastCi = (used - 1) >> 13;
                int lastLen = ((used - 1) & (Chunk - 1)) + 1;
                if (chunks[lastCi] != null && Chunk - lastLen > 30)
                {
                    Array.Resize(ref chunks[lastCi], lastLen);
                }

                if (chunks.Length > lastCi + 1)
                {
                    Array.Resize(ref chunks, lastCi + 1);
                }
            }
            else if (reservoir != null && reservoir.Length - used > 30)
            {
                Array.Resize(ref reservoir, used);
            }
        }

        // Read-only IList view over the chunked reservoir (zero-copy extents/residues).
        internal sealed class ChunkedItemList : IList<IItem>
        {
            private readonly IItem[][] chunks;
            private readonly int start;
            private readonly int count;

            internal ChunkedItemList(IItem[][] chunks, int start, int count)
            {
                this.chunks = chunks;
                this.start = start;
                this.count = count;
            }

            public IItem this[int index]
            {
                get
                {
                    int i = start + index;
                    return chunks[i >> 13][i & (Chunk - 1)];
                }
                set => throw new NotSupportedException();
            }

            public int Count => count;
            public bool IsReadOnly => true;

            public IEnumerator<IItem> GetEnumerator()
            {
                for (int i = 0; i < count; i++)
                {
                    yield return this[i];
                }
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

            public int IndexOf(IItem item)
            {
                for (int i = 0; i < count; i++)
                {
                    if (Equals(this[i], item))
                        return i;
                }

                return -1;
            }

            public bool Contains(IItem item) => IndexOf(item) >= 0;

            public void CopyTo(IItem[] array, int arrayIndex)
            {
                for (int i = 0; i < count; i++)
                {
                    array[arrayIndex + i] = this[i];
                }
            }

            public void Add(IItem item) => throw new NotSupportedException();
            public void Insert(int index, IItem item) => throw new NotSupportedException();
            public bool Remove(IItem item) => throw new NotSupportedException();
            public void RemoveAt(int index) => throw new NotSupportedException();
            public void Clear() => throw new NotSupportedException();
        }

        // upstream Sequence.materialize() default: ground the iterated items
        public IGroundedValue Materialize() => SequenceTool.ToGroundedValue(Iterate());
        public ISequence MakeRepeatable() => this; // upstream Sequence.makeRepeatable default
        private enum State
        {
            // State in which no items have yet been read
            UNREAD,
            // State in which zero or more items are in the reservoir and it is not known
            // whether more items exist
            MAYBE_MORE,
            // State in which all the items are in the reservoir
            ALL_READ,
            // State in which we are getting the base iterator. If the closure is called in this state,
            // it indicates a recursive entry, which is only possible on an error path
            BUSY,
            // State in which we know that the value is an empty sequence
            EMPTY,
            // State in which we have already encountered and reported an error in reading this variable
            // It's possible further attempts will be made to read it again: see bug 6440.
            ERROR
        }

        /// <summary>
        /// Release unused space in the reservoir (provided the amount of unused space is worth reclaiming)
        /// </summary>
        internal sealed class ProgressiveIterator : ISequenceIterator, ILastPositionFinder, IGroundedIterator
        {
            private readonly MemoSequence container;
            private int position = -1; // zero-based position in the reservoir of the
            public ProgressiveIterator(MemoSequence container)
            {
                this.container = container;
            }

            public MemoSequence GetMemoSequence()
            {
                return container;
            }

            public IItem Next()
            {
                lock (container)
                {

                    // synchronized for the case where a multi-threaded xsl:for-each is reading the variable
                    if (position == -2)
                    {

                        // means we've already returned null once, keep doing so if called again.
                        return null;
                    }

                    if (++position < container.used)
                    {
                        return container.Get(position);
                    }
                    else if (container.state == State.ALL_READ)
                    {

                        // someone else has read the input to completion in the meantime
                        position = -2;
                        return null;
                    }
                    else
                    {
                        IItem i = null;
                        try
                        {
                            i = container.inputIterator.Next();
                            if (i == null)
                            {
                                container.state = State.ALL_READ;
                                container.Condense();
                                position = -2;
                                ReportCompletion();
                                return null;
                            }
                        }
                        catch (UncheckedXPathException e)
                        {
                            container.state = State.ERROR;
                            throw;
                        }

                        position = container.used;
                        container.Append(i);
                        container.state = State.MAYBE_MORE;
                        return i;
                    }
                }
            }

            public bool SupportsGetLength()
            {
                return true;
            }

            public int GetLength()
            {
                if (container.state == State.ALL_READ)
                {
                    return container.used;
                }
                else if (container.state == State.EMPTY)
                {
                    return 0;
                }
                else
                {

                    // save the current position
                    int savePos = position;

                    // fill the reservoir
                    while (Next() != null)
                    {
                    }


                    // reset the current position
                    position = savePos;

                    // return the total number of items
                    return container.used;
                }
            }

            public bool IsActuallyGrounded()
            {
                return true;
            }

            public IGroundedValue Materialize()
            {
                if (container.state == State.ALL_READ)
                {
                    return MakeExtent();
                }
                else if (container.state == State.EMPTY)
                {
                    return EmptySequence.GetInstance();
                }
                else
                {

                    // save the current position
                    int savePos = position;

                    // fill the reservoir
                    while (Next() != null)
                    {
                    }


                    // reset the current position
                    position = savePos;

                    // return all the items
                    return MakeExtent();
                }
            }

            private IGroundedValue MakeExtent()
            {
                if (container.chunks != null)
                {
                    return SequenceExtent.MakeSequenceExtent((IList<IItem>)new ChunkedItemList(container.chunks, 0, container.used));
                }

                if (container.used == container.reservoir.Length)
                {
                    if (container.used == 0)
                    {
                        return EmptySequence.GetInstance();
                    }
                    else if (container.used == 1)
                    {
                        return container.reservoir[0];
                    }
                    else
                    {
                        return new SequenceExtent.Of<IItem>(container.reservoir);
                    }
                }
                else
                {
                    // Java: Arrays.asList(reservoir).subList(0, used) — a view, not a copy
                    return SequenceExtent.MakeSequenceExtent((IList<IItem>)new ArraySegment<IItem>(container.reservoir, 0, container.used));
                }
            }

            public IGroundedValue GetResidue()
            {
                if (container.state == State.EMPTY || position >= container.used || position == -2)
                {
                    return EmptySequence.GetInstance();
                }
                else if (container.state == State.ALL_READ)
                {
                    return ResidueExtent();
                }
                else
                {

                    // save the current position
                    int savePos = position;

                    while (Next() != null)
                    {
                    }


                    // reset the current position
                    position = savePos;

                    // return all the items
                    return ResidueExtent();
                }
            }

            private IGroundedValue ResidueExtent()
            {
                int from = position + 1;
                int len = container.used - from;
                return container.chunks != null
                    ? SequenceExtent.MakeSequenceExtent((IList<IItem>)new ChunkedItemList(container.chunks, from, len))
                    : SequenceExtent.MakeSequenceExtent((IList<IItem>)new ArraySegment<IItem>(container.reservoir, from, len));
            }

            public void Dispose()
            {
                if (container.state == State.ALL_READ)
                {
                    ReportCompletion();
                }
            }

            private void ReportCompletion()
            {

                // When we've finished with the iterator, provide feedback to the binding instruction
                // as to whether all the data was read, or whether there was an early exit. This can
                // be used to switch the evaluation strategy from lazy evaluation to eager evaluation.
                // In fact we only notify when the iterator is read to completion, otherwise we
                // would get multiple notifications for constructs like `if (!empty(x)) then x`.
                if (container.learningEvaluator != null)
                {
                    container.learningEvaluator.ReportCompletion(container.serialNumber);
                }
            }
        }
    }
}