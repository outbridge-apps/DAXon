////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Expressions
{
    internal sealed class SwitchCaseComparer : GenericAtomicComparer
    {
        public SwitchCaseComparer(IStringCollator collator, IXPathContext context) : base(collator, context)
        {
        }

        public override GenericAtomicComparer ProvideContext(IXPathContext context)
        {
            return new SwitchCaseComparer(StringCollator, context);
        }

        public override bool ComparesEqual(AtomicValue a, AtomicValue b)
        {

            if (a is StringValue && b is StringValue)
            {
                return StringCollator.ComparesEqual(a.UnicodeStringValue, b.UnicodeStringValue);
            }
            else if (a is CalendarValue && b is CalendarValue)
            {
                return ((CalendarValue)a).CompareTo((CalendarValue)b, Context.GetImplicitTimezone()) == 0;
            }
            else if (a.IsNaN() && b.IsNaN())
            {
                return true;
            }
            else
            {
                int implicitTimezone = Context.GetImplicitTimezone();
                object ac = a.GetXPathMatchKey(StringCollator, implicitTimezone);
                object bc = b.GetXPathMatchKey(StringCollator, implicitTimezone);
                return ac.Equals(bc);
            }
        }

        public override string Save()
        {

            // Note: the PackageLoader doesn't actually recognise this format. It doesn't need to, because
            // this comparer is only currently used in XQuery.
            return "EQUIV|" + base.Save();
        }
    }
}