////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace OutSmart.DAXon.Values
{
    /// <summary>
    /// The match key of a string under a collation that has byte keys, as distinct-values, group by and xsl:key hash it. A type
    /// of its own, as upstream's java.text.CollationKey is: the key of a string, equal to no xs:base64Binary of the same octets
    /// (Base64BinaryValue.Equals). Never a value a query sees: collation-key() hands out a plain xs:base64Binary.
    /// </summary>
    internal sealed class CollationKeyBytes : Base64BinaryValue
    {
        public CollationKeyBytes(byte[] key) : base(key)
        {
        }
    }
}
