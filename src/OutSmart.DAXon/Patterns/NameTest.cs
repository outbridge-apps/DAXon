////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Trees.Tiny;
using OutSmart.DAXon.Collections;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Types;
namespace OutSmart.DAXon.Patterns
{
    internal sealed class NameTest : NodeTest, IQNameTest
    {
        private readonly int nodeKind;
        private readonly int fingerprint;
        private readonly UType uType;
        private readonly NamePool namePool;
        private NamespaceUri uri; // the URI corresponding to the fingerprint - computed lazily
        private string localName; //the local name corresponding to the fingerprint - computed lazily

        /// <summary>
        /// Determine the default priority of this node test when used on its own as a Pattern
        /// </summary>
        public override double DefaultPriority => 0;

        public override int Fingerprint => fingerprint;

        public override StructuredQName MatchingNodeName
        {
            get
            {
                ComputeUriAndLocal();
                return new StructuredQName("", uri, localName);
            }
        }

        public override int PrimitiveType => nodeKind;

        public override IntSet RequiredNodeNames => (new IntSingletonSet(fingerprint));
        public NameTest(int nodeKind, NamespaceUri uri, string localName, NamePool namePool)
        {
            this.uri = uri;
            this.localName = localName;
            this.nodeKind = nodeKind;
            this.fingerprint = namePool.AllocateFingerprint(uri, localName) & NamePool.FP_MASK;
            this.namePool = namePool;
            this.uType = UType.FromTypeCode(nodeKind);
        }

        public NameTest(int nodeKind, int nameCode, NamePool namePool)
        {
            this.nodeKind = nodeKind;
            this.fingerprint = nameCode & NamePool.FP_MASK;
            this.namePool = namePool;
            this.uType = UType.FromTypeCode(nodeKind);
        }

        public NameTest(int nodeKind, INodeName name, NamePool pool)
        {
            this.uri = name.GetNamespaceUri();
            this.localName = name.GetLocalPart();
            this.nodeKind = nodeKind;
            this.fingerprint = name.ObtainFingerprint(pool);
            this.namePool = pool;
            this.uType = UType.FromTypeCode(nodeKind);
        }

        public NamePool GetNamePool()
        {
            return namePool;
        }

        public int GetNodeKind()
        {
            return nodeKind;
        }

        public override UType GetUType()
        {
            return uType;
        }

        public override bool Matches(int nodeKind, INodeName name, ISchemaType annotation)
        {
            if (nodeKind != this.nodeKind)
            {
                return false;
            }

            if (name.HasFingerprint())
            {
                return name.Fingerprint == this.fingerprint;
            }
            else
            {
                ComputeUriAndLocal();
                return name.HasURI(uri) && name.GetLocalPart().Equals(localName);
            }
        }

        public override IIntPredicateProxy GetMatcher(INodeVectorTree tree)
        {
            byte[] nodeKindArray = tree.NodeKindArray;
            int[] nameCodeArray = tree.NameCodeArray;
            return IntPredicateLambda.Of((nodeNr) => (nameCodeArray[nodeNr] & 0xfffff) == fingerprint && (nodeKindArray[nodeNr] & 0x0f) == nodeKind);
        }

        public override bool Test(NodeInfo node)
        {
            if (node.GetNodeKind() != nodeKind)
            {
                return false;
            }


            // Two different algorithms are used for name matching. If the fingerprint of the node is readily
            // available, we use it to do an integer comparison. Otherwise, we do string comparisons on the URI
            // and local name. In practice, Saxon's native node implementations use fingerprint matching, while
            // DOM and JDOM nodes use string comparison of names
            if (node.HasFingerprint())
            {
                return node.Fingerprint == fingerprint;
            }
            else
            {
                ComputeUriAndLocal();
                return localName.Equals(node.GetLocalPart()) && uri.Equals(node.GetNamespaceUri());
            }
        }

        private void ComputeUriAndLocal()
        {
            if (uri == null || localName == null)
            {
                StructuredQName name = namePool.GetUnprefixedQName(fingerprint);
                uri = name.GetNamespaceUri();
                localName = name.GetLocalPart();
            }
        }

        public bool Matches(StructuredQName qname)
        {
            ComputeUriAndLocal();
            return qname.GetLocalPart().Equals(localName) && qname.HasURI(uri);
        }

        public bool MatchesFingerprint(NamePool namePool, int fp)
        {
            return fp == fingerprint;
        }

        public NamespaceUri GetNamespaceURI()
        {
            ComputeUriAndLocal();
            return uri;
        }

        public override string ToString()
        {
            switch (nodeKind)
            {
                case Types.Type.ELEMENT:
                    return "element(" + namePool.GetEQName(fingerprint) + ")";
                case Types.Type.ATTRIBUTE:
                    return "attribute(" + namePool.GetEQName(fingerprint) + ")";
                case Types.Type.PROCESSING_INSTRUCTION:
                    return "processing-instruction(" + namePool.GetLocalName(fingerprint) + ')';
                case Types.Type.NAMESPACE:
                    return "namespace-node(" + namePool.GetLocalName(fingerprint) + ')';
            }

            return namePool.GetEQName(fingerprint);
        }

        /// <summary>
        /// Returns a hash code value for the object.
        /// </summary>
        public override int GetHashCode()
        {
            return nodeKind << 20 ^ fingerprint;
        }

        public override bool Equals(object other)
        {
            return other is NameTest && ((NameTest)other).namePool == namePool && ((NameTest)other).nodeKind == nodeKind && ((NameTest)other).fingerprint == fingerprint;
        }

        public string ExportQNameTest()
        {
            return MatchingNodeName.EQName;
        }

        public override string ExplainMismatch(IItem item, TypeHierarchy th)
        {
            string explanation = base.ExplainMismatch(item, th);
            if (explanation != null)
            {
                return explanation;
            }

            return ("The node has the wrong name");
        }

        public override string ToShortString()
        {
            switch (nodeKind)
            {
                case Types.Type.ELEMENT:
                    return GetNamespaceURI().IsEmpty() ? namePool.GetLocalName(Fingerprint) : ToString();
                case Types.Type.ATTRIBUTE:
                    return "@" + (GetNamespaceURI().IsEmpty() ? namePool.GetLocalName(Fingerprint) : ToString());
                default:
                    return ToString();
            }
        }
    }
}
