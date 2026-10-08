////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
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
    /// <summary>
    /// A Sink is a IReceiver that discards all information passed to it
    /// </summary>
    internal sealed class Sink : SequenceReceiver
    {
        public Sink(PipelineConfiguration pipe) : base(pipe)
        {
        }

        /// <summary>
        /// Start of event stream
        /// </summary>
        public override void Open()
        {
        }

        /// <summary>
        /// End of event stream
        /// </summary>
        public override void Close()
        {
        }

        /// <summary>
        /// Start of a document node.
        /// </summary>
        public override void StartDocument(int properties)
        {
        }

        /// <summary>
        /// Notify the end of a document node
        /// </summary>
        public override void EndDocument()
        {
        }

        /// <summary>
        /// Notify the start of an element
        /// </summary>
        public override void StartElement(INodeName elemName, ISchemaType type, IAttributeMap attributes, NamespaceMap namespaces, ILocation location, int properties)
        {
        }

        /// <summary>
        /// End of element
        /// </summary>
        public override void EndElement()
        {
        }

        /// <summary>
        /// Character data
        /// </summary>
        public override void Characters(UnicodeString chars, ILocation locationId, int properties)
        {
        }

        /// <summary>
        /// Processing Instruction
        /// </summary>
        public override void ProcessingInstruction(string target, UnicodeString data, ILocation locationId, int properties)
        {
        }

        /// <summary>
        /// Output a comment
        /// </summary>
        public override void Comment(UnicodeString chars, ILocation locationId, int properties)
        {
        }

        /// <summary>
        /// Output a comment
        /// </summary>
        public override void Append(IItem item, ILocation locationId, int copyNamespaces)
        {
        }

        /// <summary>
        /// Set the URI for an unparsed entity in the document.
        /// </summary>
        public override void SetUnparsedEntity(string name, string uri, string publicId)
        {
        }

        /// <summary>
        /// Set the URI for an unparsed entity in the document.
        /// </summary>
        public override bool UsesTypeAnnotations()
        {
            return false;
        }
    }
}
