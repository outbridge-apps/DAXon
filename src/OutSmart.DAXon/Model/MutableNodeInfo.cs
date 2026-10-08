////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Types;
namespace OutSmart.DAXon.Model
{
    public interface IMutableNodeInfo : NodeInfo
    {
        void SetTypeAnnotation(ISchemaType type);
        void InsertChildren(NodeInfo[] source, bool atStart, bool inherit);
        void InsertSiblings(NodeInfo[] source, bool before, bool inherit);
        void SetAttributes(IAttributeMap attributes);
        void RemoveAttribute(NodeInfo attribute);
        void AddAttribute(INodeName name, ISimpleType attType, string value, int properties, bool inheritNamespaces);
        void RemoveNamespace(string prefix);


        void AddNamespace(string prefix, NamespaceUri uri);


        void Delete();
        bool IsDeleted();
        void Replace(NodeInfo[] replacement, bool inherit);
        void ReplaceStringValue(UnicodeString stringValue);
        void Rename(INodeName newName, bool inherit);
        void AddNamespace(NamespaceBinding binding, bool inherit);
        void RemoveTypeAnnotation();
        Builder NewBuilder();
    }
}
