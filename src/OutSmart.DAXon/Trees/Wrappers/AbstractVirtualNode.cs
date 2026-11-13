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
    // Faithful port of net.sf.saxon.tree.wrapper.AbstractVirtualNode (Saxon 12.9). Was a hollow stub, so
    // no wrapping-node view (space-stripped / type-stripped trees) could exist.
    // Abstract superclass for VirtualNode implementations in which the underlying node is itself a NodeInfo.
    internal abstract class AbstractVirtualNode : IVirtualNode
    {
        protected internal NodeInfo node;
        protected internal AbstractVirtualNode parent; // null means unknown
        protected internal ITreeInfo docWrapper;

        public virtual object UnderlyingNode => node;

        public virtual object RealNode
        {
            get
            {
                object u = this;
                do
                {
                    u = ((IVirtualNode)u).UnderlyingNode;
                }
                while (u is IVirtualNode);
                return u;
            }
        }

        public virtual int Fingerprint
        {
            get
            {
                if (node.HasFingerprint())
                {
                    return node.Fingerprint;
                }
                else
                {
                    throw new NotSupportedException();
                }
            }
        }

        public virtual UnicodeString UnicodeStringValue => node.UnicodeStringValue;
        public virtual string DisplayName => node.DisplayName;

        public virtual NodeInfo Root
        {
            get
            {
                NodeInfo p = this;
                while (true)
                {
                    NodeInfo q = p.GetParent();
                    if (q == null)
                    {
                        return p;
                    }

                    p = q;
                }
            }
        }
        public virtual NamespaceMap AllNamespaces => node.AllNamespaces;

        public virtual ITreeInfo GetTreeInfo() => docWrapper;
        public virtual Configuration GetConfiguration() => node.GetConfiguration();

        public virtual bool HasFingerprint() => node.HasFingerprint();
        public virtual int GetNodeKind() => node.GetNodeKind();
        public virtual IAtomicSequence Atomize() => node.Atomize();
        public virtual ISchemaType GetSchemaType() => node.GetSchemaType();

        public override bool Equals(object other)
        {
            if (other is AbstractVirtualNode)
            {
                return node.Equals(((AbstractVirtualNode)other).node);
            }
            else
            {
                return node.Equals(other);
            }
        }

        public override int GetHashCode()
        {
            return node.GetHashCode() ^ 0x3c3c3c3c;
        }

        public virtual bool IsSameNodeInfo(NodeInfo other) => Equals(other);

        public virtual string GetSystemId() => node.GetSystemId();

        public virtual void SetSystemId(string uri)
        {
            node.SetSystemId(uri);
        }

        public virtual string GetPublicId() => node.GetPublicId();
        public virtual string GetBaseURI() => node.GetBaseURI();
        public virtual int GetLineNumber() => node.GetLineNumber();
        public virtual int GetColumnNumber() => node.GetColumnNumber();
        public virtual ILocation SaveLocation() => this;

        public virtual int CompareOrder(NodeInfo other)
        {
            if (other is AbstractVirtualNode)
            {
                return node.CompareOrder(((AbstractVirtualNode)other).node);
            }
            else
            {
                return node.CompareOrder(other);
            }
        }
        public virtual string GetStringValue() => UnicodeStringValue.ToString();
        public virtual Genre GetGenre() => Genre.NODE;

        public virtual string GetLocalPart() => node.GetLocalPart();
        public virtual NamespaceUri GetNamespaceUri() => node.GetNamespaceUri();
        public virtual string GetURI() => GetNamespaceUri().ToString();
        public virtual string GetPrefix() => node.GetPrefix();

        public abstract NodeInfo GetParent();
        public abstract IAxisIterator IterateAxis(int axisNumber);

        public virtual IAxisIterator IterateAxis(int axisNumber, INodePredicate nodeTest)
        {
            return new Navigator.AxisFilter(IterateAxis(axisNumber), nodeTest);
        }

        public virtual string GetAttributeValue(NamespaceUri uri, string local) => node.GetAttributeValue(uri, local);

        public virtual bool HasChildNodes() => node.HasChildNodes();

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
            // Note: giving the node the same ID as its underlying node is slightly questionable; depends on usage
            node.GenerateId(buffer);
        }

        public abstract void Copy(IReceiver @out, int copyOptions, ILocation locationId);

        public virtual void Deliver(IReceiver receiver, ParseOptions options) => receiver.Append(this);
        public virtual IActiveSource AsActiveSource() => new NodeSource(this);

        public virtual NamespaceBinding[] GetDeclaredNamespaces(NamespaceBinding[] buffer) => node.GetDeclaredNamespaces(buffer);

        public virtual bool IsId() => node.IsId();
        public virtual bool IsIdref() => node.IsIdref();
        public virtual bool IsNilled() => node.IsNilled();
        public virtual bool IsStreamed() => node.IsStreamed();
        public virtual string ToShortString() => node.ToShortString();

        // IItem / IGroundedValue singleton defaults
        public virtual ISequenceIterator Iterate() => SingletonIterator.MakeIterator(this);
        public virtual IItem ItemAt(int n) => n == 0 ? this : null;
        public virtual int GetLength() => 1;
        public virtual IGroundedValue Reduce() => this;
        public virtual IGroundedValue Materialize() => this;
        public virtual bool EffectiveBooleanValue() => true;
        public virtual IEnumerable<IItem> AsIterable() => new IItem[] { this };
        public virtual bool ContainsNode(NodeInfo sought) => GroundedValueDefaults.ContainsNode(this, sought);
        public virtual ISequence MakeRepeatable() => this;
        public virtual IGroundedValue Subsequence(int start, int length) => GroundedValueDefaults.Subsequence(this, start, length);
        public virtual IGroundedValue Concatenate(IGroundedValue[] others) => GroundedValueDefaults.Concatenate(this, others);

        // net472 explicit interface bridges (no covariant interface returns) — mirror TinyNodeImpl
        IItem IItem.Head() => this;
        IItem IGroundedValue.Head() => this;
        IItem ISequence.Head() => this;
        SingletonIterator IItem.Iterate() => new SingletonIterator(this);
    }
}
