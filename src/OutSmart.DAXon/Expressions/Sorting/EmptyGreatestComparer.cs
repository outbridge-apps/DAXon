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
    // Comparer for "order by ... empty greatest": an empty (null) sort key and NaN sort AFTER every ordinary
    // value. Was a hollow one-line stub that did not implement IAtomicComparer, so
    // SortKeyDefinition.MakeComparator cast it to IAtomicComparer -> InvalidCastException for any
    // `empty greatest` order-by.
    internal sealed class EmptyGreatestComparer : IAtomicComparer
    {
        private readonly IAtomicComparer baseComparer;

        public IStringCollator Collator => baseComparer.Collator;

        public EmptyGreatestComparer(IAtomicComparer baseComparer)
        {
            this.baseComparer = baseComparer;
        }

        public IAtomicComparer ProvideContext(IXPathContext context)
        {
            IAtomicComparer newBase = baseComparer.ProvideContext(context);
            if (newBase != baseComparer)
            {
                return new EmptyGreatestComparer(newBase);
            }

            return this;
        }

        public int CompareAtomicValues(AtomicValue a, AtomicValue b)
        {
            if (a == null)
            {
                return b == null ? 0 : +1;
            }
            else if (b == null)
            {
                return -1;
            }

            if (a.IsNaN())
            {
                return b.IsNaN() ? 0 : +1;
            }
            else if (b.IsNaN())
            {
                return -1;
            }

            return baseComparer.CompareAtomicValues(a, b);
        }

        public bool ComparesEqual(AtomicValue a, AtomicValue b)
        {
            return (a == null && b == null) || baseComparer.ComparesEqual(a, b);
        }

        public string Save()
        {
            return "EG|" + baseComparer.Save();
        }
    }
}
