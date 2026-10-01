////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Misc Java stdlib types Saxon references but we don't yet shim individually.

using System;
using System.Collections.Generic;
using System.IO;

namespace OutSmart.DAXon.Internal.Collections
{

    internal sealed class TimeZone
    {

        private static readonly Dictionary<string, string> IanaToWindows =
            new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "America/New_York", "Eastern Standard Time" },
            { "America/Chicago", "Central Standard Time" },
            { "America/Denver", "Mountain Standard Time" },
            { "America/Phoenix", "US Mountain Standard Time" },
            { "America/Los_Angeles", "Pacific Standard Time" },
            { "America/Anchorage", "Alaskan Standard Time" },
            { "America/Adak", "Aleutian Standard Time" },
            { "Pacific/Honolulu", "Hawaiian Standard Time" },
            { "Europe/London", "GMT Standard Time" },
            { "Europe/Dublin", "GMT Standard Time" },
            { "Europe/Paris", "Romance Standard Time" },
            { "Europe/Berlin", "W. Europe Standard Time" },
            { "Europe/Madrid", "Romance Standard Time" },
            { "Europe/Rome", "W. Europe Standard Time" },
            { "Europe/Amsterdam", "W. Europe Standard Time" },
            { "Europe/Brussels", "Romance Standard Time" },
            { "Europe/Moscow", "Russian Standard Time" },
            { "Europe/Athens", "GTB Standard Time" },
            { "Europe/Helsinki", "FLE Standard Time" },
            { "Asia/Kolkata", "India Standard Time" },
            { "Asia/Calcutta", "India Standard Time" },
            { "Asia/Tokyo", "Tokyo Standard Time" },
            { "Asia/Shanghai", "China Standard Time" },
            { "Australia/Sydney", "AUS Eastern Standard Time" },
        };
        // null => UTC stub (default ctor, implicit-operator fallbacks, SimpleTimeZone). A resolved
        // TimeZoneInfo gives DST-aware offsets — needed by format-date/time with an Olson place.
        private readonly TimeZoneInfo _tzi;

        private TimeZone() { }
        private TimeZone(TimeZoneInfo tzi) { _tzi = tzi; }

        // Offset in milliseconds at the given instant (millis since epoch), DST-aware. Mirrors
        // java.util.TimeZone.getOffset(long).
        public int GetOffset(long date)
        {
            if (_tzi == null)
            {
                return 0;
            }

            try
            {
                var when = DateTimeOffset.FromUnixTimeMilliseconds(date);
                return (int)_tzi.GetUtcOffset(when).TotalMilliseconds;
            }
            catch (ArgumentOutOfRangeException)
            {
                // Instant outside DateTimeOffset's representable range (extreme XSD years): no DST
                // data that far out, so fall back to the standard offset.
                return (int)_tzi.BaseUtcOffset.TotalMilliseconds;
            }
        }

        // Resolve an Olson/IANA (or Windows) zone id to a platform TimeZoneInfo. Unknown ids fall back
        // to the UTC stub, matching java.util.TimeZone.getTimeZone (which returns GMT for unrecognised ids).
        public static TimeZone GetTimeZone(string id)
        {
            TimeZoneInfo tzi = Resolve(id);
            return tzi == null ? new TimeZone() : new TimeZone(tzi);
        }

        // net472/Windows TimeZoneInfo speaks Windows ids, not IANA/Olson, so translate the CLDR
        // windowsZones aliases the format-date timezone tables reference; try the mapped Windows id
        // first, then the id verbatim (.NET Core accepts IANA directly). Returns null if unresolvable.
        public static TimeZoneInfo Resolve(string id)
        {
            if (id == null)
                return null;
            if (id == "UTC" || id == "GMT" || id == "Z" || id == "Etc/UTC" || id == "Etc/GMT")
                return TimeZoneInfo.Utc;
            string win = IanaToWindows.TryGetValue(id, out string w) ? w : null;
            foreach (string candidate in win != null ? new[] { win, id } : new[] { id })
            {
                try { return TimeZoneInfo.FindSystemTimeZoneById(candidate); }
                catch (TimeZoneNotFoundException) { }
                catch (InvalidTimeZoneException) { return null; }
            }
            return null;
        }
    }
}
