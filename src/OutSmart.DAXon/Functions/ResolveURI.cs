////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Net;
using System;
using System.IO;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// This class supports the resolve-uri() function in XPath 2.0
    /// </summary>
    internal sealed class ResolveURI : SystemFunction
    {
        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            AtomicValue arg0 = (AtomicValue)arguments[0].Head();
            if (arg0 == null)
            {
                return EmptySequence.GetInstance();
            }

            string relative = arg0.GetStringValue();
            string @base;
            IItem baseArg = null;
            if (GetArity() == 2)
            {
                baseArg = arguments[1].Head();
            }

            if (baseArg != null)
            {
                @base = baseArg.GetStringValue();
            }
            else
            {
                @base = StaticBaseUriString;
                if (@base == null)
                {
                    throw new XPathException("Base URI in static context of resolve-uri() is unknown", "FONS0005", context);
                }
            }

            return Resolve(@base, relative, context);
        }

        private AnyURIValue Resolve(string @base, string relative, IXPathContext context)
        {

            //        try {
            // Rule 4: "The function resolves the relative IRI reference $relative against the base IRI $base using
            // the algorithm defined in [RFC 3986], adapted by treating any ·character· that would not be valid in
            // an RFC3986 URI or relative reference in the same way that RFC3986 treats unreserved characters.
            // No percent-encoding takes place.
            // The URI classes will not handle spaces, so they travel as %20 and come back at the end. A %20 that was there
            // travels as %2520 and stays what it was (see ProtectSpaces), which Java does not do.
            bool escaped = relative.IndexOf(' ') >= 0 || @base.IndexOf(' ') >= 0;
            if (escaped)
            {
                relative = ProtectSpaces(relative);
                @base = ProtectSpaces(@base);
            }

            // The port's URI is lenient where Java's java.net.URI is strict, so reject two malformations Java
            // catches (both FORG0002): (1) a '%' not followed by two hex digits (e.g. "%gg" — the port would
            // otherwise re-escape it to "%25gg"); this runs after EscapeSpaces, which only adds valid "%20".
            if (HasMalformedEscape(relative))
            {
                throw new XPathException("Relative URI " + Err.Wrap(relative) + " contains a malformed percent-encoded octet", "FORG0002", context);
            }

            if (HasMalformedEscape(@base))
            {
                throw new XPathException("Base URI " + Err.Wrap(@base) + " contains a malformed percent-encoded octet", "FORG0002", context);
            }

            // (2) '\' is no IRI character; System.Uri reads it as '/', so '..\x' climbed a directory.
            if (relative.IndexOf('\\') >= 0)
            {
                throw new XPathException("Relative URI " + Err.Wrap(relative) + " contains a backslash, which is not allowed in a URI", "FORG0002", context);
            }

            // System.Uri refuses anything longer on .NET Framework and .NET 8. RFC 3986 has no limit and its merge needs no parser.
            if (relative.Length > URI.LongestForPlatform || @base.Length > URI.LongestForPlatform)
            {
                return ResolveLong(@base, relative, escaped, context);
            }

            URI relativeURI;
            try
            {
                relativeURI = AbsoluteOrRelativeURI(relative);
            }
            catch (URISyntaxException e)
            {
                // .NET's System.Uri mis-parses single-letter-scheme URIs (e.g. "g:h") as DOS drive
                // paths and throws; RFC 3986 treats a scheme-prefixed reference as ABSOLUTE, and
                // resolve-uri returns it unchanged (base-uri-006). Detect the scheme syntactically.
                if (URI.HasScheme(relative))
                {
                    return new AnyURIValue(escaped ? RestoreSpaces(relative) : relative);
                }

                throw new XPathException("Relative URI " + Err.Wrap(relative) + " is invalid: " + e.Message, "FORG0002", context);
            }

            // http:g, ftp:x: a scheme makes the reference absolute (RFC 3986 4.3), though System.Uri wants "//" after
            // the schemes it knows and calls them relative.
            if (relativeURI.IsAbsolute() || URI.HasScheme(relative))
            {
                return new AnyURIValue(escaped ? RestoreSpaces(relative) : relative);
            }

            // (2) RFC 3986: a relative-reference's first path segment must not contain ':' (it would be
            // mis-parsed as a scheme). ":" is neither absolute (empty scheme) nor a valid relative reference.
            if (HasColonInFirstSegment(relative))
            {
                throw new XPathException("Relative URI " + Err.Wrap(relative) + " has a ':' in its first path segment", "FORG0002", context);
            }

            URI absoluteURI;
            try
            {
                absoluteURI = new URI(@base);
            }
            catch (URISyntaxException e)
            {
                throw new XPathException("Base URI " + Err.Wrap(@base) + " is invalid: " + e.Message, "FORG0002", context);
            }

            if (!absoluteURI.IsAbsolute())
            {
                if (URI.HasScheme(@base) && PathAbsoluteBase.IsMatch(@base) && @base.IndexOf('#') < 0)
                {
                    string merged = URI.ResolveReference(@base, relative);
                    return new AnyURIValue(escaped ? RestoreSpaces(merged) : merged);
                }

                throw new XPathException("Base URI " + Err.Wrap(@base) + " is not an absolute URI", "FORG0002", context);
            }

            // Non-hierarchic: no '/' after the scheme (urn:a:b, mailto:). System.Uri gives urn:a:b a path, and resolving
            // against it made urn:x/y and urn:../y; F&O raises FORG0002, as Java does.
            bool nonHierarchic = URI.HasScheme(@base) && !@base.Substring(@base.IndexOf(':') + 1).StartsWith("/", StringComparison.Ordinal);
            if ((absoluteURI.IsOpaque() || nonHierarchic) && !@base.StartsWith("jar:", StringComparison.Ordinal))
            {

                // Special-case JAR file URLs, even though non-conformant
                throw new XPathException("Base URI " + Err.Wrap(@base) + (FileUris.IsPath(@base) ? " is a path, not a URI: write it as a file: URI" : " is a non-hierarchic URI"), "FORG0002", context);
            }

            string fragment = absoluteURI.RawFragment;
            if (fragment != null && !(fragment.Length == 0))
            {
                throw new XPathException("Base URI " + Err.Wrap(@base) + " contains a fragment identifier", "FORG0002", context);
            }

            if (!@base.StartsWith("jar:", StringComparison.Ordinal) && absoluteURI.GetPath() != null && (absoluteURI.GetPath().Length == 0))
            {

                // This deals with cases like @base=http://www.example.com - changing it to http://www.example.com/
                try
                {
                    absoluteURI = new URI(absoluteURI.Scheme, absoluteURI.UserInfo, absoluteURI.Host, absoluteURI.Port, "/", absoluteURI.GetQuery(), absoluteURI.Fragment);
                }
                catch (URISyntaxException)
                {
                    throw new XPathException("Failed to parse JAR scheme URI " + Err.Wrap(absoluteURI.ToASCIIString()), "FORG0002", context);
                }

                @base = absoluteURI.ToString();
            }

            // The spec says that special characters are not escaped, and an escape stays as it was given (URI.Resolve merges
            // those as text). jar: and file://// bases are checked by System.Uri (MakeAbsolute) but merged on the text too:
            // what System.Uri gives back has %2F, %41 and %2E decoded and file://// as two slashes, Java keeps both.
            // A path that begins with a drive, "/C:/x": System.Uri keeps the base's drive before it, C:/C:/x, and is not asked.
            bool drive = @base.StartsWith("file:", StringComparison.Ordinal) && StartsWithDrive(relative);
            bool onText = drive || @base.StartsWith("jar:", StringComparison.Ordinal) || @base.StartsWith("file:////", StringComparison.Ordinal);
            URI resolved = null;
            if (!drive)
            {
                try
                {
                    resolved = MakeAbsolute(relative, @base);
                }
                catch (URISyntaxException e)
                {
                    throw new XPathException(e.Message, "FORG0002");
                }

                if (!resolved.ToString().StartsWith("file:////", StringComparison.Ordinal))
                {
                    resolved = resolved.Normalize();
                }
            }

            string resolvedString = onText ? URI.ResolveReference(@base, relative) : resolved.ToString();
            string result = escaped ? RestoreSpaces(resolvedString) : resolvedString;

            // Test case XSLT3 resolve-uri-022. Java even after normalization can leave a URI with trailing "../" or ".." parts.
            // Pragmatically, we just strip these off. This might not be enough if there are query or fragment parts, but it
            // gets us through the test
            while (result.EndsWith("..", StringComparison.Ordinal))
            {
                result = result.Substring(0, result.Length - 2);
            }

            while (result.EndsWith("../", StringComparison.Ordinal))
            {
                result = result.Substring(0, result.Length - 3);
            }

            return new AnyURIValue(result);
        }

        // "/C:/x" or "/C:": an absolute path whose first segment is a drive.
        private static bool StartsWithDrive(string reference)
        {
            return reference.Length >= 3 && reference[0] == '/' && IsAsciiLetter(reference[1]) && reference[2] == ':'
                && (reference.Length == 3 || reference[3] == '/');
        }

        private static bool IsAsciiLetter(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');

        public static URI AbsoluteOrRelativeURI(string href)
        {
            return new URI(href);
        }

        private static bool IsHexDigit(char c) => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

        // scheme ":" path-absolute, no authority (file:/x/y, http:/x/y): absolute for RFC 3986, refused by System.Uri for
        // the schemes it knows.
        private static readonly System.Text.RegularExpressions.Regex PathAbsoluteBase =
            new System.Text.RegularExpressions.Regex("^[A-Za-z][A-Za-z0-9+.-]*:/(?!/)", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        // Shared syntax screen for URIs the lenient port URI class accepts but java.net.URI rejects
        // (same two checks as Resolve() above): a '%' not followed by two hex digits, and — for a
        // relative reference — a ':' inside the first path segment (would be mis-parsed as a scheme).
        // Used by import schema/module location validation (XQST0046) and doc() href screening (FODC0005).
        public static bool IsValidUriSyntax(string uri)
        {
            for (int i = 0; i < uri.Length; i++)
            {
                if (uri[i] == '%' && (i + 2 >= uri.Length || !IsHexDigit(uri[i + 1]) || !IsHexDigit(uri[i + 2])))
                {
                    return false;
                }
            }

            if (uri.Length > URI.LongestForPlatform)
            {
                return URI.HasScheme(uri) || !HasColonInFirstSegment(uri);
            }

            URI parsed;
            try
            {
                parsed = new URI(uri);
            }
            catch (URISyntaxException)
            {
                return false;
            }

            if (parsed.IsAbsolute())
            {
                return true;
            }

            return !HasColonInFirstSegment(uri);
        }

        // RFC 3986: the first path segment of a relative reference holds no ':', which would read as a scheme.
        private static bool HasColonInFirstSegment(string uri)
        {
            int colon = uri.IndexOf(':');
            if (colon < 0)
            {
                return false;
            }

            int segEnd = uri.IndexOfAny(new[] { '/', '?', '#' });
            return segEnd < 0 || colon < segEnd;
        }

        // The checks of Resolve that System.Uri does not make, and the merge of RFC 3986 on the text of both.
        private static AnyURIValue ResolveLong(string @base, string relative, bool escaped, IXPathContext context)
        {
            if (URI.HasScheme(relative))
            {
                return new AnyURIValue(escaped ? RestoreSpaces(relative) : relative);
            }

            if (HasColonInFirstSegment(relative))
            {
                throw new XPathException("Relative URI " + Err.Wrap(relative) + " has a ':' in its first path segment", "FORG0002", context);
            }

            if (!URI.HasScheme(@base))
            {
                throw new XPathException("Base URI " + Err.Wrap(@base) + " is not an absolute URI", "FORG0002", context);
            }

            if (!@base.Substring(@base.IndexOf(':') + 1).StartsWith("/", StringComparison.Ordinal))
            {
                throw new XPathException("Base URI " + Err.Wrap(@base) + " is a non-hierarchic URI", "FORG0002", context);
            }

            if (@base.IndexOf('#') >= 0)
            {
                throw new XPathException("Base URI " + Err.Wrap(@base) + " contains a fragment identifier", "FORG0002", context);
            }

            string merged = URI.ResolveReference(@base, relative);
            return new AnyURIValue(escaped ? RestoreSpaces(merged) : merged);
        }

        public static string TryToExpand(string systemId)
        {
            if (systemId == null || (systemId.Length == 0))
            {
                return ResolveAgainstCurrentDirectory("");
            }

            try
            {
                new Uri(systemId);
                return systemId; // all is well
            }
            catch (UriFormatException)
            {
                return ResolveAgainstCurrentDirectory(systemId);
            }
        }

        // A '%' not followed by two hex digits.
        private static bool HasMalformedEscape(string uri)
        {
            for (int i = 0; i < uri.Length; i++)
            {
                if (uri[i] == '%' && (i + 2 >= uri.Length || !IsHexDigit(uri[i + 1]) || !IsHexDigit(uri[i + 2])))
                {
                    return true;
                }
            }

            return false;
        }

        private static string ResolveAgainstCurrentDirectory(string systemId)
        {
            string dir;
            try
            {
                // Java reads System.getProperty("user.dir") = the process working directory. The .NET port
                // had Environment.GetEnvironmentVariable("user.dir"), but there is no such env var on .NET, so
                // it returned null (not an exception -> the catch never fired) and line `dir.EndsWith` NRE'd on
                // any relative/scheme-only base-uri (e.g. declare base-uri "http:/...").
                dir = Environment.CurrentDirectory;
            }
            catch (Exception)
            {

                // this doesn't work when running an applet
                return systemId;
            }
            if (dir == null)
            {
                return systemId;
            }

            if (!(dir.EndsWith("/", StringComparison.Ordinal) || systemId.StartsWith("/", StringComparison.Ordinal)))
            {
                dir = dir + '/';
            }

            try
            {
                URI currentDirectoryURI = FileUris.Of(Path.GetFullPath(dir));
                URI baseURI = currentDirectoryURI.Resolve(systemId);
                return baseURI.ToString();
            }
            catch (Exception)
            {
                return systemId;
            }
        }

        public static URI MakeAbsolute(string relativeURI, string @base)
        {
            URI absoluteURI;
            StandardURIChecker checker = StandardURIChecker.GetInstance();

            if (relativeURI == null)
            {
                if (@base == null)
                {
                    throw Failure("", "Relative and Base URI must not both be null");
                }

                absoluteURI = new URI(ResolveURI.EscapeSpaces(@base));
                checker.CheckThoroughly();
                if (!absoluteURI.IsAbsolute())
                {
                    throw Failure(@base, "Relative URI not supplied, so base URI must be absolute");
                }
                else
                {
                    return absoluteURI;
                }
            }

            if (relativeURI.StartsWith("classpath:", StringComparison.Ordinal))
            {

                // Resolving a classpath: URI involves searching the classpath.
                // There's no sense in which it makes sense to attempt to make one absolute
                // against some base URI. They're effectively absolute already.
                // (If we don't do this, passing them to System.Uri causes an exception
                // anyway.)
                return new URI(relativeURI);
            }

            if (relativeURI.StartsWith("data:", StringComparison.Ordinal))
            {

                // This is also an absolute URI...
                return new URI(relativeURI);
            }

            try
            {
                if (@base == null || (@base.Length == 0))
                {
                    absoluteURI = new URI(relativeURI);
                    if (!absoluteURI.IsAbsolute())
                    {
                        string expandedBase = ResolveURI.TryToExpand(@base);
                        if (!expandedBase.Equals(@base))
                        {

                            // prevent infinite recursion
                            return MakeAbsolute(relativeURI, expandedBase);
                        }
                    }
                }
                else if (@base.StartsWith("jar:", StringComparison.Ordinal) || @base.StartsWith("file:////", StringComparison.Ordinal))
                {

                    // jar: URIs can't be resolved by the OutSmart.DAXon.Internal.Net.URI class, because they don't actually
                    // conform with the RFC standards for hierarchic URI schemes (quite apart from not being
                    // a registered URI scheme). But they seem to be widely used.
                    // URIs starting file://// are accepted by the OutSmart.DAXon.Internal.Net.URI class, they are used to
                    // represent Windows UNC filenames. However, the OutSmart.DAXon.Internal.Net.URI algorithm for resolving
                    // a relative URI against such a base URI fails to produce a usable UNC filename (it's not
                    // clear whether Java is implementing RFC 3986 correctly here, it depends on interpretation).
                    // So we resolve via System.Uri(base, relative) for this case too, because it works.
                    try
                    {
                        Uri baseURL = BidiUri.Make(@base);
                        Uri absoluteURL = new Uri(baseURL, BidiUri.In(relativeURI));
                        absoluteURI = new URI(absoluteURL);
                    }
                    catch (UriFormatException err)
                    {
                        throw Failure(@base + " " + relativeURI, err.Message);
                    }
                }
                else if (@base.StartsWith("classpath:", StringComparison.Ordinal))
                {
                    absoluteURI = new URI(relativeURI);
                    if (!absoluteURI.IsAbsolute())
                    {

                        // URIs in the classpath: scheme are a bit of a mess. Given "classpath:/path/to/thing",
                        // if you attempt to use ClassLoader.getSystemResourceAsStream("/path/to/thing"), it
                        // will fail because the leading slash is a problem. Conversely, if you have
                        // "classpath:path/to/thing" and you try to resolve "otherthing" against it,
                        // you'll get "classpath:otherthing" which is almost certainly wrong. The only
                        // way around it seems to be to fake the scheme long enough to get correct
                        // resolution.
                        string path = @base.Substring(10);
                        URI fakeURI;
                        if (path.StartsWith("/", StringComparison.Ordinal))
                        {
                            fakeURI = URI.Create("file://" + path).Resolve(relativeURI);
                        }
                        else
                        {
                            fakeURI = URI.Create("file:///" + path).Resolve(relativeURI);
                        }

                        string cpath = fakeURI.GetPath().Substring(1);
                        if (cpath.StartsWith("../", StringComparison.Ordinal))
                        {
                            throw new ArgumentException("Attempt to navigate above root: classpath:" + cpath);
                        }

                        absoluteURI = URI.Create("classpath:" + cpath);
                    }
                }
                else
                {
                    URI baseURI;
                    try
                    {
                        baseURI = new URI(@base);
                    }
                    catch (URISyntaxException e)
                    {
                        throw Failure(@base, "Invalid base URI: " + e.Message);
                    }

                    int hash = @base.IndexOf('#');
                    if (hash >= 0)
                    {
                        @base = @base.Substring(0, hash);
                        try
                        {
                            baseURI = new URI(@base);
                            checker.CheckThoroughly();
                        }
                        catch (URISyntaxException e)
                        {
                            throw Failure(@base, "Invalid base URI: " + e.Message);
                        }
                    }

                    URI absOrRel;
                    try
                    {
                        absOrRel = AbsoluteOrRelativeURI(relativeURI); // for validation only
                        checker.CheckThoroughly();
                    }
                    catch (URISyntaxException e)
                    {
                        throw Failure(@base, "Invalid relative URI: " + e.Message);
                    }

                    if (absOrRel.IsAbsolute())
                    {
                        absoluteURI = absOrRel;
                    }
                    else
                    {
                        absoluteURI = (relativeURI.Length == 0) ? baseURI : baseURI.Resolve(relativeURI);
                    }
                }
            }
            catch (ArgumentException)
            {

                // can be thrown by resolve() when given a bad URI
                throw Failure(relativeURI, "Cannot resolve URI against base " + Err.Wrap(@base));
            }

            return absoluteURI;
        }

        private static URISyntaxException Failure(string input, string reason)
        {
            return new URISyntaxException(input, reason);
        }

        public static string EscapeSpaces(string s)
        {
            // It's not entirely clear why we have to escape spaces by hand, and not other special characters;
            // it's just that tests with a variety of filenames show that this approach seems to work.
            return s.Replace(" ", "%20");
        }

        // A space as %20, and so that it can be told from them afterwards a %20 as %2520 and a %25 as %2525.
        private static string ProtectSpaces(string s)
        {
            return s.Replace("%25", "%2525").Replace("%20", "%2520").Replace(" ", "%20");
        }

        // Undoes ProtectSpaces in one pass: %2520 holds a %20 and %252520 a %25 and a 20, whichever is met first.
        private static string RestoreSpaces(string uri)
        {
            var result = new System.Text.StringBuilder(uri.Length);
            for (int i = 0; i < uri.Length; i++)
            {
                char c = uri[i];
                if (c == '%' && string.CompareOrdinal(uri, i, "%2520", 0, 5) == 0)
                {
                    result.Append("%20");
                    i += 4;
                }
                else if (c == '%' && string.CompareOrdinal(uri, i, "%2525", 0, 5) == 0)
                {
                    result.Append("%25");
                    i += 4;
                }
                else if (c == '%' && string.CompareOrdinal(uri, i, "%20", 0, 3) == 0)
                {
                    result.Append(' ');
                    i += 2;
                }
                else
                {
                    result.Append(c);
                }
            }

            return result.ToString();
        }
    }
}