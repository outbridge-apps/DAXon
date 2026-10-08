////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Collections;
using System;
namespace OutSmart.DAXon.Regex.CharClass
{
    internal sealed class PredicateCharacterClass : ICharacterClass
    {
        private readonly Func<int, bool> predicate;
        // BMP verdict memo: the three instances (\i \c \w in Categories) are process-wide statics
        // whose predicates run several binary searches per codepoint, and a regex program tests the
        // same codepoint more than once (precondition scan + match). 0 unknown / 1 true / 2 false;
        // byte writes are atomic and idempotent, so concurrent fills are benign. Bounded: 64KB per
        // instance. Astral codepoints stay on the raw predicate.
        private readonly byte[] bmpMemo = new byte[65536];

        public PredicateCharacterClass(Func<int, bool> predicate)
        {
            this.predicate = predicate;
        }

        public bool Test(int value)
        {
            if (value >= 0 && value < 65536)
            {
                byte m = bmpMemo[value];
                if (m != 0)
                {
                    return m == 1;
                }

                bool r = predicate(value);
                bmpMemo[value] = r ? (byte)1 : (byte)2;
                return r;
            }

            return predicate(value);
        }

        public bool IsDisjoint(ICharacterClass other)
        {
            return other is InverseCharacterClass && other.IsDisjoint(this);
        }

        public IntSet GetIntSet()
        {
            return null; // Not known
        }

        // === Auto-generated stubs (StubGenerator Phase 3.1f) ===
        public IIntPredicateProxy Union(IIntPredicateProxy other) => OutSmart.DAXon.Collections.IntUnionPredicate.MakeUnion(this, other); // upstream IntPredicateProxy default
    }
}