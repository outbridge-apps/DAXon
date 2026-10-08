////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Trees.Tiny;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;

namespace OutSmart.DAXon.Expressions.Elaboration
{
    /// <summary>
    /// Fused reader for the `convertToString(atomize(child::NAME))` shape that every string-function
    /// argument over a child element compiles to - upper-case(X), translate(X,...), normalize-space(X),
    /// substring(X,...), string comparisons, sort keys. The generic pipeline builds an axis iterator, an
    /// atomizing iterator, a converting iterator and an untypedAtomic box per child just to hand back the
    /// child's string value; on a Tiny untyped tree this walks the child array directly and yields
    /// StringValues. Off the fast path - non-Tiny context item or a schema-typed tree - it returns
    /// null/false so the caller runs the generic evaluator, leaving typed-data and node semantics intact.
    /// The value produced is byte-identical: untyped-atomic to xs:string is a lexical copy, so
    /// new StringValue(GetStringValue) equals ConvertItem(MakeUntypedAtomic(GetStringValue)).
    /// </summary>
    internal static class FusedChildAtomizer
    {
        internal static bool Match(AtomicSequenceConverter conv, out int fp)
        {
            fp = -1;
            if (!BuiltInAtomicType.STRING.Equals(conv.RequiredItemType))
            {
                return false;
            }

            if (!(conv.BaseExpression is Atomizer atom))
            {
                return false;
            }

            if (!(atom.BaseExpression is AxisExpression axis) || axis.Axis != AxisInfo.CHILD)
            {
                return false;
            }

            if (!(axis.GetNodeTest() is NameTest nameTest) || nameTest.PrimitiveType != Types.Type.ELEMENT)
            {
                return false;
            }

            fp = nameTest.Fingerprint;
            return true;
        }

        // Item (head) read: the first matching child's string, or null when there is no match or the
        // context is off the fast path (the caller then runs the generic item evaluator).
        internal static StringValue ReadFirstChildString(IXPathContext context, int fp)
        {
            if (!(context.GetContextItem() is TinyParentNodeImpl tiny) || tiny.tree.TypeArray != null)
            {
                return null;
            }

            TinyTree tree = tiny.tree;
            int p = tiny.nodeNr;
            int child = p + 1;
            if (child >= tree.numberOfNodes || tree.depth[child] != tree.depth[p] + 1)
            {
                return null;   // no children
            }

            byte[] kinds = tree.nodeKind;
            int[] nextArr = tree.next;
            int[] nameCodes = tree.nameCode;
            int n = child;
            while (true)
            {
                int k = kinds[n];
                if ((k == Types.Type.ELEMENT || k == Types.Type.TEXTUAL_ELEMENT) && (nameCodes[n] & NamePool.FP_MASK) == fp)
                {
                    return new StringValue(TinyParentNodeImpl.GetStringValue(tree, n));
                }

                int n2 = nextArr[n];
                if (n2 <= n)
                {
                    return null;
                }

                n = n2;
            }
        }

        internal static bool CanFuse(IXPathContext context)
        {
            return context.GetContextItem() is TinyParentNodeImpl tiny && tiny.tree.TypeArray == null;
        }

        // Match the bare `atomize(child::NAME)` shape (no convertToString wrapper): the group-by
        // key of `xsl:for-each-group group-by="childName"` compiles to an Atomizer directly over a
        // child element step. Returns the element fingerprint, or -1. The atomized value of an
        // untyped element is xs:untypedAtomic, so the fused reader must yield MakeUntypedAtomic
        // (NOT a StringValue) to keep current-grouping-key()'s type identical.
        internal static bool MatchAtomizer(Expression keyExpr, out int fp)
        {
            fp = -1;
            if (!(keyExpr is Atomizer atom))
            {
                return false;
            }

            if (!(atom.BaseExpression is AxisExpression axis) || axis.Axis != AxisInfo.CHILD)
            {
                return false;
            }

            if (!(axis.GetNodeTest() is NameTest nameTest) || nameTest.PrimitiveType != Types.Type.ELEMENT)
            {
                return false;
            }

            fp = nameTest.Fingerprint;
            return true;
        }

        // Bare `child::NAME` element step (SingletonAtomizer's base in fn:number(childName),
        // xs:decimal(childName) and friends).
        internal static bool MatchAxis(Expression e, out int fp)
        {
            fp = -1;
            if (!(e is AxisExpression axis) || axis.Axis != AxisInfo.CHILD)
            {
                return false;
            }

            if (!(axis.GetNodeTest() is NameTest nameTest) || nameTest.PrimitiveType != Types.Type.ELEMENT)
            {
                return false;
            }

            fp = nameTest.Fingerprint;
            return true;
        }

