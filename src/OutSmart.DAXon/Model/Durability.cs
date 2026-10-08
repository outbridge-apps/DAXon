////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace OutSmart.DAXon.Model
{
    /// <summary>
    /// The Durability of a node affects how it is handled for the purposes of caching by memo functions.
    /// </summary>
    public enum Durability
    {
        /// <summary>
        /// A node is lasting if it is expected to remain accessible throughout the duration of a transformation
        /// </summary>
        LASTING,
        TEMPORARY,
        FLEETING,
        MUTABLE,
        /// <summary>
        /// Durability undefined means we don't know
        /// </summary>
        UNDEFINED
    }
}