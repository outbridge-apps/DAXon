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

namespace OutSmart.DAXon.Trees.Tiny
{
    using OutSmart.DAXon.Model;
    // BuilderMonitor inheritance trips CS0534 (abstract MarkNextNode/GetMarkedNode). Keep bare for now.
    // Inherit BuilderMonitor (impl 2 abstract members).
    internal sealed class TinyBuilderMonitor : BuilderMonitor
    {
        public override NodeInfo MarkedNode => throw new NotImplementedException("STUB: TinyBuilderMonitor.GetMarkedNode not ported (excluded stub)");
        public TinyBuilderMonitor(object a) : base(null) { }
        public override void MarkNextNode(int nodeKind) { }
    }
}
