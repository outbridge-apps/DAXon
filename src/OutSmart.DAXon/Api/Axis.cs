////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace OutSmart.DAXon.Api
{
    /// <summary>
    /// This is an enumeration class containing constants representing the thirteen XPath axes
    /// </summary>
    public enum Axis
    {
        // ANCESTOR(AxisInfo.ANCESTOR)
        ANCESTOR,
        // ANCESTOR_OR_SELF(AxisInfo.ANCESTOR_OR_SELF)
        ANCESTOR_OR_SELF,
        // ATTRIBUTE(AxisInfo.ATTRIBUTE)
        ATTRIBUTE,
        // CHILD(AxisInfo.CHILD)
        CHILD,
        // DESCENDANT(AxisInfo.DESCENDANT)
        DESCENDANT,
        // DESCENDANT_OR_SELF(AxisInfo.DESCENDANT_OR_SELF)
        DESCENDANT_OR_SELF,
        // FOLLOWING(AxisInfo.FOLLOWING)
        FOLLOWING,
        // FOLLOWING_SIBLING(AxisInfo.FOLLOWING_SIBLING)
        FOLLOWING_SIBLING,
        // PARENT(AxisInfo.PARENT)
        PARENT,
        // PRECEDING(AxisInfo.PRECEDING)
        PRECEDING,
        // PRECEDING_SIBLING(AxisInfo.PRECEDING_SIBLING)
        PRECEDING_SIBLING,
        // SELF(AxisInfo.SELF)
        SELF,
        // NAMESPACE(AxisInfo.NAMESPACE)
        NAMESPACE

        // --------------------
        // private final int axisNumber;
    }
}