////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;

namespace OutSmart.DAXon.Internal.Collections
{
    /// <summary>
    /// Java.util.StringTokenizer shim. Splits a string into tokens using a
    /// delimiter set. Saxon uses this in a handful of places (XPath parser
    /// lexer hints, locale parsing).
    ///
    /// Java semantics (preserved):
    ///   - Default delimiters: " \t\n\r\f"
    ///   - Delimiters are SETS of single characters, not regex
    ///   - When returnDelims=true, delimiters are themselves returned as tokens
    ///   - Empty tokens are NOT produced (consecutive delimiters collapse)
    /// </summary>
    internal sealed class StringTokenizer
    {
        private readonly string _str;
        private readonly string _delims;
        private readonly bool _returnDelims;
        private int _pos;

        public StringTokenizer(string str)
            : this(str, " \t\n\r\f", false) { }

        public StringTokenizer(string str, string delims)
            : this(str, delims, false) { }

        public StringTokenizer(string str, string delims, bool returnDelims)
        {
            _str = str ?? throw new ArgumentNullException(nameof(str));
            _delims = delims ?? string.Empty;
            _returnDelims = returnDelims;
            _pos = 0;
        }

        public bool HasMoreTokens()
        {
            // Skip leading delimiters if we don't return them.
            int p = _pos;
            if (!_returnDelims)
            {
                while (p < _str.Length && IsDelim(_str[p]))
                    p++;
            }
            return p < _str.Length;
        }

        public string NextToken()
        {
            if (!_returnDelims)
            {
                // Skip delimiters
                while (_pos < _str.Length && IsDelim(_str[_pos]))
                    _pos++;
            }
            if (_pos >= _str.Length)
                throw new InvalidOperationException("No more tokens");

            int start = _pos;
            if (_returnDelims && IsDelim(_str[_pos]))
            {
                // Return single delimiter as token
                _pos++;
                return _str.Substring(start, 1);
            }
            // Consume non-delimiters
            while (_pos < _str.Length && !IsDelim(_str[_pos]))
                _pos++;
            return _str.Substring(start, _pos - start);
        }

        public int CountTokens()
        {
            int saved = _pos;
            int count = 0;
            while (HasMoreTokens())
            {
                NextToken();
                count++;
            }
            _pos = saved;
            return count;
        }

        private bool IsDelim(char c) => _delims.IndexOf(c) >= 0;
    }
}
