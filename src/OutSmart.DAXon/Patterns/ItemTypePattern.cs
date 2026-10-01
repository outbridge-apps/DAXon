////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Types;

namespace OutSmart.DAXon.Patterns
{
    // Upstream: a pattern that matches any item of a given ItemType (the base of predicate
    // patterns built by PatternParser). Was a hollow stub that DROPPED the item type in its
    // constructor and answered Matches => false for everything.
    internal sealed class ItemTypePattern : Pattern
    {
        private readonly ItemType itemType;

        public ItemTypePattern(ItemType itemType)
        {
            this.itemType = itemType;
        }

        public override int ImplementationMethod => 0;
        public override double DefaultPriority => 0;
        public override Expression Copy(RebindingMap r) => this;
        public override void Export(ExpressionPresenter @out) { }
        public override ItemType GetItemType() => itemType;
        protected override int ComputeCardinality() => 0;

        public override bool Matches(IItem item, IXPathContext context)
        {
            return itemType.Matches(item, context.GetConfiguration().GetTypeHierarchy());
        }

        public override UType GetUType() => itemType.GetUType(); // DAXonItemTypeUTypeExt dispatch
    }
}
