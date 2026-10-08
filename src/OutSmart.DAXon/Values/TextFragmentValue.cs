////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Trees.Linked;
using OutSmart.DAXon.Trees.Utilities;
using OutSmart.DAXon.Types;
using System;
using System.Collections.Generic;
using System.Text;

namespace OutSmart.DAXon.Values
{
    // Faithful port of net.sf.saxon.value.TextFragmentValue (Saxon 12.9). Was a hollow stub whose MakeTextFragment
    // threw NotImplementedException, so EVERY text-only temporary tree (<xsl:variable>text</xsl:variable> and the
    // document{} equivalent) crashed — the single most common XSLT pattern in the xslt30-test corpus.
    // A temporary tree whose root document node owns a single text node.
    internal sealed class TextFragmentValue : NodeInfo
    {
        private readonly UnicodeString text;
        private readonly string baseURI;
        private string documentURI;
        private readonly GenericTreeInfo treeInfo;
        private TextFragmentTextNode textNode; // created on demand
        public UnicodeString UnicodeStringValue => text;

        public int Fingerprint => -1;
        public string DisplayName => "";
        public NamespaceMap AllNamespaces => null;
        public NodeInfo Root => this;

        private TextFragmentTextNode TextNode
        {
            get
            {
                if (textNode == null)
                {
                    textNode = new TextFragmentTextNode(this);
                }

                return textNode;
            }
        }

        public TextFragmentValue(Configuration config, UnicodeString value, string baseURI)
        {
            this.text = value;
            this.baseURI = baseURI;
            this.treeInfo = new GenericTreeInfo(config);
            this.treeInfo.SetRootNode(this);
        }

        /// <summary>
        /// Static factory method: create a result tree fragment containing a single text node,
        /// unless the text is zero length, in which case an empty document node is returned
        /// </summary>
        public static NodeInfo MakeTextFragment(Configuration config, UnicodeString value, string baseURI)
        {
            if (value.Length() == 0)
            {
                // Create a childless document node: bug 4246
                DocumentImpl doc = new DocumentImpl();
                doc.SetSystemId(baseURI);
                doc.SetBaseURI(baseURI);
                doc.SetConfiguration(config);
                return doc;
            }
            else
            {
                return new TextFragmentValue(config, value, baseURI);
            }
        }

        public ITreeInfo GetTreeInfo() => treeInfo;
        public Configuration GetConfiguration() => treeInfo.GetConfiguration();
        public int GetNodeKind() => OutSmart.DAXon.Types.Type.DOCUMENT;
        public string GetStringValue() => text.ToString();
        public Genre GetGenre() => Genre.NODE;

        public bool IsSameNodeInfo(NodeInfo other) => Equals(other);
        public bool HasFingerprint() => true;

        public void GenerateId(StringBuilder buffer)
        {
            buffer.Append("tt");
            buffer.Append(treeInfo.GetDocumentNumber());
        }

        public void SetSystemId(string systemId)
        {
            documentURI = systemId;
        }

        public string GetSystemId() => documentURI;
        public string GetPublicId() => null;
        public string GetBaseURI() => baseURI;
        public int GetLineNumber() => -1;
        public int GetColumnNumber() => -1;
        public ILocation SaveLocation() => this;

        public int CompareOrder(NodeInfo other)
        {
            if (this == other)
            {
                return 0;
            }

            return -1;
        }
        public string GetPrefix() => "";
        public NamespaceUri GetNamespaceUri() => NamespaceUri.NULL;
        public string GetURI() => GetNamespaceUri().ToString();
        public string GetLocalPart() => "";
        public bool HasChildNodes() => text.Length() != 0;
        public ISchemaType GetSchemaType() => Untyped.GetInstance();
        public NamespaceBinding[] GetDeclaredNamespaces(NamespaceBinding[] buffer) => null;
        public IAtomicSequence Atomize() => StringValue.MakeUntypedAtomic(text);
        public string GetAttributeValue(NamespaceUri uri, string local) => null;

