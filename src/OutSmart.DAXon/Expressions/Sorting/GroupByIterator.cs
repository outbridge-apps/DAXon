////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Trees.Tiny;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Expressions.Sorting
{
    internal sealed class GroupByIterator : IGroupIterator, ILastPositionFinder, ILookaheadIterator
    {
        private readonly object syncLock = new object();
        private readonly ISequenceIterator population;
        protected Expression keyExpression;
        private readonly IStringCollator collator;
        private readonly IXPathContext keyContext;
        private int position;
        protected IList<IList<IItem>> groups = new List<IList<IItem>>(40);

        // Many groups: the iterator is entered in the ledger of the call grouping, which sums them as they grow.
        private void GroupAdded()
        {
            if (groups.Count == Core.MemoryLedger.BigCount && membersSeen < Core.MemoryLedger.BigCount)
            {
                Core.MemoryLedger.Hold(this, GroupsSizer);
            }
        }

        // Many members, however few groups hold them: one group of 4 000 000 strings held 750 MB under a 100 MB limit.
        private int membersSeen;

        private void MemberSeen()
        {
            if (++membersSeen == Core.MemoryLedger.BigCount && groups.Count < Core.MemoryLedger.BigCount)
            {
                Core.MemoryLedger.Hold(this, GroupsSizer);
            }
        }

        // A group, its key and its slot, and the members held (a few, spread over the groups, tell their size).
        private static readonly Func<object, long> GroupsSizer = o =>
        {
            var it = (GroupByIterator)o;
            int n = it.groups.Count;
            var probes = new object[Core.MemoryLedger.Probes];
            int taken = 0;
            for (int i = 0; i < n && taken < probes.Length; i += Math.Max(1, n / probes.Length))
            {
                IList<IItem> g = it.groups[i];
                if (!(g is TinyGrp) && g.Count > 0)
                {
                    probes[taken++] = g[g.Count / 2];
                }
            }

            return 48 + 200L * n + 8L * it.membersSeen + Core.MemoryLedger.ProbedBytes(probes, taken, it.membersSeen);
        };

        protected IList<IAtomicSequence> groupKeys = new List<IAtomicSequence>(40);
        protected bool composite;

        public bool HasNext => position < groups.Count;
        public GroupByIterator(ISequenceIterator population, Expression keyExpression, IXPathContext keyContext, IStringCollator collator, bool composite)
        {
            this.population = population;
            this.keyExpression = keyExpression;
            this.keyContext = keyContext;
            this.collator = collator;
            this.composite = composite;
            if (composite)
            {
                BuildIndexedGroupsComposite();
            }
            else
            {
                BuildIndexedGroups();
            }
        }

        // Fused build for the tiny group-by shape (single Tiny parent population via a child axis,
        // fused child key, codepoint collation): rows are walked by node number (no node objects),
        // keys are hashed straight off the text-buffer bytes, and group members are node numbers
        // materialized on demand — allocations scale with the number of GROUPS, not the population.
        // Off-span keys (16/24-bit text, mixed content, segment splits) take the generic string
        // route inside the same loop, so grouping results are identical either way.
        // The rows are the children of parent, or with descendants the elements named rowFp below it.
        internal GroupByIterator(TinyParentNodeImpl parent, int rowFp, int keyFp, bool descendants, Expression keyExpression, IXPathContext keyContext, IStringCollator collator)
        {
            this.population = null;
            this.keyExpression = keyExpression;
            this.keyContext = keyContext;
            this.collator = collator;
            this.composite = false;
            BuildTinyGroups(parent, rowFp, keyFp, descendants);
        }

        private void BuildTinyGroups(TinyParentNodeImpl parent, int rowFp, int keyFp, bool descendants)
        {
            TinyTree tree = parent.tree;
            int p = parent.nodeNr;
            int walk = p;
            int row = descendants ? TinyText.NextDescendant(tree, p, rowFp, ref walk) : TinyText.FirstChild(tree, p);
            if (row < 0)
            {
                return;
            }

            byte[] kinds = tree.nodeKind;
            int[] nameCodes = tree.nameCode;
            var table = new SpanKeyTable<TinyGrp>();
            int steps = 0;
            while (true)
            {
                Core.Controller.CheckActiveTimeoutEvery64K(steps++);
                // textual-element rows can hold no element children, hence no keys and no group
                if (kinds[row] == Types.Type.ELEMENT && (rowFp < 0 || (nameCodes[row] & NamePool.FP_MASK) == rowFp))
                {
                    int n = TinyText.FirstChild(tree, row);
                    for (int cur; (cur = TinyText.NextChildNamed(tree, keyFp, ref n)) >= 0;)
                    {
                        // the key's bytes in place when it is one Latin-1 span, else its string
                        int h;
                        int slot;
                        UnicodeString genericKey = null;
                        if (TinyText.TrySpan(tree, cur, out byte[] b, out int off, out int len))
                        {
                            h = SpanKeyTable<TinyGrp>.Hash(b, off, len);
                            slot = table.Find(b, off, len, h);
                        }
                        else
                        {
                            genericKey = TinyParentNodeImpl.GetStringValue(tree, cur);
                            h = genericKey.GetHashCode();
                            slot = table.Find(genericKey, h);
                        }

                        if (slot < 0)
                        {
                            UnicodeString keyString = genericKey ?? new Slice8(b, off, off + len);
                            var grp = new TinyGrp(tree, row);
                            groups.Add(grp);
                            GroupAdded();
                            groupKeys.Add(StringValue.MakeUntypedAtomic(keyString));
                            table.Add(~slot, h, keyString, grp);
                        }
                        else if (table[slot].LastNode != row)
                        {
                            table[slot].AddNode(row);
                        }
                    }
                }

                int r2 = descendants ? TinyText.NextDescendant(tree, p, rowFp, ref walk) : tree.next[row];
                if (r2 <= row)
                {
                    break;
                }

                row = r2;
            }
        }

        // Group members held as node numbers; nodes materialize on access (the group consumers —
        // current-group() iteration, count(), the body's context item — each touch a member once).
        private sealed class TinyGrp : IList<IItem>
        {
            private readonly TinyTree tree;
            private int[] nodes;
            private int count;

            internal TinyGrp(TinyTree tree, int first)
            {
                this.tree = tree;
                nodes = new int[4];
                nodes[0] = first;
                count = 1;
            }

            internal int LastNode => nodes[count - 1];

            internal void AddNode(int nr)
            {
                if (count == nodes.Length)
                {
                    Array.Resize(ref nodes, count * 2);
                }

                nodes[count++] = nr;
            }

            public int Count => count;
            public bool IsReadOnly => true;

            public IItem this[int index]
            {
                get
                {
                    if (index < 0 || index >= count)
                    {
                        throw new ArgumentOutOfRangeException(nameof(index));
                    }

                    return tree.GetNode(nodes[index]);
                }
                set => throw new NotSupportedException();
            }

            public IEnumerator<IItem> GetEnumerator()
            {
                for (int i = 0; i < count; i++)
                {
                    yield return tree.GetNode(nodes[i]);
                }
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();

            public int IndexOf(IItem item)
            {
                for (int i = 0; i < count; i++)
                {
                    if (Equals(tree.GetNode(nodes[i]), item))
                    {
                        return i;
                    }
                }

                return -1;
            }

            public bool Contains(IItem item) => IndexOf(item) >= 0;

            public void CopyTo(IItem[] array, int arrayIndex)
            {
                for (int i = 0; i < count; i++)
                {
                    array[arrayIndex + i] = tree.GetNode(nodes[i]);
                }
            }

            public void Add(IItem item) => throw new NotSupportedException();
            public void Insert(int index, IItem item) => throw new NotSupportedException();
            public void RemoveAt(int index) => throw new NotSupportedException();
            public bool Remove(IItem item) => throw new NotSupportedException();
            public void Clear() => throw new NotSupportedException();
        }

        private void BuildIndexedGroups()
        {
            Dictionary<IAtomicMatchKey, IList<IItem>> index = new Dictionary<IAtomicMatchKey, IList<IItem>>(40);
            IXPathContext c2 = keyContext.NewMinorContext();
            IFocusIterator focus = c2.TrackFocus(population);
            int implicitTimezone = c2.GetImplicitTimezone();
            // Fast path for group-by="childName": the key atomize(child::NAME) otherwise allocates an
            // Atomizer + Axis iterator and runs the atomize pipeline for every item. When the item is
            // an untyped Tiny node the fused iterator reads the matching children's string values
            // straight from the Tiny arrays as xs:untypedAtomic — byte-identical, no pipeline.
            int fusedFp = FusedChildAtomizer.MatchAtomizer(keyExpression, out int ffp) ? ffp : -1;
            FusedChildAtomizer.ChildUntypedIterator fusedIter = fusedFp >= 0 ? new FusedChildAtomizer.ChildUntypedIterator() : null;

            // The key evaluator is per-population constant, so it is built once here —
            // Expression.Iterate would re-elaborate the whole evaluator chain on every item.
            // A statically singleton key also skips the per-item iterator.
            Elaborator keyElab = keyExpression.MakeElaborator();
            IItemEvaluator keyItem = !Cardinality.AllowsMany(keyExpression.GetCardinality()) ? keyElab.ElaborateForItem() : null;
            IPullEvaluator keyPull = keyItem == null ? keyElab.ElaborateForPull() : null;
            IItem item;
            while ((item = focus.Next()) != null)
            {
                Core.Controller.CheckActiveTimeout();
                MemberSeen();
                ISequenceIterator keys;
                if (fusedIter != null && item is TinyParentNodeImpl tp && tp.tree.TypeArray == null)
                {
                    fusedIter.Reset(tp, fusedFp);   // reuse one iterator across all items (fully drained each time)
                    keys = fusedIter;
                }
                else if (keyItem != null)
                {
                    AtomicValue single = (AtomicValue)keyItem(c2);
                    if (single != null)
                    {
                        ProcessKey(index, single, item, true, implicitTimezone);
                    }

                    continue;
                }
                else
                {
                    keys = keyPull.Iterate(c2);
                }

                bool firstKey = true;
                while (true)
                {
                    AtomicValue key = (AtomicValue)keys.Next();
                    if (key == null)
                    {
                        break;
                    }

                    ProcessKey(index, key, item, firstKey, implicitTimezone);
                    firstKey = false;
                }
            }
        }

        private void ProcessKey(Dictionary<IAtomicMatchKey, IList<IItem>> index, AtomicValue key, IItem item, bool firstKey, int implicitTimezone)
        {
            IAtomicMatchKey comparisonKey;
            if (key.IsNaN())
            {
                comparisonKey = DistinctValues.NaN_MATCH_KEY;
            }
            else
            {
                comparisonKey = key.GetXPathMatchKey(collator, implicitTimezone);
            }

            IList<IItem> g = index.GetOrDefault(comparisonKey);
            if (g == null)
            {
                IList<IItem> newGroup = new Grp(item);
                groups.Add(newGroup);
                GroupAdded();
                groupKeys.Add(key);
                index[comparisonKey] = newGroup;
            }
            else
            {
                if (firstKey)
                {
                    g.Add(item);
                }
                else
                {

                    // if this is not the first key value for this item, we
                    // check whether the item is already in this group before
                    // adding it again. If it is in this group, then we know
                    // it will be at the end.
                    if (g[g.Count - 1] != item)
                    {
                        g.Add(item);
                    }
                }
            }
        }

        private void BuildIndexedGroupsComposite()
        {
            Dictionary<CompositeAtomicKey, IList<IItem>> index = new Dictionary<CompositeAtomicKey, IList<IItem>>(40);
            IXPathContext c2 = keyContext.NewMinorContext();
            IFocusIterator focus = c2.TrackFocus(population);
            int implicitTimezone = c2.GetImplicitTimezone();
            IPullEvaluator keyPull = keyExpression.MakeElaborator().ElaborateForPull();   // per-population constant
            IItem item;
            while ((item = focus.Next()) != null)
            {
                Core.Controller.CheckActiveTimeout();
                MemberSeen();
                ISequenceIterator keys = keyPull.Iterate(c2);
                IList<IAtomicMatchKey> ckList = new List<IAtomicMatchKey>();
                IList<AtomicValue> compositeKey = new List<AtomicValue>();
                while (true)
                {
                    AtomicValue key = (AtomicValue)keys.Next();
                    if (key == null)
                    {
                        break;
                    }

                    compositeKey.Add(key);
                    IAtomicMatchKey comparisonKey;
                    if (key.IsNaN())
                    {
                        comparisonKey = DistinctValues.NaN_MATCH_KEY;
                    }
                    else
                    {
                        comparisonKey = key.GetXPathMatchKey(collator, implicitTimezone);
                    }

                    ckList.Add(comparisonKey);
                }

                CompositeAtomicKey cak = new CompositeAtomicKey(ckList);
                IList<IItem> g = index.GetOrDefault(cak);
                if (g == null)
                {
                    IList<IItem> newGroup = new Grp(item);
                    groups.Add(newGroup);
                    GroupAdded();
                    groupKeys.Add(new AtomicArray(compositeKey));
                    index[cak] = newGroup;
                }
                else
                {
                    g.Add(item);
                }
            }
        }

        public IAtomicSequence GetCurrentGroupingKey()
        {
            lock (syncLock)
            {
                IAtomicSequence val = groupKeys[position - 1];
                if (val == null)
                {
                    return EmptyAtomicSequence.GetInstance();
                }
                else
                {
                    return val;
                }
            }
        }

        public IGroundedValue CurrentGroup()
        {
            return SequenceExtent.MakeSequenceExtent(groups[position - 1]);
        }

        public bool SupportsHasNext()
        {
            return true;
        }

        public IItem Next()
        {
            if (position >= 0 && position < groups.Count)
            {
                position++;
                return Current();
            }
            else
            {
                position = -1;
                return null;
            }
        }

        private IItem Current()
        {
            if (position < 1)
            {
                return null;
            }


            // return the initial item of the current group
            return groups[position - 1][0];
        }

        public bool SupportsGetLength()
        {
            return true;
        }

        /// <summary>
        /// Get the last position (that is, the number of groups)
        /// </summary>
        public int GetLength()
        {
            return groups.Count;
        }
        public void Dispose() { }

        // A group always has at least one member, and the overwhelming majority of groups in a
        // large for-each-group are singletons. Grp stores the first member inline and allocates the
        // tail List only when a second member is actually added -- so a singleton group costs one
        // object instead of a List plus its backing array (~900K arrays saved on the csv_group
        // shape). It implements the IList<IItem> contract the group consumers use (Count, indexer,
        // enumeration, Add during build); structural mutation is never performed on a built group.
        private sealed class Grp : IList<IItem>
        {
            private readonly IItem first;
            private List<IItem> rest;   // null until a second member is added

            internal Grp(IItem first)
            {
                this.first = first;
            }

            public int Count => rest == null ? 1 : rest.Count + 1;
            public bool IsReadOnly => false;

            public IItem this[int index]
            {
                get => index == 0 ? first : rest[index - 1];
                set => throw new NotSupportedException();
            }

            public void Add(IItem item)
            {
                if (rest == null)
                {
                    rest = new List<IItem>(4);
                }

                rest.Add(item);
            }

            public IEnumerator<IItem> GetEnumerator()
            {
                yield return first;
                if (rest != null)
                {
                    foreach (IItem x in rest)
                    {
                        yield return x;
                    }
                }
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            public int IndexOf(IItem item)
            {
                if (Equals(first, item))
                {
                    return 0;
                }

                if (rest != null)
                {
                    int i = rest.IndexOf(item);
                    if (i >= 0)
                    {
                        return i + 1;
                    }
                }

                return -1;
            }

            public bool Contains(IItem item)
            {
                return IndexOf(item) >= 0;
            }

            public void CopyTo(IItem[] array, int arrayIndex)
            {
                array[arrayIndex++] = first;
                if (rest != null)
                {
                    rest.CopyTo(array, arrayIndex);
                }
            }

            public void Insert(int index, IItem item) => throw new NotSupportedException();
            public void RemoveAt(int index) => throw new NotSupportedException();
            public bool Remove(IItem item) => throw new NotSupportedException();
            public void Clear() => throw new NotSupportedException();
        }
    }
}
