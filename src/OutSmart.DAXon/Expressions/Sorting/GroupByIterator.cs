////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
//import com.saxonica.ee.stream.ManualGroupIterator;
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
        private int position = 0;
        protected IList<IList<IItem>> groups = new List<IList<IItem>>(40);

        // Many groups: the iterator is entered in the ledger of the call grouping, which sums them as they grow.
        private void GroupAdded()
        {
            if (groups.Count == Core.MemoryLedger.BigCount)
            {
                Core.MemoryLedger.Hold(this, GroupsSizer);
            }
        }

        private static readonly Func<object, long> GroupsSizer = o => 48 + 200L * ((GroupByIterator)o).groups.Count;   // a group, its key, its slot

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
        internal GroupByIterator(TinyParentNodeImpl parent, int rowFp, int keyFp, Expression keyExpression, IXPathContext keyContext, IStringCollator collator)
        {
            this.population = null;
            this.keyExpression = keyExpression;
            this.keyContext = keyContext;
            this.collator = collator;
            this.composite = false;
            BuildTinyGroups(parent, rowFp, keyFp);
        }

        private void BuildTinyGroups(TinyParentNodeImpl parent, int rowFp, int keyFp)
        {
            TinyTree tree = parent.tree;
            int p = parent.nodeNr;
            int firstChild = p + 1;
            if (firstChild >= tree.numberOfNodes || tree.depth[firstChild] != tree.depth[p] + 1)
            {
                return;
            }

            byte[] kinds = tree.nodeKind;
            int[] nextArr = tree.next;
            int[] nameCodes = tree.nameCode;
            int[] alphaArr = tree.alpha;
            int[] betaArr = tree.beta;
            LargeTextBuffer buffer = tree.textBuffer;

            // open-addressing key table: slot empty while tKey is null; hashes follow the
            // Slice8/UnicodeString algorithm (h = 31*h + codepoint) so span and string probes agree
            int cap = 128;
            int mask = cap - 1;
            int used = 0;
            int[] tHash = new int[cap];
            UnicodeString[] tKey = new UnicodeString[cap];
            TinyGrp[] tGroup = new TinyGrp[cap];

            int row = firstChild;
            while (true)
            {
                // textual-element rows can hold no element children, hence no keys and no group
                int rk = kinds[row];
                if (rk == Types.Type.ELEMENT && (rowFp < 0 || (nameCodes[row] & NamePool.FP_MASK) == rowFp))
                {
                    int c = row + 1;
                    if (c < tree.numberOfNodes && tree.depth[c] == tree.depth[row] + 1)
                    {
                        int n = c;
                        while (n >= 0)
                        {
                            int cur = n;
                            int n2 = nextArr[cur];
                            n = n2 > cur ? n2 : -1;
                            int ck = kinds[cur];
                            if ((ck == Types.Type.ELEMENT || ck == Types.Type.TEXTUAL_ELEMENT) && (nameCodes[cur] & NamePool.FP_MASK) == keyFp)
                            {
                                // contiguous Latin-1 span when the key is inline text or a single text child
                                int spanStart = -1;
                                int spanEnd = -1;
                                if (ck == Types.Type.TEXTUAL_ELEMENT)
                                {
                                    spanStart = alphaArr[cur];
                                    spanEnd = spanStart + betaArr[cur];
                                }
                                else
                                {
                                    int cc = cur + 1;
                                    if (cc < tree.numberOfNodes && tree.depth[cc] == tree.depth[cur] + 1
                                        && kinds[cc] == Types.Type.TEXT && nextArr[cc] <= cc)
                                    {
                                        spanStart = alphaArr[cc];
                                        spanEnd = spanStart + betaArr[cc];
                                    }
                                }

                                int h;
                                byte[] spanBytes = null;
                                int spanOff = 0;
                                int spanLen = 0;
                                UnicodeString genericKey = null;
                                if (spanStart >= 0 && buffer.TryGetByteSpan(spanStart, spanEnd, out spanBytes, out spanOff, out spanLen))
                                {
                                    h = 0;
                                    for (int i = 0; i < spanLen; i++)
                                    {
                                        h = 31 * h + (spanBytes[i + spanOff] & 0xff);
                                    }
                                }
                                else
                                {
                                    spanBytes = null;
                                    genericKey = TinyParentNodeImpl.GetStringValue(tree, cur);
                                    h = genericKey.GetHashCode();
                                }

                                int slot = h & mask;
                                TinyGrp grp = null;
                                while (tKey[slot] != null)
                                {
                                    if (tHash[slot] == h
                                        && (spanBytes != null
                                            ? (tKey[slot] is Slice8 s8 ? s8.ContentEqualsSpan(spanBytes, spanOff, spanLen)
                                                                       : new Slice8(spanBytes, spanOff, spanOff + spanLen).Equals(tKey[slot]))
                                            : genericKey.Equals(tKey[slot])))
                                    {
                                        grp = tGroup[slot];
                                        break;
                                    }

                                    slot = (slot + 1) & mask;
                                }

                                if (grp == null)
                                {
                                    UnicodeString keyString = spanBytes != null ? new Slice8(spanBytes, spanOff, spanOff + spanLen) : genericKey;
                                    grp = new TinyGrp(tree, row);
                                    groups.Add(grp);
                                    GroupAdded();
                                    groupKeys.Add(StringValue.MakeUntypedAtomic(keyString));
                                    tHash[slot] = h;
                                    tKey[slot] = keyString;
                                    tGroup[slot] = grp;
                                    if (++used * 3 > cap * 2)
                                    {
                                        int newCap = cap << 1;
                                        int newMask = newCap - 1;
                                        int[] nHash = new int[newCap];
                                        UnicodeString[] nKey = new UnicodeString[newCap];
                                        TinyGrp[] nGroup = new TinyGrp[newCap];
                                        for (int i = 0; i < cap; i++)
                                        {
                                            if (tKey[i] != null)
                                            {
                                                int s = tHash[i] & newMask;
                                                while (nKey[s] != null)
                                                {
                                                    s = (s + 1) & newMask;
                                                }

                                                nHash[s] = tHash[i];
                                                nKey[s] = tKey[i];
                                                nGroup[s] = tGroup[i];
                                            }
                                        }

                                        cap = newCap;
                                        mask = newMask;
                                        tHash = nHash;
                                        tKey = nKey;
                                        tGroup = nGroup;
                                    }
                                }
                                else if (grp.LastNode != row)
                                {
                                    grp.AddNode(row);
                                }
                            }
                        }
                    }
                }

                int r2 = nextArr[row];
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
                    // adding it again. If it @is in this group, then we know
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
        /// Get the last position (that @is, the number of groups)
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
