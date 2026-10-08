////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Expressions.Sorting;

namespace OutSmart.DAXon.Collections
{
    internal sealed class IntEmptySet : IntSet
    {
        private static readonly IntEmptySet _instance = new IntEmptySet();
        public static IntEmptySet GetInstance() => _instance;
        public override IntSet Copy() => this;
        public override IntSet MutableCopy() => this;
        public override void Clear() { }
        public override int Size() => 0;
        public override bool IsEmpty() => true;
        public override bool Contains(int value) => false;
        public override bool Remove(int value) => false;
        public override bool Add(int value) => false;
        // Was a hollow NIE stub: IntHashSet.Copy() of an empty set RETURNS this singleton, so any
        // caller iterating that copy (base ContainsAll among them) crashed instead of seeing 0 items.
        public override IIntIterator IIterator() => EmptyIntIterator.GetInstance();
    }
}
