////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// DAXonEnumExtensions.cs
//
// paulirwin converts Java enums with methods into C# enums + commented-out
// method bodies (since C# enums can't have methods). The Saxon source has
// many such enums (OccurrenceIndicator.getCardinality(), ValidationMode.getNumber(),
// FunctionStreamability.isStreaming()).
//
// This file provides extension methods that recreate the Java method semantics
// for use sites that still call them as `enum.Method()`.
//

using OutSmart.DAXon.Expressions;

namespace OutSmart.DAXon.Api
{
    public static class OccurrenceIndicatorExtensions
    {
        public static int GetCardinality(this OccurrenceIndicator ind)
        {
            switch (ind)
            {
                case OccurrenceIndicator.ZERO: return StaticProperty.EMPTY;
                case OccurrenceIndicator.ZERO_OR_ONE: return StaticProperty.ALLOWS_ZERO_OR_ONE;
                case OccurrenceIndicator.ZERO_OR_MORE: return StaticProperty.ALLOWS_ZERO_OR_MORE;
                case OccurrenceIndicator.ONE: return StaticProperty.ALLOWS_ONE;
                case OccurrenceIndicator.ONE_OR_MORE: return StaticProperty.ALLOWS_ONE_OR_MORE;
                default: return StaticProperty.EMPTY;
            }
        }

        public static OccurrenceIndicator GetOccurrenceIndicator(int cardinality)
        {
            if (cardinality == StaticProperty.EMPTY)
                return OccurrenceIndicator.ZERO;
            if (cardinality == StaticProperty.ALLOWS_ZERO_OR_ONE)
                return OccurrenceIndicator.ZERO_OR_ONE;
            if (cardinality == StaticProperty.ALLOWS_ZERO_OR_MORE)
                return OccurrenceIndicator.ZERO_OR_MORE;
            if (cardinality == StaticProperty.ALLOWS_ONE)
                return OccurrenceIndicator.ONE;
            if (cardinality == StaticProperty.ALLOWS_ONE_OR_MORE)
                return OccurrenceIndicator.ONE_OR_MORE;
            return OccurrenceIndicator.ZERO_OR_MORE;
        }

        public static bool AllowsZero(this OccurrenceIndicator ind)
            => ind == OccurrenceIndicator.ZERO || ind == OccurrenceIndicator.ZERO_OR_ONE || ind == OccurrenceIndicator.ZERO_OR_MORE;

        public static bool AllowsMany(this OccurrenceIndicator ind)
            => ind == OccurrenceIndicator.ZERO_OR_MORE || ind == OccurrenceIndicator.ONE_OR_MORE;

        public static bool Allows(this OccurrenceIndicator ind, int size)
        {
            switch (ind)
            {
                case OccurrenceIndicator.ZERO: return size == 0;
                case OccurrenceIndicator.ZERO_OR_ONE: return size <= 1;
                case OccurrenceIndicator.ZERO_OR_MORE: return true;
                case OccurrenceIndicator.ONE: return size == 1;
                case OccurrenceIndicator.ONE_OR_MORE: return size > 0;
                default: return false;
            }
        }
    }
}