        public IAxisIterator IterateAxis(int axisNumber)
        {
            switch (axisNumber)
            {
                case AxisInfo.ANCESTOR:
                case AxisInfo.ATTRIBUTE:
                case AxisInfo.FOLLOWING:
                case AxisInfo.FOLLOWING_SIBLING:
                case AxisInfo.NAMESPACE:
                case AxisInfo.PARENT:
                case AxisInfo.PRECEDING:
                case AxisInfo.PRECEDING_SIBLING:
                case AxisInfo.PRECEDING_OR_ANCESTOR:
                    return EmptyIterator.OfNodes();
                case AxisInfo.SELF:
                case AxisInfo.ANCESTOR_OR_SELF:
                    return SingleNodeIterator.MakeIterator(this);
                case AxisInfo.CHILD:
                case AxisInfo.DESCENDANT:
                    return SingleNodeIterator.MakeIterator(TextNode);
                case AxisInfo.DESCENDANT_OR_SELF:
                    NodeInfo[] nodes = { this, TextNode };
                    return new ArrayIterator.OfNodes<NodeInfo>(nodes);
                default:
                    throw new ArgumentException("Unknown axis number " + axisNumber);
            }
        }

        public IAxisIterator IterateAxis(int axisNumber, INodePredicate nodeTest)
        {
            switch (axisNumber)
            {
                case AxisInfo.ANCESTOR:
                case AxisInfo.ATTRIBUTE:
                case AxisInfo.FOLLOWING:
                case AxisInfo.FOLLOWING_SIBLING:
                case AxisInfo.NAMESPACE:
                case AxisInfo.PARENT:
                case AxisInfo.PRECEDING:
                case AxisInfo.PRECEDING_SIBLING:
                case AxisInfo.PRECEDING_OR_ANCESTOR:
                    return EmptyIterator.OfNodes();
                case AxisInfo.SELF:
                case AxisInfo.ANCESTOR_OR_SELF:
                    return Navigator.FilteredSingleton(this, nodeTest);
                case AxisInfo.CHILD:
                case AxisInfo.DESCENDANT:
                    return Navigator.FilteredSingleton(TextNode, nodeTest);
                case AxisInfo.DESCENDANT_OR_SELF:
                    bool b1 = nodeTest.Test(this);
                    NodeInfo textNode2 = TextNode;
                    bool b2 = nodeTest.Test(textNode2);
                    if (b1)
                    {
                        if (b2)
                        {
                            NodeInfo[] pair = { this, textNode2 };
                            return new ArrayIterator.OfNodes<NodeInfo>(pair);
                        }
                        else
                        {
                            return SingleNodeIterator.MakeIterator(this);
                        }
                    }
                    else
                    {
                        if (b2)
                        {
                            return SingleNodeIterator.MakeIterator(textNode2);
                        }
                        else
                        {
                            return EmptyIterator.OfNodes();
                        }
                    }

                default:
                    throw new ArgumentException("Unknown axis number " + axisNumber);
            }
        }

        public NodeInfo GetParent() => null;

        public IEnumerable<NodeInfo> Children()
        {
            if (text.Length() != 0)
            {
                yield return TextNode;
            }
        }

        public IEnumerable<NodeInfo> Children(INodePredicate filter)
        {
            if (text.Length() != 0 && filter.Test(TextNode))
            {
                yield return TextNode;
            }
        }

        public IAttributeMap Attributes() => EmptyAttributeMap.GetInstance();

        public void Copy(IReceiver @out, int copyOptions, ILocation locationId)
        {
            @out.Characters(text, locationId, ReceiverOption.NONE);
        }

        public void Deliver(IReceiver receiver, ParseOptions options) => receiver.Append(this);
        public IActiveSource AsActiveSource() => new NodeSource(this);

        public bool IsId() => false;
        public bool IsIdref() => false;
        public bool IsNilled() => false;
        public bool IsStreamed() => false;
        public string ToShortString() => "document-node()";

