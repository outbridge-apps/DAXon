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

namespace OutSmart.DAXon.Transformation
{
    // See FunctionStreamabilityExtensions.Of -- call sites need to use `FunctionStreamabilityExtensions.Of(s)` not the enum's static method.
}

