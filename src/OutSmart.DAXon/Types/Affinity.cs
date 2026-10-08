////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace OutSmart.DAXon.Types
{
    /// <summary>
    /// A set of constants enumerating the possible relationships between one type and another
    /// </summary>
    public enum Affinity
    {
        /// <summary>
        /// The two types are identical
        /// </summary>
        SAME_TYPE,
        SUBSUMES,
        SUBSUMED_BY,
        DISJOINT,
        OVERLAPS
    }
}