        // Single-child atomize for SingletonAtomizer(child::NAME): the one matching child's
        // untypedAtomic value without the per-call child iterator + node wrapper. Every case the
        // fast read cannot decide byte-identically — typed/foreign tree, a SECOND matching child
        // (XPTY0004 with the generic wording), or an empty result when empty is disallowed — sets
        // offPath and the caller runs the generic evaluator instead.
        internal static Values.AtomicValue ReadSingleChildUntyped(IXPathContext context, int fp, bool allowEmpty, out bool offPath)
        {
            return ReadSingleChildUntypedOf(context.GetContextItem(), fp, allowEmpty, out offPath);
        }

        // Same read with an explicit parent (the `$var/childName` shape).
        internal static Values.AtomicValue ReadSingleChildUntypedOf(IItem parent, int fp, bool allowEmpty, out bool offPath)
        {
            offPath = false;
            if (!(parent is TinyParentNodeImpl tiny) || tiny.tree.TypeArray != null)
            {
                offPath = true;
                return null;
            }

            TinyTree tree = tiny.tree;
            int p = tiny.nodeNr;
            int child = p + 1;
            Values.AtomicValue result = null;
            if (child < tree.numberOfNodes && tree.depth[child] == tree.depth[p] + 1)
            {
                byte[] kinds = tree.nodeKind;
                int[] nextArr = tree.next;
                int[] nameCodes = tree.nameCode;
                int n = child;
                while (n >= 0)
                {
                    int cur = n;
                    int n2 = nextArr[cur];
                    n = n2 > cur ? n2 : -1;
                    int k = kinds[cur];
                    if ((k == Types.Type.ELEMENT || k == Types.Type.TEXTUAL_ELEMENT) && (nameCodes[cur] & NamePool.FP_MASK) == fp)
                    {
                        if (result != null)
                        {
                            offPath = true;
                            return null;
                        }

                        result = StringValue.MakeUntypedAtomic(TinyParentNodeImpl.GetStringValue(tree, cur));
                    }
                }
            }

            if (result == null && !allowEmpty)
            {
                offPath = true;
                return null;
            }

            return result;
        }

