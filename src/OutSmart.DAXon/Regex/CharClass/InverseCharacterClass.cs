////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Collections;
namespace OutSmart.DAXon.Regex.CharClass
{
    internal sealed class InverseCharacterClass : ICharacterClass
    {
        private readonly ICharacterClass complement;

        public ICharacterClass Complement => complement;
        public InverseCharacterClass(ICharacterClass complement)
        {
            this.complement = complement;
        }

        public bool Test(int value)
        {
            return !complement.Test(value);
        }

        public bool IsDisjoint(ICharacterClass other)
        {
            return other == complement;
        }

        public IntSet GetIntSet()
        {
            IntSet comp = complement.GetIntSet();
            return comp == null ? null : new IntComplementSet(complement.GetIntSet());
        }

        // === Auto-generated stubs (StubGenerator Phase 3.1f) ===
        public IIntPredicateProxy Union(IIntPredicateProxy other) => OutSmart.DAXon.Collections.IntUnionPredicate.MakeUnion(this, other); // upstream IntPredicateProxy default
    }
}