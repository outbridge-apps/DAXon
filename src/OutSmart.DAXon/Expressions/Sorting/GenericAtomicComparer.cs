////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Patterns;
namespace OutSmart.DAXon.Expressions.Sorting
{
    internal class GenericAtomicComparer : IAtomicComparer
    {
        private IStringCollator collator;
        private readonly IXPathContext context;

        public virtual IStringCollator Collator => collator;

        public virtual IStringCollator StringCollator => collator;

        public virtual IXPathContext Context => context;
        public GenericAtomicComparer(IStringCollator collator, IXPathContext conversionContext)
        {
            this.collator = collator;
            if (collator == null)
            {
                this.collator = CodepointCollator.GetInstance();
            }

            context = conversionContext;
        }

        public static IAtomicComparer MakeAtomicComparer(BuiltInAtomicType type0, BuiltInAtomicType type1, IStringCollator collator, IXPathContext context)
        {
            int fp0 = type0.Fingerprint;
            int fp1 = type1.Fingerprint;
            if (fp0 == fp1)
            {
                switch (fp0)
                {
                    case StandardNames.XS_DATE_TIME:
                    case StandardNames.XS_DATE:
                    case StandardNames.XS_TIME:
                    case StandardNames.XS_G_DAY:
                    case StandardNames.XS_G_MONTH:
                    case StandardNames.XS_G_YEAR:
                    case StandardNames.XS_G_MONTH_DAY:
                    case StandardNames.XS_G_YEAR_MONTH:
                        return new CalendarValueComparer(context);
                    case StandardNames.XS_BOOLEAN:
                    case StandardNames.XS_INTEGER:
                    case StandardNames.XS_DECIMAL:
                    case StandardNames.XS_DOUBLE:
                    case StandardNames.XS_FLOAT:
                    case StandardNames.XS_DAY_TIME_DURATION:
                    case StandardNames.XS_YEAR_MONTH_DURATION:
                    case StandardNames.XS_BASE64_BINARY:
                    case StandardNames.XS_HEX_BINARY:
                        return ContextFreeAtomicComparer.GetInstance();
                    case StandardNames.XS_QNAME:
                    case StandardNames.XS_NOTATION:
                        return GenericObjectEqualityAtomicComparer.Instance;
                }
            }

            if (type0.IsPrimitiveNumeric() && type1.IsPrimitiveNumeric())
            {
                return ContextFreeAtomicComparer.GetInstance();
            }

            if ((fp0 == StandardNames.XS_STRING || fp0 == StandardNames.XS_UNTYPED_ATOMIC || fp0 == StandardNames.XS_ANY_URI) && (fp1 == StandardNames.XS_STRING || fp1 == StandardNames.XS_UNTYPED_ATOMIC || fp1 == StandardNames.XS_ANY_URI))
            {
                if (collator is CodepointCollator)
                {
                    return CodepointCollatingComparer.GetInstance();
                }
                else
                {
                    return new CollatingAtomicComparer(collator);
                }
            }

            return new GenericAtomicComparer(collator, context);
        }

        private static IAtomicComparisonFunction GetContextFreeComparisonFunction(int @operator)
        {
            return (a, b, context) =>
            {
                int comp = ((IXPathComparable)a).CompareTo((IXPathComparable)b);
                return CompareToConstant.InterpretComparisonResult(@operator, comp);
            };
        }

        private static IAtomicComparisonFunction GetFloatingPointComparisonFunction(int @operator)
        {
            return (a, b, context) =>
            {
                if (a.IsNaN() || b.IsNaN())
                {
                    return @operator == Token.FNE;
                }

                int comp = ((IXPathComparable)a).CompareTo((IXPathComparable)b);
                return CompareToConstant.InterpretComparisonResult(@operator, comp);
            };
        }

        private static int ApplyPromotion(BuiltInAtomicType type, int version)
        {
            if (type.IsPrimitiveNumeric())
            {
                return StandardNames.XS_DOUBLE;
            }

            int fp = type.Fingerprint;
            if (fp == StandardNames.XS_UNTYPED_ATOMIC || fp == StandardNames.XS_ANY_URI)
            {
                return StandardNames.XS_STRING;
            }
            else if (fp == StandardNames.XS_HEX_BINARY && version >= 40)
            {
                return StandardNames.XS_BASE64_BINARY;
            }
            else
            {
                return fp;
            }
        }

