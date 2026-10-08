////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Model;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Types;

namespace OutSmart.DAXon.Patterns
{
    internal sealed class AnchorPattern : Pattern
    {
        private static readonly AnchorPattern _instance = new AnchorPattern();
        public static AnchorPattern GetInstance() => _instance;
        public override Expression Copy(RebindingMap rebindings) => this;
        public override void Export(ExpressionPresenter ep) { }
        public override ItemType GetItemType() => AnyNodeTest.GetInstance(); // upstream: the anchor "." can be any node
        public override UType GetUType() => UType.ANY_NODE;
        // KNOWN GAP: upstream anchors via matchesBeneathAnchor (node == anchor); this port answers
        // false unconditionally — see docs/known-gaps.md §P2c.
        public override bool Matches(IItem item, IXPathContext context) => false;
    }
}
