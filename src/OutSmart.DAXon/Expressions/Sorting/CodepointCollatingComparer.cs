////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Expressions.Sorting
{
    internal sealed class CodepointCollatingComparer : IAtomicComparer
    {
        private static readonly CodepointCollator collator = CodepointCollator.GetInstance();
        private static readonly CodepointCollatingComparer THE_INSTANCE = new CodepointCollatingComparer();

        public IStringCollator Collator => collator;

        private CodepointCollatingComparer()
        {
        }
        public static CodepointCollatingComparer GetInstance()
        {
            return THE_INSTANCE;
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

            StringValue @as = (StringValue)a;
            StringValue bs = (StringValue)b;
            UnicodeString ua = @as.UnicodeStringValue;
            UnicodeString ub = bs.UnicodeStringValue;

            // Byte-rep fast path (the tree-text common case): the codepoint order Slice8.CompareTo realises, reached
            // without the collator-interface and virtual-CompareTo hops — this runs n log n times per sort.
            if (Slice8.Latin1Span(ua, out byte[] xb, out int xi, out int xe) && Slice8.TryCompare(xb, xi, xe, ub, out int order))
            {
                return order;
            }

            return collator.CompareStrings(ua, ub);
        }

        public bool ComparesEqual(AtomicValue a, AtomicValue b)
        {
            return ((StringValue)a).Equals((StringValue)b);
        }

        public string Save()
        {
            return "CCC";
        }
    }
}