        public static IAtomicComparisonFunction MakeAtomicComparisonFunction(BuiltInAtomicType type0, BuiltInAtomicType type1, IStringCollator collator, int @operator, bool allowRecursion, int version)
        {
            // NaN-free numeric pair: integer/decimal values cannot be NaN, so the double lane
            // ApplyPromotion would route them to only adds two virtual IsNaN probes per call on
            // top of the same cross-numeric CompareTo. Take the exact lane directly.
            int rawFp0 = type0.Fingerprint;
            int rawFp1 = type1.Fingerprint;
            if ((rawFp0 == StandardNames.XS_INTEGER || rawFp0 == StandardNames.XS_DECIMAL)
                && (rawFp1 == StandardNames.XS_INTEGER || rawFp1 == StandardNames.XS_DECIMAL))
            {
                return GetContextFreeComparisonFunction(@operator);
            }

            int fp0 = ApplyPromotion(type0, version);
            int fp1 = ApplyPromotion(type1, version);
            if (fp0 == fp1)
            {
                switch (fp0)
                {
                    case StandardNames.XS_DATE_TIME:
                    case StandardNames.XS_DATE:
                    case StandardNames.XS_TIME:
                    case StandardNames.XS_G_DAY:
                    case StandardNames.XS_G_MONTH:
                    case StandardNames.XS_G_YEAR:
                    case StandardNames.XS_G_MONTH_DAY:
                    case StandardNames.XS_G_YEAR_MONTH:
                        return (a, b, context) =>
                        {
                            int comp = ((CalendarValue)a).CompareTo((CalendarValue)b, context.GetImplicitTimezone());
                            return CompareToConstant.InterpretComparisonResult(@operator, comp);
                        };
                    case StandardNames.XS_DOUBLE:
                    case StandardNames.XS_FLOAT:
                        return GetFloatingPointComparisonFunction(@operator);
                    case StandardNames.XS_BOOLEAN:
                    case StandardNames.XS_DAY_TIME_DURATION:
                    case StandardNames.XS_YEAR_MONTH_DURATION:
                    case StandardNames.XS_BASE64_BINARY:
                    case StandardNames.XS_HEX_BINARY:
                        return GetContextFreeComparisonFunction(@operator);
                    case StandardNames.XS_QNAME:
                    case StandardNames.XS_NOTATION:
                        switch (@operator)
                        {
                            case Token.FEQ:
                                return (a, b, context) => a.Equals(b);
                            case Token.FNE:
                                return (a, b, context) => !a.Equals(b);
                            default:
                                return (a, b, context) =>
                                {
                                    throw new XPathException(type0 + " values cannot be compared for ordering", "XPTY0004");
                                };
                        }

                    case StandardNames.XS_STRING:
                        if (collator is CodepointCollator)
                        {
                            switch (@operator)
                            {
                                case Token.FEQ:
                                    return (a, b, context) => a.Equals(b);
                                case Token.FNE:
                                    return (a, b, context) => !a.Equals(b);
                                default:
                                    // Codepoint ordering IS UnicodeString.CompareTo — skip the collator dispatch.
                                    return (a, b, context) => CompareToConstant.InterpretComparisonResult(@operator, a.UnicodeStringValue.CompareTo(b.UnicodeStringValue));
                            }
                        }

                        return (a, b, context) =>
                        {
                            int comp = collator.CompareStrings(a.UnicodeStringValue, b.UnicodeStringValue);
                            return CompareToConstant.InterpretComparisonResult(@operator, comp);
                        };
                }
            }

            if (type0.IsDurationType() && type1.IsDurationType())
            {

                // potentially different subtypes of xs:duration - only equality comparison allowed
                switch (@operator)
                {
                    case Token.FEQ:
                        return (a, b, context) => a.Equals(b);
                    case Token.FNE:
                        return (a, b, context) => !a.Equals(b);
                    default:

                        // fall through and try again using the run-time types
                        break;
                }
            }

            if (allowRecursion)
            {

                // Get a comparison function using the run-time types rather than the static types
                // We remember the function used the first time through, and reuse it if the types are the same
                BuiltInAtomicType[] firstTimeTypes = new BuiltInAtomicType[2];
                IAtomicComparisonFunction[] firstTimeFunction = new IAtomicComparisonFunction[1];
                return (a, b, context) =>
                {
                    BuiltInAtomicType at = a.PrimitiveType;
                    BuiltInAtomicType bt = b.PrimitiveType;
                    lock (firstTimeFunction)
                    {
                        if (firstTimeFunction[0] == null)
                        {
                            IAtomicComparisonFunction comparisonFunction = MakeAtomicComparisonFunction(at, bt, collator, @operator, false, version);
                            firstTimeFunction[0] = comparisonFunction;
                            firstTimeTypes[0] = at;
                            firstTimeTypes[1] = bt;
                            return comparisonFunction.Compare(a, b, context);
                        }
                        else
                        {
                            if (firstTimeTypes[0] == at && firstTimeTypes[1] == bt)
                            {
                                return firstTimeFunction[0].Compare(a, b, context);
                            }
                            else
                            {
                                IAtomicComparisonFunction comparisonFunction = MakeAtomicComparisonFunction(at, bt, collator, @operator, false, version);
                                return comparisonFunction.Compare(a, b, context);
                            }
                        }
                    }
                };
            }
            else
            {
                return (a, b, context) =>
                {
                    throw new XPathException("Values are not comparable (" + Types.Type.DisplayTypeName(a) + ", " + Types.Type.DisplayTypeName(b) + ')', "XPTY0004", context);
                };
            }
        }