        public ISequenceIterator Iterate() => SingletonIterator.MakeIterator(this);
        public IItem ItemAt(int n) => n == 0 ? this : null;
        public int GetLength() => 1;
        public IGroundedValue Reduce() => this;
        public IGroundedValue Materialize() => this;
        public bool EffectiveBooleanValue() => true;
        public IEnumerable<IItem> AsIterable() => new IItem[] { this };
        public bool ContainsNode(NodeInfo sought) => sought != null && IsSameNodeInfo(sought);
        public ISequence MakeRepeatable() => this;
        public IGroundedValue Subsequence(int start, int length) => start <= 0 && (long)start + length > 0 ? (IGroundedValue)this : EmptySequence.GetInstance();
        public IGroundedValue Concatenate(IGroundedValue[] others)
        {
            var chain = new OutSmart.DAXon.Collections.Zeno.ZenoChain<IItem>().AddAll(((IGroundedValue)this).AsIterable());
            foreach (IGroundedValue v in others)
            {
                chain = chain.AddAll(v.AsIterable());
            }
            return new OutSmart.DAXon.Collections.Zeno.ZenoSequence(chain);
        }

        IItem IItem.Head() => this;
        IItem IGroundedValue.Head() => this;
        IItem ISequence.Head() => this;
        SingletonIterator IItem.Iterate() => new SingletonIterator(this);

        /// <summary>
        /// Inner class representing the text node; this is created on demand
        /// </summary>
        private sealed class TextFragmentTextNode : NodeInfo
        {
            private readonly TextFragmentValue fragment;
            public UnicodeString UnicodeStringValue => fragment.text;

            public int Fingerprint => -1;
            public string DisplayName => "";
            public NamespaceMap AllNamespaces => null;
            public NodeInfo Root => fragment;

            public TextFragmentTextNode(TextFragmentValue fragment)
            {
                this.fragment = fragment;
            }

            public bool HasFingerprint() => true;
            public ITreeInfo GetTreeInfo() => fragment.treeInfo;
            public Configuration GetConfiguration() => fragment.treeInfo.GetConfiguration();

            public void SetSystemId(string systemId)
            {
            }

            public int GetNodeKind() => OutSmart.DAXon.Types.Type.TEXT;
            public string GetStringValue() => fragment.text.ToString();
            public Genre GetGenre() => Genre.NODE;
            public bool IsSameNodeInfo(NodeInfo other) => Equals(other);

            public void GenerateId(StringBuilder buffer)
            {
                buffer.Append("tt");
                buffer.Append(fragment.treeInfo.GetDocumentNumber());
                buffer.Append("t1");
            }

            public string GetSystemId() => null;
            public string GetPublicId() => null;
            public string GetBaseURI() => fragment.baseURI;
            public int GetLineNumber() => -1;
            public int GetColumnNumber() => -1;
            public ILocation SaveLocation() => this;

            public int CompareOrder(NodeInfo other)
            {
                if (this == other)
                {
                    return 0;
                }

                return +1;
            }
            public string GetPrefix() => "";
            public NamespaceUri GetNamespaceUri() => NamespaceUri.NULL;
            public string GetURI() => GetNamespaceUri().ToString();
            public string GetLocalPart() => "";
            public bool HasChildNodes() => false;
            public string GetAttributeValue(NamespaceUri uri, string local) => null;
            public ISchemaType GetSchemaType() => null;
            public NamespaceBinding[] GetDeclaredNamespaces(NamespaceBinding[] buffer) => null;
            public IAtomicSequence Atomize() => StringValue.MakeUntypedAtomic(fragment.text);

            public IAxisIterator IterateAxis(int axisNumber)
            {
                switch (axisNumber)
                {
                    case AxisInfo.ANCESTOR:
                    case AxisInfo.PARENT:
                    case AxisInfo.PRECEDING_OR_ANCESTOR:
                        return SingleNodeIterator.MakeIterator(fragment);
                    case AxisInfo.ANCESTOR_OR_SELF:
                        NodeInfo[] nodes = { this, fragment };
                        return new ArrayIterator.OfNodes<NodeInfo>(nodes);
                    case AxisInfo.ATTRIBUTE:
                    case AxisInfo.CHILD:
                    case AxisInfo.DESCENDANT:
                    case AxisInfo.FOLLOWING:
                    case AxisInfo.FOLLOWING_SIBLING:
                    case AxisInfo.NAMESPACE:
                    case AxisInfo.PRECEDING:
                    case AxisInfo.PRECEDING_SIBLING:
                        return EmptyIterator.OfNodes();
                    case AxisInfo.SELF:
                    case AxisInfo.DESCENDANT_OR_SELF:
                        return SingleNodeIterator.MakeIterator(this);
                    default:
                        throw new ArgumentException("Unknown axis number " + axisNumber);
                }
            }

