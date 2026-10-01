////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
namespace OutSmart.DAXon.Internal
{
    internal static class StringBuilderExtensions
    {
        // Appends a full code point, encoding astral values as a surrogate pair
        // (System.Text.StringBuilder has no code-point-aware append on net472).
        public static System.Text.StringBuilder AppendCodePoint(this System.Text.StringBuilder sb, int codePoint)
        {
            if (codePoint < 0x10000)
            {
                sb.Append((char)codePoint);
            }
            else
            {
                int adjusted = codePoint - 0x10000;
                sb.Append((char)(0xD800 + (adjusted >> 10)));
                sb.Append((char)(0xDC00 + (adjusted & 0x3FF)));
            }
            return sb;
        }
    }
}
