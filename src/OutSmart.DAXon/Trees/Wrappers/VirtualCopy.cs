////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Trees.Utilities;
using OutSmart.DAXon.Types;
using System;
using System.Collections.Generic;
using System.Text;

namespace OutSmart.DAXon.Trees.Wrappers
{
    // Faithful port of net.sf.saxon.tree.wrapper.VirtualCopy (Saxon 12.9). Was a hollow stub, so the entire
    // lazy (pull-mode) xsl:copy-of path in CopyOf crashed for validation=preserve, and the accumulator
    // copy-propagation path could never see an original node.
    // A node that is a virtual copy of another node: same content, different identity, with the parent axis
    // truncated at the copied subtree root.
    internal class VirtualCopy : NodeInfo
    {
        protected internal Func<string> systemIdSupplier;
        protected internal NodeInfo original;
        protected internal VirtualCopy parent;
        protected internal VirtualTreeInfo tree;
        protected internal NodeInfo root; // the node forming the root of the subtree that was copied
        private bool dropNamespaces = false;

        public virtual NodeInfo OriginalNode => original;

        public virtual NamespaceMap AllNamespaces
        {
            get
            {
                if (GetNodeKind() == OutSmart.DAXon.Types.Type.ELEMENT)
                {
                    if (dropNamespaces)
                    {
                        NamespaceMap nsMap = NamespaceMap.EmptyMap();
                        NamespaceUri ns = GetNamespaceUri();
                        if (!ns.IsEmpty())
                        {
                            nsMap = nsMap.Put(GetPrefix(), ns);
                        }

                        IAxisIterator iter = original.IterateAxis(AxisInfo.ATTRIBUTE);
                        NodeInfo att;
                        while ((att = iter.Next()) != null)
                        {
                            if (!att.GetNamespaceUri().IsEmpty())
                            {
                                nsMap = nsMap.Put(att.GetPrefix(), att.GetNamespaceUri());
                            }
                        }

                        return nsMap;
                    }
                    else
                    {
                        return original.AllNamespaces;
                    }
                }
                else
                {
                    return null;
                }
            }
        }

        public virtual int Fingerprint => original.Fingerprint;

        public virtual UnicodeString UnicodeStringValue => original.UnicodeStringValue;
        public virtual string DisplayName => original.DisplayName;

        public virtual NodeInfo Root
        {
            get
            {
                NodeInfo n = this;
                while (true)
                {
                    NodeInfo p = n.GetParent();
                    if (p == null)
                    {
                        return n;
                    }

                    n = p;
                }
            }
        }

        protected VirtualCopy(NodeInfo @base, NodeInfo root)
        {
            original = @base;
            // computing the base URI can be expensive, so do it lazily
            systemIdSupplier = @base.GetBaseURI;
            this.root = root;
        }

        /// <summary>
        /// Public factory method: create a parentless virtual tree as a copy of an existing node
        /// </summary>
        public static VirtualCopy MakeVirtualCopy(NodeInfo original)
        {
            VirtualCopy vc;
            // Don't allow copies of copies of copies: define the new copy in terms of the original
            while (original is VirtualCopy)
            {
                original = ((VirtualCopy)original).original;
            }

            vc = new VirtualCopy(original, original);
            Configuration config = original.GetConfiguration();
            VirtualTreeInfo doc = new VirtualTreeInfo(config, vc);
            long docNr = config.DocumentNumberAllocator.AllocateDocumentNumber();
            doc.SetDocumentNumber(docNr);
            vc.tree = doc;
            return vc;
        }

        /// <summary>
        /// Wrap a node within an existing VirtualCopy.
        /// </summary>
        protected virtual VirtualCopy Wrap(NodeInfo node)
        {
            VirtualCopy vc = new VirtualCopy(node, root);
            vc.tree = tree;
            vc.systemIdSupplier = systemIdSupplier;
            vc.dropNamespaces = dropNamespaces;
            return vc;
        }

        public virtual VirtualTreeInfo GetTreeInfo() => tree;
        ITreeInfo NodeInfo.GetTreeInfo() => tree;

