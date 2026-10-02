////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Patterns;

namespace OutSmart.DAXon.Trees.Linked
{
    /// <summary>
    /// The descendant and descendant-or-self axes: a walk in document order bounded by the
    /// origin, iterative so the depth of the tree costs no stack.
    /// </summary>
    internal sealed class DescendantAxisIterator : TreeEnumeration
    {
        private readonly NodeImpl root;

        public DescendantAxisIterator(NodeImpl node, bool includeSelf, INodePredicate nodeTest) : base(node, nodeTest)
        {
            root = node;
            if (!includeSelf || !Conforms(node))
            {
                Advance();
            }
        }

        protected override void Step()
        {
            nextNode = nextNode.GetNextInDocument(root);
        }
    }
}
