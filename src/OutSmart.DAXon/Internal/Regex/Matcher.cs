////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
namespace OutSmart.DAXon.Internal.Regex
{
    using SysMatch = System.Text.RegularExpressions.Match;

    internal sealed class Matcher
    {
        private readonly Pattern _pattern;
        private readonly string _input;
        private SysMatch _current;

        internal Matcher(Pattern pattern, string input)
        {
            _pattern = pattern;
            _input = input;
        }

        public bool Matches()
        {
            _current = _pattern.Regex.Match(_input);
            return _current.Success && _current.Length == _input.Length;
        }

        public string Group(int group) => _current?.Groups[group]?.Value;
        public string ReplaceAll(string replacement) => _pattern.Regex.Replace(_input, replacement);
    }
}
