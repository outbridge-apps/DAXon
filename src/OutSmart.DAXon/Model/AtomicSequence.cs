////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Values;
using System.Collections.Generic;
namespace OutSmart.DAXon.Model
{
    public interface IAtomicSequence : IGroundedValue, IEnumerable<AtomicValue>
    {
        AtomicValue Head();
        IAtomicIterator Iterate();
        AtomicValue ItemAt(int n);
        int GetLength();
        UnicodeString UnicodeStringValue { get; }
        UnicodeString CanonicalLexicalRepresentation { get; }
    }
}