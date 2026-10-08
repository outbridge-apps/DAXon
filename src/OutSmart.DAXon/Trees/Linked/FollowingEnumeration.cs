////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Patterns;

namespace OutSmart.DAXon.Trees.Linked
{
    sealed class FollowingEnumeration : TreeEnumeration
    {
        private readonly NodeImpl root;

        public FollowingEnumeration(NodeImpl node, INodePredicate nodeTest) : base(node, nodeTest)
        {
            root = (NodeImpl)node.Root;
            // skip the descendant nodes if any
            int type = node.GetNodeKind();
            if (type == Types.Type.ATTRIBUTE || type == Types.Type.NAMESPACE)
            {
                nextNode = node.GetParent().GetNextInDocument(root);
            }
            else
            {
                do
                {
                    nextNode = node.GetNextSibling();
                    if (nextNode == null)
                    {
                        node = node.GetParent();
                    }
                }
                while (nextNode == null && node != null);
            }

            while (!Conforms(nextNode))
            {
                Step();
            }
        }

        protected override void Step()
        {
            nextNode = nextNode.GetNextInDocument(root);
        }
    }
}
