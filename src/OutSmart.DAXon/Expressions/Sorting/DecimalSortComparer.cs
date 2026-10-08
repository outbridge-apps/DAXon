////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Expressions.Sorting
{
    internal sealed class DecimalSortComparer : IAtomicComparer
    {
        private static readonly DecimalSortComparer THE_INSTANCE = new DecimalSortComparer();
        public static DecimalSortComparer DecimalSortComparerInstance => THE_INSTANCE;

        public IStringCollator Collator => null;

        private DecimalSortComparer()
        {
        }

        public IAtomicComparer ProvideContext(IXPathContext context)
        {
            return this;
        }

        public int CompareAtomicValues(AtomicValue a, AtomicValue b)
        {
            if (a == null)
            {
                return (b == null ? 0 : -1);
            }
            else if (b == null)
            {
                return +1;
            }

            return ((NumericValue)a).CompareTo((NumericValue)b);
        }

        public bool ComparesEqual(AtomicValue a, AtomicValue b)
        {
            return a.Equals(b);
        }

        public string Save()
        {
            return "DecSC";
        }
    }
}