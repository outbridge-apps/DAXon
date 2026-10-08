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
    internal sealed class ContextFreeAtomicComparer : IAtomicComparer
    {
        private static readonly ContextFreeAtomicComparer THE_INSTANCE = new ContextFreeAtomicComparer();

        public IStringCollator Collator => null;

        protected ContextFreeAtomicComparer()
        {
        }
        public static ContextFreeAtomicComparer GetInstance()
        {
            return THE_INSTANCE;
        }

        public IAtomicComparer ProvideContext(IXPathContext context)
        {
            return this;
        }

        public int CompareAtomicValues(AtomicValue a, AtomicValue b)
        {

            //        return ((IContextFreeAtomicValue) a).getXPathComparable()
            return ((IXPathComparable)a).CompareTo((IXPathComparable)b);
        }

        public bool ComparesEqual(AtomicValue a, AtomicValue b)
        {
            return a.Equals(b);
        }

        public string Save()
        {
            return "CAVC";
        }
    }
}