////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.IO;
using System.Text;

namespace OutSmart.DAXon.Internal.Net
{
    /// <summary>
    /// A path of this machine as a URI. System.Uri reads what looks like an escape in a path as one: new Uri(@"C:\pc%41\x")
    /// names C:\pcA\x, another file. Every path that becomes a URI comes here: the URI of the file with this very name,
    /// whose % # ? are escaped, as Java's File.toURI().
    /// </summary>
    internal static class FileUris
    {
        private static readonly bool Windows = Path.DirectorySeparatorChar == '\\';

        /// <summary>A rooted path, no URI reference: C:\x, C:/x, \\server\share\x; elsewhere than on Windows /x.</summary>
        internal static bool IsPath(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return false;
            }

            if (!Windows)
            {
                return s[0] == '/';
            }

            if (s.Length >= 3 && s[1] == ':' && (s[2] == '\\' || s[2] == '/'))
            {
                return (s[0] >= 'A' && s[0] <= 'Z') || (s[0] >= 'a' && s[0] <= 'z');
            }

            // Not \\?\ and \\.\, which no file: URI writes.
            return s.Length > 2 && s[0] == '\\' && s[1] == '\\' && s[2] != '?' && s[2] != '.';
        }

        /// <summary>
        /// The path a file: URI names. RFC 8089 reads the host localhost as this machine: file://localhost/C:/x is C:\x, where
        /// System.Uri gives the share C: of a server localhost, which no share can be called.
        /// </summary>
        internal static string LocalPathOf(Uri uri)
        {
            string path = uri.LocalPath;
            const string Localhost = @"\\localhost\";
            if (Windows && path.Length > Localhost.Length + 1 && path.StartsWith(Localhost, StringComparison.OrdinalIgnoreCase) && path[Localhost.Length + 1] == ':')
            {
                char drive = path[Localhost.Length];
                if ((drive >= 'A' && drive <= 'Z') || (drive >= 'a' && drive <= 'z'))
                {
                    return path.Substring(Localhost.Length);
                }
            }

            return path;
        }

        private static readonly char[] Escaped = { '%', '#', '?' };

        /// <summary>The text of the file: URI of a rooted path, for System.Uri to take.</summary>
        internal static string Text(string path)
        {
            string scheme = !Windows ? "file://" : path[0] == '\\' ? "file:" : "file:///";
            if (path.IndexOfAny(Escaped) < 0 && (Windows || path.IndexOf('\\') < 0))
            {
                // Nearly every path: nothing to escape, one string (a listing makes one for each of its files).
                return Windows ? scheme + path.Replace('\\', '/') : scheme + path;
            }

            var uri = new StringBuilder(path.Length + 16);
            uri.Append(scheme);
            foreach (char c in path)
            {
                switch (c)
                {
                    case '%':
                        uri.Append("%25");
                        break;
                    case '#':
                        uri.Append("%23");
                        break;
                    case '?':
                        uri.Append("%3F");
                        break;
                    case '\\':
                        uri.Append(Windows ? "/" : "%5C");
                        break;
                    default:
                        uri.Append(c);
                        break;
                }
            }

            return uri.ToString();
        }

        /// <summary>The absolute URI, as text, of a rooted path.</summary>
        internal static string Of(string path)
        {
            return IsPath(path)
                ? BidiUri.Out(new Uri(BidiUri.In(Text(path))).AbsoluteUri)
                : BidiUri.Out(new Uri(BidiUri.In(path)).AbsoluteUri);
        }

        /// <summary>
        /// What XmlResolver.ResolveUri makes of a path, its bytes kept: a rooted one against any base, a relative one with
        /// no base, against the current directory. Null for what is not a path: the resolver's own rules hold.
        /// </summary>
        internal static Uri Resolving(Uri baseUri, string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            if (IsPath(name))
            {
                return new Uri(BidiUri.In(Text(name)));
            }

            bool noBase = baseUri == null || (!baseUri.IsAbsoluteUri && baseUri.OriginalString.Length == 0);
            if (!noBase || (Uri.TryCreate(BidiUri.In(name), UriKind.RelativeOrAbsolute, out Uri reference) && reference.IsAbsoluteUri))
            {
                return null;
            }

            string full = Path.GetFullPath(name);
            return IsPath(full) ? new Uri(BidiUri.In(Text(full))) : null;
        }
    }
}
