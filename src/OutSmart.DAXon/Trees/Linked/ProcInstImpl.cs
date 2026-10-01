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
    // Was a fully hollow shell (target and data dropped on construction, kind/copy/string-value
    // all NIE): a processing-instruction in a linked tree crashed on first real use.
    internal sealed class ProcInstImpl : NodeImpl
    {
        private string target = "";
        private UnicodeString content = EmptyUnicodeString.GetInstance();
        public ProcInstImpl(object target, object data)
        {
            this.target = target?.ToString() ?? "";
            content = data as UnicodeString ?? BMPString.Of(data?.ToString() ?? "");
        }
        public void SetLocation(string systemId, int line, int column) { } /* location tracking not kept for linked-tree PIs */

        public override string GetLocalPart() => target;
        public override UnicodeString UnicodeStringValue => content;
        public override int GetNodeKind() => Types.Type.PROCESSING_INSTRUCTION;
        public override void ReplaceStringValue(UnicodeString value) => content = value;
        public override void Copy(IReceiver @out, int copyOptions, ILocation locationId) => @out.ProcessingInstruction(target, content, locationId, 0);
    }
}
