////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Types;

namespace OutSmart.DAXon.Events
{
    // Faithful port of net.sf.saxon.event.IDFilter (Saxon 12.9). Was a hollow stub that didn't even
    // extend ProxyReceiver — ResourceRequest.Resolve's fragment handling cast it to IReceiver and
    // crashed (URIs like stylesheet.xml#fragmentId, e.g. embedded xml-stylesheet references).
    // Extracts the subtree of a document rooted at the element with a given ID value. Namespace
    // declarations outside this subtree are treated as if present on the identified element.
    // Note: only looks for ID attributes, not ID elements.
    internal sealed class IDFilter : ProxyReceiver
    {
        private readonly string requiredId;
        private int activeDepth = 0;
        private bool matched = false;

        public IDFilter(IReceiver next, string id) : base(next)
        {
            this.requiredId = id;
        }

        public override void StartElement(INodeName elemName, ISchemaType type,
            IAttributeMap attributes, NamespaceMap namespaces, ILocation location, int properties)
        {
            matched = false;
            if (activeDepth == 0)
            {
                foreach (AttributeInfo att in attributes)
                {
                    if (att.GetNodeName().Equals(StandardNames.XML_ID_NAME) ||
                            ReceiverOption.Contains(att.GetProperties(), ReceiverOption.IS_ID))
                    {
                        if (att.Value.Equals(requiredId))
                        {
                            matched = true;
                        }
                    }
                }

                if (matched)
                {
                    activeDepth = 1;
                    base.StartElement(elemName, type, attributes, namespaces, location, properties);   // this remembers the details
                }
            }
            else
            {
                activeDepth++;
                base.StartElement(elemName, type, attributes, namespaces, location, properties);   // this remembers the details
            }
        }

        public override void EndElement()
        {
            if (activeDepth > 0)
            {
                nextReceiver.EndElement();
                activeDepth--;
            }
        }

        public override void Characters(UnicodeString chars, ILocation locationId, int properties)
        {
            if (activeDepth > 0)
            {
                base.Characters(chars, locationId, properties);
            }
        }

        public override void ProcessingInstruction(string target, UnicodeString data, ILocation locationId, int properties)
        {
            if (activeDepth > 0)
            {
                base.ProcessingInstruction(target, data, locationId, properties);
            }
        }

        public override void Comment(UnicodeString chars, ILocation locationId, int properties)
        {
            if (activeDepth > 0)
            {
                base.Comment(chars, locationId, properties);
            }
        }

        /// <summary>
        /// The filter inspects attribute properties (IS_ID), so type annotations are used.
        /// </summary>
        public override bool UsesTypeAnnotations()
        {
            return true;
        }
    }
}
