////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Collections;
using System.Text;

namespace OutSmart.DAXon.Serialization.CharCodes
{
    // UTF8CharacterSet stub must implement ICharacterSet (10 callers assign to ICharacterSet).
    internal sealed class UTF8CharacterSet : ICharacterSet
    {
        private static readonly UTF8CharacterSet _instance = new UTF8CharacterSet();
        public string CanonicalName => "UTF-8";
        public static UTF8CharacterSet GetInstance() => _instance;
        public bool InCharset(int ch) => true;
        // 2026-06-10: faithful encode(IntIterator) (upstream UTF8CharacterSet.java:103, used by
        // EncodeForUri.EscapeChar) - the UTF-8 byte sequence of the codepoint stream.
        public static byte[] Encode(IIntIterator codePoints)
        {
            var sb = new StringBuilder();
            while (codePoints.MoveNext())
            {
                sb.Append(char.ConvertFromUtf32(codePoints.Current));
            }
            return Encoding.UTF8.GetBytes(sb.ToString());
        }
    }
}
