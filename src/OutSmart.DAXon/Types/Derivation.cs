////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace OutSmart.DAXon.Types
{
    // Derivation enum-like constants (from XSD schema processing).
    internal static class Derivation
    {
        public const int DERIVATION_RESTRICTION = 1;
        public const int DERIVATION_EXTENSION = 2;
        public const int DERIVATION_LIST = 4;
        public const int DERIVATION_UNION = 8;
    }
}
