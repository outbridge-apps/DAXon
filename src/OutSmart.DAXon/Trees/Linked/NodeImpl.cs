////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Trees.Utilities;
using OutSmart.DAXon.Trees.Wrappers;
using OutSmart.DAXon.Values;
using System;
using System.Globalization;
using System.Collections.Generic;
using System.Text;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Text;
using System.Runtime.CompilerServices;
namespace OutSmart.DAXon.Trees.Linked
{
    public abstract class NodeImpl : IMutableNodeInfo, ISteppingNode, ISiblingCountingNode, ILocation
    {
        public static readonly char[] NODE_LETTER = new[]
        {
            'x',
            'e',
            'a',
            't',
            'x',
            'x',
            'x',
            'p',
            'c',
            'r',
            'x',
            'x',
            'x',
            'n'
        };
        private ParentNodeImpl parent;
        private int index; // Set to -1 when the node is deleted

        // Cached PhysicalRoot; re-parenting (a graft, Delete) clears it for the moved subtree.
        private DocumentImpl document;

        public virtual int Fingerprint
        {
            get
            {
                INodeName name = GetNodeName();
                if (name == null)
                {
                    return -1;
                }
                else
                {
                    return name.ObtainFingerprint(GetConfiguration().GetNamePool());
                }
            }
        }

        //
        //    }
        protected internal virtual long SequenceNumber
        {
            get
            {
                NodeImpl prev = this;
                for (int i = 0; ; i++)
                {
                    if (prev is ParentNodeImpl)
                    {
                        long prevseq = prev.SequenceNumber;
                        return prevseq == -1 ? prevseq : prevseq + 0x10000 + i; // note the 0x10000 is to leave room for namespace and attribute nodes.
                    }

                    prev = prev.PreviousInDocument;
                }
            }
        }

        //
        //    }
        public virtual string DisplayName
        {
            get
            {
                INodeName qName = GetNodeName();
                return qName == null ? "" : qName.DisplayName;
            }
        }

        //
        //    }
        public virtual NodeInfo LastChild => null;

        //
        //    }
        public virtual NodeInfo Root
        {
            get
            {
                DocumentImpl doc = PhysicalRoot;
                if (doc != null && !doc.IsImaginary())
                {
                    return doc;
                }

                // Under an imaginary document the root is the topmost element, found by a loop.
                NodeImpl top = this;
                for (NodeImpl up = GetParent(); up != null; up = up.GetParent())
                {
                    top = up;
                }

                return top;
            }
        }

        //
        //    }
        public virtual DocumentImpl PhysicalRoot
        {
            get
            {
                if (document != null)
                {
                    return document;
                }

                // Climb to the document or the first ancestor that knows it, then cache it on the way.
                DocumentImpl doc = null;
                for (NodeImpl up = parent; up != null; up = up.parent)
                {
                    doc = up as DocumentImpl ?? up.document;
                    if (doc != null)
                    {
                        break;
                    }
                }

                if (doc != null)
                {
                    for (NodeImpl n = this; n != null && n.document == null && !(n is DocumentImpl); n = n.parent)
                    {
                        n.document = doc;
                    }
                }

                return doc;
            }
        }

        //
        //    }
        public virtual NodeImpl PreviousInDocument
        {
            get
            {

                // finds the last child of the previous sibling if there is one;
                // otherwise the previous sibling element if there is one;
                // otherwise the parent, up to the anchor element.
                // If this reaches the document root, return null.
                NodeImpl prev = GetPreviousSibling();
                if (prev != null)
                {
                    return prev.LastDescendantOrSelf;
                }

                return GetParent();
            }
        }

        //
        //    }
        private NodeImpl LastDescendantOrSelf
        {
            get
            {
                NodeImpl last = (NodeImpl)LastChild;
                if (last == null)
                {
                    return this;
                }

                return last.LastDescendantOrSelf;
            }
        }

        //
        //    }
        public virtual NamespaceMap AllNamespaces => null;
        public abstract UnicodeString UnicodeStringValue { get; }
        public virtual NodeImpl Head()
        {
            return this;
        }

        //
        //    }
        public virtual ITreeInfo GetTreeInfo()
        {
            return PhysicalRoot;
        }

        public virtual ISchemaType GetSchemaType()
        {
            return Untyped.INSTANCE;
        }

        public virtual int GetColumnNumber()
        {
            if (parent == null)
            {
                return -1;
            }
            else
            {
                return parent.GetColumnNumber();
            }
        }

        public int GetSiblingPosition()
        {
            return index;
        }

        public void SetSiblingPosition(int index)
        {
            this.index = index;
        }

