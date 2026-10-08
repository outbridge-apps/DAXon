////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Trees.Tiny;
using OutSmart.DAXon.Values;

namespace OutSmart.DAXon.Expressions.Elaboration
{
    /// <summary>
    /// Fused readers for the two compiled forms of the leaf-element filter `//*[not(*)]`:
    ///   A: descendant::element()[not(child::element())]                       (element results)
    ///   B: descendant::text()[parent::element()[not(child::element())]]      (the optimizer's
    ///      push-down of a trailing /text() step)
    /// The generic pipeline pays a focus tracker plus, per candidate, a node wrapper, a child-axis
    /// iterator and an fn:not effective-boolean-value just to decide leafness; on an untyped Tiny
    /// tree the same verdict reads straight off the nodeKind/depth/next arrays. Off the fast path -
    /// non-Tiny or schema-typed context - callers run the generic evaluator, so node semantics and
    /// typed-data behavior are untouched. Results are byte-identical: both walks visit the node
    /// array in document order, and a leaf test over the child chain is exactly EBV(child::element()).
    /// </summary>
    internal static class FusedLeafFilter
    {
        internal static bool MatchLeafElements(FilterExpression fe)
        {
            return IsDescendantAxis(fe.Base, Types.Type.ELEMENT) && IsNotChildElement(fe.Filter);
        }

        internal static bool MatchLeafTexts(FilterExpression fe)
        {
            return IsDescendantAxis(fe.Base, Types.Type.TEXT)
                && fe.Filter is FilterExpression pf
                && pf.Base is AxisExpression pax && pax.Axis == AxisInfo.PARENT
                && pax.GetNodeTest() is NodeKindTest pkt && pkt.GetNodeKind() == Types.Type.ELEMENT
                && IsNotChildElement(pf.Filter);
        }

        // string-length(.) — the action step of `//*[not(*)]/string-length(.)`. The type checker
        // wraps the bare `.` in fn:string() and converter layers (same unwrap as StringLength_1).
        internal static bool IsStringLengthOfSelf(Expression action)
        {
            if (!(action is SystemFunctionCall sfc) || !(sfc.TargetFunction is StringLength_1) || sfc.GetArity() != 1)
            {
                return false;
            }

            Expression p = TransparentWrappers.Unwrap(sfc.GetArg(0),
                Peel.StringFn | Peel.Converter | Peel.Atomizer | Peel.CardinalityChecker);
            return p is ContextItemExpression;
        }

        private static bool IsDescendantAxis(Expression e, int nodeKind)
        {
            return e is AxisExpression ax && ax.Axis == AxisInfo.DESCENDANT
                && ax.GetNodeTest() is NodeKindTest kt && kt.GetNodeKind() == nodeKind;
        }

        // The optimizer rewrites the predicate not(child::element()) to empty(child::element())
        // (NotFn.MakeOptimizedFunctionCall); as a filter both mean EBV = "no element child".
        private static bool IsNotChildElement(Expression e)
        {
            return e is SystemFunctionCall sfc && (sfc.TargetFunction is Empty || sfc.TargetFunction is NotFn) && sfc.GetArity() == 1
                && sfc.GetArg(0) is AxisExpression ch && ch.Axis == AxisInfo.CHILD
                && ch.GetNodeTest() is NodeKindTest ckt && ckt.GetNodeKind() == Types.Type.ELEMENT;
        }

        // Leaf = element with no element children. A TEXTUAL_ELEMENT's only child is its fused
        // text, so it is always a leaf; for a plain ELEMENT the child chain (all children, any
        // kind) is scanned for an element kind — the same verdict as EBV(child::element()).
        internal static bool IsLeafElement(TinyTree tree, int n)
        {
            byte k = tree.nodeKind[n];
            if (k == Types.Type.TEXTUAL_ELEMENT)
            {
                return true;
            }

            if (k != Types.Type.ELEMENT)
            {
                return false;
            }

            int c = n + 1;
            if (c >= tree.numberOfNodes || tree.depth[c] != tree.depth[n] + 1)
            {
                return true;   // no children
            }

            byte[] kinds = tree.nodeKind;
            int[] nextArr = tree.next;
            while (true)
            {
                byte ck = kinds[c];
                if (ck == Types.Type.ELEMENT || ck == Types.Type.TEXTUAL_ELEMENT)
                {
                    return false;
                }

                int c2 = nextArr[c];
                if (c2 <= c)
                {
                    return true;   // a backwards jump is the owner pointer = end of siblings
                }

                c = c2;
            }
        }

        /// <summary>
        /// Form A: yields every leaf element in the subtree of the start node in document order.
        /// Wrappers are created only for the leaves that are actually returned.
        /// </summary>
        internal sealed class LeafElementIterator : ISequenceIterator, Trees.Iterators.IFastCountable
        {
            private readonly TinyTree tree;
            private readonly int stopDepth;
            private int next;

            internal LeafElementIterator(TinyParentNodeImpl start)
            {
                tree = start.tree;
                next = start.nodeNr + 1;
                stopDepth = tree.depth[start.nodeNr];
            }

            public IItem Next()
            {
                TinyTree t = tree;
                short[] d = t.depth;
                int nn = t.numberOfNodes;
                for (int n = next; n < nn && d[n] > stopDepth; n++)
                {
                    if (IsLeafElement(t, n))
                    {
                        next = n + 1;
                        return t.GetNode(n);
                    }
                }

                next = nn;
                return null;
            }

