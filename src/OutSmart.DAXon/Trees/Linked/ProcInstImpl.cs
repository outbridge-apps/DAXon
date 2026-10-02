////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Api;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Trees.Utilities;
using OutSmart.DAXon.Values;

namespace OutSmart.DAXon.Trees.Linked
{
    internal sealed class ProcInstImpl : NodeImpl
    {
        private string name;
        private UnicodeString content;
        private string systemId;
        private int lineNumber = -1;
        private int columnNumber = -1;

        public ProcInstImpl(string name, UnicodeString content)
        {
            this.name = name;
            this.content = content;
        }

        public override INodeName GetNodeName()
        {
            return new NoNamespaceName(name);
        }

        public override UnicodeString UnicodeStringValue => content;

        public override IAtomicSequence Atomize()
        {
            return new StringValue(content);
        }

        public override int GetNodeKind()
        {
            return Types.Type.PROCESSING_INSTRUCTION;
        }

        public void SetLocation(string uri, int lineNumber, int columnNumber)
        {
            systemId = uri;
            this.lineNumber = lineNumber;
            this.columnNumber = columnNumber;
        }

        public override string GetSystemId()
        {
            return systemId;
        }

        // A PI from an external entity has that entity's base URI, as in the tiny tree.
        public override string GetBaseURI()
        {
            return Navigator.GetBaseURI(this);
        }

        public override int GetLineNumber()
        {
            return lineNumber;
        }

        public override int GetColumnNumber()
        {
            return columnNumber;
        }

        public override void Copy(IReceiver @out, int copyOptions, ILocation locationId)
        {
            @out.ProcessingInstruction(name, content, locationId, ReceiverOption.NONE);
        }

        public override void Rename(INodeName newNameCode, bool inherit)
        {
            name = newNameCode.GetLocalPart();
        }

        public override void ReplaceStringValue(UnicodeString stringValue)
        {
            content = stringValue;
        }
    }
}
