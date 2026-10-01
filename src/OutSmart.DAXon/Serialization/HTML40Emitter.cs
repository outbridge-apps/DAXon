////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
using OutSmart.DAXon.Core;
namespace OutSmart.DAXon.Serialization
{
    /// <summary>
    /// This class generates HTML 4.0 output
    /// </summary>
    internal sealed class HTML40Emitter : HTMLEmitter
    {
        static HTML40Emitter()
        {
            SetEmptyTag("area");
            SetEmptyTag("base");
            SetEmptyTag("basefont");
            SetEmptyTag("br");
            SetEmptyTag("col");
            SetEmptyTag("embed");
            SetEmptyTag("frame");
            SetEmptyTag("hr");
            SetEmptyTag("img");
            SetEmptyTag("input");
            SetEmptyTag("isindex");
            SetEmptyTag("link");
            SetEmptyTag("meta");
            SetEmptyTag("param");
        }

        public HTML40Emitter()
        {
        }

        protected override bool IsHTMLElement(INodeName name)
        {
            return name.HasURI(NamespaceUri.NULL);
        }

        protected override void OpenDocument()
        {
            string versionProperty = outputProperties.GetProperty(DAXonOutputKeys.HTML_VERSION);

            // Note, we recognize html-version even when running XSLT 2.0.
            if (versionProperty == null)
            {
                versionProperty = outputProperties.GetProperty(DAXonOutputKeys.VERSION);
            }

            if (versionProperty != null)
            {
                if (versionProperty.Equals("4.0") || versionProperty.Equals("4.01"))
                {
                    version = 4;
                }
                else
                {
                    throw new XPathException("Unsupported HTML version: " + versionProperty).WithErrorCode("SESU0013");
                }
            }

            base.OpenDocument();
        }

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

                if (systemId != null || publicId != null)
                {
                    WriteDocType(null, "html", systemId, publicId);
                }

                started = true;
            }

            base.StartElement(elemName, type, attributes, namespaces, location, properties);
        }

        protected override bool RejectControlCharacters()
        {
            return true;
        }
    }
}