        /// <summary>
        /// Say that namespaces in the virtual tree should not be copied from the underlying tree
        /// (the xsl:copy-of copy-namespaces="no" semantics).
        /// </summary>
        public virtual void SetDropNamespaces(bool drop)
        {
            this.dropNamespaces = drop;
        }
        public virtual bool HasFingerprint() => original.HasFingerprint();
        public virtual int GetNodeKind() => original.GetNodeKind();

        public override bool Equals(object other)
        {
            return other is VirtualCopy
                && GetTreeInfo() == ((VirtualCopy)other).GetTreeInfo()
                && original.Equals(((VirtualCopy)other).original);
        }

        public override int GetHashCode()
        {
            return original.GetHashCode() ^ ((int)(GetTreeInfo().GetDocumentNumber() & 0x7fffffff) << 19);
        }

        public virtual bool IsSameNodeInfo(NodeInfo other) => Equals(other);

        public virtual string GetSystemId() => systemIdSupplier();
        public virtual string GetPublicId() => original != null ? original.GetPublicId() : null;
        public virtual string GetBaseURI() => Navigator.GetBaseURI(this);
        public virtual int GetLineNumber() => original.GetLineNumber();
        public virtual int GetColumnNumber() => original.GetColumnNumber();
        public virtual ILocation SaveLocation() => this;

        public virtual int CompareOrder(NodeInfo other)
        {
            if (other is VirtualCopy)
            {
                int c = root.CompareOrder(((VirtualCopy)other).root);
                if (c == 0)
                {
                    return original.CompareOrder(((VirtualCopy)other).original);
                }
                else
                {
                    return c;
                }
            }
            else
            {
                return other.CompareOrder(original);
            }
        }
        public virtual string GetStringValue() => original.GetStringValue();
        public virtual Genre GetGenre() => Genre.NODE;

        public virtual string GetLocalPart() => original.GetLocalPart();
        public virtual NamespaceUri GetNamespaceUri() => original.GetNamespaceUri();
        public virtual string GetURI() => GetNamespaceUri().ToString();
        public virtual string GetPrefix() => original.GetPrefix();
        public virtual Configuration GetConfiguration() => original.GetConfiguration();
        public virtual ISchemaType GetSchemaType() => original.GetSchemaType();

        public virtual NodeInfo GetParent()
        {
            if (original.Equals(root))
            {
                return null;
            }

            if (parent == null)
            {
                NodeInfo basep = original.GetParent();
                if (basep == null)
                {
                    return null;
                }

                parent = Wrap(basep);
            }

            return parent;
        }

        public virtual IAxisIterator IterateAxis(int axisNumber) => IterateAxis(axisNumber, AnyNodeTest.GetInstance());

        public virtual IAxisIterator IterateAxis(int axisNumber, INodePredicate nodeTest)
        {
            VirtualCopy newParent = null;
            switch (axisNumber)
            {
                case AxisInfo.CHILD:
                case AxisInfo.ATTRIBUTE:
                    newParent = this;
                    break;
                case AxisInfo.SELF:
                case AxisInfo.PRECEDING_SIBLING:
                case AxisInfo.FOLLOWING_SIBLING:
                    newParent = parent;
                    break;
                // Ensure that the ancestor, ancestor-or-self, following, and preceding axes use an implementation
                // that relies on GetParent() to escape from the subtree
                case AxisInfo.ANCESTOR:
                    return new Navigator.AxisFilter(new Navigator.AncestorEnumeration(this, false), nodeTest);
                case AxisInfo.ANCESTOR_OR_SELF:
                    return new Navigator.AxisFilter(new Navigator.AncestorEnumeration(this, true), nodeTest);
                case AxisInfo.NAMESPACE:
                    if (GetNodeKind() != OutSmart.DAXon.Types.Type.ELEMENT)
                    {
                        return EmptyIterator.OfNodes();
                    }

                    return NamespaceNode.MakeIterator(this, nodeTest);
                case AxisInfo.PARENT:
                    return Navigator.FilteredSingleton(GetParent(), nodeTest);
                case AxisInfo.PRECEDING:
                    return new Navigator.AxisFilter(new Navigator.PrecedingEnumeration(this, false), nodeTest);
                case AxisInfo.FOLLOWING:
                    return new Navigator.AxisFilter(new Navigator.FollowingEnumeration(this), nodeTest);
                case AxisInfo.PRECEDING_OR_ANCESTOR:
                    return new Navigator.AxisFilter(new Navigator.PrecedingEnumeration(this, true), nodeTest);
            }

            return MakeCopier(original.IterateAxis(axisNumber, nodeTest), newParent, !AxisInfo.isSubtreeAxis[axisNumber]);
        }

