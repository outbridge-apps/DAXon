////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Values;
using System.Collections.Generic;

namespace OutSmart.DAXon.Expressions.Sorting
{
    // The sorted merge of the merge inputs, retaining all duplicates; no grouping of adjacent items. Saxon chains one
    // two-way MergeIterator per input, which recursed through every input for every item: k inputs cost O(n·k) time
    // and k frames of stack (20 000 for-each-item anchors: 49 s, and an overflow on a small thread). One binary heap
    // over the inputs' next items instead, ties to the earlier input: the order that chain gives.
    internal sealed class MergeIterator : ISequenceIterator, ILookaheadIterator
    {
        private readonly ISequenceIterator[] inputs;
        private readonly ObjectValue<ItemWithMergeKeys>[] heads;   // the next item of each input
        private readonly int[] heap;   // numbers of the inputs that have one, least (head, number) first
        private int size;
        private readonly IComparer<ObjectValue<ItemWithMergeKeys>> comparer;

        public bool HasNext => size > 0;

        /// <summary>
        /// Create the iterator. Each input must return its items in merge key order.
        /// </summary>
        public MergeIterator(IList<ISequenceIterator> inputs, IComparer<ObjectValue<ItemWithMergeKeys>> comparer)
        {
            this.inputs = new ISequenceIterator[inputs.Count];
            inputs.CopyTo(this.inputs, 0);
            this.comparer = comparer;
            heads = new ObjectValue<ItemWithMergeKeys>[inputs.Count];
            heap = new int[inputs.Count];
            for (int i = 0; i < this.inputs.Length; i++)
            {
                OutSmart.DAXon.Core.Controller.CheckActiveTimeout();
                heads[i] = (ObjectValue<ItemWithMergeKeys>)this.inputs[i].Next();
                if (heads[i] != null)
                {
                    heap[size] = i;
                    SiftUp(size++);
                }
            }
        }

        public bool SupportsHasNext() => true;

        public IItem Next()
        {
            if (size == 0)
            {
                return null;
            }

            int top = heap[0];
            ObjectValue<ItemWithMergeKeys> current = heads[top];
            heads[top] = (ObjectValue<ItemWithMergeKeys>)inputs[top].Next();
            if (heads[top] == null)
            {
                heap[0] = heap[--size];
            }

            SiftDown(0);
            return current;
        }

        private void SiftUp(int k)
        {
            while (k > 0)
            {
                int parent = (k - 1) / 2;
                if (!Before(heap[k], heap[parent]))
                {
                    return;
                }

                Swap(k, parent);
                k = parent;
            }
        }

        private void SiftDown(int k)
        {
            while (true)
            {
                int least = k;
                int left = 2 * k + 1;
                if (left < size && Before(heap[left], heap[least]))
                {
                    least = left;
                }

                if (left + 1 < size && Before(heap[left + 1], heap[least]))
                {
                    least = left + 1;
                }

                if (least == k)
                {
                    return;
                }

                Swap(k, least);
                k = least;
            }
        }

        private void Swap(int a, int b)
        {
            int t = heap[a];
            heap[a] = heap[b];
            heap[b] = t;
        }

        // Whether input a's next item comes before input b's.
        private bool Before(int a, int b)
        {
            int c;
            try
            {
                c = comparer.Compare(heads[a], heads[b]);
            }
            catch (System.InvalidCastException)
            {
                AtomicValue a1 = heads[a].GetObject().sortKeyValues[0];
                AtomicValue a2 = heads[b].GetObject().sortKeyValues[0];
                XPathException err = new XPathException("Merge key values are of non-comparable types ("
                    + OutSmart.DAXon.Types.Type.DisplayTypeName(a1)
                    + " and " + OutSmart.DAXon.Types.Type.DisplayTypeName(a2) + ")", "XTTE2230");
                err.SetIsTypeError(true);
                throw new UncheckedXPathException(err);
            }

            return c < 0 || (c == 0 && a < b);
        }

        public void Dispose()
        {
            foreach (ISequenceIterator input in inputs)
            {
                input.Dispose();
            }
        }
    }
}
