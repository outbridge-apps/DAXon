////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Collections
{
    internal sealed class IntSetPredicate : IIntPredicateProxy
    {

        /// <summary>
        /// Convenience predicate that always matches
        /// </summary>
        public static readonly IIntPredicateProxy ALWAYS_TRUE = IntPredicateLambda.Of((i) => true);
        private readonly IntSet set;
        public IntSetPredicate(IntSet set)
        {
            if (set == null)
            {
                throw new NullReferenceException();
            }

            this.set = set;
        }

        public bool Test(int value)
        {
            return set.Contains(value);
        }

        public IntSet GetIntSet()
        {
            return set;
        }

        /// <summary>
        /// Get string representation
        /// </summary>
        public override string ToString()
        {
            return "in {" + set + "}";
        }

        // === Auto-generated stubs (StubGenerator Phase 3.1f) ===
        public IIntPredicateProxy Union(IIntPredicateProxy other) => IntUnionPredicate.MakeUnion(this, other);
    }
}
