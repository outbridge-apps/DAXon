////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Trees.Tiny;
using OutSmart.DAXon.Trees.Utilities;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Collections;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Patterns
{
    internal sealed class SameNameTest : NodeTest, IQNameTest
    {
        private readonly NodeInfo origin;

        /// <summary>
        /// Determine the default priority of this node test when used on its own as a Pattern
        /// </summary>
        public override double DefaultPriority => 0;

        public override int Fingerprint
        {
            get
            {
                if (origin.HasFingerprint())
                {
                    return origin.Fingerprint;
                }
                else
                {
                    NamePool pool = origin.GetConfiguration().GetNamePool();
                    return pool.AllocateFingerprint(origin.GetNamespaceUri(), origin.GetLocalPart());
                }
            }
        }

        public override int PrimitiveType => origin.GetNodeKind();

        public override IntSet RequiredNodeNames => (new IntSingletonSet(Fingerprint));

        public NameTest EquivalentNameTest => new NameTest(origin.GetNodeKind(), origin.GetNamespaceUri(), origin.GetLocalPart(), origin.GetConfiguration().GetNamePool());
        public SameNameTest(NodeInfo origin)
        {
            this.origin = origin;
        }

        public override UType GetUType()
        {
            return UType.FromTypeCode(origin.GetNodeKind());
        }

        public override bool Matches(int nodeKind, INodeName name, ISchemaType annotation)
        {
            if (nodeKind != origin.GetNodeKind())
            {
                return false;
            }

            if (name.HasFingerprint() && origin.HasFingerprint())
            {
                return name.Fingerprint == origin.Fingerprint;
            }
            else
            {
                return name.HasURI(origin.GetNamespaceUri()) && name.GetLocalPart().Equals(origin.GetLocalPart());
            }
        }

        public override IIntPredicateProxy GetMatcher(INodeVectorTree tree)
        {
            byte[] nodeKindArray = tree.NodeKindArray;
            int[] nameCodeArray = tree.NameCodeArray;
            return IntPredicateLambda.Of((nodeNr) =>
            {
                int k = nodeKindArray[nodeNr] & 0x0f;
                if (k == Types.Type.WHITESPACE_TEXT)
                {
                    k = Types.Type.TEXT;
                }

                if (k != origin.GetNodeKind())
                {
                    return false;
                }
                else if (origin.HasFingerprint())
                {
                    return (nameCodeArray[nodeNr] & 0xfffff) == origin.Fingerprint;
                }
                else
                {
                    return Navigator.HaveSameName(tree.GetNode(nodeNr), origin);
                }
            });
        }

        public override bool Test(NodeInfo node)
        {
            return node == origin || (node.GetNodeKind() == origin.GetNodeKind() && Navigator.HaveSameName(node, origin));
        }

        public bool Matches(StructuredQName qname)
        {
            return NameOfNode.MakeName(origin).GetStructuredQName().Equals(qname);
        }

        public bool MatchesFingerprint(NamePool namePool, int fp)
        {
            return fp == Fingerprint;
        }

        public override string ToString()
        {
            switch (origin.GetNodeKind())
            {
                case Types.Type.ELEMENT:
                    return "element(" + NameOfNode.MakeName(origin).GetStructuredQName().EQName + ")";
                case Types.Type.ATTRIBUTE:
                    return "attribute(" + NameOfNode.MakeName(origin).GetStructuredQName().EQName + ")";
                case Types.Type.PROCESSING_INSTRUCTION:
                    return "processing-instruction(" + origin.GetLocalPart() + ')';
                case Types.Type.NAMESPACE:
                    return "namespace-node(" + origin.GetLocalPart() + ')';
                case Types.Type.COMMENT:
                    return "comment()";
                case Types.Type.DOCUMENT:
                    return "document-node()";
                case Types.Type.TEXT:
                    return "text()";
                default:
                    return "***";
            }
        }

        /// <summary>
        /// Returns a hash code value for the object.
        /// </summary>
        public override int GetHashCode()
        {
            return origin.GetNodeKind() << 20 ^ origin.GetNamespaceUri().GetHashCode() ^ origin.GetLocalPart().GetHashCode();
        }

        public override bool Equals(object other)
        {
            return other is SameNameTest && Test(((SameNameTest)other).origin);
        }

        public string ExportQNameTest()
        {

            // Not applicable
            return "";
        }
    }
}
