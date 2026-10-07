////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Trees.Tiny;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// Implement XPath function fn:reverse()
    /// </summary>
    internal sealed class Reverse : SystemFunction
    {

        public override string StreamerName => "Reverse";
        public override int GetSpecialProperties(Expression[] arguments)
        {
            int baseProps = arguments[0].GetSpecialProperties();
            if ((baseProps & StaticProperty.REVERSE_DOCUMENT_ORDER) != 0)
            {
                return (baseProps & ~StaticProperty.REVERSE_DOCUMENT_ORDER) | StaticProperty.ORDERED_NODESET;
            }
            else if ((baseProps & StaticProperty.ORDERED_NODESET) != 0)
            {
                return (baseProps & ~StaticProperty.ORDERED_NODESET) | StaticProperty.REVERSE_DOCUMENT_ORDER;
            }
            else
            {
                return baseProps;
            }
        }

        public static ISequenceIterator GetReverseIterator(ISequenceIterator forwards)
        {
            if (forwards is IReversibleIterator)
            {
                return ((IReversibleIterator)forwards).GetReverseIterator();
            }
            else
            {
                return MaterializeReversed(forwards);
            }
        }

        // Materialise for reversal. A same-tree node stream is kept as a bare node-number array:
        // an int[] holds no references (the GC never scans it) and the wrappers stay gen0-short-lived,
        // where a List<IItem> retaining ~300k wrappers makes every gen0 collection walk the list and
        // puts the pointer array itself on the LOH — the dominant cost of reverse() on big inputs.
        // A stored sequence's iterator does not look at the clock, so one item in 1024 of the copy does: fold-right over
        // a stored sequence spent most of its time here.
        private static ISequenceIterator MaterializeReversed(ISequenceIterator forwards)
        {
            IItem item = forwards.Next();
            if (item is TinyNodeImpl first && !(first is TinyAttributeImpl))
            {
                TinyTree tree = first.tree;
                int[] nrs = new int[64];
                nrs[0] = first.nodeNr;
                int n = 1;
                while ((item = forwards.Next()) != null)
                {
                    // attribute/namespace wrappers and virtual textual-element children don't
                    // round-trip through TinyTree.GetNode(nr); mixed trees need real wrappers
                    if (!(item is TinyNodeImpl tn) || tn is TinyAttributeImpl || !ReferenceEquals(tn.tree, tree))
                        break;

                    if (n == nrs.Length)
                        Array.Resize(ref nrs, n << 1);

                    nrs[n++] = tn.nodeNr;
                    if ((n & 1023) == 0)
                    {
                        Core.Controller.CheckActiveTimeoutNow();
                    }
                }

                if (item == null)
                {
                    return new ReverseNodeNumberIterator(tree, nrs, n);
                }

                IList<IItem> mixed = new List<IItem>(n + 20);
                for (int i = 0; i < n; i++)
                {
                    mixed.Add(tree.GetNode(nrs[i]));
                }

                mixed.Add(item);
                while ((item = forwards.Next()) != null)
                {
                    mixed.Add(item);
                    if ((mixed.Count & 1023) == 0)
                    {
                        if (mixed.Count == Core.MemoryLedger.BigCount)
                        {
                            Core.MemoryLedger.Hold(mixed, Core.MemoryLedger.ItemListSizer);   // summed as it grows
                        }

                        Core.Controller.CheckActiveTimeoutNow();
                    }
                }

                return new ReverseListIterator(mixed);
            }

            IList<IItem> list = new List<IItem>(20);
            while (item != null)
            {
                list.Add(item);
                if ((list.Count & 1023) == 0)
                {
                    if (list.Count == Core.MemoryLedger.BigCount)
                    {
                        Core.MemoryLedger.Hold(list, Core.MemoryLedger.ItemListSizer);   // summed as it grows
                    }

                    Core.Controller.CheckActiveTimeoutNow();
                }

                item = forwards.Next();
            }

            return new ReverseListIterator(list);
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            // Java-faithful: getReverseIterator() reverses a ReversibleIterator (e.g. a grounded
            // ListIterator) as a VIEW, without materialising the input. The old code copied every item
            // into a fresh SequenceExtent unless the argument was already exactly a SequenceExtent, so
            // reverse(groundedSeq) was O(N) allocation (e.g. count(reverse($E)) walked and copied all of
            // $E). Byte-identical: same items, reverse order. The SequenceExtent case still no-copies —
            // its Iterate() yields a reversible ListIterator that GetReverseIterator handles directly.
            return SequenceTool.ToLazySequence(GetReverseIterator(arguments[0].Iterate()));
        }

        public override Expression MakeOptimizedFunctionCall(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo, params Expression[] arguments)
        {

            // When reverse() has only a zero-or-one argument, there is no need to reverse
            // This often occurs in reverse-axis steps
            if (arguments[0].GetCardinality() == StaticProperty.ALLOWS_ZERO_OR_ONE)
            {
                return arguments[0];
            }

            return base.MakeOptimizedFunctionCall(visitor, contextInfo, arguments);
        }

        public static ISequenceIterator ReverseIterator<T>(IList<T> list)
        {
            // When the backing list is already IList<IItem> (node/atomic sequences — the common case),
            // wrap it by reference and iterate backwards: no copy. The Select(...).ToList() bridge only
            // remains for the exotic unconstrained-T lists the transpiler left (Java had T extends Item).
            // The prior unconditional copy made reverse(groundedSeq) O(N) allocation even though
            // ReverseListIterator only ever reads list[--pos]. Byte-identical (same items, reverse order).
            if (list is IList<IItem> il)
            {
                return new ReverseListIterator(il);
            }

            return new ReverseListIterator(Enumerable.ToList(Enumerable.Select(list, x => (IItem)(object)x)));
        }

        internal sealed class ReverseListIterator : ISequenceIterator, ILastPositionFinder, IReversibleIterator
        {
            private int pos;
            private readonly IList<IItem> list;
            public ReverseListIterator(IList<IItem> list)
            {
                this.list = list;
                this.pos = list.Count;
            }

            public bool SupportsGetLength()
            {
                return true;
            }

            public int GetLength()
            {
                return list.Count;
            }

            public IItem Next()
            {
                return pos > 0 ? list[--pos] : null;
            }

            public ISequenceIterator GetReverseIterator()
            {
                return new ListIterator.Of<IItem>(list);
            }
            public void Dispose() { }
        }

        // Reversed view over a same-tree node-number extent; interface surface mirrors
        // ReverseListIterator (length + reversal), wrappers are created per Next() call.
        internal sealed class ReverseNodeNumberIterator : ISequenceIterator, ILastPositionFinder, IReversibleIterator
        {
            private readonly TinyTree tree;
            private readonly int[] nrs;
            private readonly int count;
            private int pos;

            internal ReverseNodeNumberIterator(TinyTree tree, int[] nrs, int count)
            {
                this.tree = tree;
                this.nrs = nrs;
                this.count = count;
                this.pos = count;
            }

            public bool SupportsGetLength()
            {
                return true;
            }

            public int GetLength()
            {
                return count;
            }

            public IItem Next()
            {
                return pos > 0 ? tree.GetNode(nrs[--pos]) : null;
            }

            public ISequenceIterator GetReverseIterator()
            {
                return new NodeNumberIterator(tree, nrs, count);
            }

            public void Dispose() { }
        }

        // Forward twin of ReverseNodeNumberIterator (produced by re-reversing).
        internal sealed class NodeNumberIterator : ISequenceIterator, ILastPositionFinder, IReversibleIterator
        {
            private readonly TinyTree tree;
            private readonly int[] nrs;
            private readonly int count;
            private int pos;

            internal NodeNumberIterator(TinyTree tree, int[] nrs, int count)
            {
                this.tree = tree;
                this.nrs = nrs;
                this.count = count;
            }

            public bool SupportsGetLength()
            {
                return true;
            }

            public int GetLength()
            {
                return count;
            }

            public IItem Next()
            {
                return pos < count ? tree.GetNode(nrs[pos++]) : null;
            }

            public ISequenceIterator GetReverseIterator()
            {
                return new ReverseNodeNumberIterator(tree, nrs, count);
            }

            public void Dispose() { }
        }
    }
}

