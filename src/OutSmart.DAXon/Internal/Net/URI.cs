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
        // The longest string System.Uri takes on .NET Framework and .NET 8.
        internal const int LongestForPlatform = 65519;

        public Uri Inner { get; }
        public string Scheme => Inner.IsAbsoluteUri ? Inner.Scheme : null;
        public string Host => Inner.IsAbsoluteUri ? Inner.Host : null;
        public int Port => Inner.IsAbsoluteUri ? Inner.Port : -1;
        public string Fragment => Inner.IsAbsoluteUri ? (Inner.Fragment.Length > 1 ? BidiUri.Out(Inner.Fragment.Substring(1)) : null) : null;
        public string RawFragment => Inner.IsAbsoluteUri ? (Inner.Fragment.Length > 1 ? BidiUri.Out(Inner.Fragment.Substring(1)) : null) : null;
        // Unlike Java getRawQuery(), System.Uri.Query is NORMALIZED (a lone '%' gets escaped, unreserved escapes
        // are pre-decoded) — not byte-for-byte raw. The collation-URI funnel relies on that normalization (round BS).
        public string RawQuery => Inner.IsAbsoluteUri ? (Inner.Query.Length > 1 ? BidiUri.Out(Inner.Query.Substring(1)) : null) : null;
        public string SchemeSpecificPart => BidiUri.Out(Inner.IsAbsoluteUri ? Inner.PathAndQuery : Inner.OriginalString);
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
            try { Inner = new Uri(BidiUri.In(str), UriKind.RelativeOrAbsolute); }
            catch (UriFormatException e) { throw new URISyntaxException(str, e.Message); }
        }
        public URI(Uri u) { Inner = u; }
        // Java URI(scheme, userInfo, host, port, path, query, fragment) component ctor.
        public URI(string scheme, string userInfo, string host, int port, string path, string query, string fragment)
            : this((string.IsNullOrEmpty(scheme) ? "" : scheme + "://") + (host ?? "") + (port >= 0 ? ":" + port : "") + (path ?? "") + (string.IsNullOrEmpty(query) ? "" : "?" + query) + (string.IsNullOrEmpty(fragment) ? "" : "#" + fragment)) { }
        public string GetPath() => BidiUri.Out(Inner.IsAbsoluteUri ? Inner.AbsolutePath : Inner.OriginalString);
        public string GetQuery() => Inner.IsAbsoluteUri ? (Inner.Query.Length > 1 ? BidiUri.Out(Inner.Query.Substring(1)) : null) : null;
        public bool IsAbsolute() => Inner.IsAbsoluteUri;
        // java.net.URI.resolve never throws; System.Uri's relative-resolution throws UriFormatException on a
        // malformed operand. Wrap it like the ctor above so a bad base/relative URI surfaces as the intended
        // URISyntaxException (→ XQST0046/FODC0005 …) at the 67 catch sites, not as a raw .NET exception.
        public URI Resolve(URI other)
        {
            if (other != null && MergesAsText(other.Inner.OriginalString))
            {
                return new URI(ResolveReference(Inner.OriginalString, other.Inner.OriginalString));
            }

            try { return new URI(new Uri(Inner, other.Inner)); }
            catch (UriFormatException e) { throw new URISyntaxException(other?.ToString(), e.Message); }
        }
        public URI Resolve(string str)
        {
            if (str != null && MergesAsText(BidiUri.In(str)))
            {
                return new URI(ResolveReference(Inner.OriginalString, BidiUri.In(str)));
            }

            try { return new URI(new Uri(Inner, BidiUri.In(str))); }
            catch (UriFormatException e) { throw new URISyntaxException(str, e.Message); }
        }

        // System.Uri's combined form decodes its base's escapes (%20 a space, %C3%A4 'ä', %41 'A'); RFC 3986 5.2 decodes
        // nothing. With an escape on either side, the reference is merged as text.
        private bool MergesAsText(string reference)
        {
            return Inner.IsAbsoluteUri && !HasScheme(reference)
                && (reference.IndexOf('%') >= 0 || Inner.OriginalString.IndexOf('%') >= 0 || BidiUri.HasStandIn(reference) || BidiUri.HasStandIn(Inner.OriginalString));
        }

        // RFC 3986 5.2.2 on the text of an absolute base and of a reference with no scheme. Nothing is decoded, encoded or
        // folded: the result keeps the form of both (Java: resolve-uri('g', 'file:/x/y') = file:/x/g).
        internal static string ResolveReference(string @base, string reference)
        {
            int colon = @base.IndexOf(':');
            string scheme = @base.Substring(0, colon + 1);
            string path = @base.Substring(colon + 1);
            int end = path.IndexOf('#');
            if (end >= 0)
            {
                path = path.Substring(0, end);
            }

            string baseQuery = null;
            end = path.IndexOf('?');
            if (end >= 0)
            {
                baseQuery = path.Substring(end);
                path = path.Substring(0, end);
            }

            // For the schemes it knows System.Uri reads '\' as '/' (a module fetched by href="sub\m.xsl" keeps it in its base).
            if (ReadsBackslashAsSlash(scheme))
            {
                path = path.Replace('\\', '/');
                reference = BackslashesInPath(reference);
            }

            string authority = "";
            if (path.StartsWith("//", StringComparison.Ordinal))
            {
                end = path.IndexOf('/', 2);
                authority = end < 0 ? path : path.Substring(0, end);
                path = end < 0 ? "" : path.Substring(end);
            }

            // A Windows drive in a file:/// base is its root, as for System.Uri: neither '/x' nor '..' goes past it.
            string drive = authority == "//" && scheme.Equals("file:", StringComparison.OrdinalIgnoreCase) && StartsWithDrive(path) ? path.Substring(0, 3) : "";
            path = path.Substring(drive.Length);

            string fragment = "";
            end = reference.IndexOf('#');
            if (end >= 0)
            {
                fragment = reference.Substring(end);
                reference = reference.Substring(0, end);
            }

            string query = null;
            end = reference.IndexOf('?');
            if (end >= 0)
            {
                query = reference.Substring(end);
                reference = reference.Substring(0, end);
            }

            if (reference.StartsWith("//", StringComparison.Ordinal))
            {
                end = reference.IndexOf('/', 2);
                authority = end < 0 ? reference : reference.Substring(0, end);
                drive = "";   // a reference with its own authority is the whole of what follows the scheme
                path = RemoveDotSegments(end < 0 ? "" : reference.Substring(end));
            }
            else if (reference.Length == 0)
            {
                query = query ?? baseQuery;
                path = path.Length == 0 && authority.Length != 0 ? "/" : path;   // http://a + #f is http://a/#f, as for System.Uri and Java
            }
            else if (reference[0] == '/')
            {
                drive = StartsWithDrive(reference) ? "" : drive;   // /D:/x names its own
                path = RemoveDotSegments(reference);
            }
            else
            {
                string directory = path.Length == 0 && authority.Length != 0 ? "/" : path.Substring(0, path.LastIndexOf('/') + 1);
                path = RemoveDotSegments(directory + reference);
            }

            return scheme + authority + drive + path + query + fragment;
        }

        private static bool ReadsBackslashAsSlash(string scheme)
        {
            return scheme.Equals("file:", StringComparison.OrdinalIgnoreCase) || scheme.Equals("http:", StringComparison.OrdinalIgnoreCase)
                || scheme.Equals("https:", StringComparison.OrdinalIgnoreCase) || scheme.Equals("ftp:", StringComparison.OrdinalIgnoreCase);
        }

        // Only the path of a reference: a query or a fragment keeps its backslashes.
        private static string BackslashesInPath(string reference)
        {
            int end = reference.IndexOfAny(new[] { '?', '#' });
            return end < 0
                ? reference.Replace('\\', '/')
                : reference.Substring(0, end).Replace('\\', '/') + reference.Substring(end);
        }

        // "/C:" followed by '/' or the end.
        private static bool StartsWithDrive(string path)
        {
            return path.Length >= 3 && path[0] == '/' && path[2] == ':' && (path.Length == 3 || path[3] == '/')
                && ((path[1] >= 'A' && path[1] <= 'Z') || (path[1] >= 'a' && path[1] <= 'z'));
        }

        // RFC 3986 scheme = ALPHA *( ALPHA / DIGIT / "+" / "-" / "." ) followed by ':'. A reference with
        // a scheme is an absolute URI reference. Used to recognise refs (e.g. "g:h") that .NET's
        // System.Uri rejects as DOS drive paths but RFC 3986 treats as absolute.
        internal static bool HasScheme(string s)
        {
            if (string.IsNullOrEmpty(s) || !((s[0] >= 'A' && s[0] <= 'Z') || (s[0] >= 'a' && s[0] <= 'z')))
            {
                return false;
            }

            for (int i = 1; i < s.Length; i++)
            {
                char c = s[i];
                if (c == ':')
                {
                    return true;
                }

                bool ok = (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')
                    || c == '+' || c == '-' || c == '.';
                if (!ok)
                {
                    return false;
                }
            }

            return false;
        }

        public string ToASCIIString() => BidiUri.Out(Inner.AbsoluteUri);
        public override string ToString() => BidiUri.Out(Inner.OriginalString);
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

        // RFC 3986 5.2.4 for an absolute path ("/a/b/../c" -> "/a/c"), by segments: linear, however many there are.
        private static string RemoveDotSegments(string path)
        {
            string[] segments = path.Split('/');
            var kept = new System.Collections.Generic.List<string>(segments.Length);
            for (int k = 0; k < segments.Length; k++)
            {
                string segment = segments[k];
                bool last = k == segments.Length - 1;
                if (segment == "." || segment == "..")
                {
                    if (segment == ".." && kept.Count > 1)
                    {
                        kept.RemoveAt(kept.Count - 1);
                    }

                    if (last)
                    {
                        kept.Add("");   // "/a/." and "/a/b/.." end in "/"
                    }

                    continue;
                }

                kept.Add(segment);
            }

            return string.Join("/", kept);
        }

        // Implicit conversion to string (paulirwin sometimes passes URI where string expected).
        public static implicit operator string(URI uri) => uri?.ToString();
        // Implicit conversion FROM string — Java has no explicit conversion;
        // Saxon code often passes a string literal where URI is expected.
        public static implicit operator URI(string s) => s == null ? null : new URI(s);
    }
}
