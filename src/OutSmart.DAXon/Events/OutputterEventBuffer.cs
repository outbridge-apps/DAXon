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
using System.Collections.Generic;

namespace OutSmart.DAXon.Events
{
    // Faithful port of net.sf.saxon.event.OutputterEventBuffer (Saxon 12.9). Was a hollow stub whose
    // every method did NOTHING and Replay was empty — xsl:try with rollback-output=yes (the DEFAULT)
    // buffered its try-branch output into a black hole, so every successful xsl:try produced empty
    // output.
    // Records Outputter events in memory for subsequent replay (used by try/catch, where events must
    // not reach the final serializer until we know no error occurs). Events retain their properties,
    // implementing "sticky disable-output-escaping".
    internal sealed class OutputterEventBuffer : Outputter
    {
        private readonly IList<OutputterEvent> buffer = new List<OutputterEvent>();

        public OutputterEventBuffer()
        {
        }

        public override void StartDocument(int properties)
        {
            buffer.Add(new OutputterEvent.StartDocument(properties));
        }

        public override void EndDocument()
        {
            buffer.Add(new OutputterEvent.EndDocument());
        }

        public override void StartElement(INodeName elemName, ISchemaType typeCode, ILocation location, int properties)
        {
            buffer.Add(new OutputterEvent.StartElement(elemName, typeCode, location, properties));
        }

        public override void StartElement(INodeName elemName, ISchemaType type, IAttributeMap attributes,
            NamespaceMap namespaces, ILocation location, int properties)
        {
            buffer.Add(new OutputterEvent.StartElement(elemName, type, location, properties));
            foreach (AttributeInfo att in attributes)
            {
                buffer.Add(new OutputterEvent.Attribute(att.GetNodeName(), att.GetType(), att.Value, att.GetLocation(), att.GetProperties()));
            }

            foreach (NamespaceBinding binding in namespaces)
            {
                buffer.Add(new OutputterEvent.Namespace(binding.GetPrefix(), binding.GetNamespaceUri(), properties));
            }

            buffer.Add(new OutputterEvent.StartContent());
        }

        public override void Attribute(INodeName name, ISimpleType type, string value, ILocation location, int properties)
        {
            buffer.Add(new OutputterEvent.Attribute(name, type, value, location, properties));
        }

        public override void Namespace(string prefix, NamespaceUri uri, int properties)
        {
            buffer.Add(new OutputterEvent.Namespace(prefix, uri, properties));
        }

        public override void StartContent()
        {
            buffer.Add(new OutputterEvent.StartContent());
        }

        public override void EndElement()
        {
            buffer.Add(new OutputterEvent.EndElement());
        }

        public override void Characters(UnicodeString chars, ILocation location, int properties)
        {
            buffer.Add(new OutputterEvent.Text(chars, location, properties));
        }

        public override void ProcessingInstruction(string name, UnicodeString data, ILocation location, int properties)
        {
            buffer.Add(new OutputterEvent.ProcessingInstruction(name, data, location, properties));
        }

        public override void Comment(UnicodeString content, ILocation location, int properties)
        {
            buffer.Add(new OutputterEvent.Comment(content, location, properties));
        }

        public override void Append(IItem item, ILocation location, int properties)
        {
            buffer.Add(new OutputterEvent.Append(item, location, properties));
        }

        public override void Close()
        {
            // no action (Java close())
        }

        /// <summary>
        /// Replay the captured events to a supplied destination.
        /// </summary>
        public void Replay(Outputter @out)
        {
            foreach (OutputterEvent @event in buffer)
            {
                @event.Replay(@out);
            }
        }
    }
}
