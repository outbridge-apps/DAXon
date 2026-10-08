////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;

using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;

namespace OutSmart.DAXon.Events
{
    internal sealed class DocumentValidator : ProxyReceiver
    {
        private bool foundElement = false;
        private int level = 0;
        private readonly string errorCode;
        public DocumentValidator(IReceiver next, string errorCode) : base(next)
        {
            this.errorCode = errorCode;
        }

        public override void SetPipelineConfiguration(PipelineConfiguration config)
        {
            base.SetPipelineConfiguration(config);
        }

        /// <summary>
        /// Start of an element
        /// </summary>
        public override void StartElement(INodeName elemName, ISchemaType type, IAttributeMap attributes, NamespaceMap namespaces, ILocation location, int properties)
        {
            if (foundElement && level == 0)
            {
                throw new XPathException("A valid document must have only one child element", errorCode);
            }

            foundElement = true;
            level++;
            nextReceiver.StartElement(elemName, type, attributes, namespaces, location, properties);
        }

        /// <summary>
        /// Character data
        /// </summary>
        public override void Characters(UnicodeString chars, ILocation locationId, int properties)
        {
            if (level == 0)
            {
                if (Whitespace.IsAllWhite(chars))
                {
                    return; // ignore whitespace outside the outermost element
                }

                throw new XPathException("A valid document must contain no text outside the outermost element (found \"" + Err.Truncate30(chars.Tidy()) + "\")", errorCode);
            }

            nextReceiver.Characters(chars, locationId, properties);
        }

        /// <summary>
        /// End of element
        /// </summary>
        public override void EndElement()
        {
            level--;
            nextReceiver.EndElement();
        }

        /// <summary>
        /// Notify the end of a document node
        /// </summary>
        public override void EndDocument()
        {
            if (level == 0)
            {
                if (!foundElement)
                {
                    throw new XPathException("A valid document must have a child element", errorCode);
                }

                foundElement = false;
                nextReceiver.EndDocument();
                level = -1;
            }
        }
    }
}
