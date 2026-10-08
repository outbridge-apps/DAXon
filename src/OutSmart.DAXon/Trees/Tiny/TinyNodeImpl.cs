////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Trees.Utilities;
using OutSmart.DAXon.Types;
using System;
using System.Globalization;
using System.Collections.Generic;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Trees.Tiny
{
    internal abstract class TinyNodeImpl : NodeInfo
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
        public readonly TinyTree tree;
        public readonly int nodeNr;
        protected internal TinyNodeImpl parent;

        public virtual long SequenceNumber => (long)nodeNr << 32;

        public virtual int Fingerprint
        {
            get
            {
                int nc = tree.nameCode[nodeNr];
                if (nc == -1)
                {
                    return -1;
                }

                return nc & NamePool.FP_MASK;
            }
        }

        public virtual string DisplayName
        {
            get
            {
                int code = tree.nameCode[nodeNr];
                if (code < 0)
                {
                    return "";
                }

                if (NamePool.IsPrefixed(code))
                {
                    return GetPrefix() + ":" + GetLocalPart();
                }
                else
                {
                    return GetLocalPart();
                }
            }
        }

        public virtual NodeInfo Root => nodeNr == 0 ? this : tree.GetRootNode();

        public virtual NamespaceMap AllNamespaces => null;

        public virtual TinyTree Tree => tree;

        public virtual int NodeNumber => nodeNr;
        public abstract UnicodeString UnicodeStringValue { get; }
        protected TinyNodeImpl(TinyTree tree, int nodeNr)
        {
            this.tree = tree;
            this.nodeNr = nodeNr;
        }

        public virtual Genre GetGenre()
        {
            return Genre.NODE;
        }

        public virtual ITreeInfo GetTreeInfo()
        {
            return tree;
        }

        public virtual ISchemaType GetSchemaType()
        {
            return null;
        }

        public virtual int GetColumnNumber()
        {
            return tree.GetColumnNumber(nodeNr);
        }

        public virtual void SetSystemId(string uri)
        {
            tree.SetSystemId(nodeNr, uri);
        }

        public virtual void SetParentNode(TinyNodeImpl parent)
        {
            this.parent = parent;
        }

        public virtual bool IsSameNodeInfo(NodeInfo other)
        {
            return this == other || (other is TinyNodeImpl && tree == ((TinyNodeImpl)other).tree && nodeNr == ((TinyNodeImpl)other).nodeNr && GetNodeKind() == other.GetNodeKind());
        }

        public override bool Equals(object other)
        {
            return other is NodeInfo && IsSameNodeInfo((NodeInfo)other);
        }

        public override int GetHashCode()
        {
            return ((int)(tree.GetDocumentNumber() & 0x3ff) << 20) ^ nodeNr ^ (GetNodeKind() << 14);
        }

        /// <summary>
        /// Get the system ID for the entity containing the node.
        /// </summary>
        public virtual string GetSystemId()
        {
            return tree.GetSystemId(nodeNr);
        }

        /// <summary>
        /// Get the system ID for the entity containing the node.
        /// </summary>
        public virtual string GetBaseURI()
        {
            return GetParent().GetBaseURI();
        }

        public virtual int GetLineNumber()
        {
            return tree.GetLineNumber(nodeNr);
        }

        public virtual ILocation SaveLocation()
        {
            return this;
        }

        public int CompareOrder(NodeInfo other)
        {
            long a = SequenceNumber;
            if (other is TinyNodeImpl)
            {
                long b = ((TinyNodeImpl)other).SequenceNumber;
                return a.CompareTo(b);
            }
            else
            {

                // it must be a namespace node, or a TinyTextualElementText node
                return 0 - other.CompareOrder(this);
            }
        }

        public bool HasFingerprint()
        {
            return true;
        }

        public virtual string GetPrefix()
        {
            int code = tree.nameCode[nodeNr];
            if (code < 0)
            {
                return "";
            }

            if (!NamePool.IsPrefixed(code))
            {
                return "";
            }

            return tree.prefixPool.GetPrefix(code >> 20);
        }

        public virtual NamespaceUri GetNamespaceUri()
        {
            int code = tree.nameCode[nodeNr];
            if (code < 0)
            {
                return NamespaceUri.NULL;
            }

            return tree.GetNamePool().GetURI(code & NamePool.FP_MASK);
        }

        public virtual bool HasURI(NamespaceUri ns)
        {
            int code = tree.nameCode[nodeNr];
            if (code < 0)
            {
                return false;
            }

            return GetNamePool().GetStructuredQName(code).HasURI(ns);
        }

        public virtual string GetLocalPart()
        {
            int code = tree.nameCode[nodeNr];
            if (code < 0)
            {
                return "";
            }

            return tree.GetNamePool().GetLocalName(code);
        }

        public virtual IAxisIterator IterateAxis(int axisNumber)
        {

            // fast path for child axis
            if (axisNumber == AxisInfo.CHILD)
            {
                if (HasChildNodes())
                {
                    return new SiblingIterator(tree, this, null, true);
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

        public virtual IAxisIterator IterateAxis(int axisNumber, INodePredicate predicate)
        {
            NodeTest nodeTest = Navigator.NodeTestFromPredicate(predicate);
            int type = GetNodeKind();
            switch (axisNumber)
            {
                case AxisInfo.ANCESTOR:
                    return new AncestorIterator(this, nodeTest);
                case AxisInfo.ANCESTOR_OR_SELF:
                    return IteratorANCESTOR(nodeTest);
                case AxisInfo.ATTRIBUTE:
                    return IteratorATTRIBUTE(type, nodeTest);
                case AxisInfo.CHILD:
                    return IteratorCHILD(nodeTest);
                case AxisInfo.DESCENDANT:
                    return IteratorDESCENDANT(type, nodeTest);
                case AxisInfo.DESCENDANT_OR_SELF:
                    return IteratorDESCENDANT_OR_SELF(nodeTest);
                case AxisInfo.FOLLOWING:
                    return IteratorFOLLOWING(type, nodeTest);
                case AxisInfo.FOLLOWING_SIBLING:
                    return IteratorFOLLOWING_SIBLING(type, nodeTest);
                case AxisInfo.NAMESPACE:
                    return IteratorNAMESPACE(type, nodeTest);
                case AxisInfo.PARENT:
                    return IteratorPARENT(nodeTest);
                case AxisInfo.PRECEDING:
                    return IteratorPRECEDING(type, axisNumber, nodeTest);
                case AxisInfo.PRECEDING_SIBLING:
                    return IteratorPRECEDING_SIBLING(type, nodeTest);
                case AxisInfo.SELF:
                    return Navigator.FilteredSingleton(this, nodeTest);
                case AxisInfo.PRECEDING_OR_ANCESTOR:
                    return IteratorPRECEDING_OR_ANCESTOR(type, nodeTest);
                default:
                    throw new ArgumentException("Unknown axis number " + axisNumber);
            }
        }

        private IAxisIterator IteratorANCESTOR(NodeTest nodeTest)
        {
            IAxisIterator ancestors = new AncestorIterator(this, nodeTest);
            if (nodeTest.Test(this))
            {
                return new PrependAxisIterator(this, ancestors);
            }
            else
            {
                return ancestors;
            }
        }

        private IAxisIterator IteratorATTRIBUTE(int type, NodeTest nodeTest)
        {
            if (type != Types.Type.ELEMENT)
            {
                return EmptyIterator.OfNodes();
            }

            if (tree.alpha[nodeNr] < 0)
            {
                return EmptyIterator.OfNodes();
            }

            return new AttributeIterator(tree, nodeNr, nodeTest);
        }

        private IAxisIterator IteratorCHILD(NodeTest nodeTest)
        {
            if (HasChildNodes())
            {
                if (nodeTest is NameTest && ((NameTest)nodeTest).GetNodeKind() == Types.Type.ELEMENT)
                {

                    // fast path for common case
                    return new NamedChildIterator(tree, this, ((NameTest)nodeTest).Fingerprint);
                }
                else
                {
                    return new SiblingIterator(tree, this, nodeTest, true);
                }
            }
            else
            {
                return EmptyIterator.OfNodes();
            }
        }

        private IAxisIterator IteratorDESCENDANT(int type, NodeTest nodeTest)
        {
            if (type == Types.Type.DOCUMENT && nodeTest is NameTest && nodeTest.PrimitiveType == Types.Type.ELEMENT)
            {
                return ((TinyDocumentImpl)this).GetAllElements(nodeTest.Fingerprint);
            }
            else if (HasChildNodes())
            {
                if (nodeTest.GetUType().Overlaps(UType.TEXT))
                {
                    return new DescendantIterator(tree, this, nodeTest);
                }
                else
                {
                    return new DescendantIteratorSansText(tree, this, nodeTest);
                }
            }
            else
            {
                return EmptyIterator.OfNodes();
            }
        }

        private IAxisIterator IteratorDESCENDANT_OR_SELF(NodeTest nodeTest)
        {
            IAxisIterator descendants = IterateAxis(AxisInfo.DESCENDANT, nodeTest);
            if (nodeTest.Test(this))
            {
                return new PrependAxisIterator(this, descendants);
            }
            else
            {
                return descendants;
            }
        }

        private IAxisIterator IteratorFOLLOWING(int type, NodeTest nodeTest)
        {
            if (type == Types.Type.ATTRIBUTE || type == Types.Type.NAMESPACE)
            {
                return new FollowingIterator(tree, (TinyNodeImpl)GetParent(), nodeTest, true);
            }
            else if (tree.depth[nodeNr] == 0)
            {
                return EmptyIterator.OfNodes();
            }
            else
            {
                return new FollowingIterator(tree, this, nodeTest, false);
            }
        }

        private IAxisIterator IteratorFOLLOWING_SIBLING(int type, NodeTest nodeTest)
        {
            if (type == Types.Type.ATTRIBUTE || type == Types.Type.NAMESPACE || tree.depth[nodeNr] == 0)
            {
                return EmptyIterator.OfNodes();
            }
            else
            {
                return new SiblingIterator(tree, this, nodeTest, false);
            }
        }

        private IAxisIterator IteratorNAMESPACE(int type, NodeTest nodeTest)
        {
            if (type != Types.Type.ELEMENT)
            {
                return EmptyIterator.OfNodes();
            }

            return NamespaceNode.MakeIterator(this, nodeTest);
        }

        private IAxisIterator IteratorPARENT(NodeTest nodeTest)
        {
            NodeInfo parent = GetParent();
            return Navigator.FilteredSingleton(parent, nodeTest);
        }

        private IAxisIterator IteratorPRECEDING(int type, int axisNumber, NodeTest nodeTest)
        {
            if (type == Types.Type.ATTRIBUTE || type == Types.Type.NAMESPACE)
            {
                return GetParent().IterateAxis(axisNumber, nodeTest);
            }
            else if (tree.depth[nodeNr] == 0)
            {
                return EmptyIterator.OfNodes();
            }
            else
            {
                return new PrecedingIterator(tree, this, nodeTest, false);
            }
        }

        private IAxisIterator IteratorPRECEDING_SIBLING(int type, NodeTest nodeTest)
        {
            if (type == Types.Type.ATTRIBUTE || type == Types.Type.NAMESPACE || tree.depth[nodeNr] == 0)
            {
                return EmptyIterator.OfNodes();
            }
            else
            {
                return new PrecedingSiblingIterator(tree, this, nodeTest);
            }
        }

        private IAxisIterator IteratorPRECEDING_OR_ANCESTOR(int type, NodeTest nodeTest)
        {
            if (type == Types.Type.DOCUMENT)
            {
                return EmptyIterator.OfNodes();
            }
            else if (type == Types.Type.ATTRIBUTE || type == Types.Type.NAMESPACE)
            {

                // See test numb32.
                TinyNodeImpl el = GetParent();
                return new PrependAxisIterator(el, new PrecedingIterator(tree, el, nodeTest, true));
            }
            else
            {
                return new PrecedingIterator(tree, this, nodeTest, true);
            }
        }

        public virtual TinyNodeImpl GetParent()
        {
            if (parent != null)
            {
                return parent;
            }

            lock (this)
            {
                if (parent == null)
                {
                    int p = GetParentNodeNr(tree, nodeNr);
                    if (p == -1)
                    {
                        return null;
                    }
                    else
                    {
                        return parent = tree.GetNode(p);
                    }
                }
                else
                {
                    return parent;
                }
            }
        }

        protected static int GetParentNodeNr(TinyTree tree, int nodeNr)
        {
            if (tree.depth[nodeNr] == 0)
            {
                return -1;
            }


            // follow the next-sibling pointers until we reach either a next sibling pointer that
            // points backwards, or a parent-pointer pseudo-node
            int p = tree.next[nodeNr];
            while (p > nodeNr)
            {
                if (tree.nodeKind[p] == Types.Type.PARENT_POINTER)
                {
                    return tree.alpha[p];
                }

                p = tree.next[p];
            }

            return p;
        }

        public virtual bool HasChildNodes()
        {

            // overridden in TinyParentNodeImpl
            return false;
        }

        public virtual string GetAttributeValue(NamespaceUri uri, string local)
        {
            return null;
        }

        public virtual Configuration GetConfiguration()
        {
            return tree.GetConfiguration();
        }

        public virtual NamePool GetNamePool()
        {
            return tree.GetNamePool();
        }

        public virtual NamespaceBinding[] GetDeclaredNamespaces(NamespaceBinding[] buffer)
        {
            return null;
        }

        public virtual void GenerateId(StringBuilder buffer)
        {
            buffer.Append('d');
            AppendIdDigits(buffer, tree.GetDocumentNumber());
            buffer.Append(NODE_LETTER[GetNodeKind()]);
            AppendIdDigits(buffer, nodeNr);
        }

        // StringBuilder.Append(long/int) on net472 formats through the CURRENT CULTURE (an NLS call
        // per append); ids are plain non-negative decimals, so write the digits directly.
        internal static void AppendIdDigits(StringBuilder buffer, long value)
        {
            if (value < 0)
            {
                buffer.Append(value.ToString(CultureInfo.InvariantCulture));   // never happens for document/node numbers; keep the old path
                return;
            }

            if (value >= 10)
            {
                AppendIdDigits(buffer, value / 10);
            }

            buffer.Append((char)('0' + (int)(value % 10)));
        }

        public virtual bool IsAncestorOrSelf(TinyNodeImpl d)
        {

            // If it's a different tree, return false
            if (tree != d.tree)
            {
                return false;
            }

            int dn = d.nodeNr;

            // If d is an attribute, then either "this" must be the same attribute, or "this" must
            // be an ancestor-or-self of the parent of d.
            if (d is TinyAttributeImpl)
            {
                if (this is TinyAttributeImpl)
                {
                    return nodeNr == dn;
                }
                else
                {
                    dn = tree.attParent[dn];
                }
            }


            // If this is an attribute, return false (we've already handled the case where it's the same attribute)
            if (this is TinyAttributeImpl)
            {
                return false;
            }


            // From now on, we know that both "this" and "dn" are nodes in the primary array
            // If this node is later in document order, return false
            if (nodeNr > dn)
            {
                return false;
            }


            // If it's the same node, return true
            if (nodeNr == dn)
            {
                return true;
            }


            // We've dealt with the "self" case: to be an ancestor, it must be an element or document node
            if (!(this is TinyParentNodeImpl))
            {
                return false;
            }


            // If this node is deeper than the target node then it can't be an ancestor
            if (tree.depth[nodeNr] >= tree.depth[dn])
            {
                return false;
            }


            // The following code will exit as soon as we find an ancestor that has a following-sibling:
            // when that happens, we know it's an ancestor iff its following-sibling is beyond the node we're
            // looking for. If the ancestor has no following sibling, we go up a level.
            // The algorithm depends on the following assertion: if A is before D in document order, then
            // either A is an ancestor of D, or some ancestor-or-self of A has a following-sibling that
            // is before-or-equal to D in document order.
            int n = nodeNr;
            while (true)
            {
                int nextSib = tree.next[n];
                if (nextSib < 0 || nextSib > dn)
                {
                    return true;
                }
                else if (tree.depth[nextSib] == 0)
                {
                    return true;
                }
                else if (nextSib < n)
                {
                    n = nextSib; // continue
                }
                else
                {
                    return false;
                }
            }
        }

        public virtual bool IsId()
        {
            return false; // overridden for element and attribute nodes
        }

        public virtual bool IsIdref()
        {
            return false; // overridden for element and attribute nodes
        }

        public virtual bool IsNilled()
        {
            return tree.IsNilled(nodeNr);
        }

        public virtual bool IsStreamed()
        {
            return false;
        }
        NodeInfo NodeInfo.GetParent() => GetParent();
        IItem IGroundedValue.Head() => this;
        IItem ISequence.Head() => this;
        public abstract int GetNodeKind();
        public virtual IAtomicSequence Atomize() => StringValue.MakeUntypedAtomic(UnicodeStringValue);
        public virtual ISequenceIterator Iterate() => SingletonIterator.MakeIterator(this);
        // A node is a singleton grounded value: item 0 is the node itself, every other index is absent.
        // (Was a hollow NIE stub; SubscriptExpression on e.g. `$node/self::x[1000]` hit it.)
        public virtual IItem ItemAt(int arg0) => arg0 == 0 ? this : null;
        public virtual IGroundedValue Subsequence(int arg0, int arg1) => (arg0 <= 0 && (long)arg0 + arg1 > 0) ? (IGroundedValue)this : OutSmart.DAXon.Values.EmptySequence.GetInstance();
        public virtual int GetLength() => 1;
        public virtual string GetStringValue() => UnicodeStringValue.ToString(); // upstream NodeInfo default method
        public virtual string GetPublicId() => null;
        public virtual void Deliver(IReceiver @out, ParseOptions options) => Events.Sender.SendDocumentInfo(this, @out, new Expressions.Parsing.Loc(GetSystemId(), -1, -1));
        IItem IItem.Head() => this;
        SingletonIterator IItem.Iterate() => new SingletonIterator(this);

        // === Auto-generated stubs (StubGenerator Phase 3.1f) ===
        public virtual string GetURI() => GetNamespaceUri() == null ? "" : GetNamespaceUri().ToString();
        public virtual IEnumerable<NodeInfo> Children() { var __it = IterateAxis(AxisInfo.CHILD); for (var __n = __it.Next(); __n != null; __n = __it.Next()) { yield return __n; } }
        public virtual IEnumerable<NodeInfo> Children(INodePredicate filter) { var __it = IterateAxis(AxisInfo.CHILD, filter); for (var __n = __it.Next(); __n != null; __n = __it.Next()) { yield return __n; } }
        public virtual IAttributeMap Attributes() // upstream NodeInfo default method
        {
            if (GetNodeKind() != Types.Type.ELEMENT)
            {
                return EmptyAttributeMap.GetInstance();
            }
            IAttributeMap atts = EmptyAttributeMap.GetInstance();
            IAxisIterator iter = IterateAxis(AxisInfo.ATTRIBUTE);
            NodeInfo attr;
            while ((attr = iter.Next()) != null)
            {
                atts = atts.Put(new AttributeInfo(NameOfNode.MakeName(attr), (ISimpleType)attr.GetSchemaType(), attr.GetStringValue(), Loc.NONE, ReceiverOption.NONE));
            }
            return atts;
        }
        public virtual void Copy(IReceiver @out, int copyOptions, ILocation locationId) => Navigator.Copy(this, @out, copyOptions, locationId); // upstream NodeInfo default
        public virtual IActiveSource AsActiveSource() => new NodeSource(this); // upstream NodeInfo default method
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
        public virtual IGroundedValue Reduce() => this; // upstream GroundedValue default method
        public virtual bool EffectiveBooleanValue() => ExpressionTool.EffectiveBooleanValue(Iterate()); // upstream GroundedValue default method
        public virtual IGroundedValue Materialize() => this;
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
        public virtual ISequence MakeRepeatable() => this;
    }
}


