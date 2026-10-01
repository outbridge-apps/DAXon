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
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Text;

namespace OutSmart.DAXon.Trees
{
    // Faithful port of net.sf.saxon.tree.NamespaceNode (Saxon 12.9). Was a hollow stub whose MakeIterator threw
    // NotImplementedException, so the namespace:: axis and namespace-node() kind test were unusable (prod-AxisStep,
    // fn-outermost/innermost namespace cases, in-scope-prefixes, etc. all raised a code-less ERR). Implements the
    // full NodeInfo surface for a namespace node; a namespace node's string value is the namespace URI and its name
    // is the prefix (an NCName in no namespace).
    internal sealed class NamespaceNode : NodeInfo
    {
        internal NodeInfo element;
        internal NamespaceBinding nsBinding;
        internal int position;
        private int fingerprint;

        public UnicodeString UnicodeStringValue => nsBinding.GetNamespaceUri().ToUnicodeString();

        public int Fingerprint
        {
            get
            {
                if (fingerprint == -1)
                {
                    if (nsBinding.GetPrefix().Length == 0)
                    {
                        return -1;
                    }
                    else
                    {
                        fingerprint = element.GetConfiguration().GetNamePool().AllocateFingerprint(
                            NamespaceUri.NULL, nsBinding.GetPrefix());
                    }
                }
                return fingerprint;
            }
        }
        public string DisplayName => GetLocalPart();
        public NodeInfo Root => element.Root;
        public NamespaceMap AllNamespaces => null;

        public NamespaceNode(NodeInfo element, NamespaceBinding nscode, int position)
        {
            this.element = element;
            this.nsBinding = nscode;
            this.position = position;
            fingerprint = -1; // evaluated lazily to avoid NamePool access
        }

        public ITreeInfo GetTreeInfo() => element.GetTreeInfo();
        public Genre GetGenre() => Genre.NODE;
        public int GetNodeKind() => OutSmart.DAXon.Types.Type.NAMESPACE;

        public override bool Equals(object other)
        {
            return other is NamespaceNode
                && element.Equals(((NamespaceNode)other).element)
                && nsBinding.Equals(((NamespaceNode)other).nsBinding);
        }

        public override int GetHashCode() => element.GetHashCode() ^ (position << 13);

        public bool IsSameNodeInfo(NodeInfo other) => Equals(other);

        public string GetSystemId() => element.GetSystemId();
        public string GetPublicId() => element.GetPublicId();
        public string GetBaseURI() => null; // the base URI of a namespace node is the empty sequence
        public int GetLineNumber() => element.GetLineNumber();
        public int GetColumnNumber() => element.GetColumnNumber();
        public ILocation SaveLocation() => this;

        public int CompareOrder(NodeInfo other)
        {
            if (other is NamespaceNode && element.Equals(((NamespaceNode)other).element))
            {
                int c = position - ((NamespaceNode)other).position;
                return c < 0 ? -1 : (c > 0 ? 1 : 0);
            }
            else if (element.Equals(other))
            {
                return +1;
            }
            else
            {
                return element.CompareOrder(other);
            }
        }
        public string GetStringValue() => nsBinding.GetNamespaceUri().ToString();

        public bool HasFingerprint() => true;

        public string GetLocalPart() => nsBinding.GetPrefix();
        public NamespaceUri GetNamespaceUri() => NamespaceUri.NULL;
        public string GetURI() => GetNamespaceUri().ToString();
        public string GetPrefix() => "";
        public Configuration GetConfiguration() => element.GetConfiguration();
        public ISchemaType GetSchemaType() => BuiltInAtomicType.STRING;
        public NodeInfo GetParent() => element;

        public IAxisIterator IterateAxis(int axisNumber) => IterateAxis(axisNumber, AnyNodeTest.GetInstance());

