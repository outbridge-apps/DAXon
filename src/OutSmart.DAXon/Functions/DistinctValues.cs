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
        // is bounded by the distinct count, not the row count). Span probing and growth mirror the
        // fused group-index builder; a field off the span path (16-bit text, cross-segment, mixed
        // content, whitespace-packed) is read via GetStringValue and cut with codepoint Substring.
        internal sealed class TinyDistinctPrefixIterator : ISequenceIterator
        {
            private readonly List<AtomicValue> values = new List<AtomicValue>();
            private int pos;

            internal TinyDistinctPrefixIterator(Trees.Tiny.TinyParentNodeImpl top, int rowFp, int fieldFp, int prefixLen)
            {
                Trees.Tiny.TinyTree tree = top.tree;
                int p = top.nodeNr;
                int firstRow = p + 1;
                if (firstRow >= tree.numberOfNodes || tree.depth[firstRow] != tree.depth[p] + 1)
                {
                    return;
                }

                byte[] kinds = tree.nodeKind;
                int[] nextArr = tree.next;
                int[] nameCodes = tree.nameCode;
                int[] alphaArr = tree.alpha;
                int[] betaArr = tree.beta;
                Text.LargeTextBuffer buffer = tree.textBuffer;

                int cap = 128;
                int mask = cap - 1;
                int used = 0;
                int[] tHash = new int[cap];
                Text.UnicodeString[] tKey = new Text.UnicodeString[cap];

                int row = firstRow;
                while (true)
                {
                    if (kinds[row] == Types.Type.ELEMENT && (rowFp < 0 || (nameCodes[row] & NamePool.FP_MASK) == rowFp))
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
                                if ((ck == Types.Type.ELEMENT || ck == Types.Type.TEXTUAL_ELEMENT) && (nameCodes[cur] & NamePool.FP_MASK) == fieldFp)
                                {
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

                                    if (spanStart >= 0 && spanEnd - spanStart > prefixLen)
                                    {
                                        // 8-bit segments hold one codepoint per byte, so the byte cut IS the codepoint cut
                                        spanEnd = spanStart + prefixLen;
                                    }

                                    int h;
                                    int spanOff = 0;
                                    int spanLen = 0;
                                    Text.UnicodeString genericKey = null;
                                    byte[] spanBytes;
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
                                        genericKey = Trees.Tiny.TinyParentNodeImpl.GetStringValue(tree, cur);
                                        if (genericKey.Length32() > prefixLen)
                                        {
                                            genericKey = genericKey.Substring(0, prefixLen);
                                        }

                                        h = genericKey.GetHashCode();
                                    }

                                    int slot = h & mask;
                                    bool seen = false;
                                    while (tKey[slot] != null)
                                    {
                                        if (tHash[slot] == h
                                            && (spanBytes != null
                                                ? (tKey[slot] is Text.Slice8 s8 ? s8.ContentEqualsSpan(spanBytes, spanOff, spanLen)
                                                                                : new Text.Slice8(spanBytes, spanOff, spanOff + spanLen).Equals(tKey[slot]))
                                                : genericKey.Equals(tKey[slot])))
                                        {
                                            seen = true;
                                            break;
                                        }

                                        slot = (slot + 1) & mask;
                                    }

                                    if (!seen)
                                    {
                                        Text.UnicodeString key = spanBytes != null ? new Text.Slice8(spanBytes, spanOff, spanOff + spanLen) : genericKey;
                                        tHash[slot] = h;
                                        tKey[slot] = key;
                                        values.Add(new StringValue(key));
                                        if (++used * 3 > cap * 2)
                                        {
                                            int cap2 = cap << 1;
                                            int mask2 = cap2 - 1;
                                            int[] h2 = new int[cap2];
                                            Text.UnicodeString[] k2 = new Text.UnicodeString[cap2];
                                            for (int s = 0; s < cap; s++)
                                            {
                                                if (tKey[s] != null)
                                                {
                                                    int ns = tHash[s] & mask2;
                                                    while (k2[ns] != null)
                                                    {
                                                        ns = (ns + 1) & mask2;
                                                    }

                                                    h2[ns] = tHash[s];
                                                    k2[ns] = tKey[s];
                                                }
                                            }

                                            cap = cap2;
                                            mask = mask2;
                                            tHash = h2;
                                            tKey = k2;
                                        }
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

            public IItem Next()
            {
                return pos < values.Count ? values[pos++] : null;
            }

            public void Dispose() { }
        }

        /// <summary>
        /// IIterator class to return the distinct values in a sequence
        /// </summary>
        internal sealed class DistinctIterator : ISequenceIterator
        {
            private readonly ISequenceIterator @base;
            private readonly IStringCollator collator;
            private readonly IXPathContext context;
            private readonly HashSet<IAtomicMatchKey> lookup = new HashSet<IAtomicMatchKey>();
            private static readonly Func<object, long> LookupSizer = o => 48 + 112L * ((HashSet<IAtomicMatchKey>)o).Count;   // a slot and a key each
            private readonly IAction onDuplicates;
            public DistinctIterator(ISequenceIterator @base, IStringCollator collator, IXPathContext context)
            {
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
                        if (lookup.Count == Core.MemoryLedger.BigCount)
                        {
                            Core.MemoryLedger.Hold(lookup, LookupSizer);   // summed as it grows
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
            IItem ISequenceIterator.Next() => Next(); // redirect StubGen hollow to the real covariant Next(); default = silent empty iteration
        }
    }
}
