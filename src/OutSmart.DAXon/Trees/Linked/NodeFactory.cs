////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Types;
namespace OutSmart.DAXon.Trees.Linked
{
    public interface INodeFactory
    {
        ElementImpl MakeElementNode(NodeInfo parent, INodeName nameCode, ISchemaType elementType, bool isNilled, IAttributeMap attlist, NamespaceMap namespaces, PipelineConfiguration pipe, ILocation locationId, int sequenceNumber);
        TextImpl MakeTextNode(NodeInfo parent, UnicodeString content);
    }
}