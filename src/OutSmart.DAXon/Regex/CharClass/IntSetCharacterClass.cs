////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Regex.CharClass
{
    internal sealed class IntSetCharacterClass : ICharacterClass
    {
        private readonly IntSet intSet;
        public IntSetCharacterClass(IntSet intSet)
        {
            this.intSet = intSet;
        }

        public IntSet GetIntSet()
        {
            return intSet;
        }

        public bool Test(int value)
        {
            return intSet.Contains(value);
        }

        public bool IsDisjoint(ICharacterClass other)
        {
            if (other is IntSetCharacterClass)
            {
                return intSet.Intersect(((IntSetCharacterClass)other).intSet).IsEmpty();
            }
            else if (other is InverseCharacterClass)
            {
                return other.IsDisjoint(this);
            }
            else
            {
                return false;
            }
        }

        // === Auto-generated stubs (StubGenerator Phase 3.1f) ===
        public IIntPredicateProxy Union(IIntPredicateProxy other) => OutSmart.DAXon.Collections.IntUnionPredicate.MakeUnion(this, other); // upstream IntPredicateProxy default
    }
}