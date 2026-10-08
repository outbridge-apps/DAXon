////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace OutSmart.DAXon.Api
{
    // Helper for OccurrenceIndicator (enum) — Java had this as a
    // static method on the enum, but C# enums can't have static methods.
    public static class OccurrenceIndicatorHelper
    {
        public static OccurrenceIndicator GetOccurrenceIndicator(int cardinality)
        {
            // Map StaticProperty.* cardinality bit to OccurrenceIndicator.
            // StaticProperty constants we'd reference are in OutSmart.DAXon.Expressions,
            // but to keep this stub minimal we use the canonical Saxon ordering:
            //   EMPTY -> ZERO; ALLOWS_ZERO_OR_ONE -> ZERO_OR_ONE;
            //   ALLOWS_ZERO_OR_MORE -> ZERO_OR_MORE; ALLOWS_ONE -> ONE;
            //   ALLOWS_ONE_OR_MORE -> ONE_OR_MORE.
            // For unknown cardinalities, fall back to ZERO_OR_MORE.
            switch (cardinality & 0x000FE000) // StaticProperty cardinality bits
            {
                case 0x00010000: return OccurrenceIndicator.ZERO;             // EMPTY
                case 0x00020000: return OccurrenceIndicator.ZERO_OR_ONE;      // ALLOWS_ZERO_OR_ONE
                case 0x00040000: return OccurrenceIndicator.ZERO_OR_MORE;     // ALLOWS_ZERO_OR_MORE
                case 0x00080000: return OccurrenceIndicator.ONE;              // ALLOWS_ONE
                default: return OccurrenceIndicator.ONE_OR_MORE;
            }
        }
    }
}