        public virtual string GetAttributeValue(NamespaceUri uri, string local) => original.GetAttributeValue(uri, local);

        public virtual bool HasChildNodes() => original.HasChildNodes();

        public virtual IEnumerable<NodeInfo> Children()
        {
            IAxisIterator it = IterateAxis(AxisInfo.CHILD);
            NodeInfo n;
            while ((n = it.Next()) != null)
            {
                yield return n;
            }
        }

        public virtual IEnumerable<NodeInfo> Children(INodePredicate filter)
        {
            IAxisIterator it = IterateAxis(AxisInfo.CHILD, filter);
            NodeInfo n;
            while ((n = it.Next()) != null)
            {
                yield return n;
            }
        }

        public virtual IAttributeMap Attributes()
        {
            if (GetNodeKind() == OutSmart.DAXon.Types.Type.ELEMENT)
            {
                List<AttributeInfo> list = new List<AttributeInfo>();
                IAxisIterator iter = IterateAxis(AxisInfo.ATTRIBUTE);
                NodeInfo attr;
                while ((attr = iter.Next()) != null)
                {
                    list.Add(new AttributeInfo(NameOfNode.MakeName(attr), (ISimpleType)attr.GetSchemaType(),
                        attr.GetStringValue(), Loc.NONE, ReceiverOption.NONE));
                }

                return SequenceTool.AttributeMapFromList(list);
            }

            return EmptyAttributeMap.GetInstance();
        }

        public virtual void GenerateId(StringBuilder buffer)
        {
            buffer.Append('d');
            buffer.Append(GetTreeInfo().GetDocumentNumber());
            original.GenerateId(buffer);
        }

        public virtual void Copy(IReceiver @out, int copyOptions, ILocation locationId)
        {
            if (dropNamespaces)
            {
                copyOptions &= ~CopyOptions.ALL_NAMESPACES;
            }

            original.Copy(@out, copyOptions, locationId);
        }

        public virtual NamespaceBinding[] GetDeclaredNamespaces(NamespaceBinding[] buffer)
        {
            if (GetNodeKind() == OutSmart.DAXon.Types.Type.ELEMENT)
            {
                if (dropNamespaces)
                {
                    List<NamespaceBinding> allNamespaces = new List<NamespaceBinding>(5);
                    NamespaceUri ns = GetNamespaceUri();
                    if (ns.IsEmpty())
                    {
                        if (GetParent() != null && !GetParent().GetNamespaceUri().IsEmpty())
                        {
                            allNamespaces.Add(new NamespaceBinding("", NamespaceUri.NULL));
                        }
                    }
                    else
                    {
                        allNamespaces.Add(new NamespaceBinding(GetPrefix(), GetNamespaceUri()));
                    }

                    foreach (AttributeInfo att in original.Attributes())
                    {
                        INodeName name = att.GetNodeName();
                        if (name.GetNamespaceUri() != null)
                        {
                            NamespaceBinding b = new NamespaceBinding(name.GetPrefix(), name.GetNamespaceUri());
                            if (!allNamespaces.Contains(b))
                            {
                                allNamespaces.Add(b);
                            }
                        }
                    }

                    return allNamespaces.ToArray();
                }
                else
                {
                    if (original == root)
                    {
                        List<NamespaceBinding> bindings = new List<NamespaceBinding>();
                        foreach (NamespaceBinding binding in original.AllNamespaces)
                        {
                            bindings.Add(binding);
                        }

                        return bindings.ToArray();
                    }
                    else
                    {
                        return original.GetDeclaredNamespaces(buffer);
                    }
                }
            }
            else
            {
                return null;
            }
        }

        public virtual void SetSystemId(string systemId)
        {
            this.systemIdSupplier = () => systemId;
        }

