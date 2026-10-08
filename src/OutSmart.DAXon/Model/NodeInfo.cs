////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Trees.Iterators;
using System.Collections.Generic;
using System.Text;
using OutSmart.DAXon.Types;
namespace OutSmart.DAXon.Model
{
    public interface NodeInfo : IItem, ILocation, IActiveSource
    {
        ITreeInfo GetTreeInfo();
        Configuration GetConfiguration();



        int GetNodeKind();
        bool IsSameNodeInfo(NodeInfo other);



        bool Equals(object other);
        int GetHashCode();
        string GetSystemId();
        string GetPublicId();



        string GetBaseURI();
        int GetLineNumber();



        int GetColumnNumber();



        int CompareOrder(NodeInfo other);
        bool HasFingerprint();
        int Fingerprint { get; }
        string GetLocalPart();
        NamespaceUri GetNamespaceUri();
        string GetURI();



        string DisplayName { get; }
        string GetPrefix();
        ISchemaType GetSchemaType();













        IAtomicSequence Atomize();
        NodeInfo GetParent();
        IAxisIterator IterateAxis(int axisNumber);



        IAxisIterator IterateAxis(int axisNumber, INodePredicate predicate);
        string GetAttributeValue(NamespaceUri uri, string local);




        NodeInfo Root { get; }
        bool HasChildNodes();
        IEnumerable<NodeInfo> Children();



        IEnumerable<NodeInfo> Children(INodePredicate filter);



        IAttributeMap Attributes();














        void GenerateId(StringBuilder buffer);
        void Copy(IReceiver @out, int copyOptions, ILocation locationId);



        void Deliver(IReceiver receiver, ParseOptions options);



        IActiveSource AsActiveSource();



        void SetSystemId(string systemId);
        NamespaceBinding[] GetDeclaredNamespaces(NamespaceBinding[] buffer);
        NamespaceMap AllNamespaces { get; }
        bool IsId();



        bool IsIdref();



        bool IsNilled();



        bool IsStreamed();



        string ToShortString();























        Genre GetGenre();


    }
}
