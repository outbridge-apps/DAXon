////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2020 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Model;

namespace OutSmart.DAXon.Events
{
    internal class ProxyOutputter : Outputter
    {
        private readonly Outputter next;

        public virtual Outputter NextOutputter => next;
        public ProxyOutputter(Outputter next)
        {
            this.next = next;
            SetPipelineConfiguration(next.GetPipelineConfiguration());
            SetSystemId(next.GetSystemId());
        }

        public override void Open()
        {
            next.Open();
        }

        public override void StartDocument(int properties)
        {
            next.StartDocument(properties);
        }

        public override void EndDocument()
        {
            next.EndDocument();
        }

        public override void SetUnparsedEntity(string name, string systemID, string publicID)
        {
            next.SetUnparsedEntity(name, systemID, publicID);
        }

        public override void StartElement(INodeName elemName, ISchemaType typeCode, ILocation location, int properties)
        {
            next.StartElement(elemName, typeCode, location, properties);
        }

        public override void StartElement(INodeName elemName, ISchemaType type, IAttributeMap attributes, NamespaceMap namespaces, ILocation location, int properties)
        {
            next.StartElement(elemName, type, attributes, namespaces, location, properties);
        }

        public override void Namespace(string prefix, NamespaceUri namespaceUri, int properties)
        {
            next.Namespace(prefix, namespaceUri, properties);
        }

        public override void Attribute(INodeName attName, ISimpleType typeCode, string value, ILocation location, int properties)
        {
            next.Attribute(attName, typeCode, value, location, properties);
        }

        public override void StartContent()
        {
            next.StartContent();
        }

        public override void EndElement()
        {
            next.EndElement();
        }

        public override void Characters(UnicodeString chars, ILocation location, int properties)
        {
            next.Characters(chars, location, properties);
        }

        public override void ProcessingInstruction(string name, UnicodeString data, ILocation location, int properties)
        {
            next.ProcessingInstruction(name, data, location, properties);
        }

        public override void Comment(UnicodeString content, ILocation location, int properties)
        {
            next.Comment(content, location, properties);
        }

        public override void Append(IItem item, ILocation locationId, int properties)
        {
            next.Append(item, locationId, properties);
        }

        public override void Append(IItem item)
        {
            next.Append(item);
        }

        public override void Close()
        {
            next.Close();
        }

        public override void Dispose()
        {
            next?.Dispose();
        }

        public override bool UsesTypeAnnotations()
        {
            return next.UsesTypeAnnotations();
        }
    }
}