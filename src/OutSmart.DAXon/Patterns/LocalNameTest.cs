////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Trees.Tiny;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Collections;
using OutSmart.DAXon.Internal.Collections;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Patterns
{
    public sealed class LocalNameTest : NodeTest, IQNameTest
    {
        private readonly NamePool namePool;
        private readonly int nodeKind;
        private readonly string localName;
        private readonly UType uType;

        public int NodeKind => nodeKind;

        public override IntSet RequiredNodeNames => null;

        public string FullAlphaCode => BasicAlphaCode + " n*:" + localName;

        public override double DefaultPriority => -0.25;

        public string LocalName => localName;

        public override int PrimitiveType => nodeKind;

        public NamePool NamePool => namePool;
        public LocalNameTest(NamePool pool, int nodeKind, string localName)
        {
            this.namePool = pool;
            this.nodeKind = nodeKind;
            this.localName = localName;
            uType = UType.FromTypeCode(nodeKind);
        }

        public override UType GetUType()
        {
            return uType;
        }

        public override bool Matches(int nodeKind, INodeName name, ISchemaType annotation)
        {
            return name != null && nodeKind == this.nodeKind && localName.Equals(name.GetLocalPart());
        }

        public override IIntPredicateProxy GetMatcher(INodeVectorTree tree)
        {
            byte[] nodeKindArray = tree.NodeKindArray;
            int[] nameCodeArray = tree.NameCodeArray;
            if (nodeKind == Types.Type.ELEMENT && tree is TinyTree)
            {
                Dictionary<string, IntSet> localNameIndex = ((TinyTree)tree).LocalNameIndex;
                IntSet intSet = localNameIndex.GetOrDefault(localName);
                if (intSet == null)
                {
                    return IntPredicateLambda.Of((n) => false);
                }
                else
                {
                    return IntPredicateLambda.Of((nodeNr) => intSet.Contains(nameCodeArray[nodeNr] & NamePool.FP_MASK) && (nodeKindArray[nodeNr] & 0x0f) == Types.Type.ELEMENT);
                }
            }
            else
            {
                return IntPredicateLambda.Of((nodeNr) => (nodeKindArray[nodeNr] & 0x0f) == nodeKind && localName.Equals(namePool.GetLocalName(nameCodeArray[nodeNr] & NamePool.FP_MASK)));
            }
        }

        public override bool Test(NodeInfo node)
        {
            return localName.Equals(node.GetLocalPart()) && nodeKind == node.GetNodeKind();
        }

        public bool Matches(StructuredQName qname)
        {
            return localName.Equals(qname.GetLocalPart());
        }

        public bool MatchesFingerprint(NamePool namePool, int fp)
        {
            return namePool.GetLocalName(fp).Equals(localName);
        }

        public override string ToString()
        {
            switch (nodeKind)
            {
                case Types.Type.ELEMENT:
                    return "*:" + localName;
                case Types.Type.ATTRIBUTE:
                    return "@*:" + localName;
                default:
                    return "(*" + nodeKind + "*):" + localName; // should not be used
            }
        }

        /// <summary>
        /// Returns a hash code value for the object.
        /// </summary>
        public override int GetHashCode()
        {
            return nodeKind << 20 ^ localName.GetHashCode();
        }

        public override bool Equals(object other)
        {
            return other is LocalNameTest && ((LocalNameTest)other).nodeKind == nodeKind && ((LocalNameTest)other).localName.Equals(localName);
        }

        public string ExportQNameTest()
        {
            return "*:" + localName;
        }

        public override string ExplainMismatch(IItem item, TypeHierarchy th)
        {
            string explanation = base.ExplainMismatch(item, th);
            if (explanation != null)
            {
                return explanation;
            }

            return ("The node has the wrong local name");
        }
    }
}