            public bool TryFastCount(out int count)
            {
                TinyTree t = tree;
                short[] d = t.depth;
                int nn = t.numberOfNodes;
                int c = 0;
                for (int n = next; n < nn && d[n] > stopDepth; n++)
                {
                    if (IsLeafElement(t, n))
                    {
                        c++;
                    }
                }

                next = nn;
                count = c;
                return true;
            }

            public void Dispose() { }
        }

        /// <summary>
        /// Form B: yields every text node whose parent is a leaf element, in document order.
        /// Texts of leaf elements are disjoint ranges ordered by their element, so walking leaf
        /// elements and emitting each one's text children reproduces descendant order exactly.
        /// With <c>atomize</c> the untypedAtomic value is built straight from the text buffer —
        /// no text-node wrapper at all (an untyped text atomizes to its string value verbatim).
        /// </summary>
        internal sealed class LeafTextIterator : ISequenceIterator, Trees.Iterators.IFastCountable
        {
            private readonly TinyTree tree;
            private readonly bool atomize;
            private readonly int stopDepth;
            private int scan;              // next array index the element scan will inspect
            private int child = -1;        // cursor over the current leaf's child chain, -1 = none
            private int virtualText = -1;  // TEXTUAL_ELEMENT whose inline text is pending

            internal LeafTextIterator(TinyParentNodeImpl start, bool atomize)
            {
                tree = start.tree;
                this.atomize = atomize;
                stopDepth = tree.depth[start.nodeNr];
                scan = start.nodeNr + 1;

                // descendant::text() of the start node itself: a leaf start element contributes
                // its own text children (the subtree scan below only finds descendant elements)
                byte k = tree.nodeKind[start.nodeNr];
                if (k == Types.Type.TEXTUAL_ELEMENT)
                {
                    virtualText = start.nodeNr;
                }
                else if (k == Types.Type.ELEMENT && IsLeafElement(tree, start.nodeNr))
                {
                    child = FirstChild(start.nodeNr);
                }
            }

            private int FirstChild(int n)
            {
                int c = n + 1;
                return c < tree.numberOfNodes && tree.depth[c] == tree.depth[n] + 1 ? c : -1;
            }

            public IItem Next()
            {
                TinyTree t = tree;
                byte[] kinds = t.nodeKind;
                short[] d = t.depth;
                int[] nextArr = t.next;
                int nn = t.numberOfNodes;
                while (true)
                {
                    if (virtualText >= 0)
                    {
                        int v = virtualText;
                        virtualText = -1;
                        return atomize
                            ? StringValue.MakeUntypedAtomic(TinyTextImpl.GetStringValue(t, v))
                            : (IItem)((TinyTextualElement)t.GetNode(v)).TextNode;
                    }

                    while (child >= 0)
                    {
                        int c = child;
                        int c2 = nextArr[c];
                        child = c2 > c ? c2 : -1;
                        byte ck = kinds[c];
                        if (ck == Types.Type.TEXT)
                        {
                            return atomize
                                ? StringValue.MakeUntypedAtomic(TinyTextImpl.GetStringValue(t, c))
                                : (IItem)t.GetNode(c);
                        }

                        if (ck == Types.Type.WHITESPACE_TEXT)
                        {
                            return atomize
                                ? StringValue.MakeUntypedAtomic(WhitespaceTextImpl.GetStringValue(t, c))
                                : (IItem)t.GetNode(c);
                        }
                    }

                    int n = scan;
                    if (n >= nn || d[n] <= stopDepth)
                    {
                        scan = nn;
                        return null;
                    }

                    scan = n + 1;
                    byte k = kinds[n];
                    if (k == Types.Type.TEXTUAL_ELEMENT)
                    {
                        virtualText = n;
                    }
                    else if (k == Types.Type.ELEMENT && IsLeafElement(t, n))
                    {
                        child = FirstChild(n);
                    }
                }
            }

            public bool TryFastCount(out int count)
            {
                TinyTree t = tree;
                byte[] kinds = t.nodeKind;
                short[] d = t.depth;
                int nn = t.numberOfNodes;
                int c = 0;
                if (virtualText >= 0)
                {
                    c++;
                    virtualText = -1;
                }

                c += CountChainTexts(child);
                child = -1;
                for (int n = scan; n < nn && d[n] > stopDepth; n++)
                {
                    byte k = kinds[n];
                    if (k == Types.Type.TEXTUAL_ELEMENT)
                    {
                        c++;
                    }
                    else if (k == Types.Type.ELEMENT && IsLeafElement(t, n))
                    {
                        c += CountChainTexts(FirstChild(n));
                    }
                }

                scan = nn;
                count = c;
                return true;
            }

            private int CountChainTexts(int c)
            {
                byte[] kinds = tree.nodeKind;
                int[] nextArr = tree.next;
                int total = 0;
                while (c >= 0)
                {
                    byte ck = kinds[c];
                    if (ck == Types.Type.TEXT || ck == Types.Type.WHITESPACE_TEXT)
                    {
                        total++;
                    }

                    int c2 = nextArr[c];
                    c = c2 > c ? c2 : -1;
                }

                return total;
            }

            public void Dispose() { }
        }
    }
}
