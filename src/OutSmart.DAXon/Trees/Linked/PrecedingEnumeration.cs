////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Patterns;

namespace OutSmart.DAXon.Trees.Linked
{
    sealed class PrecedingEnumeration : TreeEnumeration
    {
        private NodeImpl nextAncestor;

        public PrecedingEnumeration(NodeImpl node, INodePredicate nodeTest) : base(node, nodeTest)
        {
            // we need to avoid returning ancestors of the starting node
            nextAncestor = node.GetParent();
            Advance();
        }

        protected override bool Conforms(NodeImpl node)
        {
            // ASSERT: we'll never test the root node, because it's always
            // an ancestor, so nextAncestor will never be null.
            if (node != null)
            {
                if (node.Equals(nextAncestor))
                {
                    nextAncestor = nextAncestor.GetParent();
                    return false;
                }
            }

            return base.Conforms(node);
        }

        protected override void Step()
        {
            nextNode = nextNode.PreviousInDocument;
        }
    }
}