        public virtual IAtomicSequence Atomize() => original.Atomize();
        public virtual bool IsId() => original.IsId();
        public virtual bool IsIdref() => original.IsIdref();
        public virtual bool IsNilled() => original.IsNilled();
        public virtual bool IsStreamed() => false;
        public virtual string ToShortString() => original.ToShortString();

        public virtual void Deliver(IReceiver receiver, ParseOptions options) => receiver.Append(this);
        public virtual IActiveSource AsActiveSource() => new NodeSource(this);

        /// <summary>
        /// Ask whether a node in the source tree is within the scope of this virtual copy
        /// </summary>
        protected internal virtual bool IsIncludedInCopy(NodeInfo sourceNode)
        {
            return Navigator.IsAncestorOrSelf(root, sourceNode);
        }

        /// <summary>
        /// Create an iterator that makes and returns virtual copies of nodes on the original tree
        /// </summary>
        protected virtual VirtualCopier MakeCopier(IAxisIterator axis, VirtualCopy newParent, bool testInclusion)
        {
            return new VirtualCopier(this, axis, newParent, testInclusion);
        }

        // IItem / IGroundedValue singleton defaults
        public virtual ISequenceIterator Iterate() => SingletonIterator.MakeIterator(this);
        public virtual IItem ItemAt(int n) => n == 0 ? this : null;
        public virtual int GetLength() => 1;
        public virtual IGroundedValue Reduce() => this;
        public virtual IGroundedValue Materialize() => this;
        public virtual bool EffectiveBooleanValue() => true;
        public virtual IEnumerable<IItem> AsIterable() => new IItem[] { this };
        public virtual bool ContainsNode(NodeInfo sought) => sought != null && IsSameNodeInfo(sought);
        public virtual ISequence MakeRepeatable() => this;
        public virtual IGroundedValue Subsequence(int start, int length) => (start <= 0 && (long)start + length > 0) ? (IGroundedValue)this : OutSmart.DAXon.Values.EmptySequence.GetInstance(); // singleton item (upstream GroundedValue default)
        public virtual IGroundedValue Concatenate(IGroundedValue[] others)
        {
            // upstream GroundedValue default: chain this value's items with the others
            var __chain = new OutSmart.DAXon.Collections.Zeno.ZenoChain<OutSmart.DAXon.Model.IItem>().AddAll(((OutSmart.DAXon.Model.IGroundedValue)this).AsIterable());
            foreach (OutSmart.DAXon.Model.IGroundedValue __v in others)
                __chain = __chain.AddAll(__v.AsIterable());
            return new OutSmart.DAXon.Collections.Zeno.ZenoSequence(__chain);
        }

        // net472 explicit interface bridges (no covariant interface returns) — mirror TinyNodeImpl
        IItem IItem.Head() => this;
        IItem IGroundedValue.Head() => this;
        IItem ISequence.Head() => this;
        SingletonIterator IItem.Iterate() => new SingletonIterator(this);

        /// <summary>
        /// VirtualCopier implements the XPath axes as applied to a VirtualCopy node, copying each node found
        /// on the underlying axis and truncating axes that stray outside the copied subtree.
        /// </summary>
        protected internal sealed class VirtualCopier : IAxisIterator
        {
            protected VirtualCopy node;
            protected IAxisIterator @base;
            private readonly VirtualCopy parent;
            protected bool testInclusion;

            public VirtualCopier(VirtualCopy node, IAxisIterator @base, VirtualCopy parent, bool testInclusion)
            {
                this.node = node;
                this.@base = @base;
                this.parent = parent;
                this.testInclusion = testInclusion;
            }

            public NodeInfo Next()
            {
                NodeInfo next = @base.Next();
                if (next != null)
                {
                    if (testInclusion && !node.IsIncludedInCopy(next))
                    {
                        // we're only interested in nodes within the subtree that was copied.
                        // Assert: once we find a node outside this subtree, all further nodes will also be outside
                        //         the subtree.
                        return null;
                    }

                    VirtualCopy vc = node.Wrap(next);
                    vc.parent = parent;
                    vc.systemIdSupplier = node.systemIdSupplier;
                    next = vc;
                }

                return next;
            }

            IItem ISequenceIterator.Next() => Next();

            public void Dispose()
            {
                @base.Dispose();
            }
        }
    }
}
