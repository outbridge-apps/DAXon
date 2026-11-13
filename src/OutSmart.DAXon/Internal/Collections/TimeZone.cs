////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Misc Java stdlib types Saxon references but we don't yet shim individually.

using System;
using System.Collections.Generic;

namespace OutSmart.DAXon.Internal.Collections
{

    internal sealed class TimeZone
    {
        // null => UTC stub (default ctor, implicit-operator fallbacks, SimpleTimeZone).
        private readonly ZoneRules rules;

        private TimeZone() { }
        private TimeZone(ZoneRules rules) { this.rules = rules; }

        // Offset in milliseconds at the given instant (millis since epoch), DST-aware. Mirrors
        // java.util.TimeZone.getOffset(long).
        public int GetOffset(long date)
        {
            return rules == null ? 0 : rules.OffsetAt(date / 1000) * 1000;
        }

        // Unknown ids fall back to the UTC stub, matching java.util.TimeZone.getTimeZone (which returns GMT for unrecognised ids).
        public static TimeZone GetTimeZone(string id)
        {
            ZoneRules found = ZoneRules.Find(id);
            return found == null ? new TimeZone() : new TimeZone(found);
        }
    }
}
