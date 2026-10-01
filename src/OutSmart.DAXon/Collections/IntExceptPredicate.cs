////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;

namespace OutSmart.DAXon.Collections
{
    // Set difference of two int predicates: matches value iff p1 matches AND p2 does NOT.
    // Was a hollow stub returning `a` (ignoring b), which broke regex character-class subtraction
    // for predicate-backed classes like `[\w-[b-y]]` (\w has no explicit IntSet -> the predicate path).
    internal sealed class IntExceptPredicate : IIntPredicateProxy
    {
        private readonly IIntPredicateProxy p1;
        private readonly IIntPredicateProxy p2;
        private IntExceptPredicate(IIntPredicateProxy p1, IIntPredicateProxy p2)
        {
            this.p1 = p1;
            this.p2 = p2;
        }

        public static IIntPredicateProxy MakeDifference(IIntPredicateProxy p1, IIntPredicateProxy p2) => new IntExceptPredicate(p1, p2);

        public bool Test(int value) => p1.Test(value) && !p2.Test(value);

        public IIntPredicateProxy Union(IIntPredicateProxy other) => IntUnionPredicate.MakeUnion(this, other);
    }
}
