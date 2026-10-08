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

using System;

namespace OutSmart.DAXon.Transformation
{
    internal static class FunctionStreamabilityExtensions
    {
        // Java FunctionStreamability.isStreaming() returns true for all values except UNCLASSIFIED.
        public static bool IsStreaming(this FunctionStreamability s)
            => s != FunctionStreamability.UNCLASSIFIED;
        // Java FunctionStreamability.isConsuming() (upstream trans/FunctionStreamability.java:25-27).
        public static bool IsConsuming(this FunctionStreamability s)
            => s == FunctionStreamability.ABSORBING || s == FunctionStreamability.SHALLOW_DESCENT || s == FunctionStreamability.DEEP_DESCENT;
        // Java's FunctionStreamability.of(String) -- enum static factory.
        public static FunctionStreamability Of(string name)
        {
            if (string.IsNullOrEmpty(name))
                return FunctionStreamability.UNCLASSIFIED;
            FunctionStreamability v;
            return Enum.TryParse(name.Replace("-", "_").ToUpperInvariant(), out v) ? v : FunctionStreamability.UNCLASSIFIED;
        }
    }
}
