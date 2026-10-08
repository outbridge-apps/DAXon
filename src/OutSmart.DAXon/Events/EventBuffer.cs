////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Types;
using System.Collections.Generic;
using OutSmart.DAXon.Model;

namespace OutSmart.DAXon.Events
{
    internal sealed class EventBuffer : SequenceReceiver
    {
        private readonly IList<object> buffer = new List<object>();
        public EventBuffer(PipelineConfiguration pipe) : base(pipe)
        {
        }

        public override bool UsesTypeAnnotations() => false; // events are replayed verbatim

        public override void StartDocument(int properties)
        {
            buffer.Add(new Event.StartDocument(properties));
        }

        public override void EndDocument()
        {
            buffer.Add(new Event.EndDocument());
        }

        public override void StartElement(INodeName elemName, ISchemaType typeCode, IAttributeMap attributes, NamespaceMap namespaces, ILocation location, int properties)
        {
            buffer.Add(new Event.StartElement(elemName, typeCode, attributes, namespaces, location, properties));
        }

        public override void EndElement()
        {
            buffer.Add(new Event.EndElement());
        }

        public override void Characters(UnicodeString chars, ILocation location, int properties)
        {
            buffer.Add(new Event.Text(chars, location, properties));
        }

        public override void ProcessingInstruction(string name, UnicodeString data, ILocation location, int properties)
        {
            buffer.Add(new Event.ProcessingInstruction(name, data, location, properties));
        }

        public override void Comment(UnicodeString content, ILocation location, int properties)
        {
            buffer.Add(new Event.Comment(content, location, properties));
        }

        public override void Append(IItem item, ILocation location, int properties)
        {
            buffer.Add(new Event.Append(item, location, properties));
        }

        public override void Close()
        {
        }

        // no action
        public void Replay(IReceiver @out)
        {
            foreach (Event @event in buffer)
            {
                @event.Replay(@out);
            }
        }
    }
}
