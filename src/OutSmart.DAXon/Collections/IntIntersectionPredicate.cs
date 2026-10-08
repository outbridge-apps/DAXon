////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace OutSmart.DAXon.Collections
{
    // Predicate testing a AND b, mirroring upstream IntersectionPredicate. The sole caller is
    // CombinedNodeTest.GetMatcher — dropping `b` here would make the tree-scan fast path match
    // nodes that satisfy only the first of the combined tests.
    internal static class IntIntersectionPredicate
    {
        public static IIntPredicateProxy MakeIntersection(IIntPredicateProxy a, IIntPredicateProxy b)
            => new IntPredicateLambda(v => a.Test(v) && b.Test(v));
    }
}
