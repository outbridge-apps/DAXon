////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;

namespace OutSmart.DAXon.Internal.Net
{
    /// <summary>Java URI shim over System.Uri.</summary>
    public sealed class URI
    {
        public Uri Inner { get; }
        public string Scheme => Inner.IsAbsoluteUri ? Inner.Scheme : null;
        public string Host => Inner.IsAbsoluteUri ? Inner.Host : null;
        public int Port => Inner.IsAbsoluteUri ? Inner.Port : -1;
        public string Fragment => Inner.IsAbsoluteUri ? (Inner.Fragment.Length > 1 ? Inner.Fragment.Substring(1) : null) : null;
        public string RawFragment => Inner.IsAbsoluteUri ? (Inner.Fragment.Length > 1 ? Inner.Fragment.Substring(1) : null) : null;
        // Unlike Java getRawQuery(), System.Uri.Query is NORMALIZED (a lone '%' gets escaped, unreserved escapes
        // are pre-decoded) — not byte-for-byte raw. The collation-URI funnel relies on that normalization (round BS).
        public string RawQuery => Inner.IsAbsoluteUri ? (Inner.Query.Length > 1 ? Inner.Query.Substring(1) : null) : null;
        public string SchemeSpecificPart => Inner.IsAbsoluteUri ? Inner.PathAndQuery : Inner.OriginalString;
        public string UserInfo => Inner.IsAbsoluteUri ? Inner.UserInfo : null;
        // Java's `new URI(str)` signals a malformed URI with URISyntaxException; System.Uri throws
        // UriFormatException. The whole port is written to the Java contract (67 `catch (URISyntaxException)`
        // sites, 0 catch UriFormatException), so translate here — otherwise a bad URI (e.g. an invalid
        // collation URI) escapes every handler as a raw .NET exception instead of the intended FOCH0002/FORG0002.
        public URI(string str)
        {
            // java.net.URI rejects a second '#' (the fragment may not contain one); System.Uri tolerates it.
            if (str != null && str.IndexOf('#') != str.LastIndexOf('#'))
            {
                throw new URISyntaxException(str, "URI contains more than one '#'");
            }

            // Half a surrogate pair is no character of a URI. System.Uri takes the string and throws
            // UriFormatException later, from whichever property is read first - past every catch written for this.
            if (str != null && !ReferenceEquals(OutSmart.DAXon.Text.StringTool.WithoutHalfPairs(str), str))
            {
                throw new URISyntaxException(str, "URI contains half a surrogate pair");
            }
            try { Inner = new Uri(str, UriKind.RelativeOrAbsolute); }
            catch (UriFormatException e) { throw new URISyntaxException(str, e.Message); }
        }
        public URI(Uri u) { Inner = u; }
        // Java URI(scheme, userInfo, host, port, path, query, fragment) component ctor.
        public URI(string scheme, string userInfo, string host, int port, string path, string query, string fragment)
            : this((string.IsNullOrEmpty(scheme) ? "" : scheme + "://") + (host ?? "") + (port >= 0 ? ":" + port : "") + (path ?? "") + (string.IsNullOrEmpty(query) ? "" : "?" + query) + (string.IsNullOrEmpty(fragment) ? "" : "#" + fragment)) { }
        public string GetPath() => Inner.IsAbsoluteUri ? Inner.AbsolutePath : Inner.OriginalString;
        public string GetQuery() => Inner.IsAbsoluteUri ? (Inner.Query.Length > 1 ? Inner.Query.Substring(1) : null) : null;
        public bool IsAbsolute() => Inner.IsAbsoluteUri;
        // java.net.URI.resolve never throws; System.Uri's relative-resolution throws UriFormatException on a
        // malformed operand. Wrap it like the ctor above so a bad base/relative URI surfaces as the intended
        // URISyntaxException (→ XQST0046/FODC0005 …) at the 67 catch sites, not as a raw .NET exception.
        public URI Resolve(URI other)
        {
            try { return new URI(new Uri(Inner, other.Inner)); }
            catch (UriFormatException e) { throw new URISyntaxException(other?.ToString(), e.Message); }
        }
        public URI Resolve(string str)
        {
            try { return new URI(new Uri(Inner, str)); }
            catch (UriFormatException e) { throw new URISyntaxException(str, e.Message); }
        }
        public string ToASCIIString() => Inner.AbsoluteUri;
        public override string ToString() => Inner.OriginalString;
        public static URI Create(string str) => new URI(str);
        public bool IsOpaque() => Inner.IsAbsoluteUri && string.IsNullOrEmpty(Inner.AbsolutePath);
        // Java URI.normalize(): RFC 3986 §5.2.4 remove_dot_segments applied to the path only.
        // ToString() reports OriginalString (Java keeps the un-normalized form too), so /././ and /../
        // must be collapsed HERE — textually, to leave percent-encoding and IRI characters untouched.
        public URI Normalize()
        {
            if (!Inner.IsAbsoluteUri)
                return this;
            string s = Inner.OriginalString;
            int schemeSep = s.IndexOf("://", StringComparison.Ordinal);
            if (schemeSep < 0) // opaque URI — nothing to normalize
            {
                return this;
            }
            int pathStart = s.IndexOf('/', schemeSep + 3);
            if (pathStart < 0)
                return this;
            int pathEnd = s.IndexOfAny(new[] { '?', '#' }, pathStart);
            if (pathEnd < 0)
                pathEnd = s.Length;
            string path = s.Substring(pathStart, pathEnd - pathStart);
            string norm = RemoveDotSegments(path);
            return norm == path ? this : new URI(s.Substring(0, pathStart) + norm + s.Substring(pathEnd));
        }

        private static string RemoveDotSegments(string inp)
        {
            var sb = new System.Text.StringBuilder(inp.Length);
            while (inp.Length > 0)
            {
                if (inp.StartsWith("../", StringComparison.Ordinal))
                {
                    inp = inp.Substring(3);
                }
                else if (inp.StartsWith("./", StringComparison.Ordinal))
                {
                    inp = inp.Substring(2);
                }
                else if (inp.StartsWith("/./", StringComparison.Ordinal))
                {
                    inp = "/" + inp.Substring(3);
                }
                else if (inp == "/.")
                {
                    inp = "/";
                }
                else if (inp.StartsWith("/../", StringComparison.Ordinal)) { inp = "/" + inp.Substring(4); TrimLastSegment(sb); }
                else if (inp == "/..") { inp = "/"; TrimLastSegment(sb); }
                else if (inp == "." || inp == "..")
                {
                    inp = "";
                }
                else
                {
                    int idx = inp[0] == '/' ? inp.IndexOf('/', 1) : inp.IndexOf('/');
                    if (idx < 0) { sb.Append(inp); inp = ""; }
                    else
                    {
                        sb.Append(inp, 0, idx);
                        inp = inp.Substring(idx);
                    }
                }
            }

            return sb.ToString();
        }

        private static void TrimLastSegment(System.Text.StringBuilder sb)
        {
            for (int i = sb.Length - 1; i >= 0; i--)
            {
                if (sb[i] == '/')
                {
                    sb.Length = i;
                    return;
                }
            }

            sb.Length = 0;
        }
        // Implicit conversion to string (paulirwin sometimes passes URI where string expected).
        public static implicit operator string(URI uri) => uri?.ToString();
        // Implicit conversion FROM string — Java has no explicit conversion;
        // Saxon code often passes a string literal where URI is expected.
        public static implicit operator URI(string s) => s == null ? null : new URI(s);
    }
}
