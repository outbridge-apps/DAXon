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
            // We rely on the Java implementation, but the Java implementation will not handle invalid characters
            // notably spaces. If there are spaces present, we escape them to prevent Java objecting, and then unescape
            // them at the end. We accept the consequence that if the input contains both escaped and unescaped spaces,
            // they will all be unescaped at the end.
            bool escaped = false;
            if (relative.Contains(" "))
            {
                relative = EscapeSpaces(relative);
                escaped = true;
            }

            if (@base.Contains(" "))
            {
                @base = EscapeSpaces(@base);
                escaped = true;
            }

            // The port's URI is lenient where Java's java.net.URI is strict, so reject two malformations Java
            // catches (both FORG0002): (1) a '%' not followed by two hex digits (e.g. "%gg" — the port would
            // otherwise re-escape it to "%25gg"); this runs after EscapeSpaces, which only adds valid "%20".
            for (int __i = 0; __i < relative.Length; __i++)
            {
                if (relative[__i] == '%' && (__i + 2 >= relative.Length || !IsHexDigit(relative[__i + 1]) || !IsHexDigit(relative[__i + 2])))
                {
                    throw new XPathException("Relative URI " + Err.Wrap(relative) + " contains a malformed percent-encoded octet", "FORG0002", context);
                }
            }

            URI relativeURI = null;
            try
            {
                relativeURI = AbsoluteOrRelativeURI(relative);
            }
            catch (URISyntaxException e)
            {
                // .NET's System.Uri mis-parses single-letter-scheme URIs (e.g. "g:h") as DOS drive
                // paths and throws; RFC 3986 treats a scheme-prefixed reference as ABSOLUTE, and
                // resolve-uri returns it unchanged (base-uri-006). Detect the scheme syntactically.
                if (HasUriScheme(relative))
                {
                    return new AnyURIValue(relative);
                }

                throw new XPathException("Relative URI " + Err.Wrap(relative) + " is invalid: " + e.Message, "FORG0002", context);
            }

            if (relativeURI.IsAbsolute())
            {
                return new AnyURIValue(relative);
            }

            // (2) RFC 3986: a relative-reference's first path segment must not contain ':' (it would be
            // mis-parsed as a scheme). ":" is neither absolute (empty scheme) nor a valid relative reference.
            int __segEnd = relative.Length;
            foreach (char __d in new[] { '/', '?', '#' })
            {
                int __p = relative.IndexOf(__d);
                if (__p >= 0 && __p < __segEnd)
                {
                    __segEnd = __p;
                }
            }
            int __colon = relative.IndexOf(':');
            if (__colon >= 0 && __colon < __segEnd)
            {
                throw new XPathException("Relative URI " + Err.Wrap(relative) + " has a ':' in its first path segment", "FORG0002", context);
            }

            URI absoluteURI = null;
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
                throw new XPathException("Base URI " + Err.Wrap(@base) + " is not an absolute URI", "FORG0002", context);
            }

            if (absoluteURI.IsOpaque() && !@base.StartsWith("jar:", StringComparison.Ordinal))
            {

                // Special-case JAR file URLs, even though non-conformant
                throw new XPathException("Base URI " + Err.Wrap(@base) + " is a non-hierarchic URI", "FORG0002", context);
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

            URI resolved = null;
            try
            {
                resolved = MakeAbsolute(relative, @base);
            }
            catch (URISyntaxException e)
            {
                throw new XPathException(e.Message, "FORG0002");
            }

            if (!resolved.ToASCIIString().StartsWith("file:////", StringComparison.Ordinal))
            {
                resolved = resolved.Normalize();
            }


            // The spec says that special characters are not escaped. But if the input was percent-escaped,
            // we want the output to be percent-escaped too. Java achieves this automatically, but on C#
            // it needs special attention.
            bool inputIsPercentEncoded = @base.Contains("%") || relative.Contains("%");
            string resolvedString = inputIsPercentEncoded ? resolved.ToASCIIString() : resolved.ToString();
            string result = escaped ? UnescapeSpaces(resolvedString) : resolvedString;

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

        public static URI AbsoluteOrRelativeURI(string href)
        {
            return new URI(href);
        }

        private static bool IsHexDigit(char c) => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');

        // RFC 3986 scheme = ALPHA *( ALPHA / DIGIT / "+" / "-" / "." ) followed by ':'. A reference with
        // a scheme is an absolute URI reference. Used to recognise refs (e.g. "g:h") that .NET's
        // System.Uri rejects as DOS drive paths but RFC 3986 treats as absolute.
        private static bool HasUriScheme(string s)
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

            int segEnd = uri.Length;
            foreach (char d in new[] { '/', '?', '#' })
            {
                int p = uri.IndexOf(d);
                if (p >= 0 && p < segEnd)
                {
                    segEnd = p;
                }
            }
            int colon = uri.IndexOf(':');
            return colon < 0 || colon >= segEnd;
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
                URI currentDirectoryURI = new Uri(Path.GetFullPath(dir)).AbsoluteUri;
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
                checker.CheckThoroughly(absoluteURI);
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
                        Uri baseURL = new Uri(@base);
                        Uri absoluteURL = new Uri(baseURL, relativeURI);
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
                            checker.CheckThoroughly(baseURI);
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
                        checker.CheckThoroughly(absOrRel);
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
            int i = s.IndexOf(' ');
            if (i < 0)
            {
                return s;
            }

            return (i == 0 ? "" : s.Substring(0, i)) + "%20" + (i == s.Length - 1 ? "" : EscapeSpaces(s.Substring(i + 1)));
        }

        public static string UnescapeSpaces(string uri)
        {
            return uri.Replace("%20", " ");
        }
    }
}