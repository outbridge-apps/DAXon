////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Collections;
namespace OutSmart.DAXon.Regex.CharClass
{
    internal class SingletonCharacterClass : ICharacterClass
    {
        private readonly int codepoint;

        public virtual int Codepoint => codepoint;
        public SingletonCharacterClass(int codepoint)
        {
            this.codepoint = codepoint;
        }

        public virtual bool Test(int value)
        {
            return value == codepoint;
        }

        public virtual bool IsDisjoint(ICharacterClass other)
        {
            return !other.Test(codepoint);
        }

        public virtual IntSet GetIntSet()
        {
            return new IntSingletonSet(codepoint);
        }

        // === Auto-generated stubs (StubGenerator Phase 3.1f) ===
        public virtual IIntPredicateProxy Union(IIntPredicateProxy other) => OutSmart.DAXon.Collections.IntUnionPredicate.MakeUnion(this, other); // upstream IntPredicateProxy default
    }
}