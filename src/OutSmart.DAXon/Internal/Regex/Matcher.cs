////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
namespace OutSmart.DAXon.Internal.Regex
{
    using System;
    using SysMatch = System.Text.RegularExpressions.Match;

    internal sealed class Matcher
    {
        private readonly Pattern _pattern;
        private readonly string _input;
        private SysMatch _current;
        private int _searchStart;

        internal Matcher(Pattern pattern, string input)
        {
            _pattern = pattern;
            _input = input;
            _searchStart = 0;
        }

        public bool Matches()
        {
            _current = _pattern.Regex.Match(_input);
            return _current.Success && _current.Length == _input.Length;
        }

        public bool Find()
        {
            _current = _pattern.Regex.Match(_input, _searchStart);
            if (_current.Success)
            {
                _searchStart = _current.Index + Math.Max(1, _current.Length);
                return true;
            }
            return false;
        }

        public string Group(int group) => _current?.Groups[group]?.Value;

        public int Start() => _current?.Index ?? -1;
        public int End() => _current == null ? -1 : _current.Index + _current.Length;
        public string ReplaceAll(string replacement) => _pattern.Regex.Replace(_input, replacement);
    }
}
