////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Collections;
namespace OutSmart.DAXon.Regex.CharClass
{
    internal sealed class EmptyCharacterClass : ICharacterClass
    {
        private static readonly EmptyCharacterClass THE_INSTANCE = new EmptyCharacterClass();
        private static readonly InverseCharacterClass COMPLEMENT = new InverseCharacterClass(THE_INSTANCE);

        public static ICharacterClass Complement => COMPLEMENT;

        private EmptyCharacterClass()
        {
        }
        public static EmptyCharacterClass GetInstance()
        {
            return THE_INSTANCE;
        }

        public bool Test(int value)
        {
            return false;
        }

        public bool IsDisjoint(ICharacterClass other)
        {

            // the empty set is disjoint with every other set including itself, in the sense that the
            // intersection of the two sets is empty
            return true;
        }

        public IntSet GetIntSet()
        {
            return IntEmptySet.GetInstance();
        }

        // === Auto-generated stubs (StubGenerator Phase 3.1f) ===

        public IIntPredicateProxy Union(IIntPredicateProxy other) => OutSmart.DAXon.Collections.IntUnionPredicate.MakeUnion(this, other); // upstream IntPredicateProxy default
    }
}