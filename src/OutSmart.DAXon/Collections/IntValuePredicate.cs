////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace OutSmart.DAXon.Collections
{
    /// <summary>
    /// An Func<int, bool> that matches a single specific integer
    /// </summary>
    internal sealed class IntValuePredicate : IIntPredicateProxy
    {
        private readonly int target;
        public IntValuePredicate(int target)
        {
            this.target = target;
        }

        public bool Test(int value)
        {
            return value == target;
        }

        public int GetTarget()
        {
            return target;
        }

        // === Auto-generated stubs (StubGenerator Phase 3.1f) ===
        public IIntPredicateProxy Union(IIntPredicateProxy other) => IntUnionPredicate.MakeUnion(this, other);
    }
}