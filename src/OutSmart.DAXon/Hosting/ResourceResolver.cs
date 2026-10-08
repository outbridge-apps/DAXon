////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace OutSmart.DAXon.Lib
{
    public interface IResourceResolver
    {
        // P5 resolver-interface rework: the native resolver contract returns a .NET-typed ResolvedResource
        // (byte stream / char reader / node tree) rather than a JAXP Source. See ResolvedResource.
        ResolvedResource Resolve(ResourceRequest request);
    }
}