            public IAxisIterator IterateAxis(int axisNumber, INodePredicate nodeTest)
            {
                switch (axisNumber)
                {
                    case AxisInfo.ANCESTOR:
                    case AxisInfo.PARENT:
                    case AxisInfo.PRECEDING_OR_ANCESTOR:
                        return Navigator.FilteredSingleton(fragment, nodeTest);
                    case AxisInfo.ANCESTOR_OR_SELF:
                        bool matchesDoc = nodeTest.Test(fragment);
                        bool matchesText = nodeTest.Test(this);
                        if (matchesDoc && matchesText)
                        {
                            NodeInfo[] nodes = { this, fragment };
                            return new ArrayIterator.OfNodes<NodeInfo>(nodes);
                        }
                        else if (matchesDoc)
                        {
                            return SingleNodeIterator.MakeIterator(fragment);
                        }
                        else if (matchesText)
                        {
                            return SingleNodeIterator.MakeIterator(this);
                        }
                        else
                        {
                            return EmptyIterator.OfNodes();
                        }

                    case AxisInfo.ATTRIBUTE:
                    case AxisInfo.CHILD:
                    case AxisInfo.DESCENDANT:
                    case AxisInfo.FOLLOWING:
                    case AxisInfo.FOLLOWING_SIBLING:
                    case AxisInfo.NAMESPACE:
                    case AxisInfo.PRECEDING:
                    case AxisInfo.PRECEDING_SIBLING:
                        return EmptyIterator.OfNodes();
                    case AxisInfo.SELF:
                    case AxisInfo.DESCENDANT_OR_SELF:
                        return Navigator.FilteredSingleton(this, nodeTest);
                    default:
                        throw new ArgumentException("Unknown axis number " + axisNumber);
                }
            }

            public NodeInfo GetParent() => fragment;

            public IEnumerable<NodeInfo> Children()
            {
                yield break;
            }

            public IEnumerable<NodeInfo> Children(INodePredicate filter)
            {
                yield break;
            }

            public IAttributeMap Attributes() => EmptyAttributeMap.GetInstance();

            public void Copy(IReceiver @out, int copyOptions, ILocation locationId)
            {
                @out.Characters(fragment.text, locationId, ReceiverOption.NONE);
            }

            public void Deliver(IReceiver receiver, ParseOptions options) => receiver.Append(this);
            public IActiveSource AsActiveSource() => new NodeSource(this);

            public bool IsId() => false;
            public bool IsIdref() => false;
            public bool IsNilled() => false;
            public bool IsStreamed() => false;
            public string ToShortString() => "text()";

            public ISequenceIterator Iterate() => SingletonIterator.MakeIterator(this);
            public IItem ItemAt(int n) => n == 0 ? this : null;
            public int GetLength() => 1;
            public IGroundedValue Reduce() => this;
            public IGroundedValue Materialize() => this;
            public bool EffectiveBooleanValue() => true;
            public IEnumerable<IItem> AsIterable() => new IItem[] { this };
            public bool ContainsNode(NodeInfo sought) => sought != null && IsSameNodeInfo(sought);
            public ISequence MakeRepeatable() => this;
            public IGroundedValue Subsequence(int start, int length) => start <= 0 && (long)start + length > 0 ? (IGroundedValue)this : EmptySequence.GetInstance();
            public IGroundedValue Concatenate(IGroundedValue[] others)
        {
            var chain = new OutSmart.DAXon.Collections.Zeno.ZenoChain<IItem>().AddAll(((IGroundedValue)this).AsIterable());
            foreach (IGroundedValue v in others)
            {
                chain = chain.AddAll(v.AsIterable());
            }
            return new OutSmart.DAXon.Collections.Zeno.ZenoSequence(chain);
        }

            IItem IItem.Head() => this;
            IItem IGroundedValue.Head() => this;
            IItem ISequence.Head() => this;
            SingletonIterator IItem.Iterate() => new SingletonIterator(this);
        }
    }
}
