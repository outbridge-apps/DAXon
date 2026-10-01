////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Types;

namespace OutSmart.DAXon.Serialization
{
    // For xhtml/html5 output: elements in the XHTML/SVG/MathML namespaces are serialized with that namespace
    // as the DEFAULT namespace (no prefix). Was a hollow stub.
    internal sealed class XHTMLPrefixRemover : ProxyReceiver
    {
        public XHTMLPrefixRemover(IReceiver next) : base(next)
        {
        }

        private bool IsSpecial(NamespaceUri uri)
        {
            return uri.Equals(NamespaceUri.XHTML) || uri.Equals(NamespaceUri.SVG) || uri.Equals(NamespaceUri.MATHML);
        }

        public override void StartElement(INodeName elemName, ISchemaType type, IAttributeMap attributes, NamespaceMap namespaces, ILocation location, int properties)
        {
            foreach (NamespaceBinding ns in namespaces)
            {
                if (IsSpecial(ns.GetNamespaceUri()))
                {
                    namespaces = namespaces.Remove(ns.GetPrefix());
                }
            }

            if (IsSpecial(elemName.GetNamespaceUri()))
            {
                NamespaceUri uri = elemName.GetNamespaceUri();
                if (elemName.GetPrefix().Length != 0)
                {
                    elemName = new FingerprintedQName("", uri, elemName.GetLocalPart());
                }

                namespaces = namespaces.Put("", uri);
            }

            foreach (AttributeInfo att in attributes)
            {
                if (IsSpecial(att.GetNodeName().GetNamespaceUri()))
                {
                    namespaces = namespaces.Put(att.GetNodeName().GetPrefix(), att.GetNodeName().GetNamespaceUri());
                }
            }

            nextReceiver.StartElement(elemName, type, attributes, namespaces, location, properties);
        }
    }
}
