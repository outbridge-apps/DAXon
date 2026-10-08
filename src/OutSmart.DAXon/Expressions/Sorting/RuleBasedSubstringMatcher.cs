////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Text;

namespace OutSmart.DAXon.Expressions.Sorting
{
    internal sealed class RuleBasedSubstringMatcher : IStringCollator
    {
        public string CollationURI => null;
        public RuleBasedSubstringMatcher() { }
        public int CompareStrings(UnicodeString o1, UnicodeString o2) => 0;
        public bool ComparesEqual(UnicodeString s1, UnicodeString s2) => false;
        public bool IsEqualToEmpty(UnicodeString s1) => false;
        public IAtomicMatchKey GetCollationKey(UnicodeString s) => null;
    }
}
