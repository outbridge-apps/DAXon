////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Collections;

namespace OutSmart.DAXon.Regex.CharClass
{
    /// <summary>
    /// Class subtraction [p1-[p2]] where either side has no IntSet extent. MakeDifference used
    /// to close over the operands and allocate an IntExceptPredicate INSIDE the closure - a
    /// fresh allocation per character tested - and nested subtractions stacked a frame per
    /// level; the nesting depth is bounded by the parse-time probe in ParseCharacterClass.
    /// </summary>
    internal sealed class DifferenceCharacterClass : ICharacterClass
    {
        private readonly ICharacterClass include;
        private readonly ICharacterClass exclude;
        public DifferenceCharacterClass(ICharacterClass include, ICharacterClass exclude)
        {
            this.include = include;
            this.exclude = exclude;
        }

        public bool Test(int value)
        {
            return include.Test(value) && !exclude.Test(value);
        }

        public bool IsDisjoint(ICharacterClass other)
        {
            // everything this class matches, include matches
            return include.IsDisjoint(other);
        }

        public IntSet GetIntSet()
        {
            return null; // only built when an operand has no extent
        }

        public IIntPredicateProxy Union(IIntPredicateProxy other) => IntUnionPredicate.MakeUnion(this, other);
    }
}
