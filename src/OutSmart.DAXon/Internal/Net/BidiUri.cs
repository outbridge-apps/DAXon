////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Text;

namespace OutSmart.DAXon.Internal.Net
{
    /// <summary>
    /// In an AppDomain that targets .NET Framework before 4.7.2 (some web hosts still run as 4.5) System.Uri drops the bidi
    /// formatting characters U+200E, U+200F and U+202A-U+202E from a URI, escaped or not
    /// (Switch.System.Uri.DontKeepUnicodeBidiFormattingCharacters): resolve-uri lost them and doc() or unparsed-text()
    /// read another file. Around each System.Uri the engine makes they stand in as the private-use characters
    /// U+F8E0-U+F8E6, which it keeps, and are put back in what it gives. Elsewhere this does nothing.
    /// </summary>
    internal static class BidiUri
    {
        // Measured, not read from the switch: its default comes from the AppDomain's target framework.
        internal static readonly bool Active = Drops();

        private static readonly char[] Marks = { '‎', '‏', '‪', '‫', '‬', '‭', '‮' };
        private const char FirstStandIn = '';

        private static bool Drops()
        {
            try
            {
                return new Uri("http://a/b%E2%80%8Ec").AbsoluteUri.IndexOf("%E2%80%8E", StringComparison.OrdinalIgnoreCase) < 0;
            }
            catch (UriFormatException)
            {
                return false;
            }
        }

        /// <summary>A URI on its way into System.Uri: each mark, escaped or not, as its stand-in.</summary>
        internal static string In(string s)
        {
            if (!Active || s == null || Plain(s, (char)0x200E, (char)0x202E))
            {
                return s;
            }

            StringBuilder sb = null;
            for (int i = 0; i < s.Length; i++)
            {
                int mark = MarkIndex(s[i]);
                int escaped = mark < 0 ? EscapedMark(s, i) : -1;
                if (mark < 0 && escaped < 0)
                {
                    sb?.Append(s[i]);
                    continue;
                }

                sb = sb ?? new StringBuilder(s, 0, i, s.Length + 8);
                if (mark >= 0)
                {
                    sb.Append((char)(FirstStandIn + mark));
                }
                else
                {
                    sb.Append("%EF%A3%A").Append((char)('0' + escaped));   // U+F8E0 + k in UTF-8: EF A3 A0+k
                    i += 8;
                }
            }

            return sb == null ? s : sb.ToString();
        }

        /// <summary>What System.Uri gave back, escaped or decoded: each stand-in as the mark it stands for.</summary>
        internal static string Out(string s)
        {
            if (!Active || s == null || Plain(s, FirstStandIn, (char)(FirstStandIn + 6)))
            {
                return s;
            }

            StringBuilder sb = null;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                int standIn = c >= FirstStandIn && c < FirstStandIn + Marks.Length ? c - FirstStandIn : -1;
                int escaped = standIn < 0 ? EscapedStandIn(s, i) : -1;
                if (standIn < 0 && escaped < 0)
                {
                    sb?.Append(c);
                    continue;
                }

                sb = sb ?? new StringBuilder(s, 0, i, s.Length);
                if (standIn >= 0)
                {
                    sb.Append(Marks[standIn]);
                }
                else
                {
                    sb.Append(EscapedMarks[escaped]);
                    i += 8;
                }
            }

            return sb == null ? s : sb.ToString();
        }

        /// <summary>Whether a text from In carries a stand-in, which System.Uri would escape when it combines two URIs.</summary>
        internal static bool HasStandIn(string s)
        {
            if (!Active || s == null)
            {
                return false;
            }

            for (int i = 0; i < s.Length; i++)
            {
                if ((s[i] >= FirstStandIn && s[i] < FirstStandIn + Marks.Length) || EscapedStandIn(s, i) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        internal static Uri Make(string s, UriKind kind = UriKind.Absolute)
        {
            return new Uri(In(FileUris.IsPath(s) ? FileUris.Text(s) : s), kind);
        }

        internal static bool TryMake(string s, UriKind kind, out Uri uri)
        {
            return Uri.TryCreate(In(FileUris.IsPath(s) ? FileUris.Text(s) : s), kind, out uri);
        }

        /// <summary>The file a file: URI names, its marks in place.</summary>
        internal static string LocalPath(string uri)
        {
            return Out(FileUris.LocalPathOf(Make(uri)));
        }

        private static readonly string[] EscapedMarks = { "%E2%80%8E", "%E2%80%8F", "%E2%80%AA", "%E2%80%AB", "%E2%80%AC", "%E2%80%AD", "%E2%80%AE" };

        // No '%' and nothing in [low, high]: nothing to change, as nearly every URI.
        private static bool Plain(string s, char low, char high)
        {
            foreach (char c in s)
            {
                if (c == '%' || (c >= low && c <= high))
                {
                    return false;
                }
            }

            return true;
        }

        // The index of c in Marks, or -1: U+200E, U+200F, U+202A-U+202E. Array.IndexOf cost the 4.5 mode microseconds a URI.
        private static int MarkIndex(char c)
        {
            if (c < 0x200E || c > 0x202E)
            {
                return -1;
            }

            return c == 0x200E ? 0 : c == 0x200F ? 1 : c >= 0x202A ? c - 0x202A + 2 : -1;
        }

        // %E2%80%8E ... %E2%80%AE at i (either case): the index of that mark, or -1.
        private static int EscapedMark(string s, int i)
        {
            if (s[i] != '%' || i + 9 > s.Length || string.Compare(s, i, "%E2%80%", 0, 7, StringComparison.OrdinalIgnoreCase) != 0)
            {
                return -1;
            }

            for (int k = 0; k < EscapedMarks.Length; k++)
            {
                if (string.Compare(s, i + 7, EscapedMarks[k], 7, 2, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return k;
                }
            }

            return -1;
        }

        // %EF%A3%A0 ... %EF%A3%A6 at i: the index of that stand-in, or -1.
        private static int EscapedStandIn(string s, int i)
        {
            if (s[i] != '%' || i + 9 > s.Length || string.Compare(s, i, "%EF%A3%A", 0, 8, StringComparison.OrdinalIgnoreCase) != 0)
            {
                return -1;
            }

            int k = s[i + 8] - '0';
            return k >= 0 && k < Marks.Length ? k : -1;
        }
    }
}
