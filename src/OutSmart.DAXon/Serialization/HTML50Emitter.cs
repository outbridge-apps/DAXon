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
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
using OutSmart.DAXon.Core;
using System.IO;
using OutSmart.DAXon.Lib;
namespace OutSmart.DAXon.Serialization
{
    /// <summary>
    /// This class generates HTML 5.0 output
    /// </summary>
    internal sealed class HTML50Emitter : HTMLEmitter
    {

        private static readonly byte[] DOCTYPE = StringConstants.Bytes("<!DOCTYPE HTML>");
        private static readonly byte[] NEWLINE = StringConstants.Bytes("\n");
        static HTML50Emitter()
        {
            SetEmptyTag("area");
            SetEmptyTag("base");
            SetEmptyTag("base");
            SetEmptyTag("basefont");
            SetEmptyTag("br");
            SetEmptyTag("col");

            //setEmptyTag("command"); // bug 3277 (spec bug 30119)
            SetEmptyTag("embed");
            SetEmptyTag("frame");
            SetEmptyTag("hr");
            SetEmptyTag("img");
            SetEmptyTag("input");
            SetEmptyTag("isindex");
            SetEmptyTag("keygen");
            SetEmptyTag("link");
            SetEmptyTag("meta");
            SetEmptyTag("param");
            SetEmptyTag("source");
            SetEmptyTag("track");
            SetEmptyTag("wbr");
        }

        public HTML50Emitter()
        {
            version = 5;
        }

        protected override bool IsHTMLElement(INodeName name)
        {
            NamespaceUri uri = name.GetNamespaceUri();
            return uri.IsEmpty() || uri.Equals(NamespaceUri.XHTML);
        }

        protected override void OpenDocument()
        {
            version = 5;
            base.OpenDocument();
        }

        protected override void WriteDocType(INodeName name, string displayName, string systemId, string publicId)
        {
            try
            {
                if (systemId == null && publicId == null)
                {
                    if (name.GetLocalPart().Equals("html", global::System.StringComparison.OrdinalIgnoreCase))
                    {
                        writer.WriteAscii(DOCTYPE);
                        if ("yes".Equals(outputProperties.GetProperty("indent", "yes")))
                        {
                            writer.WriteAscii(NEWLINE);
                        }
                    }
                }
                else
                {
                    base.WriteDocType(name, displayName, systemId, publicId);
                }
            }
            catch (IOException err)
            {
                throw new XPathException(err?.Message);
            }
        }
        protected override bool WriteDocTypeWithNullSystemId()
        {
            return true;
        }

        /// <summary>
        /// Output element start tag
        /// </summary>
        public override void StartElement(INodeName elemName, ISchemaType type, IAttributeMap attributes, NamespaceMap namespaces, ILocation location, int properties)
        {
            if (!started)
            {
                OpenDocument();
                string systemId = outputProperties.GetProperty(DAXonOutputKeys.DOCTYPE_SYSTEM);
                string publicId = outputProperties.GetProperty(DAXonOutputKeys.DOCTYPE_PUBLIC);

                // Treat "" as equivalent to absent. This goes beyond what the spec strictly allows.
                if ("".Equals(systemId))
                {
                    systemId = null;
                }

                if ("".Equals(publicId))
                {
                    publicId = null;
                }

                WriteDocType(elemName, "html", systemId, publicId);
                started = true;
            }

            base.StartElement(elemName, type, attributes, namespaces, location, properties);
        }

        /// <summary>
        /// Output element start tag
        /// </summary>
        protected override bool RejectControlCharacters()
        {
            return false;
        }
    }
}
