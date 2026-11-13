////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Expressions.Sorting
{
    internal sealed class DocumentOrderIterator : ISequenceIterator
    {
        private readonly ISequenceIterator iterator;
        private readonly List<NodeInfo> sequence; // explicit type ArrayList used so C# List.Sort() is available
        private NodeInfo current;

        // Peeked factory for the hot docOrder sites: 0/1-item inputs return the Empty/Singleton
        // iterator ITSELF - no wrapper allocation and no per-item dedup Equals on the way out
        // (typical shape: docOrder around a singleton path inside a per-item lambda). The non-node
        // XPTY0004 check matches the constructor's.
        internal static ISequenceIterator Of(ISequenceIterator @base, IComparer<NodeInfo> comparer)
        {
            IItem first = @base.Next();
            if (first == null)
            {
                return EmptyIterator.GetInstance();
            }

            if (!(first is NodeInfo))
            {
                throw new XPathException("Item in input for sorting is not a node: " + Err.Depict(first), "XPTY0004");
            }

            IItem second = @base.Next();
            if (second == null)
            {
                return SingletonIterator.MakeIterator(first);
            }

            return new DocumentOrderIterator(@base, (NodeInfo)first, second, comparer);
        }

        // The whole input is held while it is sorted: the clock is read as it collects, and a big list is entered in the
        // memory ledger (the nodes are their trees'; the list holds references).
        private static void Added(List<NodeInfo> sequence)
        {
            int n = sequence.Count;
            if ((n & 0xFF) == 0)
            {
                if (n == Core.MemoryLedger.BigCount)
                {
                    Core.MemoryLedger.Hold(sequence, ListSizer);
                }

                Core.Controller.CheckActiveTimeout();
            }
        }

        // The nodes as well: tree nodes made per step are flyweights the list keeps (8 B each counted, 1.6 GB held under 300 MB).
        private static readonly Func<object, long> ListSizer = o => Core.MemoryLedger.ListBytes((List<NodeInfo>)o);

        private DocumentOrderIterator(ISequenceIterator @base, NodeInfo first, IItem second, IComparer<NodeInfo> comparer)
        {
            int len = SequenceTool.SupportsGetLength(@base) ? SequenceTool.GetLength(@base) : 50;
            sequence = new List<NodeInfo>(len < 2 ? 2 : len);
            sequence.Add(first);
            IItemConsumer<IItem> add = (item) =>
            {
                if (item is NodeInfo)
                {
                    sequence.Add((NodeInfo)item);
                    Added(sequence);
                }
                else
                {
                    throw new XPathException("Item in input for sorting is not a node: " + Err.Depict(item), "XPTY0004");
                }
            };
            add(second);
            SequenceTool.Supply(@base, add);
            DeadlineSort.Sort(sequence, comparer);
            iterator = new NodeListIterator(sequence);
        }

        // Implement the ISequenceIterator as a wrapper around the underlying iterator
        // over the sequenceExtent, but looking ahead to remove duplicates.
        public NodeInfo Next()
        {
            while (true)
            {
                NodeInfo next = (NodeInfo)iterator.Next();
                if (next == null)
                {
                    current = null;
                    return null;
                }

                if (!next.Equals(current))
                {
                    current = next;
                    return current;
                }
            }
        }
        IItem ISequenceIterator.Next() => Next();
        public void Dispose() { }
    }
}

