////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Collections;

namespace OutSmart.DAXon.Text
{

    // Runtime: real code-point iterator for the string-backed UnicodeString stubs
    // (StringView/BMPString/EmptyUnicodeString/Twine16). Java UnicodeString.codePoints()
    // returns an IntIterator over Unicode code points; the stubs returned null, which NRE'd
    // any consumer (first hit: StandardDiagnostics.expandSpecialCharacters in the error
    // reporter). Surrogate-pair aware so astral code points iterate as single values.
    internal sealed class StrCodePointIterator : AbstractIntIterator
    {
        private readonly string _s;
        private int _i;
        public StrCodePointIterator(string s) { _s = s ?? ""; }
        public override bool HasNext() => _i < _s.Length;
        public override int Next()
        {
            if (_i >= _s.Length)
                return -1;
            char c = _s[_i++];
            if (char.IsHighSurrogate(c) && _i < _s.Length && char.IsLowSurrogate(_s[_i]))
                return char.ConvertToUtf32(c, _s[_i++]);
            return c;
        }
    }
}
