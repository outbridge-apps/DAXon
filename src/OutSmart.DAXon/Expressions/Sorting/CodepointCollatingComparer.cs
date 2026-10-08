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

            // Byte-rep fast path (Slice8/Twine8, the tree-text common case): the same per-byte
            // codepoint order Slice8.CompareTo realises, reached without the collator-interface
            // and virtual-CompareTo hops — this runs n log n times per sort.
            byte[] xb = null;
            int xi = 0, xe = 0;
            if (ua is Slice8 xs8)
            {
                xb = xs8.ByteArray;
                xi = xs8.Start;
                xe = xs8.End;
            }
            else if (ua is Twine8 xt8)
            {
                xb = xt8.ByteArray;
                xe = xb.Length;
            }

            if (xb != null)
            {
                byte[] yb = null;
                int yi = 0, ye = 0;
                if (ub is Slice8 ys8)
                {
                    yb = ys8.ByteArray;
                    yi = ys8.Start;
                    ye = ys8.End;
                }
                else if (ub is Twine8 yt8)
                {
                    yb = yt8.ByteArray;
                    ye = yb.Length;
                }

                if (yb != null)
                {
                    int i = xi, j = yi;
                    while (i < xe && j < ye)
                    {
                        int diff = (xb[i++] & 0xff) - (yb[j++] & 0xff);
                        if (diff != 0)
                        {
                            return diff;
                        }
                    }

                    return (xe - xi).CompareTo(ye - yi);
                }
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