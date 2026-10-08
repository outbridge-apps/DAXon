////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Types;
using System;
namespace OutSmart.DAXon.Patterns
{
    internal sealed class NodeSelector : NodeTest
    {
        private readonly Func<NodeInfo, bool> predicate;

        public override double DefaultPriority => 0;
        private NodeSelector(Func<NodeInfo, bool> predicate)
        {
            this.predicate = predicate;
        }

        public static NodeSelector Of(Func<NodeInfo, bool> predicate)
        {
            return new NodeSelector(predicate);
        }

        public override bool Test(NodeInfo node)
        {
            return predicate(node);
        }

        public override bool Matches(int nodeKind, INodeName name, ISchemaType annotation)
        {
            throw new NotSupportedException("INodePredicate doesn't support this method");
        }
    }
}
