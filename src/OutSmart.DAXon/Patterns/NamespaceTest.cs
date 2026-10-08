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
    public sealed class NamespaceTest : NodeTest, IQNameTest
    {
        private readonly NamePool namePool;
        private readonly int nodeKind;
        private readonly UType uType;
        private readonly NamespaceUri uri;

        public int NodeKind => nodeKind;

        public override IntSet RequiredNodeNames => null;

        public string FullAlphaCode => BasicAlphaCode + " nQ{" + uri + "}*";

        public override double DefaultPriority => -0.25;

        public override int PrimitiveType => nodeKind;

        public NamespaceUri NamespaceURI => uri;
        public NamespaceTest(NamePool pool, int nodeKind, NamespaceUri uri)
        {
            namePool = pool;
            this.nodeKind = nodeKind;
            this.uri = uri;
            this.uType = UType.FromTypeCode(nodeKind);
        }

        public override UType GetUType()
        {
            return uType;
        }

        public override bool Matches(int nodeKind, INodeName name, ISchemaType annotation)
        {
            return name != null && name.HasURI(uri);
        }

        public override IIntPredicateProxy GetMatcher(INodeVectorTree tree)
        {
            byte[] nodeKindArray = tree.NodeKindArray;
            int[] nameCodeArray = tree.NameCodeArray;
            return IntPredicateLambda.Of((nodeNr) =>
            {
                int fp = nameCodeArray[nodeNr] & 0xfffff;
                return fp != -1 && (nodeKindArray[nodeNr] & 0x0f) == nodeKind && uri.Equals(namePool.GetURI(fp));
            });
        }

        public override bool Test(NodeInfo node)
        {
            return node.GetNodeKind() == nodeKind && node.GetNamespaceUri().Equals(uri);
        }

        public bool Matches(StructuredQName qname)
        {
            return qname.HasURI(uri);
        }

        public bool MatchesFingerprint(NamePool namePool, int fp)
        {
            return namePool.GetURI(fp).Equals(uri);
        }

        public override string ToString()
        {
            switch (nodeKind)
            {
                case Types.Type.ELEMENT:
                    return "Q{" + uri + "}*";
                case Types.Type.ATTRIBUTE:
                    return "@Q{" + uri + "}*";
                default:

                    // should not happen
                    return "(*" + nodeKind + "*)Q{" + uri + "}*";
            }
        }

        /// <summary>
        /// Returns a hash code value for the object.
        /// </summary>
        public override int GetHashCode()
        {
            return uri.GetHashCode() << 5 + nodeKind;
        }

        public override bool Equals(object other)
        {
            return other is NamespaceTest && ((NamespaceTest)other).namePool == namePool && ((NamespaceTest)other).nodeKind == nodeKind && ((NamespaceTest)other).uri.Equals(uri);
        }

        public string ExportQNameTest()
        {
            return "Q{" + uri + "}*";
        }

        public override string ExplainMismatch(IItem item, TypeHierarchy th)
        {
            string explanation = base.ExplainMismatch(item, th);
            if (explanation != null)
            {
                return explanation;
            }

            return ("The node @is in the wrong namespace");
        }
    }
}
