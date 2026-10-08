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
    /// <summary>
    /// A comparer specifically for comparing two date, time, or dateTime values
    /// </summary>
    internal sealed class CalendarValueComparer : IAtomicComparer
    {
        private readonly IXPathContext context;

        public IStringCollator Collator => null;
        public CalendarValueComparer(IXPathContext context)
        {
            this.context = context;
        }

        public IAtomicComparer ProvideContext(IXPathContext context)
        {
            return new CalendarValueComparer(context);
        }

        public int CompareAtomicValues(AtomicValue a, AtomicValue b)
        {
            if (a == null)
            {
                return b == null ? 0 : -1;
            }
            else if (b == null)
            {
                return +1;
            }

            return ((CalendarValue)a).CompareTo((CalendarValue)b, context.GetImplicitTimezone());
        }

        public bool ComparesEqual(AtomicValue a, AtomicValue b)
        {
            return CompareAtomicValues(a, b) == 0;
        }

        public string Save()
        {
            return "CalVC";
        }
    }
}