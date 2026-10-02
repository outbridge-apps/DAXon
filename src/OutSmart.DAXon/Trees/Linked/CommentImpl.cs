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
using OutSmart.DAXon.Values;

namespace OutSmart.DAXon.Trees.Linked
{
    internal sealed class CommentImpl : NodeImpl
    {
        private UnicodeString comment;
        private string systemId;
        private int lineNumber = -1;
        private int columnNumber = -1;

        public CommentImpl(UnicodeString content)
        {
            comment = content;
        }

        public override UnicodeString UnicodeStringValue => comment;

        public override IAtomicSequence Atomize()
        {
            return new StringValue(comment);
        }

        public override int GetNodeKind()
        {
            return Types.Type.COMMENT;
        }

        public override void Copy(IReceiver @out, int copyOptions, ILocation locationId)
        {
            @out.Comment(comment, locationId, ReceiverOption.NONE);
        }

        public override void ReplaceStringValue(UnicodeString stringValue)
        {
            comment = stringValue;
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

        public override int GetLineNumber()
        {
            return lineNumber;
        }

        public override int GetColumnNumber()
        {
            return columnNumber;
        }
    }
}
