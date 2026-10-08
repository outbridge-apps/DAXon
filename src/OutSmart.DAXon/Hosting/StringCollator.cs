////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Text;
namespace OutSmart.DAXon.Lib
{
    public interface IStringCollator
    {
        string CollationURI { get; }
        int CompareStrings(UnicodeString o1, UnicodeString o2);
        bool ComparesEqual(UnicodeString s1, UnicodeString s2);
        bool IsEqualToEmpty(UnicodeString s1);



        IAtomicMatchKey GetCollationKey(UnicodeString s);
    }
}
