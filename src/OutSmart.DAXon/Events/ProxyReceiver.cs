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
    public class ProxyReceiver : SequenceReceiver
    {
        protected IReceiver nextReceiver;

        public virtual IReceiver NextReceiver => nextReceiver;
        public ProxyReceiver(IReceiver nextReceiver) : base(nextReceiver.GetPipelineConfiguration())
        {
            SetUnderlyingReceiver(nextReceiver);
            SetPipelineConfiguration(nextReceiver.GetPipelineConfiguration());
        }

        public override void SetSystemId(string systemId)
        {

            if (systemId != this.systemId)
            {

                // use of == rather than equals() is deliberate, since this is only an optimization
                this.systemId = systemId;
                nextReceiver.SetSystemId(systemId);
            }
        }

        public virtual void SetUnderlyingReceiver(IReceiver receiver)
        {
            nextReceiver = receiver;
        }

        public override void SetPipelineConfiguration(PipelineConfiguration pipe)
        {
            if (pipelineConfiguration != pipe)
            {
                pipelineConfiguration = pipe;
                if (nextReceiver.GetPipelineConfiguration() != pipe)
                {
                    nextReceiver.SetPipelineConfiguration(pipe);
                }
            }
        }

        /// <summary>
        /// Get the namepool for this configuration
        /// </summary>
        public override NamePool GetNamePool()
        {
            return pipelineConfiguration.GetConfiguration().GetNamePool();
        }

        /// <summary>
        /// Start of event stream
        /// </summary>
        public override void Open()
        {
            nextReceiver.Open();
        }

        /// <summary>
        /// Start of event stream
        /// </summary>
        public override void Close()
        {

            // Note: It's wrong to assume that because we've finished writing to this
            // receiver, then we've also finished writing to other receivers in the pipe.
            // In the case where the rest of the pipe is to stay open, the caller should
            // either avoid doing the close(), or should first set the underlying receiver
            // to null.
            nextReceiver.Close();
        }

        // Abort-path release: propagate down the pipe so the tail's resources (e.g. an
        // emitter's output file) are freed without emitting close events.
        public override void Dispose()
        {
            nextReceiver?.Dispose();
        }

        /// <summary>
        /// Start of a document node.
        /// </summary>
        public override void StartDocument(int properties)
        {
            nextReceiver.StartDocument(properties);
        }

        /// <summary>
        /// Notify the end of a document node
        /// </summary>
        public override void EndDocument()
        {
            nextReceiver.EndDocument();
        }

        /// <summary>
        /// Notify the end of a document node
        /// </summary>
        public override void StartElement(INodeName elemName, ISchemaType type, IAttributeMap attributes, NamespaceMap namespaces, ILocation location, int properties)
        {
            nextReceiver.StartElement(elemName, type, attributes, namespaces, location, properties);
        }

        /// <summary>
        /// End of element
        /// </summary>
        public override void EndElement()
        {
            nextReceiver.EndElement();
        }

        /// <summary>
        /// Character data
        /// </summary>
        public override void Characters(UnicodeString chars, ILocation locationId, int properties)
        {
            nextReceiver.Characters(chars, locationId, properties);
        }

        /// <summary>
        /// Processing Instruction
        /// </summary>
        public override void ProcessingInstruction(string target, UnicodeString data, ILocation locationId, int properties)
        {
            nextReceiver.ProcessingInstruction(target, data, locationId, properties);
        }

        /// <summary>
        /// Output a comment
        /// </summary>
        public override void Comment(UnicodeString chars, ILocation locationId, int properties)
        {
            nextReceiver.Comment(chars, locationId, properties);
        }

        public override void SetUnparsedEntity(string name, string uri, string publicId)
        {
            nextReceiver.SetUnparsedEntity(name, uri, publicId);
        }

        public override void Append(IItem item, ILocation locationId, int properties)
        {
            nextReceiver.Append(item, locationId, properties);
        }

        public override bool UsesTypeAnnotations()
        {
            return nextReceiver.UsesTypeAnnotations();
        }
    }
}
