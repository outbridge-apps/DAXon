////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;

namespace OutSmart.DAXon.Internal.Collections
{
    // The rules of a named place in the IANA time zone database, which comes with the engine (data/tzdb.bin, made by
    // tools/TzData): the registry of Windows has little history (Asia/Kathmandu in 1980) and not every zone, and differs
    // from one machine to the next. A name the database does not have may still be a zone of the platform.
    internal sealed class ZoneRules
    {
        private const string ResourceName = "Net.Sf.Saxon.tzdb.bin";

        // Before 1900 a place has its present standard offset, as java.util.TimeZone has it: not the local mean time of the database.
        private const long Start1900 = -2208988800;

        internal static readonly ZoneRules Utc = new ZoneRules(0);

        private static readonly Lazy<Database> Data = new Lazy<Database>(Database.Load);

        private readonly TimeZoneInfo platform;
        private readonly long[] instants = new long[0];
        private readonly int[] offsets;         // the wall offset before instants[0], then after each
        private readonly int[] standards;       // the same for the standard offset
        private readonly YearlyRule[] yearly = new YearlyRule[0];

        private ZoneRules(int fixedOffset)
        {
            offsets = new[] { fixedOffset };
            standards = new[] { fixedOffset };
        }

        private ZoneRules(TimeZoneInfo platform)
        {
            this.platform = platform;
        }

        private ZoneRules(byte[] blob)
        {
            int at = 0;
            int first = (int)ReadZig(blob, ref at);
            int firstStandard = (int)ReadZig(blob, ref at);
            int n = (int)ReadVar(blob, ref at);
            instants = new long[n];
            offsets = new int[n + 1];
            standards = new int[n + 1];
            offsets[0] = first;
            standards[0] = firstStandard;
            long previous = 0;
            for (int i = 0; i < n; i++)
            {
                previous += ReadZig(blob, ref at);
                instants[i] = previous;
                offsets[i + 1] = (int)ReadZig(blob, ref at);
                standards[i + 1] = (int)ReadZig(blob, ref at);
            }

            int rules = (int)ReadVar(blob, ref at);
            yearly = new YearlyRule[rules];
            for (int i = 0; i < rules; i++)
            {
                yearly[i] = new YearlyRule(blob, ref at);
            }
        }

        /// <summary>The rules of a place by its IANA name; else those of a zone of the platform by its own name; else null.</summary>
        internal static ZoneRules Find(string id)
        {
            if (id == null)
            {
                return null;
            }

            if (id == "UTC" || id == "GMT" || id == "Z")
            {
                return Utc;
            }

            return Data.Value.Find(id) ?? OfPlatform(id);
        }

        private static ZoneRules OfPlatform(string id)
        {
            try
            {
                return new ZoneRules(TimeZoneInfo.FindSystemTimeZoneById(id));
            }
            catch (Exception e) when (e is TimeZoneNotFoundException || e is InvalidTimeZoneException || e is ArgumentException || e is System.Security.SecurityException)
            {
                return null;
            }
        }

        /// <summary>Seconds east of UTC on the clocks of this place at the instant.</summary>
        internal int OffsetAt(long epochSeconds)
        {
            if (platform != null)
            {
                return PlatformOffset(epochSeconds, false);
            }

            int n = instants.Length;
            if (n == 0)
            {
                return offsets[0];
            }

            if (epochSeconds < Start1900)
            {
                return standards[n];
            }

            if (yearly.Length > 0 && epochSeconds > instants[n - 1])
            {
                return RuledOffset(epochSeconds);
            }

            return offsets[IndexAt(epochSeconds)];
        }

        /// <summary>The same with no summer time in it.</summary>
        internal int StandardOffsetAt(long epochSeconds)
        {
            return platform != null ? PlatformOffset(epochSeconds, true)
                : instants.Length == 0 ? standards[0]
                : epochSeconds < Start1900 ? standards[instants.Length]
                : standards[IndexAt(epochSeconds)];
        }

        internal bool IsDaylightAt(long epochSeconds)
        {
            if (platform == null)
            {
                return OffsetAt(epochSeconds) != StandardOffsetAt(epochSeconds);
            }

            try
            {
                return platform.IsDaylightSavingTime(DateTimeOffset.FromUnixTimeSeconds(epochSeconds));
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;   // a year .NET's calendar does not reach
            }
        }

        private int PlatformOffset(long epochSeconds, bool standard)
        {
            try
            {
                return standard ? (int)platform.BaseUtcOffset.TotalSeconds : (int)platform.GetUtcOffset(DateTimeOffset.FromUnixTimeSeconds(epochSeconds)).TotalSeconds;
            }
            catch (ArgumentOutOfRangeException)
            {
                return (int)platform.BaseUtcOffset.TotalSeconds;
            }
        }

        // Into offsets and standards: what holds at the instant, a transition taking effect at its own second.
        private int IndexAt(long epochSeconds)
        {
            int low = 0;
            int high = instants.Length - 1;
            while (low <= high)
            {
                int middle = (low + high) >> 1;
                if (instants[middle] <= epochSeconds)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle - 1;
                }
            }

            return low;
        }

        private int RuledOffset(long epochSeconds)
        {
            long year = YearOf(FloorDiv(epochSeconds + offsets[offsets.Length - 1], 86400));
            foreach (YearlyRule rule in yearly)
            {
                if (epochSeconds < rule.InstantIn(year))
                {
                    return rule.Before;
                }
            }

            return yearly[yearly.Length - 1].After;
        }

        private static long FloorDiv(long a, long b)
        {
            long q = a / b;
            return (a % b != 0 && (a < 0) != (b < 0)) ? q - 1 : q;
        }

