////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Expressions.Sorting
{
    internal class NumericComparer : IAtomicComparer
    {
        private static readonly NumericComparer THE_INSTANCE = new NumericComparer();
        protected StringToDouble converter = StringToDouble.GetInstance();

        public virtual IStringCollator Collator => null;

        protected NumericComparer()
        {
        }
        public static NumericComparer GetInstance()
        {
            return THE_INSTANCE;
        }

        public virtual IAtomicComparer ProvideContext(IXPathContext context)
        {
            return this;
        }

        public virtual int CompareAtomicValues(AtomicValue a, AtomicValue b)
        {
            // Double/double (the data-type="number" sort-key common case): same NaN-first order
            // and </> compare as the general chain below (±0 equal), minus the is-chains and
            // virtual GetDoubleValue hops — this runs n log n times per sort.
            if (a is DoubleValue dva && b is DoubleValue dvb)
            {
                double x = dva.GetDoubleValue(), y = dvb.GetDoubleValue();
                if (double.IsNaN(x))
                {
                    return double.IsNaN(y) ? 0 : -1;
                }

                if (double.IsNaN(y))
                {
                    return +1;
                }

                return x < y ? -1 : x > y ? +1 : 0;
            }

            double d1, d2;
            if (a is NumericValue)
            {
                d1 = ((NumericValue)a).GetDoubleValue();
            }
            else if (a == null)
            {
                d1 = double.NaN;
            }
            else
            {
                d1 = converter.TryStringToNumber(a.UnicodeStringValue, out double v1) ? v1 : double.NaN;
            }

            if (b is NumericValue)
            {
                d2 = ((NumericValue)b).GetDoubleValue();
            }
            else if (b == null)
            {
                d2 = double.NaN;
            }
            else
            {
                d2 = converter.TryStringToNumber(b.UnicodeStringValue, out double v2) ? v2 : double.NaN;
            }

            if (double.IsNaN(d1))
            {
                if (double.IsNaN(d2))
                {
                    return 0;
                }
                else
                {
                    return -1;
                }
            }

            if (double.IsNaN(d2))
            {
                return +1;
            }

            if (d1 < d2)
            {
                return -1;
            }

            if (d1 > d2)
            {
                return +1;
            }

            return 0;
        }

        public virtual bool ComparesEqual(AtomicValue a, AtomicValue b)
        {
            return CompareAtomicValues(a, b) == 0;
        }

        public virtual string Save()
        {
            return "NC";
        }
    }
}