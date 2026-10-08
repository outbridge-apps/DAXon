////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Model;

namespace OutSmart.DAXon.Events
{
    internal sealed class LocationCopier : ICopyInformee
    {
        private readonly bool wholeDocument;
        private readonly string systemId;
        public LocationCopier(bool wholeDocument, string systemId)
        {
            this.wholeDocument = wholeDocument;
            this.systemId = systemId;
        }

        public ILocation NotifyElementNode(NodeInfo element)
        {
            string systemId = wholeDocument ? element.GetSystemId() : element.GetBaseURI();

            // The logic behind this is that if we are copying the whole document, we will be copying all
            // the relevant xml:base attributes; so retaining the systemId values is sufficient to enable
            // the base URIs of the nodes to be preserved. But if we only copy an element (for example
            // an xsl:import-schema element - see test schema091 - then its base URI might be affected
            // by xml:base attributes that aren't being copied. Ideally we would have two separate properties,
            // but XDM doesn't work that way.
            int lineNumber = element.GetLineNumber();
            int columnNumber = element.GetColumnNumber();
            return new Loc(systemId, lineNumber, columnNumber);
        }

        public string GetSystemId()
        {
            return systemId;
        }
    }
}
