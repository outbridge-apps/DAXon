////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Types;

namespace OutSmart.DAXon.Trees.Tiny
{
    // The text of an untyped Tiny node read in place, and plain numbers parsed from it: what sum(), avg(), a comparison
    // with a number or a cast needs from a node, without the untypedAtomic value, its string and the converted value
    // that atomizing and converting allocate for each node. Anything else answers false and the caller atomizes.
    internal static class TinyText
    {
        // The node's string value as Latin1 bytes: a text node, or an element whose content is one text node or a
        // textual element. False for a typed tree, other content, wider text or text across two buffer segments.
        internal static bool TrySpan(IItem item, out byte[] bytes, out int off, out int len)
        {
            bytes = null;
            off = 0;
            len = 0;
            if (!(item is TinyNodeImpl node) || node.tree.TypeArray != null || !(node is TinyParentNodeImpl || node is TinyTextImpl))
            {
                return false;
            }

            return TrySpan(node.tree, node.nodeNr, out bytes, out off, out len);
        }

        // The same for node number n of an untyped tree.
        internal static bool TrySpan(TinyTree tree, int n, out byte[] bytes, out int off, out int len)
        {
            return TrySpan(tree, n, int.MaxValue, out bytes, out off, out len);
        }

        // The same, cut to its first maxLen characters: an 8-bit span holds one character a byte.
        internal static bool TrySpan(TinyTree tree, int n, int maxLen, out byte[] bytes, out int off, out int len)
        {
            bytes = null;
            off = 0;
            len = 0;
            int kind = tree.nodeKind[n];
            if (kind == Type.ELEMENT)
            {
                int c = n + 1;
                if (c >= tree.numberOfNodes || tree.depth[c] != tree.depth[n] + 1 || tree.nodeKind[c] != Type.TEXT || tree.next[c] > c)
                {
                    return false;
                }

                n = c;
            }
            else if (kind != Type.TEXTUAL_ELEMENT && kind != Type.TEXT)
            {
                return false;
            }

            int start = tree.alpha[n];
            return tree.textBuffer.TryGetByteSpan(start, start + System.Math.Min(tree.beta[n], maxLen), out bytes, out off, out len);
        }

        // The first child of node p, or -1.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        internal static int FirstChild(TinyTree tree, int p)
        {
            int child = p + 1;
            return child < tree.numberOfNodes && tree.depth[child] == tree.depth[p] + 1 ? child : -1;
        }

        // The next element child named fp from child n along the sibling chain, or -1; n moves past it (-1 at the end: a
        // backwards jump in the chain is the owner pointer). The one walk of the fused child paths, which kept a copy each.
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        internal static int NextChildNamed(TinyTree tree, int fp, ref int n)
        {
            byte[] kinds = tree.nodeKind;
            int[] nextArr = tree.next;
            int[] nameCodes = tree.nameCode;
            while (n >= 0)
            {
                int cur = n;
                int n2 = nextArr[cur];
                n = n2 > cur ? n2 : -1;
                int k = kinds[cur];
                if ((k == Type.ELEMENT || k == Type.TEXTUAL_ELEMENT) && (nameCodes[cur] & NamePool.FP_MASK) == fp)
                {
                    return cur;
                }
            }

            return -1;
        }

        // axis::NAME, a step to the elements of one name on that axis: the fingerprint of NAME, or -1 for any other expression.
        internal static int ElementStep(Expressions.Expression e, int axis)
        {
            return e is Expressions.AxisExpression step && step.Axis == axis
                && step.GetNodeTest() is Patterns.NameTest test && test.PrimitiveType == Type.ELEMENT ? test.Fingerprint : -1;
        }


        // descendant::NAME of an element: the fingerprint of NAME, or -1 for any other expression.
        internal static int DescendantName(Expressions.Expression e)
        {
            return ElementStep(e, AxisInfo.DESCENDANT);
        }

        // NODES/NAME, a child step to elements of one name, under the document-order sort a path gets: the fingerprint of
        // NAME and the start expression, or -1.
        internal static int ChildName(Expressions.Expression e, out Expressions.Expression start)
        {
            start = null;
            if (e is Expressions.Sorting.DocumentSorter sorter)
            {
                e = sorter.BaseExpression;
            }

            int fp = e is Expressions.SlashExpression slash ? ElementStep(slash.GetActionExpression(), AxisInfo.CHILD) : -1;
            if (fp >= 0)
            {
                start = ((Expressions.SlashExpression)e).GetSelectExpression();
            }

            return fp;
        }

        // Takes a child node by number; false stops the walk.
        internal delegate bool ChildVisitor(TinyTree tree, int child);

