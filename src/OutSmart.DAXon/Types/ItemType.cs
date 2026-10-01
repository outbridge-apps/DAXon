////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Misc Java stdlib types Saxon references but we don't yet shim individually.

using System;
using System.Collections.Generic;
using System.IO;

// Saxon-internal stub namespaces — sub-packages permanently excluded for now.
// Stubs only what's needed for top-level references to resolve.

namespace OutSmart.DAXon.Types
{
    // ItemType interface (collided with s9api.ItemType during transpile — Java has both).
    // OutSmart.DAXon.Internal can't reference OutSmart.DAXon.Model — stubs use object placeholders.
    public interface ItemType
    {
        int PrimitiveType { get; }
        bool IsAtomicType();
        bool IsPlainType();
        string BasicAlphaCode { get; }
        bool Matches(OutSmart.DAXon.Model.IItem item, TypeHierarchy th);
    }
}
