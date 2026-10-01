////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Ported from upstream net/sf/saxon/expr/UnionIterator.java (replaces the hollow stub).
// A multi-way union delivering the sorted results obtained from a number of sorted input iterators.

using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Trees.Iterators;

namespace OutSmart.DAXon.Expressions
{
    internal sealed class UnionIterator : ISequenceIterator, ILookaheadIterator
    {

        private readonly SortedSet<Intake> intakes;

        public bool HasNext => intakes.Count != 0;

        /// <summary>
        /// Create the iterator. The input iterators must each return nodes in document order.
        /// </summary>
        /// <param name="inputs">iterators over the operand sequences (each in document order)</param>
        /// <param name="comparer">tests whether nodes are in document order</param>
        public UnionIterator(IList<ISequenceIterator> inputs, IComparer<NodeInfo> comparer)
        {
            intakes = new SortedSet<Intake>(new IntakeComparer(comparer));
            foreach (ISequenceIterator seq in inputs)
            {
                NodeInfo next = (NodeInfo)seq.Next();
                while (next != null)
                {
                    bool added = intakes.Add(new Intake(seq, next));
                    if (added)
                    {
                        break;
                    }
                    else
                    {
                        // the node was a duplicate, so skip it
                        next = (NodeInfo)seq.Next();
                    }
                }
            }
        }

        public bool SupportsHasNext() => true;

        public IItem Next()
        {
            // The intakes are sorted, so the first is the next node in document order.
            if (intakes.Count == 0)
            {
                return null;
            }

            Intake nextIntake = intakes.Min;
            intakes.Remove(nextIntake);

            // Replenish from the corresponding iterator, skipping duplicates (a next node equal either to
            // the node just removed, or to any node still held in another intake).
            ISequenceIterator iter = nextIntake.Iter;
            NodeInfo nextNode = (NodeInfo)iter.Next();
            while (nextNode != null)
            {
                bool added = false;
                if (!nextNode.IsSameNodeInfo(nextIntake.NextNode))
                {
                    added = intakes.Add(new Intake(iter, nextNode));
                }

                if (added)
                {
                    break;
                }
                else
                {
                    nextNode = (NodeInfo)iter.Next();
                }
            }

            return nextIntake.NextNode;
        }

        public void Close()
        {
            foreach (Intake intake in intakes)
            {
                intake.Iter.Dispose();
            }
        }

        public void Dispose() => Close();
        // A sorted set of "intakes", one per input iterator that is not yet exhausted; each holds the
        // iterator and the next node it will deliver, sorted by document order of that node. Java uses a
        // TreeSet whose add() also does duplicate elimination (compare()==0 means the same node).
        private sealed class Intake
        {
            public ISequenceIterator Iter;
            public NodeInfo NextNode;

            public Intake(ISequenceIterator iter, NodeInfo nextNode)
            {
                Iter = iter;
                NextNode = nextNode;
            }
        }

        private sealed class IntakeComparer : IComparer<Intake>
        {
            private readonly IComparer<NodeInfo> itemOrderComparer;

            public IntakeComparer(IComparer<NodeInfo> itemOrderComparer)
            {
                this.itemOrderComparer = itemOrderComparer;
            }

            public int Compare(Intake o1, Intake o2)
            {
                return itemOrderComparer.Compare(o1.NextNode, o2.NextNode);
            }
        }
    }
}
