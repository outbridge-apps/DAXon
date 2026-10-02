////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace OutSmart.DAXon.Trees.Utilities
{
    /// <summary>
    /// A node whose base URI is Navigator's xml:base rule applied over its own parent chain.
    /// Navigator climbs through such ancestors in a loop instead of calling GetBaseURI once per level.
    /// </summary>
    internal interface IInheritedBaseUri
    {
        /// <summary>True where the node starts a new external entity, whose system ID is then its base.</summary>
        bool IsTopWithinEntity();

        /// <summary>The node's cached base URI, or null when it has none cached.</summary>
        string KnownBaseUri { get; }

        void RememberBaseUri(string uri);
    }
}
