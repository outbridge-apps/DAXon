////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;

namespace OutSmart.DAXon.Trees.Linked
{
    // Was a fully hollow shell (content dropped on construction, kind/copy/string-value all NIE):
    // a comment in a linked tree crashed the moment anything asked for its kind or value.
    internal sealed class CommentImpl : NodeImpl
    {
        private UnicodeString content = EmptyUnicodeString.GetInstance();
        public CommentImpl(object data) { content = data as UnicodeString ?? BMPString.Of(data?.ToString() ?? ""); }
        public void SetLocation(string systemId, int line, int column) { } /* location tracking not kept for linked-tree comments */

        public override UnicodeString UnicodeStringValue => content;
        public override int GetNodeKind() => Types.Type.COMMENT;
        public override void ReplaceStringValue(UnicodeString value) => content = value;
        public override void Copy(IReceiver @out, int copyOptions, ILocation locationId) => @out.Comment(content, locationId, 0);
    }
}