        // Single-child string for fn:string(child::NAME): the one matching child's string value as
        // xs:string (fn:string result type, NOT untypedAtomic). Empty is always allowed (fn:string of
        // () is ""); a SECOND matching child or an off-path tree sets offPath and the caller runs the
        // generic evaluator, whose CardinalityChecker raises the exact XPTY0004.
        internal static StringValue ReadSingleChildString(IItem parent, int fp, out bool offPath)
        {
            offPath = false;
            if (!(parent is TinyParentNodeImpl tiny) || tiny.tree.TypeArray != null)
            {
                offPath = true;
                return null;
            }

            TinyTree tree = tiny.tree;
            int p = tiny.nodeNr;
            int child = p + 1;
            StringValue result = null;
            if (child < tree.numberOfNodes && tree.depth[child] == tree.depth[p] + 1)
            {
                byte[] kinds = tree.nodeKind;
                int[] nextArr = tree.next;
                int[] nameCodes = tree.nameCode;
                int n = child;
                while (n >= 0)
                {
                    int cur = n;
                    int n2 = nextArr[cur];
                    n = n2 > cur ? n2 : -1;
                    int k = kinds[cur];
                    if ((k == Types.Type.ELEMENT || k == Types.Type.TEXTUAL_ELEMENT) && (nameCodes[cur] & NamePool.FP_MASK) == fp)
                    {
                        if (result != null)
                        {
                            offPath = true;
                            return null;
                        }

                        result = new StringValue(TinyParentNodeImpl.GetStringValue(tree, cur));
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Like <see cref="ChildStringIterator"/> but yields xs:untypedAtomic values (the atomized
        /// value of an untyped element), for the group-by key fast path. Walks the Tiny child array
        /// directly in document order; byte-identical to atomize(child::NAME) over an untyped tree.
        /// </summary>
        internal sealed class ChildUntypedIterator : ISequenceIterator
        {
            private TinyTree tree;
            private int fp;
            private int n;   // cursor over the sibling chain, -1 = exhausted

            // Reusable: GroupByIterator drains this fully per item, then re-points it at the next
            // parent (single-threaded, no retained reference), so ONE instance serves the whole
            // population instead of allocating an iterator per item.
            internal ChildUntypedIterator() { }

            internal void Reset(TinyParentNodeImpl parent, int fingerprint)
            {
                tree = parent.tree;
                fp = fingerprint;
                int p = parent.nodeNr;
                int child = p + 1;
                n = (child < tree.numberOfNodes && tree.depth[child] == tree.depth[p] + 1) ? child : -1;
            }

            public IItem Next()
            {
                byte[] kinds = tree.nodeKind;
                int[] nextArr = tree.next;
                int[] nameCodes = tree.nameCode;
                while (n >= 0)
                {
                    int cur = n;
                    int n2 = nextArr[cur];
                    n = n2 > cur ? n2 : -1;   // a backwards jump is the owner pointer = end of siblings
                    int k = kinds[cur];
                    if ((k == Types.Type.ELEMENT || k == Types.Type.TEXTUAL_ELEMENT) && (nameCodes[cur] & NamePool.FP_MASK) == fp)
                    {
                        return StringValue.MakeUntypedAtomic(TinyParentNodeImpl.GetStringValue(tree, cur));
                    }
                }

                return null;
            }

            public void Dispose() { }
        }

        /// <summary>
        /// Fused convert-to-string for `xs:string* from atomize(NODES)` on an untyped source (the
        /// as="xs:string*" coercion of a node sequence, e.g. a sorted entity field): each node's
        /// string value becomes the xs:string directly — no untypedAtomic intermediate, no
        /// converting-iterator layer. Content-identical for every node kind: atomize of an untyped
        /// node is its string value (as untypedAtomic or xs:string), and the string promoter keeps
        /// that value verbatim.
        /// </summary>
        internal sealed class NodeToStringIterator : ISequenceIterator
        {
            private readonly ISequenceIterator nodes;

            internal NodeToStringIterator(ISequenceIterator nodes)
            {
                this.nodes = nodes;
            }

            public IItem Next()
            {
                IItem n = nodes.Next();
                if (n == null)
                {
                    return null;
                }

                try
                {
                    return new StringValue(((NodeInfo)n).UnicodeStringValue);
                }
                catch (Transformation.XPathException e)
                {
                    throw new Transformation.UncheckedXPathException(e);
                }
            }

            public void Dispose()
            {
                nodes.Dispose();
            }
        }

        /// <summary>
        /// Fused atomizer for the sequence shape `atomize($nodes/childName)` (distinct-values,
        /// string-join, aggregates over an entity field): per parent from the select stream, matching
        /// children are read straight off the Tiny arrays as xs:untypedAtomic — no per-parent axis
        /// iterator, no child node wrapper, no per-item MappingIterator step. A parent off the fast
        /// path (typed or foreign tree) atomizes through the same one-to-one UntypedAtomizingIterator
        /// the generic pipeline applies; the caller's oneToOne gate guarantees that form is valid.
        /// </summary>
        internal sealed class ChildSequenceAtomizeIterator : ISequenceIterator
        {
            private readonly ISequenceIterator parents;
            private readonly int fp;
            private readonly NodeTest nodeTest;
            private readonly ChildUntypedIterator flat = new ChildUntypedIterator();   // reused per parent
            private ISequenceIterator inner;
            private bool innerIsFlat;

            internal ChildSequenceAtomizeIterator(ISequenceIterator parents, int fp, NodeTest nodeTest)
            {
                this.parents = parents;
                this.fp = fp;
                this.nodeTest = nodeTest;
            }

            public IItem Next()
            {
                while (true)
                {
                    if (inner != null)
                    {
                        IItem item = inner.Next();
                        if (item != null)
                        {
                            return item;
                        }

                        if (!innerIsFlat)
                        {
                            inner.Dispose();
                        }

                        inner = null;
                    }

                    IItem p = parents.Next();
                    if (p == null)
                    {
                        return null;
                    }

                    if (p is TinyParentNodeImpl tiny && tiny.tree.TypeArray == null)
                    {
                        flat.Reset(tiny, fp);
                        inner = flat;
                        innerIsFlat = true;
                    }
                    else
                    {
                        inner = new Trees.Iterators.UntypedAtomizingIterator(((NodeInfo)p).IterateAxis(AxisInfo.CHILD, nodeTest));
                        innerIsFlat = false;
                    }
                }
            }

            public void Dispose()
            {
                parents.Dispose();
                if (inner != null && !innerIsFlat)
                {
                    inner.Dispose();
                }
            }
        }

        /// <summary>
        /// Yields a StringValue for every matching child in document order - the whole atomized sequence,
        /// so a cardinality check upstream still sees 2+ items and raises the same XPTY0004.
        /// </summary>
        internal sealed class ChildStringIterator : ISequenceIterator
        {
            private readonly TinyTree tree;
            private readonly int fp;
            private int n;   // cursor over the sibling chain, -1 = exhausted

            internal ChildStringIterator(TinyParentNodeImpl parent, int fp)
            {
                tree = parent.tree;
                this.fp = fp;
                int p = parent.nodeNr;
                int child = p + 1;
                n = (child < tree.numberOfNodes && tree.depth[child] == tree.depth[p] + 1) ? child : -1;
            }

            public IItem Next()
            {
                byte[] kinds = tree.nodeKind;
                int[] nextArr = tree.next;
                int[] nameCodes = tree.nameCode;
                while (n >= 0)
                {
                    int cur = n;
                    int n2 = nextArr[cur];
                    n = n2 > cur ? n2 : -1;   // a backwards jump is the owner pointer = end of siblings
                    int k = kinds[cur];
                    if ((k == Types.Type.ELEMENT || k == Types.Type.TEXTUAL_ELEMENT) && (nameCodes[cur] & NamePool.FP_MASK) == fp)
                    {
                        return new StringValue(TinyParentNodeImpl.GetStringValue(tree, cur));
                    }
                }

                return null;
            }

            public void Dispose() { }
        }
    }
}
