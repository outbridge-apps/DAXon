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

using OutSmart.DAXon.Lib;

namespace OutSmart.DAXon.Api
{

    public static class ValidationModeExtensions
    {
        public static int GetNumber(this ValidationMode mode)
        {
            switch (mode)
            {
                case ValidationMode.STRICT: return Validation.STRICT;
                case ValidationMode.LAX: return Validation.LAX;
                case ValidationMode.PRESERVE: return Validation.PRESERVE;
                case ValidationMode.STRIP: return Validation.STRIP;
                case ValidationMode.DEFAULT: return Validation.DEFAULT;
                default: return Validation.DEFAULT;
            }
        }

        public static ValidationMode Get(int number)
        {
            if (number == Validation.STRICT)
                return ValidationMode.STRICT;
            if (number == Validation.LAX)
                return ValidationMode.LAX;
            if (number == Validation.STRIP)
                return ValidationMode.STRIP;
            if (number == Validation.PRESERVE)
                return ValidationMode.PRESERVE;
            return ValidationMode.DEFAULT;
        }
    }
}
