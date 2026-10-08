////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// This class supports the get_X_from_Y functions defined in XPath 2.0
    /// </summary>
    public abstract class AccessorFn : ScalarSystemFunction
    {

        public abstract Component ComponentId { get; }
        public override IntegerValue[] IntegerBounds
        {
            get
            {
                switch (ComponentId)
                {
                    case Component.YEAR:
                        return new IntegerValue[]
                        {
                        Int64Value.MakeIntegerValue(-100000),
                        Int64Value.MakeIntegerValue(+100000)
                        };
                    case Component.MONTH:
                        return new IntegerValue[]
                        {
                        Int64Value.MakeIntegerValue(-11),
                        Int64Value.MakeIntegerValue(+11)
                        };
                    case Component.DAY:
                        return new IntegerValue[]
                        {
                        Int64Value.MakeIntegerValue(-31),
                        Int64Value.MakeIntegerValue(+31)
                        };
                    case Component.HOURS:
                        return new IntegerValue[]
                        {
                        Int64Value.MakeIntegerValue(-24),
                        Int64Value.MakeIntegerValue(+24)
                        };
                    case Component.MINUTES:
                        return new IntegerValue[]
                        {
                        Int64Value.MakeIntegerValue(-59),
                        Int64Value.MakeIntegerValue(+59)
                        };
                    case Component.SECONDS:
                        return new IntegerValue[]
                        {
                        Int64Value.MakeIntegerValue(-59),
                        Int64Value.MakeIntegerValue(+59)
                        };
                    default:
                        return null;
                }
            }
        }

        public override AtomicValue Evaluate(IItem item, IXPathContext context)
        {
            return ((AtomicValue)item).GetComponent(ComponentId);
        }

        public override Elaborator GetElaborator()
        {
            return new AccessorFnElaborator();
        }
        public enum Component
        {
            YEAR,
            MONTH,
            DAY,
            HOURS,
            MINUTES,
            SECONDS,
            TIMEZONE,
            LOCALNAME,
            NAMESPACE,
            PREFIX,
            MICROSECONDS,
            NANOSECONDS,
            WHOLE_SECONDS,
            YEAR_ALLOWING_ZERO
            , ERA
            , INSTANT_SECONDS
            , OFFSET_SECONDS
            , MONTH_OF_YEAR
            , DAY_OF_MONTH
            , DAY_OF_YEAR
            , DAY_OF_WEEK
            , ALIGNED_DAY_OF_WEEK_IN_MONTH
            , ALIGNED_DAY_OF_WEEK_IN_YEAR
            , ALIGNED_WEEK_OF_MONTH
            , ALIGNED_WEEK_OF_YEAR
            , YEAR_OF_ERA
            , PROLEPTIC_MONTH
            , EPOCH_DAY
            , CLOCK_HOUR_OF_DAY
            , CLOCK_HOUR_OF_AMPM
            , HOUR_OF_DAY
            , HOUR_OF_AMPM
            , MILLI_OF_SECOND
            , MILLI_OF_DAY
            , MICRO_OF_SECOND
            , MICRO_OF_DAY
            , NANO_OF_SECOND
            , NANO_OF_DAY
            , MINUTE_OF_HOUR
            , MINUTE_OF_DAY
            , SECOND_OF_MINUTE
            , SECOND_OF_DAY
            , AMPM_OF_DAY
        }

        internal sealed class YearFromDateTime : AccessorFn
        {
            public override Component ComponentId => Component.YEAR;
        }

        internal sealed class MonthFromDateTime : AccessorFn
        {
            public override Component ComponentId => Component.MONTH;
        }

        internal sealed class DayFromDateTime : AccessorFn
        {
            public override Component ComponentId => Component.DAY;
        }

        internal sealed class HoursFromDateTime : AccessorFn
        {
            public override Component ComponentId => Component.HOURS;
        }

        internal sealed class MinutesFromDateTime : AccessorFn
        {
            public override Component ComponentId => Component.MINUTES;
        }

        internal sealed class SecondsFromDateTime : AccessorFn
        {
            public override Component ComponentId => Component.SECONDS;
        }

        internal sealed class TimezoneFromDateTime : AccessorFn
        {
            public override Component ComponentId => Component.TIMEZONE;
        }

        internal sealed class YearFromDate : AccessorFn
        {
            public override Component ComponentId => Component.YEAR;
        }

        internal sealed class MonthFromDate : AccessorFn
        {
            public override Component ComponentId => Component.MONTH;
        }

        internal sealed class DayFromDate : AccessorFn
        {
            public override Component ComponentId => Component.DAY;
        }

        internal sealed class TimezoneFromDate : AccessorFn
        {
            public override Component ComponentId => Component.TIMEZONE;
        }

        internal sealed class HoursFromTime : AccessorFn
        {
            public override Component ComponentId => Component.HOURS;
        }

        internal sealed class MinutesFromTime : AccessorFn
        {
            public override Component ComponentId => Component.MINUTES;
        }

        internal sealed class SecondsFromTime : AccessorFn
        {
            public override Component ComponentId => Component.SECONDS;
        }

        internal sealed class TimezoneFromTime : AccessorFn
        {
            public override Component ComponentId => Component.TIMEZONE;
        }

        internal sealed class YearsFromDuration : AccessorFn
        {
            public override Component ComponentId => Component.YEAR;
        }

        internal sealed class MonthsFromDuration : AccessorFn
        {
            public override Component ComponentId => Component.MONTH;
        }

        internal sealed class DaysFromDuration : AccessorFn
        {
            public override Component ComponentId => Component.DAY;
        }

        internal sealed class HoursFromDuration : AccessorFn
        {
            public override Component ComponentId => Component.HOURS;
        }

        internal sealed class MinutesFromDuration : AccessorFn
        {
            public override Component ComponentId => Component.MINUTES;
        }

        internal sealed class SecondsFromDuration : AccessorFn
        {
            public override Component ComponentId => Component.SECONDS;
        }

        internal sealed class LocalNameFromQName : AccessorFn
        {
            public override Component ComponentId => Component.LOCALNAME;
        }

        internal sealed class PrefixFromQName : AccessorFn
        {
            public override Component ComponentId => Component.PREFIX;
        }

        internal sealed class NamespaceUriFromQName : AccessorFn
        {
            public override Component ComponentId => Component.NAMESPACE;
        }

        /// <summary>
        /// Elaborator for accessor functions such as hours-from-date-Time, minutes-from-duration
        /// </summary>
        internal sealed class AccessorFnElaborator : ItemElaborator
        {
            public override IItemEvaluator ElaborateForItem()
            {
                SystemFunctionCall fnc = (SystemFunctionCall)GetExpression();
                AccessorFn fn = (AccessorFn)fnc.TargetFunction;
                Component component = fn.ComponentId;
                IItemEvaluator argEval = fnc.GetArg(0).MakeElaborator().ElaborateForItem();
                bool nullable = Cardinality.AllowsZero(fnc.GetArg((0)).GetCardinality());
                return (context) =>
                {
                    AtomicValue @base = ((AtomicValue)argEval.Eval(context));
                    if (nullable && @base == null)
                    {
                        return null;
                    }

                    return @base.GetComponent(component);
                };
            }
        }
    }
}
