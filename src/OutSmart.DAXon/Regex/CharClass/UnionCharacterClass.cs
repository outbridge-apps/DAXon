////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Collections;

namespace OutSmart.DAXon.Regex.CharClass
{
    /// <summary>
    /// Union of character classes tested by a loop over the members. MakeUnion used to fold
    /// accumulation loops into nested test closures - one stack frame per member on EVERY
    /// Test call - and a class body of escapes or an alternation list is as long as the
    /// pattern makes it, which for a dynamic pattern means as long as the input makes it.
    /// </summary>
    internal sealed class UnionCharacterClass : ICharacterClass
    {
        private readonly ICharacterClass[] members;
        public UnionCharacterClass(ICharacterClass[] members)
        {
            this.members = members;
        }

        public bool Test(int value)
        {
            foreach (ICharacterClass member in members)
            {
                if (member.Test(value))
                {
                    return true;
                }
            }

            return false;
        }

        public bool IsDisjoint(ICharacterClass other)
        {
            // the union matches what any member matches
            foreach (ICharacterClass member in members)
            {
                if (!member.IsDisjoint(other))
                {
                    return false;
                }
            }

            return true;
        }

        public IntSet GetIntSet()
        {
            return null; // only built when some member has no extent
        }

        public IIntPredicateProxy Union(IIntPredicateProxy other) => OutSmart.DAXon.Collections.IntUnionPredicate.MakeUnion(this, other); // upstream IntPredicateProxy default
    }
}
