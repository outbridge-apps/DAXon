////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Text;
namespace OutSmart.DAXon.Lib
{
    internal interface ISubstringMatcher : IStringCollator
    {
        bool Contains(UnicodeString s1, UnicodeString s2);
        bool StartsWith(UnicodeString s1, UnicodeString s2);
        bool EndsWith(UnicodeString s1, UnicodeString s2);
        UnicodeString SubstringBefore(UnicodeString s1, UnicodeString s2);
        UnicodeString SubstringAfter(UnicodeString s1, UnicodeString s2);
    }
}