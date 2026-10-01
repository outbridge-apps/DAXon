////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Trees.Linked
{
    internal sealed class AttributeImpl : NodeImpl
    {

        public override int Fingerprint
        {
            get
            {
                if (GetRawParent() == null || GetSiblingPosition() == -1)
                {

                    // implies this node is deleted
                    return -1;
                }

                return GetNodeName().ObtainFingerprint(GetNamePool());
            }
        }

        protected internal override long SequenceNumber
        {
            get
            {
                long parseq = GetRawParent().SequenceNumber;
                return (parseq == -1 ? parseq : parseq + 0x8000 + GetSiblingPosition()); // note the 0x8000 is to leave room for namespace nodes
            }
        }

        public override UnicodeString UnicodeStringValue => StringView.Tidy(GetAttributeInfo().Value);

        /// <summary>
        /// Get the previous node in document order (skipping attributes)
        /// </summary>
        public override NodeImpl PreviousInDocument => GetParent();
        public AttributeImpl(ElementImpl element, int index)
        {
            SetRawParent(element);
            SetSiblingPosition(index);
        }

        private AttributeInfo GetAttributeInfo()
        {
            return GetRawParent().Attributes().ItemAt(GetSiblingPosition());
        }

        public override INodeName GetNodeName()
        {
            if (GetRawParent() == null || GetSiblingPosition() == -1)
            {

                // implies this node is deleted
                return null;
            }

            return GetAttributeInfo().GetNodeName();
        }

        public override ISchemaType GetSchemaType()
        {
            return GetAttributeInfo().GetType();
        }

        public override bool IsId()
        {
            return GetAttributeInfo().IsId();
        }

        public override bool IsIdref()
        {
            if (ReceiverOption.Contains(GetAttributeInfo().GetProperties(), ReceiverOption.IS_IDREF))
            {
                return true;
            }

            return ElementImpl.IsIdRefNode(this);
        }

        public override bool Equals(object other)
        {
            if (!(other is AttributeImpl))
            {
                return false;
            }

            if (this == other)
            {
                return true;
            }

            AttributeImpl otherAtt = (AttributeImpl)other;
            return GetRawParent().Equals(otherAtt.GetRawParent()) && GetSiblingPosition() == otherAtt.GetSiblingPosition();
        }

        public override int GetHashCode()
        {
            return GetRawParent().GetHashCode() ^ (GetSiblingPosition() << 16);
        }

        public override int GetNodeKind()
        {
            return Types.Type.ATTRIBUTE;
        }

        public override string GetStringValue()
        {
            return GetAttributeInfo().Value;
        }

        /// <summary>
        /// Get next sibling - not defined for attributes
        /// </summary>
        public override NodeImpl GetNextSibling()
        {
            return null;
        }

        /// <summary>
        /// Get previous sibling - not defined for attributes
        /// </summary>
        public override NodeImpl GetPreviousSibling()
        {
            return null;
        }

        public override NodeImpl GetNextInDocument(NodeImpl anchor)
        {
            if (anchor == this)
                return null;
            return GetParent().GetNextInDocument(anchor);
        }

        public override void GenerateId(StringBuilder buffer)
        {
            GetParent().GenerateId(buffer);
            buffer.Append('a');
            buffer.Append(GetSiblingPosition());
        }

        public override void Copy(IReceiver @out, int copyOptions, ILocation locationId)
        {
            throw new ArgumentException();
        }

        public override void Delete()
        {
            if (!IsDeleted())
            {
                if (GetRawParent() != null)
                {
                    GetRawParent().RemoveAttribute(this); //                IAttributeMap oldAtts = getRawParent().attributes();
                    //                IAttributeMap newAtts = oldAtts.remove(getNodeName());
                }

                SetRawParent(null);
                SetSiblingPosition(-1);
            }
        }

        public override bool IsDeleted()
        {

            // Note that the attribute node may be represented by more than one object
            return GetRawParent() == null || GetAttributeInfo() is AttributeInfo.Deleted || GetRawParent().IsDeleted();
        }

        public override void Replace(NodeInfo[] replacement, bool inherit)
        {
            if (IsDeleted())
            {
                throw new InvalidOperationException("Cannot replace a deleted node");
            }

            if (GetParent() == null)
            {
                throw new InvalidOperationException("Cannot replace a parentless node");
            }

            ParentNodeImpl element = GetRawParent();
            Delete();
            foreach (NodeInfo n in replacement)
            {
                if (n.GetNodeKind() != Types.Type.ATTRIBUTE)
                {
                    throw new ArgumentException("Replacement nodes must be attributes");
                }

                element.AddAttribute(NameOfNode.MakeName(n), BuiltInAtomicType.UNTYPED_ATOMIC, n.GetStringValue(), ReceiverOption.NONE, inherit);
            }
        }

        public override void Rename(INodeName newNameCode, bool inherit)
        {

            // The attribute node itself is transient; we need to update the attribute collection held in the parent
            ElementImpl owner = (ElementImpl)GetRawParent();
            if (owner != null && !IsDeleted())
            {
                AttributeInfo att = GetAttributeInfo();
                int properties = att.GetProperties() & ~(ReceiverOption.IS_ID | ReceiverOption.IS_IDREF);
                owner.SetAttributeInfo(GetSiblingPosition(), new AttributeInfo(newNameCode, BuiltInAtomicType.UNTYPED_ATOMIC, att.Value, att.GetLocation(), properties));
                NamespaceUri newURI = newNameCode.GetNamespaceUri();
                if (!newURI.IsEmpty())
                {

                    // new attribute name @is in a namespace
                    string newPrefix = newNameCode.GetPrefix();
                    NamespaceBinding newBinding = new NamespaceBinding(newPrefix, newURI);
                    NamespaceUri oldURI = ((ElementImpl)GetRawParent()).GetURIForPrefix(newPrefix, false);
                    if (oldURI == null)
                    {
                        owner.AddNamespace(newBinding, inherit);
                    }
                    else if (!oldURI.Equals(newURI))
                    {
                        throw new ArgumentException("Namespace binding of new name conflicts with existing namespace binding");
                    }
                }
            }
        }

        public override void ReplaceStringValue(UnicodeString stringValue)
        {
            ElementImpl owner = (ElementImpl)GetRawParent();
            if (owner != null && !IsDeleted())
            {
                AttributeInfo att = GetAttributeInfo();
                owner.SetAttributeInfo(GetSiblingPosition(), new AttributeInfo(att.GetNodeName(), att.GetType(), stringValue.ToString(), att.GetLocation(), att.GetProperties()));
            }
        }

        public override void RemoveTypeAnnotation()
        {
            ElementImpl owner = (ElementImpl)GetRawParent();
            if (owner != null && !IsDeleted())
            {
                AttributeInfo att = GetAttributeInfo();
                owner.SetAttributeInfo(GetSiblingPosition(), new AttributeInfo(att.GetNodeName(), BuiltInAtomicType.UNTYPED_ATOMIC, att.Value, att.GetLocation(), att.GetProperties()));
                owner.RemoveTypeAnnotation();
            }
        }

        public override void SetTypeAnnotation(ISchemaType type)
        {
            if (!(type is ISimpleType))
            {
                throw new ArgumentException("Attribute type must be a simple type");
            }

            ElementImpl owner = (ElementImpl)GetRawParent();
            if (owner != null && !IsDeleted())
            {
                AttributeInfo att = GetAttributeInfo();
                owner.SetAttributeInfo(GetSiblingPosition(), new AttributeInfo(att.GetNodeName(), (ISimpleType)type, att.Value, att.GetLocation(), att.GetProperties()));
                owner.RemoveTypeAnnotation();
            }
        }
    }
}
