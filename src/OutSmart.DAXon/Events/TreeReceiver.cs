////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Types;
using System;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Events
{
    internal class TreeReceiver : SequenceReceiver
    {
        private readonly IReceiver nextReceiver;
        private int level = 0;
        private bool[] isDocumentLevel = new bool[20];
        public TreeReceiver(IReceiver nextInChain) : base(nextInChain.GetPipelineConfiguration())
        {
            nextReceiver = nextInChain;
            previousAtomic = false;
            SetPipelineConfiguration(nextInChain.GetPipelineConfiguration());
        }

        public override void SetSystemId(string systemId)
        {
            if (systemId != null && !systemId.Equals(this.systemId))
            {
                this.systemId = systemId;
                if (nextReceiver != null)
                {
                    nextReceiver.SetSystemId(systemId);
                }
            }
        }

        public override void SetPipelineConfiguration(PipelineConfiguration pipe)
        {
            if (pipelineConfiguration != pipe)
            {
                pipelineConfiguration = pipe;
                if (nextReceiver != null)
                {
                    nextReceiver.SetPipelineConfiguration(pipe);
                }
            }
        }

        /// <summary>
        /// Start of event sequence
        /// </summary>
        public override void Open()
        {
            if (nextReceiver == null)
            {
                throw new InvalidOperationException("TreeReceiver.open(): no underlying receiver provided");
            }

            nextReceiver.Open();
            previousAtomic = false;
        }

        /// <summary>
        /// End of event sequence
        /// </summary>
        public override void Close()
        {
            if (nextReceiver != null)
            {
                nextReceiver.Close();
            }

            previousAtomic = false;
        }

        public override void Dispose()
        {
            nextReceiver?.Dispose();
        }

        /// <summary>
        /// Start of a document node.
        /// </summary>
        public override void StartDocument(int properties)
        {
            if (level == 0)
            {
                nextReceiver.StartDocument(properties);
            }

            if (isDocumentLevel.Length - 1 < level)
            {
                Array.Resize(ref isDocumentLevel, level * 2);
            }

            isDocumentLevel[level++] = true;
        }

        /// <summary>
        /// Notify the end of a document node
        /// </summary>
        public override void EndDocument()
        {
            level--;
            if (level == 0)
            {
                nextReceiver.EndDocument();
            }
        }

        /// <summary>
        /// Notify the start of an element
        /// </summary>
        public override void StartElement(INodeName elemName, ISchemaType type, IAttributeMap attributes, NamespaceMap namespaces, ILocation location, int properties)
        {
            nextReceiver.StartElement(elemName, type, attributes, namespaces, location, properties);
            previousAtomic = false;
            if (isDocumentLevel.Length - 1 < level)
            {
                Array.Resize(ref isDocumentLevel, level * 2);
            }

            isDocumentLevel[level++] = false;
        }

        /// <summary>
        /// End of element
        /// </summary>
        public override void EndElement()
        {
            nextReceiver.EndElement();
            previousAtomic = false;
            level--;
        }

        /// <summary>
        /// Character data
        /// </summary>
        public override void Characters(UnicodeString chars, ILocation locationId, int properties)
        {
            if (!chars.IsEmpty())
            {
                nextReceiver.Characters(chars, locationId, properties);
            }

            previousAtomic = false;
        }

        /// <summary>
        /// Processing Instruction
        /// </summary>
        public override void ProcessingInstruction(string target, UnicodeString data, ILocation locationId, int properties)
        {
            nextReceiver.ProcessingInstruction(target, data, locationId, properties);
            previousAtomic = false;
        }

        /// <summary>
        /// Output a comment
        /// </summary>
        public override void Comment(UnicodeString chars, ILocation locationId, int properties)
        {
            nextReceiver.Comment(chars, locationId, properties);
            previousAtomic = false;
        }

        /// <summary>
        /// Set the URI for an unparsed entity in the document.
        /// </summary>
        public override void SetUnparsedEntity(string name, string uri, string publicId)
        {
            nextReceiver.SetUnparsedEntity(name, uri, publicId);
        }

        /// <summary>
        /// Append an arbitrary item (node or atomic value) to the output
        /// </summary>
        public override void Append(IItem item, ILocation locationId, int copyNamespaces)
        {
            Decompose(item, locationId, copyNamespaces);
        }

        /// <summary>
        /// Append an arbitrary item (node or atomic value) to the output
        /// </summary>
        public override bool UsesTypeAnnotations()
        {
            return nextReceiver.UsesTypeAnnotations();
        }
    }
}
