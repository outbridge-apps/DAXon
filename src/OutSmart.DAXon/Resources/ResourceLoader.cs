////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Internal.Net;
using OutSmart.DAXon.Internal.Charsets;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Streams;
using System.IO;
namespace OutSmart.DAXon.Resources
{
    internal sealed class ResourceLoader
    {
        public static int MAX_REDIRECTS = 20;
        public static URLConnection UrlConnection(Uri url)
        {
            return UrlConnection(url, null, OutSmart.DAXon.Api.ResourceKind.Document);
        }

        // Under a restricted resource policy the redirects are followed here, hop by hop, and every
        // target is checked like the first URI - a host rule checked only on the first URL would not
        // hold across a redirect. Otherwise HttpWebRequest follows them itself, as it always has.
        public static URLConnection UrlConnection(Uri url, Configuration config, OutSmart.DAXon.Api.ResourceKind kind)
        {
            if (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps)
            {
                bool manual = ResourceGate.IsRestricted(config);
                var visited = new HashSet<string>();
                var cookies = new System.Net.CookieContainer();   // one fetch, all of its hops
                int count = MAX_REDIRECTS;
                for (; ; )
                {
                    HttpURLConnection conn = new HttpURLConnection(url);
                    conn.SetInstanceFollowRedirects(!manual);
                    conn.Cookies = cookies;
                    int status = conn.ResponseCode;
                    bool redirect = manual
                        ? IsManualRedirect(status)
                        : status == HttpURLConnection.HTTP_MOVED_PERM || status == HttpURLConnection.HTTP_MOVED_TEMP;
                    string location = redirect ? conn.GetHeaderField("Location") : null;
                    if (manual && redirect && location == null)
                    {
                        // nowhere to go: the 3xx itself is the answer, as the platform treats it
                        redirect = false;
                    }

                    if (redirect)
                    {
                        // Dormant with the default policy: HttpWebRequest follows redirects inside
                        // GetResponse, so a 3xx reaches this point only on the restricted path. The
                        // header name had been mangled to "ILocation" by the ILocation-type rename
                        // sweep - a silent artifact precisely because the loop was dormant.
                        Uri previous = url;

                        // Every header read above needs the response, so release it only now - but
                        // release it on EVERY exit from this hop, including the throws below.
                        // Nobody will ever read this hop's body, and an abandoned response keeps its
                        // socket checked out of the pool until finalization.
                        conn.Disconnect();
                        try
                        {
                            url = new Uri(url, location);
                        }
                        catch (UriFormatException e)
                        {
                            throw new IOException("Redirect from " + previous + " to an invalid location: " + e.Message, e);
                        }

                        if (manual)
                        {
                            CheckHop(previous, url, config, kind);
                        }

                        if (visited.Contains(location))
                        {
                            throw new IOException("HTTP redirect loop through " + location);
                        }

                        visited.Add(location);
                        count -= 1;
                        if (count < 0)
                        {
                            throw new IOException("HTTP redirects more than " + MAX_REDIRECTS + " times");
                        }
                    }
                    else
                    {
                        // The caller reads this one's body, so it stays open: closing it is the
                        // caller's job, via the stream it obtains from InputStream.
                        return conn;
                    }
                }
            }
            else
            {
                return new URLConnection(url);
            }
        }

        private static bool IsManualRedirect(int status)
        {
            return status == HttpURLConnection.HTTP_MOVED_PERM || status == HttpURLConnection.HTTP_MOVED_TEMP
                || status == HttpURLConnection.HTTP_SEE_OTHER || status == HttpURLConnection.HTTP_TEMP_REDIRECT
                || status == HttpURLConnection.HTTP_PERM_REDIRECT;
        }

        // A hop goes only where the platform itself would follow - HTTP(S), never HTTPS to HTTP, never
        // to file: or another scheme - and then only where the policy allows the first URI to go.
        private static void CheckHop(Uri from, Uri to, Configuration config, OutSmart.DAXon.Api.ResourceKind kind)
        {
            bool web = to.Scheme == Uri.UriSchemeHttp || to.Scheme == Uri.UriSchemeHttps;
            if (!web || (from.Scheme == Uri.UriSchemeHttps && to.Scheme == Uri.UriSchemeHttp))
            {
                throw new IOException("Redirect from " + from + " to " + to + " refused");
            }

            string denied = ResourceGate.CheckRead(config, to.AbsoluteUri, kind);
            if (denied != null)
            {
                throw ResourceGate.Denied("Redirect from " + from + ": " + denied, kind);
            }
        }

        public static System.IO.Stream UrlStream(Configuration config, string url)
        {
            return UrlStream(config, url, OutSmart.DAXon.Api.ResourceKind.Document);
        }

        public static System.IO.Stream UrlStream(Configuration config, string url, OutSmart.DAXon.Api.ResourceKind kind)
        {
            if (config != null && url.StartsWith("classpath:", StringComparison.Ordinal))
            {
                string path;
                if (url.Length > 10 && url[10] == '/')
                {
                    path = url.Substring(11);
                }
                else
                {
                    path = url.Substring(10);
                }

                // Without a host loader, classpath: URIs are unresolvable.
                if (config.DynamicLoader is NoDynamicLoader)
                {
                    throw new IOException("Cannot resolve classpath: URI (no dynamic loader configured): " + url);
                }

                return config.DynamicLoader.GetResourceAsStream(path);
            }
            else
            {
                return ResourceLoader.UrlConnection(new Uri(url), config, kind)
                    .DecodedStream(InputSizeLimit.MaxFor(config), url, kind == OutSmart.DAXon.Api.ResourceKind.Text ? "FOUT1170" : "FODC0002");
            }
        }

        public static ResolvedResource TypedResource(Configuration config, string url)
        {
            if (config != null && url.StartsWith("classpath:", StringComparison.Ordinal))
            {
                return new ResolvedResource { Stream = UrlStream(config, url), SystemId = url };
            }
            else
            {
                URLConnection conn = ResourceLoader.UrlConnection(new Uri(url), config, OutSmart.DAXon.Api.ResourceKind.Text);
                System.IO.Stream inputStream = new BufferedStream(conn.DecodedStream(InputSizeLimit.MaxFor(config), url, "FOUT1170"));

                return new ResolvedResource { Stream = inputStream, ContentType = conn.ContentType, SystemId = url };
            }
        }
    }
}
