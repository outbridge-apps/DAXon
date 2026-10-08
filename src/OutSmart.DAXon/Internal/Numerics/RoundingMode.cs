////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
namespace OutSmart.DAXon.Internal.Numerics
{
    /// <summary>Java.math.RoundingMode enum -- Saxon uses HALF_UP / HALF_DOWN / etc.
    /// Implicit conversion to int via the underlying byte value (Java's API takes
    /// either int or RoundingMode interchangeably).
    /// </summary>
    public enum RoundingMode
    {
        UP = 0,
        DOWN = 1,
        CEILING = 2,
        FLOOR = 3,
        HALF_UP = 4,
        HALF_DOWN = 5,
        HALF_EVEN = 6,
        UNNECESSARY = 7
    }
}
