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
    internal sealed class CollatingAtomicComparer : IAtomicComparer
    {
        private readonly IStringCollator collator;

        public IStringCollator Collator => collator;
        public CollatingAtomicComparer(IStringCollator collator)
        {
            if (collator == null)
            {
                this.collator = CodepointCollator.GetInstance();
            }
            else
            {
                this.collator = collator;
            }
        }

        public IAtomicComparer ProvideContext(IXPathContext context)
        {
            return this;
        }

        public int CompareAtomicValues(AtomicValue a, AtomicValue b)
        {
            if (a == null)
            {
                if (b == null)
                {
                    return 0;
                }
                else
                {
                    return -1;
                }
            }
            else if (b == null)
            {
                return +1;
            }

            return collator.CompareStrings(a.UnicodeStringValue, b.UnicodeStringValue);
        }

        public bool ComparesEqual(AtomicValue a, AtomicValue b)
        {
            return CompareAtomicValues(a, b) == 0;
        }

        public string Save()
        {
            return "CAC|" + Collator.CollationURI;
        }

        public override int GetHashCode()
        {
            return collator.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            return obj is CollatingAtomicComparer && collator.Equals(((CollatingAtomicComparer)obj).collator);
        }
    }
}