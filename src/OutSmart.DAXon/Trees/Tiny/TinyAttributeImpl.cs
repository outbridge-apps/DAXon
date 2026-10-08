////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Types;
using System;
using System.Text;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Trees.Tiny
{
    internal sealed class TinyAttributeImpl : TinyNodeImpl
    {

        public override NodeInfo Root
        {
            get
            {
                NodeInfo parent = GetParent();
                if (parent == null)
                {
                    return this; // doesn't happen - parentless attributes are represented by the Orphan class
                }
                else
                {
                    return parent.Root;
                }
            }
        }

        public override long SequenceNumber => GetParent().SequenceNumber + 0x8000 + (nodeNr - tree.alpha[tree.attParent[nodeNr]]);

        public override UnicodeString UnicodeStringValue => StringView.Of(tree.attValue[nodeNr]).Tidy();

        public override int Fingerprint => tree.attCode[nodeNr] & 0xfffff;

        public override string DisplayName
        {
            get
            {
                int code = tree.attCode[nodeNr];
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
        public TinyAttributeImpl(TinyTree tree, int nodeNr) : base(tree, nodeNr)
        {
        }

        public override void SetSystemId(string uri)
        {
        }

        public override string GetSystemId()
        {
            NodeInfo parent = GetParent();
            return parent == null ? null : GetParent().GetSystemId();
        }

        public override TinyNodeImpl GetParent()
        {
            return tree.GetNode(tree.attParent[nodeNr]);
        }

        public override int GetNodeKind()
        {
            return Types.Type.ATTRIBUTE;
        }

        public override string GetPrefix()
        {
            int code = tree.attCode[nodeNr];
            if (!NamePool.IsPrefixed(code))
            {
                return "";
            }

            return tree.prefixPool.GetPrefix(code >> 20);
        }

        public override string GetLocalPart()
        {
            return tree.GetNamePool().GetLocalName(tree.attCode[nodeNr]);
        }

        public override NamespaceUri GetNamespaceUri()
        {
            int code = tree.attCode[nodeNr];
            if (!NamePool.IsPrefixed(code))
            {
                return NamespaceUri.NULL;
            }

            return tree.GetNamePool().GetURI(code);
        }

        public override bool HasURI(NamespaceUri ns)
        {
            int code = tree.attCode[nodeNr];
            if (!NamePool.IsPrefixed(code))
            {
                return ns.IsEmpty();
            }

            return GetNamePool().GetStructuredQName(code).HasURI(ns);
        }

        public override ISchemaType GetSchemaType()
        {
            if (tree.attType == null)
            {
                return BuiltInAtomicType.UNTYPED_ATOMIC;
            }

            return tree.GetAttributeType(nodeNr);
        }

        public override IAtomicSequence Atomize()
        {
            return tree.GetTypedValueOfAttribute(this, nodeNr);
        }

        public override void GenerateId(StringBuilder buffer)
        {
            GetParent().GenerateId(buffer);
            buffer.Append('a');
            TinyNodeImpl.AppendIdDigits(buffer, tree.attCode[nodeNr]); // we previously used the attribute name. But this breaks the requirement
            // that the result of generate-id consists entirely of alphanumeric ASCII
            // characters
        }

        /// <summary>
        /// Copy this node to a given {@code IReceiver}
        /// </summary>
        public override void Copy(IReceiver @out, int copyOptions, ILocation locationId)
        {
            throw new NotSupportedException("copy() applied to attribute node");
        }

        /// <summary>
        /// Get the line number of the node within its source document entity
        /// </summary>
        public override int GetLineNumber()
        {
            return GetParent().GetLineNumber();
        }

        public override int GetColumnNumber()
        {
            return GetParent().GetColumnNumber();
        }

        public override bool IsId()
        {
            return tree.IsIdAttribute(nodeNr);
        }

        public override bool IsIdref()
        {
            return tree.IsIdrefAttribute(nodeNr);
        }

        public override bool IsNilled()
        {
            return false;
        }

        public override int GetHashCode()
        {
            return ((int)(tree.GetDocumentNumber() & 0x3ff) << 20) ^ nodeNr ^ 7 << 17;
        }
    }
}
