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
    internal sealed class UniversalPattern : Pattern
    {
        private static readonly UniversalPattern _instance = new UniversalPattern();
        public override int ImplementationMethod => 0;
        /* upstream ctor: setPriority(-1) — bare "." must lose to kind tests like namespace-node() (-0.5) */
        public override double DefaultPriority => -1;
        public UniversalPattern() { }
        public static UniversalPattern GetInstance() => _instance;
        public override Expression Copy(RebindingMap r) => this;
        public override void Export(ExpressionPresenter @out) { }
        public override ItemType GetItemType() => AnyItemType.GetInstance(); /* upstream: the universal pattern matches any item */
        protected override int ComputeCardinality() => 0;
        public override bool Matches(IItem item, IXPathContext context) => true;
        public override UType GetUType() => UType.ANY;
    }
}
