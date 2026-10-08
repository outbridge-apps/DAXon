////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Model;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Types;

// Faithful port of net.sf.saxon.event.EventMonitor (Saxon 12.9). Was a hollow stub — it discarded every
// event and hasBeenWrittenTo() always returned false, so xsl:try rollback-output="no" lost the try body's
// output and never raised XTDE3530 when recovery was impossible (try-034). A filter that passes all events
// down the pipeline unchanged while recording whether any data has been written. Kept in OutSmart.DAXon.Functions
// (its historic home here) — TryCatch is the sole caller.
namespace OutSmart.DAXon.Functions
{
    internal sealed class EventMonitor : Outputter
    {
        private bool written;
        private readonly Outputter next;

        public EventMonitor(Outputter next)
        {
            this.next = next;
        }

        public override void StartDocument(int properties)
        {
            written = true;
            next.StartDocument(properties);
        }

        public override void StartElement(INodeName elemName, ISchemaType type, ILocation location, int properties)
        {
            written = true;
            next.StartElement(elemName, type, location, properties);
        }

        public override void EndElement()
        {
            written = true;
            next.EndElement();
        }

        public override void Attribute(INodeName name, ISimpleType type, string value, ILocation location, int properties)
        {
            written = true;
            next.Attribute(name, type, value, location, properties);
        }

        public override void Namespace(string prefix, NamespaceUri uri, int properties)
        {
            written = true;
            next.Namespace(prefix, uri, properties);
        }

        public override void StartContent()
        {
            written = true;
            next.StartContent();
        }

        public override void Characters(UnicodeString chars, ILocation location, int properties)
        {
            written = true;
            next.Characters(chars, location, properties);
        }

        public override void ProcessingInstruction(string name, UnicodeString data, ILocation location, int properties)
        {
            written = true;
            next.ProcessingInstruction(name, data, location, properties);
        }

        public override void Comment(UnicodeString content, ILocation location, int properties)
        {
            written = true;
            next.Comment(content, location, properties);
        }

        public override void Append(IItem item, ILocation location, int properties)
        {
            written = true;
            next.Append(item, location, properties);
        }

        public override void EndDocument()
        {
            written = true;
            next.EndDocument();
        }

        public bool HasBeenWrittenTo()
        {
            return written;
        }
    }
}