        public IAxisIterator IterateAxis(int axisNumber, INodePredicate predicate)
        {
            NodeTest nodeTest = Navigator.NodeTestFromPredicate(predicate);
            switch (axisNumber)
            {
                case AxisInfo.ANCESTOR:
                    return element.IterateAxis(AxisInfo.ANCESTOR_OR_SELF, nodeTest);

                case AxisInfo.ANCESTOR_OR_SELF:
                    if (nodeTest.Test(this))
                    {
                        return new PrependAxisIterator(this, element.IterateAxis(AxisInfo.ANCESTOR_OR_SELF, nodeTest));
                    }
                    else
                    {
                        return element.IterateAxis(AxisInfo.ANCESTOR_OR_SELF, nodeTest);
                    }

                case AxisInfo.ATTRIBUTE:
                case AxisInfo.CHILD:
                case AxisInfo.DESCENDANT:
                case AxisInfo.DESCENDANT_OR_SELF:
                case AxisInfo.FOLLOWING_SIBLING:
                case AxisInfo.NAMESPACE:
                case AxisInfo.PRECEDING_SIBLING:
                    return EmptyIterator.OfNodes();

                case AxisInfo.FOLLOWING:
                    return new Navigator.AxisFilter(
                        new Navigator.FollowingEnumeration(this),
                        nodeTest);

                case AxisInfo.PARENT:
                    return Navigator.FilteredSingleton(element, nodeTest);

                case AxisInfo.PRECEDING:
                    return new Navigator.AxisFilter(
                        new Navigator.PrecedingEnumeration(this, false),
                        nodeTest);

                case AxisInfo.SELF:
                    return Navigator.FilteredSingleton(this, nodeTest);

                case AxisInfo.PRECEDING_OR_ANCESTOR:
                    return new Navigator.AxisFilter(
                        new Navigator.PrecedingEnumeration(this, true),
                        nodeTest);

                default:
                    throw new ArgumentException("Unknown axis number " + axisNumber);
            }
        }

        public string GetAttributeValue(NamespaceUri uri, string local) => null;
        public bool HasChildNodes() => false;

        public IEnumerable<NodeInfo> Children()
        {
            yield break;
        }

        public IEnumerable<NodeInfo> Children(INodePredicate filter)
        {
            yield break;
        }

        public IAttributeMap Attributes() => EmptyAttributeMap.GetInstance();

        public void GenerateId(StringBuilder buffer)
        {
            element.GenerateId(buffer);
            buffer.Append('n');
            buffer.Append(position);
        }

        public void Copy(IReceiver @out, int copyOptions, ILocation locationId) => @out.Append(this);

        public void Deliver(IReceiver receiver, ParseOptions options) => receiver.Append(this);

        public IActiveSource AsActiveSource() => new NodeSource(this);

        public void SetSystemId(string systemId)
        {
            // no action: namespace nodes have the same base URI as their parent
        }

        public NamespaceBinding[] GetDeclaredNamespaces(NamespaceBinding[] buffer) => null;

        public IAtomicSequence Atomize() => new StringValue(GetStringValue());

        public bool IsId() => false;
        public bool IsIdref() => false;
        public bool IsNilled() => false;
        public bool IsStreamed() => element.IsStreamed();
        public string ToShortString() => "namespace node " + DisplayName;

        // IItem / IGroundedValue singleton defaults
        public ISequenceIterator Iterate() => SingletonIterator.MakeIterator(this);
        public IItem ItemAt(int n) => n == 0 ? this : null;
        public int GetLength() => 1;
        public IGroundedValue Reduce() => this;
        public IGroundedValue Materialize() => this;
        public bool EffectiveBooleanValue() => true; // a single node is always true
        public IEnumerable<IItem> AsIterable() => new IItem[] { this };
        public bool ContainsNode(NodeInfo sought) => sought != null && IsSameNodeInfo(sought);
        public ISequence MakeRepeatable() => this;
        public IGroundedValue Subsequence(int start, int length) => (start <= 0 && (long)start + length > 0) ? (IGroundedValue)this : OutSmart.DAXon.Values.EmptySequence.GetInstance(); // singleton item (upstream GroundedValue default)
        public IGroundedValue Concatenate(IGroundedValue[] others)
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
        /// Factory method to create an iterator over the in-scope namespace nodes of an element.
        /// </summary>
        public static IAxisIterator MakeIterator(NodeInfo element, INodePredicate test)
        {
            List<NodeInfo> nodes = new List<NodeInfo>();
            int position = 0;
            bool foundXML = false;
            foreach (NamespaceBinding binding in element.AllNamespaces)
            {
                if (binding.GetPrefix().Equals("xml"))
                {
                    foundXML = true;
                }
                NamespaceNode node = new NamespaceNode(element, binding, position++);
                if (test.Test(node))
                {
                    nodes.Add(node);
                }
            }
            if (!foundXML)
            {
                NamespaceNode node = new NamespaceNode(element, NamespaceBinding.XML, position);
                if (test.Test(node))
                {
                    nodes.Add(node);
                }
            }
            return new NodeListIterator(nodes);
        }
    }
}
