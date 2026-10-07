////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System.Text;

namespace OutSmart.DAXon.Expressions.Sorting
{
    /// <summary>
    /// What Saxon-HE's Java collator ignores at primary strength besides case and accents: spaces, dashes, zero-width
    /// and control characters - the '=' and ';' entries of java.text.CollationRules. Other punctuation counts at every
    /// strength (measured against Saxon-HE 12.9 over ASCII, Latin-1 and the General Punctuation block). Comparison and
    /// keys only: Saxon's substring matching does not skip them (contains('x-y', 'xy') is false there).
    /// </summary>
    internal static class PrimaryIgnorables
    {
        internal static bool Is(char c)
        {
            switch (c)
            {
                case ' ':
                case '-':
                case ' ':
                case '­':
                case '−':
                case '　':
                case '﻿':
                    return true;
                default:
                    return c < ' ' || (c >= '\u007F' && c <= '\u009F') || (c >= ' ' && c <= '―');
            }
        }

        // s without its ignorable characters; s itself when it has none.
        internal static string Strip(string s)
        {
            int i = 0;
            while (i < s.Length && !Is(s[i]))
            {
                Core.Controller.CheckActiveTimeoutEvery64K(i);   // both loops: the strings a collation compares can be long
                i++;
            }

            if (i == s.Length)
            {
                return s;
            }

            var kept = new StringBuilder(s.Length);
            kept.Append(s, 0, i);
            for (; i < s.Length; i++)
            {
                Core.Controller.CheckActiveTimeoutEvery64K(i);
                if (!Is(s[i]))
                {
                    kept.Append(s[i]);
                }
            }

            return kept.ToString();
        }
    }
}
