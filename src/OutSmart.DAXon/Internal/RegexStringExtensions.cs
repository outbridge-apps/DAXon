////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
namespace OutSmart.DAXon.Internal
{
    // String helpers whose argument is a REGEX pattern (java.lang.String.split/matches/replaceFirst
    // heritage) — named *Regex so they cannot be mistaken for the literal-argument BCL methods.
    internal static class RegexStringExtensions
    {
        public static string[] SplitRegex(this string s, string regex)
            => s == null ? new string[0] : System.Text.RegularExpressions.Regex.Split(s, regex);

        // limit > 0 caps the part count and keeps the tail VERBATIM (separators included) —
        // Java's split(regex, limit) semantics, which .NET's count overload matches exactly.
        // (The old shim re-joined the tail WITHOUT separators, corrupting e.g. "k=v=w" splits.)
        public static string[] SplitRegex(this string s, string regex, int limit)
        {
            if (s == null)
                return new string[0];
            if (limit > 0)
                return new System.Text.RegularExpressions.Regex(regex).Split(s, limit);
            return System.Text.RegularExpressions.Regex.Split(s, regex);
        }

        // Whole-string regex match.
        public static bool MatchesRegex(this string s, string regex)
            => s != null && System.Text.RegularExpressions.Regex.IsMatch(s, "^(?:" + regex + ")$");

        // Regex-based replace, first occurrence only.
        public static string ReplaceFirstRegex(this string s, string regex, string replacement)
            => s == null ? null : new System.Text.RegularExpressions.Regex(regex).Replace(s, replacement, 1);
    }
}