        public virtual IAtomicSequence Atomize()
        {
            ISchemaType stype = GetSchemaType();
            if (stype == Untyped.INSTANCE || stype == BuiltInAtomicType.UNTYPED_ATOMIC)
            {
                return StringValue.MakeUntypedAtomic(UnicodeStringValue);
            }
            else
            {
                return stype.Atomize(this);
            }
        }

        public virtual void SetSystemId(string uri)
        {

            // overridden in DocumentImpl and ElementImpl
            NodeInfo p = GetParent();
            if (p != null)
            {
                p.SetSystemId(uri);
            }
        }

        //
        //    }
        public override bool Equals(object other)
        {

            // default implementation: differs for attribute and namespace nodes
            return this == other;
        }

        //
        //    }
        public override int GetHashCode()
        {
            return RuntimeHelpers.GetHashCode(this);
        }

        public virtual INodeName GetNodeName()
        {
            return null;
        }

        public virtual bool HasFingerprint()
        {
            return true;
        }

        public virtual IAttributeMap Attributes()
        {
            return EmptyAttributeMap.GetInstance();
        }

        //
        //    }
        /// <summary>
        /// Get a character string that uniquely identifies this node
        /// </summary>
        public virtual void GenerateId(StringBuilder buffer)
        {
            long seq = SequenceNumber;
            if (seq == -1)
            {
                PhysicalRoot.GenerateId(buffer);
                buffer.Append(NODE_LETTER[GetNodeKind()]);
                buffer.Append(seq.ToString(CultureInfo.InvariantCulture)).Append('h').Append(GetHashCode().ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                parent.GenerateId(buffer);
                buffer.Append(NODE_LETTER[GetNodeKind()]);
                buffer.Append(index);
            }
        }

        //
        //    }
        /// <summary>
        /// Get the system ID for the node. Default implementation for child nodes.
        /// </summary>
        public virtual string GetSystemId()
        {
            return parent.GetSystemId();
        }

        //
        //    }
        public virtual string GetBaseURI()
        {
            return parent.GetBaseURI();
        }

        //
        //    }
        public int CompareOrder(NodeInfo other)
        {
            if (other is NamespaceNode)
            {
                return 0 - other.CompareOrder(this);
            }

            long a = SequenceNumber;
            long b = ((NodeImpl)other).SequenceNumber;
            if (a == -1 || b == -1)
            {

                // Nodes added by XQuery Update do not have sequence numbers
                return Navigator.CompareOrder(this, (NodeImpl)other);
            }

            return a.CompareTo(b);
        }

        //
        //    }
        public virtual Configuration GetConfiguration()
        {
            return PhysicalRoot.GetConfiguration();
        }

        //
        //    }
        public virtual NamePool GetNamePool()
        {
            return PhysicalRoot.GetNamePool();
        }

        //
        //    }
        public virtual string GetPrefix()
        {
            INodeName qName = GetNodeName();
            return qName == null ? "" : qName.GetPrefix();
        }

        //
        //    }
        public virtual NamespaceUri GetNamespaceUri()
        {
            INodeName qName = GetNodeName();
            return qName == null ? NamespaceUri.NULL : qName.GetNamespaceUri();
        }

        //
        //    }
        public virtual string GetLocalPart()
        {
            INodeName qName = GetNodeName();
            return qName == null ? "" : qName.GetLocalPart();
        }

        //
        //    }
        public virtual int GetLineNumber()
        {
            return parent.GetLineNumber();
        }

        //
        //    }
        public virtual ILocation SaveLocation()
        {
            return this;
        }

        //
        //    }
        public NodeImpl GetParent()
        {
            if (parent is DocumentImpl && ((DocumentImpl)parent).IsImaginary())
            {
                return null;
            }

            return parent;
        }

        //
        //    }
        protected ParentNodeImpl GetRawParent()
        {
            return parent;
        }

        //
        //    }
        public void SetRawParent(ParentNodeImpl parent)
        {
            if (this.parent != null && this.parent != parent)
            {
                ForgetDocument();
            }

            this.parent = parent;
            document = null;
        }

        // A subtree moved under another parent (grafted, deleted) may now hang under another document.
        private void ForgetDocument()
        {
            for (NodeImpl n = this; n != null; n = n.GetNextInDocument(this))
            {
                n.document = null;
            }
        }

        //
        //    }
        public virtual NodeImpl GetPreviousSibling()
        {
            if (parent == null)
            {
                return null;
            }

            return parent.GetNthChild(index - 1);
        }

        //
        //    }
        public virtual NodeImpl GetNextSibling()
        {
            if (parent == null)
            {
                return null;
            }

            return parent.GetNthChild(index + 1);
        }

        //
        //    }
        public virtual NodeImpl GetFirstChild()
        {
            return null;
        }

        //
        //    }
        public virtual IAxisIterator IterateAxis(int axisNumber)
        {

            // Fast path for child axis
            if (axisNumber == AxisInfo.CHILD)
            {
                if (this is ParentNodeImpl)
                {
                    return ((ParentNodeImpl)this).IterateChildren(null);
                }
                else
                {
                    return EmptyIterator.OfNodes();
                }
            }
            else
            {
                return IterateAxis(axisNumber, AnyNodeTest.GetInstance());
            }
        }

        //
        //    }
        public virtual IAxisIterator IterateAxis(int axisNumber, INodePredicate predicate)
        {
            NodeTest nodeTest = Navigator.NodeTestFromPredicate(predicate);
            switch (axisNumber)
            {
                case AxisInfo.ANCESTOR:
                    return new AncestorEnumeration(this, nodeTest, false);
                case AxisInfo.ANCESTOR_OR_SELF:
                    return new AncestorEnumeration(this, nodeTest, true);
                case AxisInfo.ATTRIBUTE:
                    if (GetNodeKind() != Types.Type.ELEMENT)
                    {
                        return EmptyIterator.OfNodes();
                    }
                    else
                    {
                        return ((ElementImpl)this).IterateAttributes(nodeTest);
                    }

                case AxisInfo.CHILD:
                    if (this is ParentNodeImpl)
                    {
                        return ((ParentNodeImpl)this).IterateChildren(nodeTest);
                    }
                    else
                    {
                        return EmptyIterator.OfNodes();
                    }

                case AxisInfo.DESCENDANT:
                    if (GetNodeKind() == Types.Type.DOCUMENT && nodeTest is NameTest && ((NameTest)nodeTest).PrimitiveType == Types.Type.ELEMENT)
                    {
                        return ((DocumentImpl)this).GetAllElements(((NameTest)nodeTest).Fingerprint);
                    }
                    else if (HasChildNodes())
                    {
                        return new DescendantAxisIterator(this, false, nodeTest);
                    }
                    else
                    {
                        return EmptyIterator.OfNodes();
                    }

                case AxisInfo.DESCENDANT_OR_SELF:
                    return new DescendantAxisIterator(this, true, nodeTest);
                case AxisInfo.FOLLOWING:
                    return new FollowingEnumeration(this, nodeTest);
                case AxisInfo.FOLLOWING_SIBLING:
                    return new FollowingSiblingEnumeration(this, nodeTest);
                case AxisInfo.NAMESPACE:
                    if (GetNodeKind() != Types.Type.ELEMENT)
                    {
                        return EmptyIterator.OfNodes();
                    }

                    return NamespaceNode.MakeIterator(this, nodeTest);
                case AxisInfo.PARENT:
                    NodeInfo parent = GetParent();
                    if (parent == null)
                    {
                        return EmptyIterator.OfNodes();
                    }

                    return Navigator.FilteredSingleton(parent, nodeTest);
                case AxisInfo.PRECEDING:
                    return new PrecedingEnumeration(this, nodeTest);
                case AxisInfo.PRECEDING_SIBLING:
                    return new PrecedingSiblingEnumeration(this, nodeTest);
                case AxisInfo.SELF:
                    return Navigator.FilteredSingleton(this, nodeTest);
                case AxisInfo.PRECEDING_OR_ANCESTOR:
                    return new PrecedingOrAncestorEnumeration(this, nodeTest);
                default:
                    throw new ArgumentException("Unknown axis number " + axisNumber);
            }
        }

        //
        //    }


        public virtual string GetAttributeValue(string uri, string localName) => default;

        public virtual string GetAttributeValue(NamespaceUri uri, string localName)
        {
            return null;
        }

        //
        //    }
        public virtual NodeImpl GetNextInDocument(NodeImpl anchor)
        {

            // find the first child node if there is one; otherwise the next sibling node
            // if there is one; otherwise the next sibling of the parent, grandparent, etc, up to the anchor element.
            // If this yields no result, return null.
            NodeImpl next = GetFirstChild();
            if (next != null)
            {
                return next;
            }

            if (this == anchor)
            {
                return null;
            }

            next = GetNextSibling();
            if (next != null)
            {
                return next;
            }

            NodeImpl parent = this;
            while (true)
            {
                parent = parent.GetParent();
                if (parent == null)
                {
                    return null;
                }

                if (parent == anchor)
                {
                    return null;
                }

                next = parent.GetNextSibling();
                if (next != null)
                {
                    return next;
                }
            }
        }

        //
        //    }
        public virtual NodeImpl GetSuccessorElement(ISteppingNode anchor, NamespaceUri uri, string local)
        {
            NodeImpl next = GetNextInDocument((NodeImpl)anchor);
            while (next != null && !(next.GetNodeKind() == Types.Type.ELEMENT && (uri == null || next.GetNodeName().HasURI(uri)) && (local == null || local.Equals(next.GetLocalPart()))))
            {
                next = next.GetNextInDocument((NodeImpl)anchor);
            }

            return next;
        }

        //
        //    }
        public virtual NamespaceBinding[] GetDeclaredNamespaces(NamespaceBinding[] buffer)
        {
            return null;
        }

        //
        //    }
        public virtual bool HasChildNodes()
        {
            return GetFirstChild() != null;
        }

        //
        //    }
        public virtual void SetTypeAnnotation(ISchemaType type)
        {
        }

        //
        //    }
        public virtual void Delete()
        {

            // Overridden for attribute nodes
            if (parent != null)
            {
                parent.RemoveChild(this);
                DocumentImpl newRoot = new DocumentImpl();
                newRoot.SetConfiguration(GetConfiguration());
                newRoot.SetImaginary(true);
                parent = newRoot;
                ForgetDocument();
            }

            index = -1;
        }

        //
        //    }
        public virtual bool IsDeleted()
        {
            return index == -1 || (parent != null && parent.IsDeleted());
        }

        //
        //    }
        public virtual void SetAttributes(IAttributeMap attributes)
        {
            throw new NotSupportedException("setAttributes() applies only to element nodes");
        }

        //
        //    }
        public virtual void RemoveAttribute(NodeInfo attribute)
        {
        }

        //
        //    }
        public virtual void AddAttribute(INodeName name, ISimpleType attType, string value, int properties, bool inheritNamespaces)
        {
        }

        //
        //    }
        public virtual void Rename(INodeName newNameCode, bool inherit)
        {
        }

        //
        //    }
        public virtual void AddNamespace(NamespaceBinding nscode, bool inherit)
        {
        }

        //
        //    }
        public virtual void Replace(NodeInfo[] replacement, bool inherit)
        {
            if (IsDeleted())
            {
                throw new InvalidOperationException("Cannot replace a deleted node");
            }

            if (GetParent() == null)
            {
                throw new InvalidOperationException("Cannot replace a parentless node");
            }

            parent.ReplaceChildrenAt(replacement, index, inherit);
            parent = null;
            index = -1; // mark the node as deleted
        }

        //
        //    }
        public virtual void InsertChildren(NodeInfo[] source, bool atStart, bool inherit)
        {
        }

        //
        //    }
        public virtual void InsertSiblings(NodeInfo[] source, bool before, bool inherit)
        {
            if (parent == null)
            {
                throw new InvalidOperationException("Cannot add siblings if there is no parent");
            }

            parent.InsertChildrenAt(source, before ? index : index + 1, inherit);
        }

        //
        //    }
        public virtual void RemoveTypeAnnotation()
        {
        }

        //
        //    }
        public virtual Builder NewBuilder()
        {
            return PhysicalRoot.NewBuilder();
        }

        //
        //    }
        public virtual bool EffectiveBooleanValue()
        {
            return true;
        }
        ISteppingNode ISteppingNode.GetParent() => GetParent();
        ISteppingNode ISteppingNode.GetNextSibling() => GetNextSibling();
        ISteppingNode ISteppingNode.GetPreviousSibling() => GetPreviousSibling();
        ISteppingNode ISteppingNode.GetFirstChild() => GetFirstChild();
        ISteppingNode ISteppingNode.GetSuccessorElement(ISteppingNode arg0, NamespaceUri arg1, string arg2) => GetSuccessorElement(arg0, arg1, arg2);
        NodeInfo NodeInfo.GetParent() => GetParent();
        IItem IGroundedValue.Head() => this;
        IItem ISequence.Head() => this;
        public abstract void ReplaceStringValue(UnicodeString arg0);
        public abstract int GetNodeKind();
        public virtual Genre GetGenre() => Genre.NODE; // upstream NodeInfo default
        // A node is a singleton grounded value (mirrors TinyNodeImpl; these were hollow NIE stubs —
        // a variable bound to a linked-tree node crashed VariableReference.Iterate, docbook-001).
        public virtual ISequenceIterator Iterate() => SingletonIterator.MakeIterator(this);
        public virtual IItem ItemAt(int arg0) => arg0 == 0 ? this : null;
        public virtual IGroundedValue Subsequence(int arg0, int arg1) => (arg0 <= 0 && (long)arg0 + arg1 > 0) ? (IGroundedValue)this : OutSmart.DAXon.Values.EmptySequence.GetInstance();
        public virtual int GetLength() => 1;
        public virtual string GetStringValue() => UnicodeStringValue.ToString();
        // Node-as-ActiveSource: stream this subtree's events (mirrors NodeSource; was a hollow NIE stub).
        public virtual void Deliver(IReceiver @out, ParseOptions options) => Events.Sender.SendDocumentInfo(this, @out, new Expressions.Parsing.Loc(GetSystemId(), -1, -1));
        public virtual string GetPublicId() => null; // upstream NodeInfo default
        IItem IItem.Head() => this;
        SingletonIterator IItem.Iterate() => new SingletonIterator(this);

        // Namespace mutation is defined only for elements (ElementImpl overrides); the old NIE
        // stubs here are now the upstream UnsupportedOperation contract, coded.
        public virtual void RemoveNamespace(string prefix) => throw new InvalidOperationException("Namespaces can only be removed from an element node");
        public virtual void AddNamespace(string prefix, NamespaceUri uri) => throw new InvalidOperationException("Namespaces can only be added to an element node");
        public virtual bool IsSameNodeInfo(NodeInfo other) => this == other; // node identity
        public virtual string GetURI() => GetNodeName() == null ? "" : GetNodeName().GetURI();
        public virtual IEnumerable<NodeInfo> Children() => new Navigator.ChildrenAsIterable(this);
        public virtual IEnumerable<NodeInfo> Children(INodePredicate filter) => new Navigator.ChildrenAsIterable(this, filter);
        public abstract void Copy(IReceiver @out, int copyOptions, ILocation locationId);
        public virtual IActiveSource AsActiveSource() => new NodeSource(this);
        public virtual bool IsId() => false; // upstream NodeInfo/Item default
        public virtual bool IsIdref() => false; // upstream NodeInfo/Item default
        public virtual bool IsNilled() => false; // upstream NodeInfo/Item default
        public virtual bool IsStreamed() => false; // upstream NodeInfo/Item default
        public virtual string ToShortString()
        {
            // upstream NodeInfo default toShortString()
            switch (GetNodeKind())
            {
                case OutSmart.DAXon.Types.Type.DOCUMENT: return "document-node()";
                case OutSmart.DAXon.Types.Type.ELEMENT: return "<" + DisplayName + "/>";
                case OutSmart.DAXon.Types.Type.ATTRIBUTE: return "@" + DisplayName;
                case OutSmart.DAXon.Types.Type.TEXT: return "text(\"" + OutSmart.DAXon.Transformation.Err.Truncate30(UnicodeStringValue) + "\")";
                case OutSmart.DAXon.Types.Type.COMMENT: return "<!--" + OutSmart.DAXon.Transformation.Err.Truncate30(UnicodeStringValue) + "-->";
                case OutSmart.DAXon.Types.Type.PROCESSING_INSTRUCTION: return "<?" + DisplayName + "?>";
                case OutSmart.DAXon.Types.Type.NAMESPACE:
                    string __prefix = GetLocalPart();
                    return "xmlns" + (__prefix.Length == 0 ? "" : ":" + __prefix) + "=\"" + UnicodeStringValue + "\"";
                default: return "";
            }
        }
        public virtual IGroundedValue Reduce() => this; // upstream GroundedValue default
        public virtual IGroundedValue Materialize() => this; // upstream GroundedValue default
        public virtual IEnumerable<IItem> AsIterable() => new IItem[] { this }; // singleton grounded value (upstream GroundedValue default for an Item)
        public virtual bool ContainsNode(NodeInfo sought) => OutSmart.DAXon.Expressions.SingletonIntersectExpression.ContainsNode(((OutSmart.DAXon.Model.ISequence)this).Iterate(), sought); // upstream GroundedValue default
        public virtual IGroundedValue Concatenate(IGroundedValue[] others)
        {
            // upstream GroundedValue default: chain this value's items with the others
            var __chain = new OutSmart.DAXon.Collections.Zeno.ZenoChain<OutSmart.DAXon.Model.IItem>().AddAll(((OutSmart.DAXon.Model.IGroundedValue)this).AsIterable());
            foreach (OutSmart.DAXon.Model.IGroundedValue __v in others)
                __chain = __chain.AddAll(__v.AsIterable());
            return new OutSmart.DAXon.Collections.Zeno.ZenoSequence(__chain);
        }
        public virtual ISequence MakeRepeatable() => this; // upstream Sequence.makeRepeatable default
    }
}