        public virtual GenericAtomicComparer ProvideContext(IXPathContext context)
        {
            return new GenericAtomicComparer(collator, context);
        }

        public virtual int CompareAtomicValues(AtomicValue a, AtomicValue b)
        {

            if (a == null)
            {
                return b == null ? 0 : -1;
            }
            else if (b == null)
            {
                return +1;
            }

            if (a is StringValue && b is StringValue)
            {
                return collator.CompareStrings(a.UnicodeStringValue, b.UnicodeStringValue);
            }
            else
            {
                int implicitTimezone = context.GetImplicitTimezone();
                IXPathComparable ac = a.GetXPathComparable(collator, implicitTimezone);
                IXPathComparable bc = b.GetXPathComparable(collator, implicitTimezone);
                if (ac == null || bc == null)
                {
                    XPathException e = new XPathException("Objects are not comparable (" + Types.Type.DisplayTypeName(a) + ", " + Types.Type.DisplayTypeName(b) + ')', "XPTY0004");
                    throw new ComparisonException(e);
                }
                else
                {
                    return ac.CompareTo(bc);
                }
            }
        }

        public virtual bool ComparesEqual(AtomicValue a, AtomicValue b)
        {

            if (a is StringValue && b is StringValue)
            {
                return collator.ComparesEqual(a.UnicodeStringValue, b.UnicodeStringValue);
            }
            else if (a is CalendarValue && b is CalendarValue)
            {
                return ((CalendarValue)a).CompareTo((CalendarValue)b, context.GetImplicitTimezone()) == 0;
            }
            else
            {
                int implicitTimezone = context.GetImplicitTimezone();
                IAtomicMatchKey ac = a.GetXPathMatchKey(collator, implicitTimezone);
                IAtomicMatchKey bc = b.GetXPathMatchKey(collator, implicitTimezone);
                return ac.Equals(bc);
            }
        }

        public virtual string Save()
        {
            return "GAC|" + collator.CollationURI;
        }

        public override int GetHashCode()
        {
            return collator.GetHashCode();
        }

        public override bool Equals(object obj)
        {

            // In considering whether two GenericAtomicComparers are equal, we ignore the dynamic context, because this
            // is only ever used to test the implicit timezone, and in all reasonable scenarios, the implicit timezone
            // is global.
            return obj is GenericAtomicComparer && collator.Equals(((GenericAtomicComparer)obj).collator);
        }
        IAtomicComparer IAtomicComparer.ProvideContext(IXPathContext arg0) => ProvideContext(arg0);

        // IAtomicComparisonFunction interface->delegate for lambda assignability.
        internal delegate bool IAtomicComparisonFunction(AtomicValue v0, AtomicValue v1, IXPathContext context);
    }
}