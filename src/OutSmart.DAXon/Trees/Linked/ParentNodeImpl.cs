////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
//import com.sun.tools.javac.util.List;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Trees.Utilities;
using OutSmart.DAXon.Internal.Collections;
using System;
namespace OutSmart.DAXon.Trees.Linked
{
    public abstract class ParentNodeImpl : NodeImpl
    {
        private object _children; // null for no _children
        private int sequence; // sequence number allocated during original tree creation.
        protected internal override long SequenceNumber => GetRawSequenceNumber() == -1 ? -1 : (long)GetRawSequenceNumber() << 32;

        public int NumberOfChildren
        {
            get
            {
                if (_children == null)
                {
                    return 0;
                }
                else if (_children is NodeImpl)
                {
                    return 1;
                }
                else
                {
                    return ((NodeInfo[])_children).Length;
                }
            }
        }

        public override NodeInfo LastChild
        {
            get
            {
                if (_children == null)
                {
                    return null;
                }

                if (_children is NodeImpl)
                {
                    return (NodeImpl)_children;
                }

                NodeImpl[] n = (NodeImpl[])_children;
                return n[n.Length - 1];
            }
        }

        public override UnicodeString UnicodeStringValue
        {
            get
            {
                UnicodeBuilder sb = null;
                NodeImpl next = GetFirstChild();
                while (next != null)
                {
                    if (next is TextImpl)
                    {
                        if (sb == null)
                        {
                            sb = new UnicodeBuilder();
                        }

                        sb.Accept(next.UnicodeStringValue);
                    }

                    next = next.GetNextInDocument(this);
                }

                if (sb == null)
                {
                    return EmptyUnicodeString.GetInstance();
                }

                return sb.ToUnicodeString();
            }
        }

        protected int GetRawSequenceNumber()
        {
            return sequence;
        }

        public void SetRawSequenceNumber(int seq)
        {
            sequence = seq;
        }

        public void SetChildren(object children)
        {
            this._children = children;
        }

        public override bool HasChildNodes()
        {
            return _children != null;
        }

        public IAxisIterator IterateChildren(NodeTest test)
        {
            if (_children == null)
            {
                return EmptyIterator.OfNodes();
            }
            else if (_children is NodeImpl)
            {
                NodeImpl child = (NodeImpl)_children;
                if (test == null || test == AnyNodeTest.GetInstance())
                {
                    return SingleNodeIterator.MakeIterator(child);
                }
                else
                {
                    return Navigator.FilteredSingleton(child, test);
                }
            }
            else
            {
                if (test == null || test == AnyNodeTest.GetInstance())
                {
                    return new OfNodes<NodeImpl>((NodeImpl[])_children);
                }
                else
                {
                    return new ChildEnumeration(this, test);
                }
            }
        }

        public override NodeImpl GetFirstChild()
        {
            if (_children == null)
            {
                return null;
            }
            else if (_children is NodeImpl)
            {
                return (NodeImpl)_children;
            }
            else
            {
                return ((NodeImpl[])_children)[0];
            }
        }

        public NodeImpl GetNthChild(int n)
        {
            if (_children == null)
            {
                return null;
            }

            if (_children is NodeImpl)
            {
                return n == 0 ? (NodeImpl)_children : null;
            }

            NodeImpl[] nodes = (NodeImpl[])_children;
            if (n < 0 || n >= nodes.Length)
            {
                return null;
            }

            return nodes[n];
        }

