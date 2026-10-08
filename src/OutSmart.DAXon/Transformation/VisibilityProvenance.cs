////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2020 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace OutSmart.DAXon.Transformation
{
    /// <summary>
    /// Indicates where the visibility property of a component came from
    /// </summary>
    public enum VisibilityProvenance
    {
        DEFAULTED,
        EXPLICIT,
        EXPOSED,
        ACCEPTED,
        DERIVED
    }
}