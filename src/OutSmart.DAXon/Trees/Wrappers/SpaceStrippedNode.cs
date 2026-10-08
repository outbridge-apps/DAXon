////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;

namespace OutSmart.DAXon.Trees.Wrappers
{
    // Faithful port of net.sf.saxon.tree.wrapper.SpaceStrippedNode (Saxon 12.9). Was a hollow stub (in the
    // WRONG namespace OutSmart.DAXon.Core, root of src) whose IsPreservedNode always returned true.
    // A view of a node in a virtual tree with whitespace text nodes stripped: axis iterations skip
    // whitespace-only text nodes that xsl:strip-space says should be absent.
    internal sealed class SpaceStrippedNode : AbstractVirtualNode, IWrappingFunction
    {

        public override UnicodeString UnicodeStringValue
        {
            get
            {
                // Might not be the same as the string value of the underlying node because of space stripping
                switch (GetNodeKind())
                {
                    case OutSmart.DAXon.Types.Type.DOCUMENT:
                    case OutSmart.DAXon.Types.Type.ELEMENT:
                        IAxisIterator iter = IterateAxis(AxisInfo.DESCENDANT, NodeKindTest.MakeNodeKindTest(OutSmart.DAXon.Types.Type.TEXT));
                        UnicodeBuilder sb = new UnicodeBuilder();
                        NodeInfo it;
                        while ((it = iter.Next()) != null)
                        {
                            sb.Accept(it.UnicodeStringValue);
                        }

                        return sb.ToUnicodeString();
                    default:
                        return node.UnicodeStringValue;
                }
            }
        }

        protected SpaceStrippedNode(NodeInfo node, SpaceStrippedNode parent)
        {
            this.node = node;
            this.parent = parent;
        }

        protected internal static SpaceStrippedNode MakeWrapper(NodeInfo node, SpaceStrippedDocument docWrapper, SpaceStrippedNode parent)
        {
            SpaceStrippedNode wrapper = new SpaceStrippedNode(node, parent);
            wrapper.docWrapper = docWrapper;
            return wrapper;
        }

        public IVirtualNode MakeWrapper(NodeInfo node, IVirtualNode parent)
        {
            SpaceStrippedNode wrapper = new SpaceStrippedNode(node, (SpaceStrippedNode)parent);
            wrapper.docWrapper = this.docWrapper;
            return wrapper;
        }

        // Whiteness without materializing the text: tiny nodes (real and textual-element virtual)
        // scan the shared text buffer in place; the generic path keeps the old string route.
        private static bool IsAllWhiteText(NodeInfo node)
        {
            if (node is Tiny.WhitespaceTextImpl)
            {
                return true;   // compressed whitespace: all-white by construction
            }

            if (node is Tiny.TinyTextImpl tt)
            {
                return Tiny.TinyTextImpl.IsWhitespaceOnly(tt.tree, tt.nodeNr);
            }

            if (node is Tiny.TinyTextualElement.TinyTextualElementText tet)
            {
                return tet.IsWhitespaceOnly;
            }

            return Whitespace.IsAllWhite(node.UnicodeStringValue);
        }

        /// <summary>
        /// Ask whether a node is preserved after whitespace stripping
        /// </summary>
        public static bool IsPreservedNode(NodeInfo node, SpaceStrippedDocument docWrapper, NodeInfo actualParent)
        {
            // Non-text nodes, non-whitespace nodes, and parentless nodes are preserved
            if (node.GetNodeKind() != OutSmart.DAXon.Types.Type.TEXT || actualParent == null || !IsAllWhiteText(node))
            {
                return true;
            }

            // if the node has a simple type annotation, it is preserved
            ISchemaType type = actualParent.GetSchemaType();
            if (type.IsSimpleType() || ((IComplexType)type).IsSimpleContent())
            {
                return true;
            }

            // if there is an ancestor with xml:space="preserve", it is preserved
            if (docWrapper.ContainsPreserveSpace())
            {
                NodeInfo p = actualParent;
                // the document contains one or more xml:space="preserve" attributes, so we need to see
                // if one of them is on an ancestor of this node
                while (p.GetNodeKind() == OutSmart.DAXon.Types.Type.ELEMENT)
                {
                    string val = p.GetAttributeValue(NamespaceUri.XML, "space");
                    if (val != null)
                    {
                        if (val == "preserve")
                        {
                            return true;
                        }
                        else if (val == "default")
                        {
                            break;
                        }
                    }

                    p = p.GetParent();
                }
            }

            // if there is an ancestor whose type has an assertion, it is preserved
            if (docWrapper.ContainsAssertions())
            {
                NodeInfo p = actualParent;
                while (p.GetNodeKind() == OutSmart.DAXon.Types.Type.ELEMENT)
                {
                    ISchemaType t = p.GetSchemaType();
                    if (t is IComplexType && ((IComplexType)t).HasAssertions())
                    {
                        return true;
                    }

                    p = p.GetParent();
                }
            }

            // otherwise it depends on xsl:strip-space
            return docWrapper.PreservedByRule(actualParent);
        }

        public override IAtomicSequence Atomize()
        {
            if (GetNodeKind() == OutSmart.DAXon.Types.Type.ELEMENT)
            {
                return GetSchemaType().Atomize(this);
            }
            else
            {
                return node.Atomize();
            }
        }