        // The children named fp of the start nodes, in document order, by number with no node objects: the starts are
        // descendant::startFp of an untyped Tiny context node, or what `starts` gives. They must be parent nodes of one
        // untyped tree, at one depth, in increasing order - then the children are distinct and in order. False when they are
        // not: the caller drops what it took and takes the general path.
        internal static bool WalkChildren(Expressions.IXPathContext context, Expressions.Elaboration.IPullEvaluator starts, int startFp, int fp, ChildVisitor visit)
        {
            int steps = 0;
            int depth = -1;
            if (startFp >= 0 && context.GetContextItem() is TinyParentNodeImpl top && top.tree.TypeArray == null)
            {
                TinyTree t = top.tree;
                int n = top.nodeNr;
                for (int p; (p = NextDescendant(t, top.nodeNr, startFp, ref n)) >= 0;)
                {
                    if (depth >= 0 && t.depth[p] != depth)
                    {
                        return false;
                    }

                    depth = t.depth[p];
                    if (!VisitChildren(t, p, fp, visit, ref steps))
                    {
                        break;
                    }
                }

                return true;
            }

            TinyTree tree = null;
            int last = -1;
            ISequenceIterator iter = starts.Iterate(context);
            for (IItem item; (item = iter.Next()) != null;)
            {
                if (!(item is TinyParentNodeImpl node) || node.tree.TypeArray != null || (tree != null && node.tree != tree)
                    || node.nodeNr <= last || (depth >= 0 && node.tree.depth[node.nodeNr] != depth))
                {
                    iter.Dispose();
                    return false;
                }

                tree = node.tree;
                last = node.nodeNr;
                depth = tree.depth[last];
                if (!VisitChildren(tree, last, fp, visit, ref steps))
                {
                    iter.Dispose();
                    break;
                }
            }

            return true;
        }

        private static bool VisitChildren(TinyTree tree, int p, int fp, ChildVisitor visit, ref int steps)
        {
            Core.Controller.CheckActiveTimeoutEvery64K(steps++);
            int n = FirstChild(tree, p);
            for (int c; (c = NextChildNamed(tree, fp, ref n)) >= 0;)
            {
                Core.Controller.CheckActiveTimeoutEvery64K(steps++);
                if (!visit(tree, c))
                {
                    return false;
                }
            }

            return true;
        }

        // The elements named fp below node `from` of the tree, by number in document order, as descendant::NAME selects them
        // but with no node object for each; -1 at the end. Start with n = from.
        internal static int NextDescendant(TinyTree tree, int from, int fp, ref int n)
        {
            short top = tree.depth[from];
            byte[] kinds = tree.nodeKind;
            int[] names = tree.nameCode;
            short[] depths = tree.depth;
            int count = tree.numberOfNodes;
            while (++n < count && depths[n] > top)
            {
                int k = kinds[n];
                if ((k == Type.ELEMENT || k == Type.TEXTUAL_ELEMENT) && (names[n] & NamePool.FP_MASK) == fp)
                {
                    return n;
                }
            }

            n = count;
            return -1;
        }

        // [ws] -? digits [. digits] [ws] with at most 15 significant digits, as StringToDouble.TryExact converts them: the
        // digits as an exact long over an exact power of ten, correctly rounded. False for anything else.
        internal static bool TryPlainDouble(byte[] b, int off, int len, out double value)
        {
            value = 0;
            return TryPlainDigits(new ByteText(b), off, len, out long num, out int scale, out bool negative, out _, 15)
                && Types.StringToDouble.TryExact(num, -scale, negative, out value);
        }

        // The plain number an item of an untyped tree holds as text: an element, a text node, or an attribute of a tree
        // with no attribute types. False: the caller atomizes.
        internal static bool TryNodeDouble(IItem item, out double value)
        {
            if (item is TinyAttributeImpl attribute)
            {
                value = 0;
                string text = attribute.tree.attValue[attribute.nodeNr];
                return attribute.tree.attType == null
                    && TryPlainDigits(new CharText(text), 0, text.Length, out long num, out int scale, out bool negative, out _, 15)
                    && Types.StringToDouble.TryExact(num, -scale, negative, out value);
            }

            value = 0;
            return TrySpan(item, out byte[] b, out int off, out int len) && TryPlainDouble(b, off, len, out value);
        }

        // [ws] -? digits [ws] within a long of 18 digits: the xs:integer the cast gives.
        internal static bool TryPlainInteger(byte[] b, int off, int len, out long value)
        {
            value = 0;
            if (!TryPlainDigits(new ByteText(b), off, len, out long num, out _, out bool negative, out bool point, 18) || point)
            {
                return false;
            }

            value = negative ? -num : num;
            return true;
        }

        private static bool TryPlainDigits<T>(T b, int off, int len, out long num, out int scale, out bool negative, out bool point, int maxDigits)
            where T : struct, IText
        {
            num = 0;
            scale = 0;
            negative = false;
            point = false;
            int i = off;
            int end = off + len;
            while (i < end && IsWhite(b[i]))
            {
                i++;
            }

            while (end > i && IsWhite(b[end - 1]))
            {
                end--;
            }

            if (i < end && b[i] == '-')
            {
                negative = true;
                i++;
            }

            int digits = 0;
            bool any = false;
            for (; i < end; i++)
            {
                int c = b[i];
                if (c >= '0' && c <= '9')
                {
                    any = true;
                    if (num != 0 || c != '0')
                    {
                        if (++digits > maxDigits)
                        {
                            return false;
                        }
                    }

                    num = num * 10 + (c - '0');
                    if (point)
                    {
                        scale++;
                    }
                }
                else if (c == '.' && !point)
                {
                    point = true;
                }
                else
                {
                    return false;
                }
            }

            return any;
        }

        private static bool IsWhite(int c)
        {
            return c == ' ' || c == '\t' || c == '\n' || c == '\r';
        }

        // What a node's plain-number text is read from: tree text in bytes, an attribute's value in a string.
        private interface IText
        {
            int this[int i] { get; }
        }

        private readonly struct ByteText : IText
        {
            private readonly byte[] b;
            internal ByteText(byte[] b) { this.b = b; }
            public int this[int i] => b[i];
        }

        private readonly struct CharText : IText
        {
            private readonly string s;
            internal CharText(string s) { this.s = s; }
            public int this[int i] => s[i];
        }
    }
}
