////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace OutSmart.DAXon.Expressions.Elaboration
{
    /// <summary>
    /// A {@link IPingable} represents an object that can receive an event with no associated information.
    /// </summary>
    internal interface IPingable
    {
        /// <summary>
        /// Notify that an event has occurred; the recipient is expected to know what this means.
        /// </summary>
        void Ping();
    }
}