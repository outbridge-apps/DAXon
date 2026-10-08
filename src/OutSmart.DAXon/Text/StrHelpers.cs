////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;

namespace OutSmart.DAXon.Text
{

    // RequireNonNegativeInt helper used by Twine* / BMPString /
    // Slice16 -- Saxon source assumes a static helper that narrows a long
    // index to int with a bounds check.
    internal static class StrHelpers
    {
        public static int requireNonNegativeInt(long v)
        {
            if (v < 0)
                throw new ArgumentOutOfRangeException(nameof(v), "must be non-negative");
            if (v > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(v), "exceeds int range");
            return (int)v;
        }
    }
}
