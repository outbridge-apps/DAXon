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
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Values;
using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Lib;
namespace OutSmart.DAXon.Trees.Utilities
{
    internal sealed class Orphan : IMutableNodeInfo
    {
        private short kind;
        private INodeName nodeName;
        private UnicodeString stringValue;
        private ISchemaType typeAnnotation;
        private int options = ReceiverOption.NONE;
        private readonly GenericTreeInfo treeInfo;

        public int Fingerprint
        {
            get
            {
                throw new NotSupportedException();
            }
        }

        public UnicodeString UnicodeStringValue => stringValue;

        public string DisplayName
        {
            get
            {
                if (nodeName == null)
                {
                    return "";
                }
                else
                {
                    return nodeName.DisplayName;
                }
            }
        }

        public NodeInfo Root => this;

        public NamespaceMap AllNamespaces => null;
        public Orphan(Configuration config)
        {
            treeInfo = new GenericTreeInfo(config);
            treeInfo.SetRootNode(this);
        }

        public ITreeInfo GetTreeInfo()
        {
            return treeInfo;
        }

        public string GetSystemId()
        {
            return treeInfo.SystemId;
        }

        public string GetPublicId()
        {
            return treeInfo.GetPublicId();
        }

        public void SetSystemId(string systemId)
        {
            treeInfo.SystemId = systemId;
        }

        public bool EffectiveBooleanValue()
        {
            return true;
        }

        public void SetNodeKind(short kind)
        {
            this.kind = kind;
        }

        public void SetNodeName(INodeName nodeName)
        {
            this.nodeName = nodeName;
        }

        public void SetStringValue(UnicodeString stringValue)
        {
            this.stringValue = stringValue;
        }

        public void SetTypeAnnotation(ISchemaType typeAnnotation)
        {
            this.typeAnnotation = typeAnnotation;
        }

        private void SetOption(int option, bool on)
        {
            if (on)
            {
                options |= option;
            }
            else
            {
                options &= ~option;
            }
        }

        private bool IsOption(int option)
        {
            return ReceiverOption.Contains(options, option);
        }

        public void SetDisableOutputEscaping(bool doe)
        {
            SetOption(ReceiverOption.DISABLE_ESCAPING, doe);
        }

        public int GetNodeKind()
        {
            return kind;
        }

        public bool HasFingerprint()
        {
            return false;
        }

        public IAtomicSequence Atomize()
        {
            switch (GetNodeKind())
            {
                case Types.Type.COMMENT:
                case Types.Type.PROCESSING_INSTRUCTION:
                    return new StringValue(stringValue);
                case Types.Type.TEXT:
                case Types.Type.DOCUMENT:
                case Types.Type.NAMESPACE:
                    return StringValue.MakeUntypedAtomic(UnicodeStringValue);
                default:
                    if (typeAnnotation == null || typeAnnotation == Untyped.INSTANCE || typeAnnotation == BuiltInAtomicType.UNTYPED_ATOMIC)
                    {
                        return StringValue.MakeUntypedAtomic(UnicodeStringValue);
                    }
                    else
                    {
                        return typeAnnotation.Atomize(this);
                    }
            }
        }

        public ISchemaType GetSchemaType()
        {
            if (typeAnnotation == null)
            {
                if (kind == Types.Type.ELEMENT)
                {
                    return Untyped.INSTANCE;
                }
                else if (kind == Types.Type.ATTRIBUTE)
                {
                    return BuiltInAtomicType.UNTYPED_ATOMIC;
                }
            }

            return typeAnnotation;
        }

