////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// The XPath 2.0 distinct-values() function, with the collation argument already known
    /// </summary>
    internal sealed class DistinctValues : CollatingFunctionFixed
    {
        public static readonly IAtomicMatchKey NaN_MATCH_KEY = new QNameValue("", NamespaceUri.SAXON, "+NaN+");
        public override string StreamerName => "DistinctValues";

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            IStringCollator collator = StringCollator;
            return new LazySequence(new DistinctIterator(arguments[0].Iterate(), collator, context));
        }

        public override Elaborator GetElaborator()
        {
            return new DistinctValuesFnElaborator();
        }

        // Fused distinct-prefix for `distinct-values(ROWS/child::FIELD ! substring(., 1, K))` with
        // codepoint collation over an untyped Tiny source: rows and fields walk the Tiny arrays and
        // the K-codepoint prefix dedups through a span hash table - no field node objects, no
        // per-row iterators, a value allocated only per DISTINCT prefix. Any other shape, or an
        // off-path start at runtime, runs the generic pipeline unchanged.
        internal sealed class DistinctValuesFnElaborator : Expressions.Elaboration.PullElaborator
        {
            private static Expression Unwrap(Expression e)
            {
                while (true)
                {
                    if (e is HomogeneityChecker hc)
                    {
                        e = hc.BaseExpression;
                    }
                    else if (e is DocumentSorter ds)
                    {
                        e = ds.BaseExpression;
                    }
                    else
                    {
                        return e;
                    }
                }
            }

            private static bool MatchChildStep(Expression step, out int fp)
            {
                fp = -1;
                if (!(step is AxisExpression axis) || axis.Axis != AxisInfo.CHILD)
                {
                    return false;
                }

                Patterns.NodeTest t = axis.GetNodeTest();
                if (t is Patterns.NameTest nt && nt.PrimitiveType == Types.Type.ELEMENT)
                {
                    fp = nt.Fingerprint;
                    return true;
                }

                return t is Patterns.NodeKindTest nk && nk.GetNodeKind() == Types.Type.ELEMENT;
            }

            private static bool MatchSubstringPrefix(Expression action, out int k)
            {
                k = 0;
                if (!(action is SystemFunctionCall sfc) || !(sfc.TargetFunction is Substring) || sfc.GetArity() != 3)
                {
                    return false;
                }

                Expression a0 = Expressions.Elaboration.TransparentWrappers.Unwrap(sfc.GetArg(0),
                    Expressions.Elaboration.Peel.Converter | Expressions.Elaboration.Peel.Atomizer
                    | Expressions.Elaboration.Peel.SingletonAtomizer);
                if (!(a0 is ContextItemExpression))
                {
                    return false;
                }

                if (!(sfc.GetArg(1) is Literal l1) || !(l1.GroundedValue is Int64Value v1) || v1.LongValue() != 1)
                {
                    return false;
                }

                if (!(sfc.GetArg(2) is Literal l2) || !(l2.GroundedValue is Int64Value v2))
                {
                    return false;
                }

                long len = v2.LongValue();
                if (len < 1 || len > 1_000_000)
                {
                    return false;
                }

                k = (int)len;
                return true;
            }

            public override Expressions.Elaboration.IPullEvaluator ElaborateForPull()
            {
                SystemFunctionCall fnc = (SystemFunctionCall)GetExpression();
                DistinctValues fn = (DistinctValues)fnc.TargetFunction;
                Expressions.Elaboration.IPullEvaluator Generic()
                {
                    var g = new SystemFunctionCall.SystemFunctionCallElaborator();
                    g.SetExpression(fnc);
                    return g.ElaborateForPull();
                }

                if (fnc.GetArity() == 1 && fn.StringCollator is CodepointCollator && fnc.GetArg(0) is Atomizer atom
                    && atom.BaseExpression.GetItemType() is Patterns.NodeTest test
                    && test.PrimitiveType != Types.Type.ATTRIBUTE)   // an attribute's value is a string, not a span: nothing to read in place
                {
                    Expressions.Elaboration.IPullEvaluator nodes = atom.BaseExpression.MakeElaborator().ElaborateForPull();
                    int fp = Trees.Tiny.TinyText.DescendantName(atom.BaseExpression);
                    return (context) => fp >= 0 && context.GetContextItem() is Trees.Tiny.TinyParentNodeImpl top && top.tree.TypeArray == null
                        ? new NodeDistinctIterator(top.tree, top.nodeNr, fp, fn.StringCollator, context)
                        : new NodeDistinctIterator(nodes.Iterate(context), fn.StringCollator, context);
                }

                if (fnc.GetArity() != 1 || !(fn.StringCollator is CodepointCollator)
                    || !(Unwrap(fnc.GetArg(0)) is Expressions.Instructions.ForEach fe)
                    || !MatchSubstringPrefix(fe.GetActionExpression(), out int prefixLen)
                    || !(Unwrap(fe.GetSelectExpression()) is SlashExpression fieldSlash)
                    || !MatchChildStep(Unwrap(fieldSlash.GetStep()), out int fieldFp) || fieldFp < 0
                    || !(Unwrap(fieldSlash.Start) is SlashExpression rowSlash)
                    || !MatchChildStep(Unwrap(rowSlash.GetStep()), out int rowFp))
                {
                    return Generic();
                }

                Expressions.Elaboration.IPullEvaluator startPull = rowSlash.Start.MakeElaborator().ElaborateForPull();
                Expressions.Elaboration.IPullEvaluator fallback = Generic();
                return (context) =>
                {
                    ISequenceIterator si = startPull.Iterate(context);
                    IItem p1 = si.Next();
                    if (p1 is Trees.Tiny.TinyParentNodeImpl top && top.tree.TypeArray == null && si.Next() == null)
                    {
                        return new TinyDistinctPrefixIterator(top, rowFp, fieldFp, prefixLen);
                    }

                    return fallback.Iterate(context);
                };
            }
        }

        // Serves the distinct K-prefixes in first-occurrence order, built eagerly (the population
        // is bounded by the distinct count, not the row count). The table is the fused group-index
        // builder's (SpanKeyTable); a field off the span path (16-bit text, cross-segment, mixed
        // content, whitespace-packed) is read via GetStringValue and cut with codepoint Substring.
        internal sealed class TinyDistinctPrefixIterator : ISequenceIterator
        {
            private readonly List<AtomicValue> values = new List<AtomicValue>();
            private static readonly Func<object, long> Sizer = o => 48 + 112L * ((TinyDistinctPrefixIterator)o).values.Count;   // a value, its key and a slot each
            private int pos;

            internal TinyDistinctPrefixIterator(Trees.Tiny.TinyParentNodeImpl top, int rowFp, int fieldFp, int prefixLen)
            {
                Trees.Tiny.TinyTree tree = top.tree;
                int row = Trees.Tiny.TinyText.FirstChild(tree, top.nodeNr);
                if (row < 0)
                {
                    return;
                }

                byte[] kinds = tree.nodeKind;
                int[] nameCodes = tree.nameCode;
                var seen = new Trees.Tiny.SpanKeyTable<bool>();
                int steps = 0;
                while (true)
                {
                    Core.Controller.CheckActiveTimeoutEvery64K(steps++);
                    if (kinds[row] == Types.Type.ELEMENT && (rowFp < 0 || (nameCodes[row] & NamePool.FP_MASK) == rowFp))
                    {
                        int n = Trees.Tiny.TinyText.FirstChild(tree, row);
                        for (int cur; (cur = Trees.Tiny.TinyText.NextChildNamed(tree, fieldFp, ref n)) >= 0;)
                        {
                            // the prefix's bytes in place when the field is one Latin-1 span, else its string, cut
                            int h;
                            int slot;
                            Text.UnicodeString genericKey = null;
                            if (Trees.Tiny.TinyText.TrySpan(tree, cur, prefixLen, out byte[] b, out int off, out int len))
                            {
                                h = Trees.Tiny.SpanKeyTable<bool>.Hash(b, off, len);
                                slot = seen.Find(b, off, len, h);
                            }
                            else
                            {
                                genericKey = Trees.Tiny.TinyParentNodeImpl.GetStringValue(tree, cur);
                                if (genericKey.Length32() > prefixLen)
                                {
                                    genericKey = genericKey.Substring(0, prefixLen);
                                }

                                h = genericKey.GetHashCode();
                                slot = seen.Find(genericKey, h);
                            }

                            if (slot < 0)
                            {
                                Text.UnicodeString key = genericKey ?? new Text.Slice8(b, off, off + len);
                                seen.Add(~slot, h, key, true);
                                values.Add(new StringValue(key));
                                if (values.Count == Core.MemoryLedger.BigCount)
                                {
                                    Core.MemoryLedger.Hold(this, Sizer);   // summed as it grows
                                }
                            }
                        }
                    }

                    int r2 = tree.next[row];
                    if (r2 <= row)
                    {
                        break;
                    }

                    row = r2;
                }
            }

            public IItem Next()
            {
                return pos < values.Count ? values[pos++] : null;
            }

            public void Dispose() { }
        }

        /// <summary>
        /// IIterator class to return the distinct values in a sequence
        /// </summary>
        // distinct-values(data(NODES)) under the codepoint collation: the text of a node of an untyped Tiny tree is looked up
        // by its bytes in a table of its own, and a value is made only for a text not seen before; descendant::NAME of a
        // Tiny node is walked by node number. The first item off that path moves the texts seen into the generic lookup,
        // which serves everything from there on.
        internal sealed class NodeDistinctIterator : ISequenceIterator
        {
            private readonly ISequenceIterator nodes;   // null: the descendants named fp of node `from`, by number
            private readonly Trees.Tiny.TinyTree tree;
            private readonly int from;
            private readonly int fp;
            private int n;
            private readonly IStringCollator collator;
            private readonly IXPathContext context;
            private Trees.Tiny.SpanKeyTable<bool> table = new Trees.Tiny.SpanKeyTable<bool>();   // null once handed over
            private DistinctIterator generic;
            private int steps;
            private static readonly Func<object, long> Sizer = o => 48 + 72L * (((NodeDistinctIterator)o).table?.Count ?? 0);

            internal NodeDistinctIterator(ISequenceIterator nodes, IStringCollator collator, IXPathContext context)
            {
                this.nodes = nodes;
                this.collator = collator;
                this.context = context;
            }

            internal NodeDistinctIterator(Trees.Tiny.TinyTree tree, int from, int fp, IStringCollator collator, IXPathContext context)
            {
                this.tree = tree;
                this.from = from;
                this.fp = fp;
                n = from;
                this.collator = collator;
                this.context = context;
            }

            public IItem Next()
            {
                if (generic != null)
                {
                    return generic.Next();
                }

                while (true)
                {
                    byte[] b;
                    int off;
                    int len;
                    if (nodes == null)
                    {
                        int cur = Trees.Tiny.TinyText.NextDescendant(tree, from, fp, ref n);
                        if (cur < 0)
                        {
                            return null;
                        }

                        if (!Trees.Tiny.TinyText.TrySpan(tree, cur, out b, out off, out len))
                        {
                            IItem node = tree.GetNode(cur);
                            if (!Latin1Text(node, out b, out off, out len))
                            {
                                return HandOver(node);
                            }
                        }
                    }
                    else
                    {
                        IItem item = nodes.Next();
                        if (item == null)
                        {
                            return null;
                        }

                        if (!Trees.Tiny.TinyText.TrySpan(item, out b, out off, out len) && !Latin1Text(item, out b, out off, out len))
                        {
                            return HandOver(item);
                        }
                    }

                    Core.Controller.CheckActiveTimeoutEvery64K(steps++);
                    int h = Trees.Tiny.SpanKeyTable<bool>.Hash(b, off, len);
                    int slot = table.Find(b, off, len, h);
                    if (slot >= 0)
                    {
                        continue;
                    }

                    var key = new Text.Slice8(b, off, off + len);
                    table.Add(~slot, h, key, true);
                    if (table.Count == Core.MemoryLedger.BigCount)
                    {
                        Core.MemoryLedger.Hold(this, Sizer);
                    }

                    return StringValue.MakeUntypedAtomic(key);
                }
            }

            // The rest goes the generic way: this item's values first, then the remaining nodes.
            private IItem HandOver(IItem item)
            {
                var seen = new HashSet<IAtomicMatchKey>();
                foreach (Text.UnicodeString k in table.Keys())
                {
                    seen.Add((Text.Slice8)k);
                }

                table = null;   // the generic lookup holds them now; the ledger sees an empty table

                ISequenceIterator values = Atomizer.GetAtomizingIterator(Trees.Iterators.SingletonIterator.MakeIterator(item), false);
                ISequenceIterator rest = nodes ?? new Descendants(tree, from, fp, n);
                generic = new DistinctIterator(new Then(values, rest), collator, context, seen);
                return generic.Next();
            }

            // A node of an untyped Tiny tree whose text is not one span (it crosses a buffer segment, or the element has
            // mixed content) but is all Latin1: its bytes, copied, so the lookup stays on the byte table. Only nodes that
            // atomize to xs:untypedAtomic: a comment or a PI is an xs:string, a typed attribute its type.
            private static bool Latin1Text(IItem item, out byte[] bytes, out int off, out int len)
            {
                bytes = null;
                off = 0;
                len = 0;
                if (!(item is Trees.Tiny.TinyNodeImpl node) || node.tree.TypeArray != null
                    || !(node is Trees.Tiny.TinyParentNodeImpl || node is Trees.Tiny.TinyTextImpl || node is Trees.Tiny.WhitespaceTextImpl
                        || (node is Trees.Tiny.TinyAttributeImpl && node.tree.attType == null)))
                {
                    return false;
                }

                Text.UnicodeString s = node.UnicodeStringValue;
                int count = s.Length32();
                var copy = new byte[count];
                for (int i = 0; i < count; i++)
                {
                    int c = s.CodePointAt(i);
                    if (c > 0xFF)
                    {
                        return false;
                    }

                    copy[i] = (byte)c;
                }

                bytes = copy;
                len = count;
                return true;
            }

            // The descendants still to come, as nodes.
            private sealed class Descendants : ISequenceIterator
            {
                private readonly Trees.Tiny.TinyTree tree;
                private readonly int from;
                private readonly int fp;
                private int n;

                internal Descendants(Trees.Tiny.TinyTree tree, int from, int fp, int n)
                {
                    this.tree = tree;
                    this.from = from;
                    this.fp = fp;
                    this.n = n;
                }

                public IItem Next()
                {
                    int cur = Trees.Tiny.TinyText.NextDescendant(tree, from, fp, ref n);
                    return cur < 0 ? null : tree.GetNode(cur);
                }

                public void Dispose()
                {
                }
            }

            // An item's atomized values, then those of the nodes still to come.
            // Each node atomized on its own: rest is part-read, and GetAtomizingIterator would take its length for what is left.
            private sealed class Then : ISequenceIterator
            {
                private ISequenceIterator values;
                private readonly ISequenceIterator rest;

                internal Then(ISequenceIterator first, ISequenceIterator rest)
                {
                    values = first;
                    this.rest = rest;
                }

                public IItem Next()
                {
                    while (true)
                    {
                        IItem v = values?.Next();
                        if (v != null)
                        {
                            return v;
                        }

                        IItem node = rest.Next();
                        if (node == null)
                        {
                            values = null;
                            return null;
                        }

                        // Most nodes have one value: no iterator for it.
                        Model.IAtomicSequence atomized = node.Atomize();
                        if (atomized is AtomicValue one)
                        {
                            values = null;
                            return one;
                        }

                        values = atomized.Iterate();
                    }
                }

                public void Dispose()
                {
                    rest.Dispose();
                }
            }

            public void Dispose()
            {
                nodes?.Dispose();
            }
        }

        internal sealed class DistinctIterator : ISequenceIterator
        {
            private readonly ISequenceIterator @base;
            private readonly IStringCollator collator;
            private readonly IXPathContext context;
            private readonly HashSet<IAtomicMatchKey> lookup;
            private static readonly Func<object, long> LookupSizer = o => 48 + 112L * ((HashSet<IAtomicMatchKey>)o).Count;   // a slot and a key each

            // The keys' strings as well: 8100 distinct strings of 32 000 chars held 736 MB under 100 MB at 112 B a key.
            private long weight;   // of medium strings (MemoryLedger.Weighs)
            private long keyBytes;
            private static readonly Func<object, long> KeysSizer = o => 48 + 112L * ((DistinctIterator)o).lookup.Count + ((DistinctIterator)o).keyBytes;
            private readonly IAction onDuplicates;
            private bool held;
            public DistinctIterator(ISequenceIterator @base, IStringCollator collator, IXPathContext context)
                : this(@base, collator, context, new HashSet<IAtomicMatchKey>())
            {
            }

            // seen: the keys already served, by a fused lookup that hands the rest over.
            internal DistinctIterator(ISequenceIterator @base, IStringCollator collator, IXPathContext context, HashSet<IAtomicMatchKey> seen)
            {
                lookup = seen;
                if (seen.Count >= Core.MemoryLedger.BigCount)
                {
                    Core.MemoryLedger.Hold(lookup, LookupSizer);
                    held = true;
                }

                this.@base = @base;
                this.collator = collator;
                this.context = context;
            }

            public AtomicValue Next()
            {
                int implicitTimezone = context.GetImplicitTimezone();
                while (true)
                {
                    AtomicValue nextBase = (AtomicValue)@base.Next();
                    if (nextBase == null)
                    {
                        return null;
                    }

                    IAtomicMatchKey key;
                    if (nextBase.IsNaN())
                    {
                        key = NaN_MATCH_KEY;
                    }
                    else
                    {
                        try
                        {
                            key = nextBase.GetXPathMatchKey(collator, implicitTimezone);
                        }
                        catch (NoDynamicContextException e)
                        {
                            throw new UncheckedXPathException(e);
                        }
                    }

                    if (lookup.Add(key))
                    {
                        keyBytes += Core.MemoryLedger.MemberBytes(nextBase);
                        if (!held && (lookup.Count >= Core.MemoryLedger.BigCount || Core.MemoryLedger.Weighs(nextBase, ref weight)))
                        {
                            Core.MemoryLedger.Hold(this, KeysSizer);   // summed as it grows
                            held = true;
                        }


                        // returns true if newly added (if not, keep looking)
                        return nextBase;
                    }
                    else if (onDuplicates != null)
                    {
                        try
                        {
                            onDuplicates.DoAction();
                        }
                        catch (XPathException e)
                        {

                            // should not happen
                            throw new UncheckedXPathException(e);
                        }
                    }
                }
            }

            public void Dispose()
            {
                @base.Dispose();
            }
            IItem ISequenceIterator.Next() => Next();
        }
    }
}
