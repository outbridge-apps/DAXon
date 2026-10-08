////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace OutSmart.DAXon.Collections
{
    internal sealed class IntUnionPredicate : IIntPredicateProxy
    {
        private readonly IIntPredicateProxy p1;
        private readonly IIntPredicateProxy p2;
        private IntUnionPredicate(IIntPredicateProxy p1, IIntPredicateProxy p2)
        {
            this.p1 = p1;
            this.p2 = p2;
        }

        public static IIntPredicateProxy MakeUnion(IIntPredicateProxy p1, IIntPredicateProxy p2)
        {
            return new IntUnionPredicate(p1, p2);
        }

        public bool Test(int value)
        {
            return p1.Test(value) || p2.Test(value);
        }

        public override string ToString()
        {
            return p1.ToString() + "||" + p2.ToString();
        }

        // === Auto-generated stubs (StubGenerator Phase 3.1f) ===
        public IIntPredicateProxy Union(IIntPredicateProxy other) => MakeUnion(this, other); // upstream IntPredicateProxy default method
    }
}