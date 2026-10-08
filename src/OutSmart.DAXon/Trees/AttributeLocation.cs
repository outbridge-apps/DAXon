////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Trees.Utilities;
namespace OutSmart.DAXon.Trees
{
    /// <summary>
    /// A ILocation corresponding to an attribute in a document (often a stylesheet)
    /// </summary>
    internal sealed class AttributeLocation : ILocation
    {
        private readonly string systemId;
        private readonly int lineNumber;
        private readonly int columnNumber;
        private readonly StructuredQName elementName;
        private readonly StructuredQName attributeName;
        private NodeInfo elementNode;

        public StructuredQName ElementName => elementName;

        public StructuredQName AttributeName => attributeName;
        public AttributeLocation(NodeInfo element, StructuredQName attributeName)
        {
            this.systemId = element.GetSystemId();
            this.lineNumber = element.GetLineNumber();
            this.columnNumber = element.GetColumnNumber();
            this.elementName = Navigator.GetNodeName(element);
            this.attributeName = attributeName;
            if (element.GetConfiguration().GetBooleanProperty(Feature<bool>.RETAIN_NODE_FOR_DIAGNOSTICS))
            {
                this.elementNode = element;
            }
        }

        public AttributeLocation(StructuredQName elementName, StructuredQName attributeName, ILocation location)
        {
            this.systemId = location.GetSystemId();
            this.lineNumber = location.GetLineNumber();
            this.columnNumber = location.GetColumnNumber();
            this.elementName = elementName;
            this.attributeName = attributeName;
        }

        public int GetColumnNumber()
        {
            return columnNumber;
        }

        public string GetSystemId()
        {
            return systemId;
        }

        public string GetPublicId()
        {
            return null;
        }

        public int GetLineNumber()
        {
            return lineNumber;
        }

        public ILocation SaveLocation()
        {
            return this;
        }
    }
}