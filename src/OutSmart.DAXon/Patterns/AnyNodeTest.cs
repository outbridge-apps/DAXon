////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;

using OutSmart.DAXon.Trees.Tiny;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Collections;
namespace OutSmart.DAXon.Patterns
{
    internal sealed class AnyNodeTest : NodeTest, IQNameTest
    {
        private static readonly AnyNodeTest THE_INSTANCE = new AnyNodeTest();

        public override double DefaultPriority => -0.5;

        private AnyNodeTest()
        {
        }
        public static AnyNodeTest GetInstance()
        {
            return THE_INSTANCE;
        }

        public override UType GetUType()
        {
            return UType.ANY_NODE;
        }

        public override bool Matches(int nodeKind, INodeName name, ISchemaType annotation)
        {
            return nodeKind != Types.Type.PARENT_POINTER;
        }

        public override IIntPredicateProxy GetMatcher(INodeVectorTree tree)
        {
            byte[] nodeKindArray = tree.NodeKindArray;
            return IntPredicateLambda.Of((nodeNr) => nodeKindArray[nodeNr] != Types.Type.PARENT_POINTER);
        }

        public override bool Test(NodeInfo node)
        {
            return true;
        }

        public bool Matches(StructuredQName qname)
        {
            return true;
        }

        public bool MatchesFingerprint(NamePool namePool, int fp)
        {
            return true;
        }

        public override string ToString()
        {
            return "node()";
        }

        public string ExportQNameTest()
        {
            return "*";
        }
    }
}
