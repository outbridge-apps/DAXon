////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Trees.Iterators;
using System;
namespace OutSmart.DAXon.Regex
{
    public interface IRegularExpression
    {
        bool Matches(UnicodeString input);
        bool ContainsMatch(UnicodeString input);
        IAtomicIterator Tokenize(UnicodeString input);
        IRegexIterator Analyze(UnicodeString input);
        UnicodeString Replace(UnicodeString input, UnicodeString replacement);
        UnicodeString ReplaceWith(UnicodeString input, Func<UnicodeString, UnicodeString[], UnicodeString> replacement);
        string Flags { get; }
        bool IsPlatformNative();
    }
}