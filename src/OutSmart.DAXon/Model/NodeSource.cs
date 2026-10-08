////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Lib;
namespace OutSmart.DAXon.Model
{
    internal sealed class NodeSource : IActiveSource
    {
        private readonly NodeInfo node;
        private string systemId;

        public NodeInfo Node => node;
        public NodeSource(NodeInfo node)
        {
            this.node = node;
            this.systemId = node.GetSystemId();
        }

        public void Deliver(IReceiver receiver, ParseOptions options)
        {
            Sender.SendDocumentInfo(node, receiver, new Loc(GetSystemId(), -1, -1));
        }

        public void SetSystemId(string systemId)
        {
            this.systemId = systemId;
        }

        public string GetSystemId()
        {
            if (systemId == null)
            {
                return node.GetSystemId();
            }
            else
            {
                return this.systemId;
            }
        }
    }
}