        public virtual void RemoveChild(NodeImpl child)
        {
            if (_children == null)
            {
                return;
            }

            if (_children == child)
            {
                _children = null;
                return;
            }

            NodeImpl[] nodes = (NodeImpl[])_children;
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i] == child)
                {
                    if (nodes.Length == 2)
                    {
                        _children = nodes[1 - i];
                    }
                    else
                    {
                        NodeImpl[] n2 = new NodeImpl[nodes.Length - 1];
                        if (i > 0)
                        {
                            Array.Copy(nodes, 0, n2, 0, i);
                        }

                        if (i < nodes.Length - 1)
                        {
                            Array.Copy(nodes, i + 1, n2, i, nodes.Length - i - 1);
                        }

                        _children = CleanUpChildren(n2);
                    }

                    break;
                }
            }
        }

        private NodeImpl[] CleanUpChildren(NodeImpl[] children)
        {
            bool prevText = false;
            int j = 0;
            NodeImpl[] c2 = new NodeImpl[children.Length];
            foreach (NodeImpl node in children)
            {
                if (node is TextImpl)
                {
                    if (prevText)
                    {
                        TextImpl prev = (TextImpl)c2[j - 1];
                        prev.ReplaceStringValue(prev.UnicodeStringValue.Concat(node.UnicodeStringValue));
                    }
                    else if (!node.UnicodeStringValue.IsEmpty())
                    {
                        prevText = true;
                        node.SetSiblingPosition(j);
                        c2[j++] = node;
                    }
                }
                else
                {
                    node.SetSiblingPosition(j);
                    c2[j++] = node;
                    prevText = false;
                }
            }

            if (j == c2.Length)
            {
                return c2;
            }
            else
            {
                return ArrayTools.CopyOf(c2, j);
            }
        }

        public virtual void AddChild(NodeImpl node, int index)
        {
            lock (this)
            {
                NodeImpl[] c;
                if (_children == null)
                {
                    c = new NodeImpl[10];
                }
                else if (_children is NodeImpl)
                {
                    c = new NodeImpl[10];
                    c[0] = (NodeImpl)_children;
                }
                else
                {
                    c = (NodeImpl[])_children;
                }

                if (index >= c.Length)
                {
                    Array.Resize(ref c, c.Length * 2);
                }

                c[index] = node;
                node.SetRawParent(this);
                node.SetSiblingPosition(index);
                _children = c;
            }
        }

        public override void InsertChildren(NodeInfo[] source, bool atStart, bool inherit)
        {
            if (atStart)
            {
                InsertChildrenAt(source, 0, inherit);
            }
            else
            {
                InsertChildrenAt(source, NumberOfChildren, inherit);
            }
        }

        public virtual void InsertChildrenAt(NodeInfo[] source, int index, bool inherit)
        {
            lock (this)
            {
                if (source.Length == 0)
                {
                    return;
                }

                NodeImpl[] source2 = AdjustSuppliedNodeArray(source, inherit);
                if (_children == null)
                {
                    if (source2.Length == 1)
                    {
                        _children = source2[0];
                        ((NodeImpl)_children).SetSiblingPosition(0);
                    }
                    else
                    {
                        _children = CleanUpChildren(source2);
                    }
                }
                else if (_children is NodeImpl)
                {
                    int adjacent = index == 0 ? source2.Length - 1 : 0;
                    if (_children is TextImpl && source2[adjacent] is TextImpl)
                    {
                        if (index == 0)
                        {
                            source2[adjacent].ReplaceStringValue(source2[adjacent].UnicodeStringValue.Concat(((TextImpl)_children).UnicodeStringValue));
                        }
                        else
                        {
                            source2[adjacent].ReplaceStringValue(((TextImpl)_children).UnicodeStringValue.Concat(source2[adjacent].UnicodeStringValue));
                        }

                        _children = CleanUpChildren(source2);
                    }
                    else
                    {
                        NodeImpl[] n2 = new NodeImpl[source2.Length + 1];
                        if (index == 0)
                        {
                            Array.Copy(source2, 0, n2, 0, source2.Length);
                            n2[source2.Length] = (NodeImpl)_children;
                        }
                        else
                        {
                            n2[0] = (NodeImpl)_children;
                            Array.Copy(source2, 0, n2, 1, source2.Length);
                        }

                        _children = CleanUpChildren(n2);
                    }
                }
                else
                {
                    NodeImpl[] n0 = (NodeImpl[])_children;
                    NodeImpl[] n2 = new NodeImpl[n0.Length + source2.Length];
                    Array.Copy(n0, 0, n2, 0, index);
                    Array.Copy(source2, 0, n2, index, source2.Length);
                    Array.Copy(n0, index, n2, index + source2.Length, n0.Length - index);
                    _children = CleanUpChildren(n2);
                }
            }
        }

        private NodeImpl ConvertForeignNode(NodeInfo source)
        {
            if (!(source is NodeImpl))
            {
                int kind = source.GetNodeKind();
                switch (kind)
                {
                    case Types.Type.TEXT:
                        return new TextImpl(source.UnicodeStringValue);
                    case Types.Type.COMMENT:
                        return new CommentImpl(source.UnicodeStringValue);
                    case Types.Type.PROCESSING_INSTRUCTION:
                        return new ProcInstImpl(source.GetLocalPart(), source.UnicodeStringValue);
                    case Types.Type.ELEMENT:
                        Builder builder;
                        try
                        {
                            builder = new LinkedTreeBuilder(GetConfiguration().MakePipelineConfiguration(), Durability.MUTABLE);
                            builder.Open();
                            source.Copy(builder, CopyOptions.ALL_NAMESPACES, Loc.NONE);
                            builder.Close();
                        }
                        catch (XPathException)
                        {
                            throw new ArgumentException("Failed to convert inserted element node to an instance of OutSmart.DAXon.Model.Tree.ElementImpl");
                        }

                        return (NodeImpl)builder.CurrentRoot;
                    default:
                        throw new ArgumentException("Cannot insert a node unless it is an element, comment, text node, or processing instruction");
                }
            }

            return (NodeImpl)source;
        }

        public virtual void ReplaceChildrenAt(NodeInfo[] source, int index, bool inherit)
        {
            lock (this)
            {
                if (_children == null)
                {
                    return;
                }

                NodeImpl[] source2 = AdjustSuppliedNodeArray(source, inherit);
                if (_children is NodeImpl)
                {
                    if (source2.Length == 0)
                    {
                        _children = null;
                    }
                    else if (source2.Length == 1)
                    {
                        _children = source2[0];
                    }
                    else
                    {
                        NodeImpl[] n2 = new NodeImpl[source2.Length];
                        Array.Copy(source2, 0, n2, 0, source.Length);
                        _children = CleanUpChildren(n2);
                    }
                }
                else
                {
                    NodeImpl[] n0 = (NodeImpl[])_children;
                    NodeImpl[] n2 = new NodeImpl[n0.Length + source2.Length - 1];
                    Array.Copy(n0, 0, n2, 0, index);
                    Array.Copy(source2, 0, n2, index, source2.Length);
                    Array.Copy(n0, index + 1, n2, index + source2.Length, n0.Length - index - 1);
                    _children = CleanUpChildren(n2);
                }
            }
        }

        private NodeImpl[] AdjustSuppliedNodeArray(NodeInfo[] source, bool inherit)
        {
            NodeImpl[] source2 = new NodeImpl[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                source2[i] = ConvertForeignNode(source[i]);
                NodeImpl child = source2[i];
                child.SetRawParent(this);
                if (child is ElementImpl)
                {

                    // If the child has no xmlns="xxx" declaration, then add an xmlns="" to prevent false inheritance
                    // from the new parent
                    ((ElementImpl)child).FixupInsertedNamespaces(inherit);
                }
            }

            return source2;
        }

        public virtual void Compact(int size)
        {
            lock (this)
            {
                if (size == 0)
                {
                    _children = null;
                }
                else if (size == 1)
                {
                    if (_children is NodeImpl[])
                    {
                        _children = ((NodeImpl[])_children)[0];
                    }
                }
                else
                {
                    _children = ArrayTools.CopyOf((NodeImpl[])_children, size);
                }
            }
        }
    }
}