        // Days since 1970-01-01 of a date of the proleptic Gregorian calendar, and back to its year.
        private static long DaysOf(long year, int month, int day)
        {
            year -= month <= 2 ? 1 : 0;
            long era = FloorDiv(year, 400);
            long yearOfEra = year - era * 400;
            long dayOfYear = (153 * (month + (month > 2 ? -3 : 9)) + 2) / 5 + day - 1;
            long dayOfEra = yearOfEra * 365 + yearOfEra / 4 - yearOfEra / 100 + dayOfYear;
            return era * 146097 + dayOfEra - 719468;
        }

        private static long YearOf(long days)
        {
            days += 719468;
            long era = FloorDiv(days, 146097);
            long dayOfEra = days - era * 146097;
            long yearOfEra = (dayOfEra - dayOfEra / 1460 + dayOfEra / 36524 - dayOfEra / 146096) / 365;
            long dayOfYear = dayOfEra - (365 * yearOfEra + yearOfEra / 4 - yearOfEra / 100);
            long monthIndex = (5 * dayOfYear + 2) / 153;
            return yearOfEra + era * 400 + (monthIndex >= 10 ? 1 : 0);
        }

        private static long ReadVar(byte[] b, ref int at)
        {
            long v = 0;
            int shift = 0;
            while (true)
            {
                byte x = b[at++];
                v |= (long)(x & 0x7F) << shift;
                if ((x & 0x80) == 0)
                {
                    return v;
                }

                shift += 7;
            }
        }

        private static long ReadZig(byte[] b, ref int at)
        {
            long v = ReadVar(b, ref at);
            return (long)((ulong)v >> 1) ^ -(v & 1);
        }

        // "The last Sunday of March at 01:00 UTC", as java.time.zone.ZoneOffsetTransitionRule holds it.
        private sealed class YearlyRule
        {
            private readonly int month;
            private readonly int dayIndicator;
            private readonly int dayOfWeek;   // 1 Monday .. 7 Sunday, 0 none
            private readonly int secondOfDay;
            private readonly int definition;  // 0 UTC, 1 wall clock, 2 standard
            private readonly int standard;

            internal readonly int Before;
            internal readonly int After;

            internal YearlyRule(byte[] b, ref int at)
            {
                month = b[at++];
                dayIndicator = (int)ReadZig(b, ref at);
                dayOfWeek = b[at++];
                secondOfDay = (int)ReadVar(b, ref at);
                definition = b[at++];
                standard = (int)ReadZig(b, ref at);
                Before = (int)ReadZig(b, ref at);
                After = (int)ReadZig(b, ref at);
            }

            internal long InstantIn(long year)
            {
                long day;
                if (dayIndicator < 0)
                {
                    long firstOfNext = month == 12 ? DaysOf(year + 1, 1, 1) : DaysOf(year, month + 1, 1);
                    day = firstOfNext + dayIndicator;
                    day -= dayOfWeek == 0 ? 0 : Modulo(WeekdayOf(day) - dayOfWeek, 7);   // on or before
                }
                else
                {
                    day = DaysOf(year, month, dayIndicator);
                    day += dayOfWeek == 0 ? 0 : Modulo(dayOfWeek - WeekdayOf(day), 7);   // on or after
                }

                long local = day * 86400 + secondOfDay;
                return definition == 0 ? local : definition == 2 ? local - standard : local - Before;
            }

            // 1970-01-01 was a Thursday.
            private static int WeekdayOf(long days)
            {
                return (int)Modulo(days + 3, 7) + 1;
            }

            private static long Modulo(long a, long b)
            {
                long m = a % b;
                return m < 0 ? m + b : m;
            }
        }

        private sealed class Database
        {
            private readonly Dictionary<string, int> indexOf;
            private readonly byte[][] blobs;
            private readonly ZoneRules[] made;

            internal readonly string Version;

            private Database(string version, Dictionary<string, int> indexOf, byte[][] blobs)
            {
                Version = version;
                this.indexOf = indexOf;
                this.blobs = blobs;
                made = new ZoneRules[blobs.Length];
            }

            internal static Database Load()
            {
                byte[] all;
                using (Stream raw = typeof(ZoneRules).GetTypeInfo().Assembly.GetManifestResourceStream(ResourceName))
                using (var inflate = new DeflateStream(raw, CompressionMode.Decompress))
                using (var bytes = new MemoryStream())
                {
                    inflate.CopyTo(bytes);
                    all = bytes.ToArray();
                }

                int at = 0;
                string version = ReadString(all, ref at);
                int names = (int)ReadVar(all, ref at);
                var indexOf = new Dictionary<string, int>(names, StringComparer.Ordinal);
                for (int i = 0; i < names; i++)
                {
                    string name = ReadString(all, ref at);
                    indexOf[name] = (int)ReadVar(all, ref at);
                }

                var blobs = new byte[(int)ReadVar(all, ref at)][];
                for (int i = 0; i < blobs.Length; i++)
                {
                    int length = (int)ReadVar(all, ref at);
                    blobs[i] = new byte[length];
                    Buffer.BlockCopy(all, at, blobs[i], 0, length);
                    at += length;
                }

                return new Database(version, indexOf, blobs);
            }

            private static string ReadString(byte[] b, ref int at)
            {
                int length = (int)ReadVar(b, ref at);
                string s = Encoding.ASCII.GetString(b, at, length);
                at += length;
                return s;
            }

            // Two threads may make the same rules at once: both are right, and either is kept.
            internal ZoneRules Find(string id)
            {
                if (!indexOf.TryGetValue(id, out int i))
                {
                    return null;
                }

                ZoneRules rules = System.Threading.Volatile.Read(ref made[i]);
                if (rules == null)
                {
                    rules = new ZoneRules(blobs[i]);
                    System.Threading.Volatile.Write(ref made[i], rules);
                }

                return rules;
            }
        }
    }
}