        public override bool Equals(object other)
        {
            if (other is SpaceStrippedNode)
            {
                return node.Equals(((SpaceStrippedNode)other).node);
            }
            else
            {
                return node.Equals(other);
            }
        }

        public override int GetHashCode()
        {
            return node.GetHashCode();
        }

        public override int CompareOrder(NodeInfo other)
        {
            if (other is SpaceStrippedNode)
            {
                return node.CompareOrder(((SpaceStrippedNode)other).node);
            }
            else
            {
                return node.CompareOrder(other);
            }
        }

        public override NodeInfo GetParent()
        {
            if (parent == null)
            {
                NodeInfo realParent = node.GetParent();
                if (realParent != null)
                {
                    parent = MakeWrapper(realParent, (SpaceStrippedDocument)docWrapper, null);
                }
            }

            return parent;
        }

        public override IAxisIterator IterateAxis(int axisNumber, INodePredicate nodeTest)
        {
            if (nodeTest is NodeTest && ((NodeTest)nodeTest).GetUType().Intersection(UType.TEXT) == UType.VOID
                || axisNumber == AxisInfo.ATTRIBUTE || axisNumber == AxisInfo.NAMESPACE)
            {
                // iteration does not include text nodes, so no stripping needed
                return new WrappingIterator(node.IterateAxis(axisNumber, nodeTest), this, GetParentForAxis(axisNumber));
            }
            else
            {
                return new StrippingIterator(node.IterateAxis(axisNumber, nodeTest), (SpaceStrippedDocument)docWrapper, GetParentForAxis(axisNumber));
            }
        }

        public override IAxisIterator IterateAxis(int axisNumber)
        {
            switch (axisNumber)
            {
                case AxisInfo.ATTRIBUTE:
                case AxisInfo.NAMESPACE:
                    return new WrappingIterator(node.IterateAxis(axisNumber), this, this);
                case AxisInfo.CHILD:
                    return new StrippingIterator(node.IterateAxis(axisNumber), (SpaceStrippedDocument)docWrapper, this);
                case AxisInfo.FOLLOWING_SIBLING:
                case AxisInfo.PRECEDING_SIBLING:
                    SpaceStrippedNode parent = (SpaceStrippedNode)GetParent();
                    if (parent == null)
                    {
                        return EmptyIterator.OfNodes();
                    }
                    else
                    {
                        return new StrippingIterator(node.IterateAxis(axisNumber), (SpaceStrippedDocument)docWrapper, parent);
                    }

                default:
                    return new StrippingIterator(node.IterateAxis(axisNumber), (SpaceStrippedDocument)docWrapper, null);
            }
        }

        private SpaceStrippedNode GetParentForAxis(int axisNumber)
        {
            switch (axisNumber)
            {
                case AxisInfo.CHILD:
                case AxisInfo.ATTRIBUTE:
                case AxisInfo.NAMESPACE:
                    return this;
                case AxisInfo.FOLLOWING_SIBLING:
                case AxisInfo.PRECEDING_SIBLING:
                    return (SpaceStrippedNode)GetParent();
                default:
                    return null;
            }
        }

        public override void Copy(IReceiver @out, int copyOptions, ILocation locationId)
        {
            // The underlying code does not do whitespace stripping. So we need to interpose
            // a stripper. Moreover, if the node is typed and we are removing type annotations,
            // then we need to take care that we're not applying space-stripping to the untyped
            // version of the document (test case strip-space-008)
            IReceiver temp = @out;
            Stripper stripper = new Stripper(((SpaceStrippedDocument)docWrapper).StrippingRule, temp);
            node.Copy(stripper, copyOptions, locationId);
        }

        /// <summary>
        /// A StrippingIterator delivers wrappers for the nodes delivered by its underlying iterator,
        /// skipping whitespace text nodes that are to be stripped.
        /// </summary>
        private sealed class StrippingIterator : IAxisIterator
        {
            private readonly IAxisIterator @base;
            private readonly SpaceStrippedNode parent;
            private NodeInfo currentVirtualNode;
            private readonly SpaceStrippedDocument docWrapper;

            public StrippingIterator(IAxisIterator @base, SpaceStrippedDocument docWrapper, SpaceStrippedNode parent)
            {
                this.@base = @base;
                this.docWrapper = docWrapper;
                this.parent = parent;
            }

            public NodeInfo Next()
            {
                NodeInfo nextRealNode;
                do
                {
                    nextRealNode = @base.Next();
                    if (nextRealNode == null)
                    {
                        return null;
                    }
                    // otherwise skip this whitespace text node
                }
                while (!IsPreserved(nextRealNode));

                currentVirtualNode = MakeWrapper(nextRealNode, docWrapper, parent);
                return currentVirtualNode;
            }

            private bool IsPreserved(NodeInfo nextRealNode)
            {
                if (nextRealNode.GetNodeKind() != OutSmart.DAXon.Types.Type.TEXT)
                {
                    return true;
                }

                // Cheap in-place whiteness first: non-white text (the common case) is preserved
                // without resolving the parent (a sibling-chain walk on tiny text nodes).
                if (!IsAllWhiteText(nextRealNode))
                {
                    return true;
                }

                NodeInfo actualParent = parent == null ? nextRealNode.GetParent() : parent.node;
                return IsPreservedNode(nextRealNode, docWrapper, actualParent);
            }

            IItem ISequenceIterator.Next() => Next();

            public void Dispose()
            {
                @base.Dispose();
            }
        }
    }
}
