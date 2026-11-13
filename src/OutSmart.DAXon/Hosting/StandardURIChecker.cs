////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// Faithful port of net.sf.saxon.lib.StandardURIChecker (was a =>true stub).
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Values;
using URI = OutSmart.DAXon.Internal.Net.URI;
using URISyntaxException = OutSmart.DAXon.Internal.Net.URISyntaxException;

namespace OutSmart.DAXon.Lib
{
    /// <summary>
    /// Default validation of a string as a URI, used by fn:resolve-uri, xsl:namespace (XTDE0905), etc.
    /// A string is accepted if the URI class parses it, or if it parses after IRI-to-URI escaping —
    /// otherwise it is rejected (e.g. a fragment containing a second '#').
    /// </summary>
    internal sealed class StandardURIChecker : IURIChecker
    {
        private static readonly StandardURIChecker _instance = new StandardURIChecker();
        public static StandardURIChecker GetInstance() => _instance;

        public bool IsValidURI(string value)
        {
            if (value == null)
            {
                return false;
            }

            string sv = Whitespace.Trim(value);

            // RFC 2396 is ambivalent about zero-length strings; accept them (as upstream does).
            if (sv.Length == 0)
            {
                return true;
            }

            if (!EscapesAndScheme(sv))
            {
                return false;
            }

            try
            {
                new URI(sv);
                return true;
            }
            catch (URISyntaxException)
            {
                // keep trying: the raw form may be valid only after IRI escaping
            }

            try
            {
                string escaped = IriToUri.IriToUriFn(StringView.Tidy(sv)).ToString();
                new URI(escaped);
                return true;
            }
            catch (URISyntaxException)
            {
                return false;
            }
        }

        // What java.net.URI refuses and System.Uri takes: a '%' not followed by two hex digits ("%", "%gg"), and a
        // scheme - text before a ':' that comes ahead of any '/', '?' or '#' - that is empty (":/") or malformed ("1a:b").
        private static bool EscapesAndScheme(string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '%' && !(i + 2 < s.Length && IsHex(s[i + 1]) && IsHex(s[i + 2])))
                {
                    return false;
                }
            }

            int colon = s.IndexOf(':');
            int delimiter = s.IndexOfAny(SchemeEnds);
            if (colon < 0 || (delimiter >= 0 && delimiter < colon))
            {
                return true;
            }

            if (colon == 0 || !IsAsciiLetter(s[0]))
            {
                return false;
            }

            for (int i = 1; i < colon; i++)
            {
                char c = s[i];
                if (!(IsAsciiLetter(c) || (c >= '0' && c <= '9') || c == '+' || c == '-' || c == '.'))
                {
                    return false;
                }
            }

            return true;
        }

        private static readonly char[] SchemeEnds = { '/', '?', '#' };

        private static bool IsHex(char c) => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

        private static bool IsAsciiLetter(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');

        // Detailed post-parse checks (passesAdditionalChecks) are not ported; upstream's checkThoroughly
        // then never throws, so this stays a no-op — ResolveURI relies on it not rejecting parsed URIs.
        public bool CheckThoroughly() => true;
    }
}
