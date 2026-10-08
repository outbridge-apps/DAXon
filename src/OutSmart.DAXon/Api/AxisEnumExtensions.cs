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

using OutSmart.DAXon.Model;

namespace OutSmart.DAXon.Api
{
    // Axis.GetAxisNumber() — Java enum had this method.
    public static class AxisEnumExtensions
    {
        // The enum keeps its declaration order (hosts are compiled against it); AxisInfo numbers
        // NAMESPACE before PARENT, so the last five differ.
        public static int GetAxisNumber(this Axis axis)
        {
            switch (axis)
            {
                case Axis.ANCESTOR:
                    return AxisInfo.ANCESTOR;
                case Axis.ANCESTOR_OR_SELF:
                    return AxisInfo.ANCESTOR_OR_SELF;
                case Axis.ATTRIBUTE:
                    return AxisInfo.ATTRIBUTE;
                case Axis.CHILD:
                    return AxisInfo.CHILD;
                case Axis.DESCENDANT:
                    return AxisInfo.DESCENDANT;
                case Axis.DESCENDANT_OR_SELF:
                    return AxisInfo.DESCENDANT_OR_SELF;
                case Axis.FOLLOWING:
                    return AxisInfo.FOLLOWING;
                case Axis.FOLLOWING_SIBLING:
                    return AxisInfo.FOLLOWING_SIBLING;
                case Axis.PARENT:
                    return AxisInfo.PARENT;
                case Axis.PRECEDING:
                    return AxisInfo.PRECEDING;
                case Axis.PRECEDING_SIBLING:
                    return AxisInfo.PRECEDING_SIBLING;
                case Axis.SELF:
                    return AxisInfo.SELF;
                case Axis.NAMESPACE:
                    return AxisInfo.NAMESPACE;
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(axis));
            }
        }
    }
}
