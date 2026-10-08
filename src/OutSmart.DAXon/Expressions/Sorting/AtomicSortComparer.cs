////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using System;
namespace OutSmart.DAXon.Expressions.Sorting
{
    internal sealed class AtomicSortComparer : IAtomicComparer
    {

        //} else
        public static IAtomicMatchKey COLLATION_KEY_NaN = new MatchKeyForNaN();
        private IStringCollator collator;
        private readonly int itemType;
        private readonly int implicitTimezone; // dynamic-context constant: hoisted from per-comparison GetImplicitTimezone() chase

        public IStringCollator Collator => collator;

        protected AtomicSortComparer(IStringCollator collator, int itemType, IXPathContext context)
        {
            this.collator = collator;
            if (collator == null)
            {
                this.collator = CodepointCollator.GetInstance();
            }

            this.itemType = itemType;
            this.implicitTimezone = context.GetImplicitTimezone();
        }
        public static IAtomicComparer MakeSortComparer(IStringCollator collator, int itemType, IXPathContext context)
        {
            switch (itemType)
            {
                case StandardNames.XS_STRING:
                case StandardNames.XS_UNTYPED_ATOMIC:
                case StandardNames.XS_ANY_URI:
                    if (collator is CodepointCollator)
                    {
                        return CodepointCollatingComparer.GetInstance();
                    }
                    else
                    {
                        return new CollatingAtomicComparer(collator);
                    }

                case StandardNames.XS_INTEGER:
                case StandardNames.XS_DECIMAL:
                    return DecimalSortComparer.DecimalSortComparerInstance;
                case StandardNames.XS_DOUBLE:
                case StandardNames.XS_FLOAT:
                case StandardNames.XS_NUMERIC:
                    return DoubleSortComparer.GetInstance();
                case StandardNames.XS_DATE_TIME:
                case StandardNames.XS_DATE:
                case StandardNames.XS_TIME:
                    return new CalendarValueComparer(context);
                default:

                    // use the general-purpose comparer that handles all types
                    return new AtomicSortComparer(collator, itemType, context);
            }
        }

        public IAtomicComparer ProvideContext(IXPathContext context)
        {
            return new AtomicSortComparer(collator, itemType, context);
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


            // Delete the following five lines to fix bug 3450
            // End of fix for 3450
            // Double/double (the numeric-sort common case): same result as the general chain below —
            // NaN screen, then NumericValue.CompareTo's ==/< compare (±0 equal) — without 2× virtual
            // IsNaN, 2× GetXPathComparable and the interface CompareTo hop per comparison (n log n).
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

                return x == y ? 0 : x < y ? -1 : +1;
            }

            if (a.IsNaN())
            {
                return b.IsNaN() ? 0 : -1;
            }
            else if (b.IsNaN())
            {
                return +1;
            }
            else if (a is StringValue && b is StringValue)
            {
                return collator.CompareStrings(a.UnicodeStringValue, b.UnicodeStringValue);
            }
            else
            {
                IXPathComparable ac = a.GetXPathComparable(collator, implicitTimezone);
                IXPathComparable bc = b.GetXPathComparable(collator, implicitTimezone);
                if (ac == null || bc == null)
                {
                    return CompareNonComparables(a, b);
                }
                else
                {
                    try
                    {
                        return ac.CompareTo(bc);
                    }
                    catch (InvalidCastException)
                    {
                        string message = "Cannot compare " + a.PrimitiveType.DisplayName + " with " + b.PrimitiveType.DisplayName;

                        // Direct users to bug 3450 which explains a 2017 bug fix that may cause previously
                        // working applications to fail
                        if (a.IsUntypedAtomic() || b.IsUntypedAtomic())
                        {
                            message += ". Further information: see http://saxonica.plan.io/issues/3450";
                        }

                        throw new InvalidCastException(message);
                    }
                }
            }
        }

        //} else
        protected int CompareNonComparables(AtomicValue a, AtomicValue b)
        {
            XPathException err = new XPathException("Values are not comparable (" + Types.Type.DisplayTypeName(a) + ", " + Types.Type.DisplayTypeName(b) + ')', "XPTY0004");
            throw new ComparisonException(err);
        }

        public bool ComparesEqual(AtomicValue a, AtomicValue b)
        {
            return CompareAtomicValues(a, b) == 0;
        }
        public string Save()
        {
            return "AtSC|" + itemType + "|" + Collator.CollationURI;
        }

        private sealed class MatchKeyForNaN : IAtomicMatchKey
        {
            public AtomicValue AsAtomic()
            {

                // The logic here is to choose a value that compares equal to itself but not equal to any other
                // value. We use StructuredQName because it has a simple equals() method.
                return new QNameValue("saxon", NamespaceUri.Of("http://saxon.sf.net/collation-key"), "NaN");
            }
        }
    }
}