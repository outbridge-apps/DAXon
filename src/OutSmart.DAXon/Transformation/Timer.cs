////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Numerics;
using System;
using System.Text;
namespace OutSmart.DAXon.Transformation
{
    /// <summary>
    /// Utility class for collecting and reporting timing information, used only under diagnostic control
    /// </summary>
    public class Timer
    {
        private static readonly DayTimeDurationValue milliSecond = new DayTimeDurationValue(1, 0, 0, 0, 0, 1000);
        private readonly long start;
        private long prev;
        public Timer()
        {
            start = (DateTime.Now.Ticks * 100L);
            prev = start;
        }

        public static string ShowExecutionTimeNano(long nanosecs)
        {
            if (nanosecs < 1000000000)
            {

                // time less than one second
                return (nanosecs / 1000000) + "ms";
            }
            else
            {
                try
                {
                    double millisecs = nanosecs / 1000000;
                    DayTimeDurationValue d = (DayTimeDurationValue)milliSecond.Multiply(millisecs);
                    long days = ((NumericValue)d.GetComponent(AccessorFn.Component.DAY)).LongValue();
                    long hours = ((NumericValue)d.GetComponent(AccessorFn.Component.HOURS)).LongValue();
                    long minutes = ((NumericValue)d.GetComponent(AccessorFn.Component.MINUTES)).LongValue();
                    BigDecimal seconds = ((NumericValue)d.GetComponent(AccessorFn.Component.SECONDS)).GetDecimalValue();
                    StringBuilder fsb = new StringBuilder(256);
                    if (days > 0)
                    {
                        fsb.Append(days + "days ");
                    }

                    if (hours > 0)
                    {
                        fsb.Append(hours + "h ");
                    }

                    if (minutes > 0)
                    {
                        fsb.Append(minutes + "m ");
                    }

                    fsb.Append(seconds + "s");
                    return fsb.ToString() + " (" + nanosecs / 1000000 + "ms)";
                }
                catch (XPathException)
                {
                    return nanosecs / 1000000 + "ms";
                }
            }
        }

        public static string ShowMemoryUsed()
        {
            long value = GC.GetTotalMemory(false);
            return "Memory used: " + (value / 1000000) + "Mb";
        }

        public virtual void Report(string label)
        {
            long time = (DateTime.Now.Ticks * 100L);
            Console.Error.WriteLine(label + " " + (time - prev) / 1000000 + "ms");
            prev = time;
        }

        public virtual void ReportCumulative(string label)
        {
            long time = (DateTime.Now.Ticks * 100L);
            Console.Error.WriteLine(label + " " + (time - start) / 1000000 + "ms");
            prev = time;
        }
    }
}