        public bool Equals(NodeInfo other)
        {
            return this == other;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public string GetBaseURI()
        {
            if (kind == Types.Type.PROCESSING_INSTRUCTION)
            {
                return GetSystemId();
            }
            else
            {
                return null;
            }
        }

        public ILocation SaveLocation()
        {
            return this;
        }

        public int CompareOrder(NodeInfo other)
        {

            // are they the same node?
            if (this.Equals(other))
            {
                return 0;
            }

            return this.GetHashCode() < other.GetHashCode() ? -1 : +1;
        }

        public string GetLocalPart()
        {
            if (nodeName == null)
            {
                return "";
            }
            else
            {
                return nodeName.GetLocalPart();
            }
        }

        public NamespaceUri GetNamespaceUri()
        {
            if (nodeName == null)
            {
                return NamespaceUri.NULL;
            }
            else
            {
                return nodeName.GetNamespaceUri();
            }
        }

        public string GetPrefix()
        {
            if (nodeName == null)
            {
                return "";
            }
            else
            {
                return nodeName.GetPrefix();
            }
        }

        public NodeInfo GetParent()
        {
            return null;
        }

        public IAxisIterator IterateAxis(int axisNumber)
        {
            switch (axisNumber)
            {
                case AxisInfo.ANCESTOR_OR_SELF:
                case AxisInfo.DESCENDANT_OR_SELF:
                case AxisInfo.SELF:
                    return SingleNodeIterator.MakeIterator(this);
                case AxisInfo.ANCESTOR:
                case AxisInfo.ATTRIBUTE:
                case AxisInfo.CHILD:
                case AxisInfo.DESCENDANT:
                case AxisInfo.FOLLOWING:
                case AxisInfo.FOLLOWING_SIBLING:
                case AxisInfo.NAMESPACE:
                case AxisInfo.PARENT:
                case AxisInfo.PRECEDING:
                case AxisInfo.PRECEDING_SIBLING:
                case AxisInfo.PRECEDING_OR_ANCESTOR:
                    return EmptyIterator.OfNodes();
                default:
                    throw new ArgumentException("Unknown axis number " + axisNumber);
            }
        }

        public IAxisIterator IterateAxis(int axisNumber, INodePredicate nodeTest)
        {
            switch (axisNumber)
            {
                case AxisInfo.ANCESTOR_OR_SELF:
                case AxisInfo.DESCENDANT_OR_SELF:
                case AxisInfo.SELF:
                    return Navigator.FilteredSingleton(this, nodeTest);
                case AxisInfo.ANCESTOR:
                case AxisInfo.ATTRIBUTE:
                case AxisInfo.CHILD:
                case AxisInfo.DESCENDANT:
                case AxisInfo.FOLLOWING:
                case AxisInfo.FOLLOWING_SIBLING:
                case AxisInfo.NAMESPACE:
                case AxisInfo.PARENT:
                case AxisInfo.PRECEDING:
                case AxisInfo.PRECEDING_SIBLING:
                case AxisInfo.PRECEDING_OR_ANCESTOR:
                    return EmptyIterator.OfNodes();
                default:
                    throw new ArgumentException("Unknown axis number " + axisNumber);
            }
        }

        public string GetAttributeValue(NamespaceUri uri, string local)
        {
            return null;
        }

        public bool HasChildNodes()
        {
            return false;
        }

        public void GenerateId(StringBuilder buffer)
        {
            buffer.Append('Q');
            buffer.Append(GetHashCode().ToString(CultureInfo.InvariantCulture));
        }

        public NamespaceBinding[] GetDeclaredNamespaces(NamespaceBinding[] buffer)
        {
            return null;
        }

        public bool IsId()
        {
            return IsOption(ReceiverOption.IS_ID) || (kind == Types.Type.ATTRIBUTE && nodeName.Equals(StandardNames.XML_ID_NAME));
        }

        public bool IsIdref()
        {
            return IsOption(ReceiverOption.IS_IDREF);
        }

        public bool IsDisableOutputEscaping()
        {
            return IsOption(ReceiverOption.DISABLE_ESCAPING);
        }

        public void InsertChildren(NodeInfo[] source, bool atStart, bool inherit)
        {
        }

        public void InsertSiblings(NodeInfo[] source, bool before, bool inherit)
        {
        }

        public void SetAttributes(IAttributeMap attributes)
        {
            throw new NotSupportedException();
        }

        public void RemoveAttribute(NodeInfo attribute)
        {
        }

        public void AddAttribute(INodeName nameCode, ISimpleType attType, string value, int properties, bool inheritNamespaces)
        {
        }

        public void Delete()
        {

            // no action other than to mark it deleted: node has no parent from which it can be detached
            kind = -1;
        }

        public bool IsDeleted()
        {
            return kind == -1;
        }

        public void Replace(NodeInfo[] replacement, bool inherit)
        {
            throw new InvalidOperationException("Cannot replace a parentless node");
        }

        public void ReplaceStringValue(UnicodeString stringValue)
        {
            this.stringValue = stringValue;
        }

        public void Rename(INodeName newNameCode, bool inherit)
        {
            if (kind == Types.Type.ATTRIBUTE || kind == Types.Type.PROCESSING_INSTRUCTION)
            {
                nodeName = newNameCode;
            }
        }

        public void AddNamespace(NamespaceBinding nscode, bool inherit)
        {
        }

        public void RemoveTypeAnnotation()
        {
            typeAnnotation = BuiltInAtomicType.UNTYPED_ATOMIC;
        }

        public Builder NewBuilder()
        {
            throw new NotSupportedException("Cannot create children for an Orphan node");
        }
        public Genre GetGenre() => Genre.NODE; // upstream NodeInfo default (only Orphan carries this stub)
        public ISequenceIterator Iterate() => SingletonIterator.MakeIterator(this); // upstream Item default
        public IItem ItemAt(int arg0) => arg0 == 0 ? this : null; // upstream Item default
        public IItem Head() => this; // upstream Item default
        public IGroundedValue Subsequence(int arg0, int arg1) => arg0 <= 0 && (long)arg0 + arg1 > 0 ? (IGroundedValue)this : (IGroundedValue)EmptySequence.GetInstance(); // upstream Item default
        public int GetLength() => 1; // upstream Item default
        public string GetStringValue() => stringValue == null ? "" : stringValue.ToString(); // upstream: the node's string value
        public int GetLineNumber() => -1; // an orphan has no source location
        public int GetColumnNumber() => -1;
        public void Deliver(IReceiver @out, ParseOptions options) => Events.Sender.SendDocumentInfo(this, @out, new Expressions.Parsing.Loc(GetSystemId(), -1, -1));
        SingletonIterator IItem.Iterate() => new SingletonIterator(this);

        // Formerly NIE stubs; an orphan is a parentless leaf, so most answers are fixed.
        public void RemoveNamespace(string prefix) => throw new InvalidOperationException("Namespaces can only be removed from an element node");
        public void AddNamespace(string prefix, NamespaceUri uri) => throw new InvalidOperationException("Namespaces can only be added to an element node");
        public Configuration GetConfiguration() => treeInfo.GetConfiguration(); // upstream Orphan: treeInfo.getConfiguration()
        public bool IsSameNodeInfo(NodeInfo other) => this == other; // node identity
        public string GetURI() => nodeName == null ? "" : nodeName.GetURI();
        public IEnumerable<NodeInfo> Children() => Enumerable.Empty<NodeInfo>();
        public IEnumerable<NodeInfo> Children(INodePredicate filter) => Enumerable.Empty<NodeInfo>();
        public IAttributeMap Attributes() => EmptyAttributeMap.GetInstance();
        public void Copy(IReceiver @out, int copyOptions, ILocation locationId) => Navigator.Copy(this, @out, copyOptions, locationId); // upstream NodeInfo default
        public IActiveSource AsActiveSource() => new NodeSource(this); // upstream NodeInfo default method
        public bool IsNilled() => false;
        public bool IsStreamed() => false;
        public string ToShortString() => ToString();
        public IGroundedValue Reduce() => this; // upstream GroundedValue default method
        public IGroundedValue Materialize() => this; // upstream GroundedValue default method
        public IEnumerable<IItem> AsIterable() => new IItem[] { this }; // singleton grounded value (upstream GroundedValue default for an Item)
        public bool ContainsNode(NodeInfo sought) => this == sought;
        public IGroundedValue Concatenate(IGroundedValue[] others)
        {
            // upstream GroundedValue default: chain this item with the others
            var chain = new OutSmart.DAXon.Collections.Zeno.ZenoChain<IItem>().AddAll(AsIterable());
            foreach (IGroundedValue v in others)
            {
                chain = chain.AddAll(v.AsIterable());
            }

            return new OutSmart.DAXon.Collections.Zeno.ZenoSequence(chain);
        }
        // A node is a single item - already repeatable.
        public ISequence MakeRepeatable() => this;
    }
}

