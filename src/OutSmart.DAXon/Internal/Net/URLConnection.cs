////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;

namespace OutSmart.DAXon.Internal.Net
{

    // Resource connection: file: opens the local file; other schemes go through WebRequest/WebResponse.
    internal class URLConnection
    {
        protected readonly global::System.Uri _url;
        protected global::System.Net.WebResponse _resp;
        // false only on the resource-policy path, which follows redirects itself (ResourceLoader)
        protected bool followRedirects = true;
        // shared by the hops of one fetch when ResourceLoader follows the redirects itself
        internal global::System.Net.CookieContainer Cookies { get; set; }
        private bool IsFile => _url != null && _url.IsAbsoluteUri && _url.Scheme == global::System.Uri.UriSchemeFile;
        // Translate native I/O failures to the OutSmart.DAXon.Internal.IO family — transpiled callers
        // catch IOException per the Java contract (DirectResourceResolver "carry on", UnparsedTextFunction
        // HandleIOError -> FOUT1170); a native System.IO exception flies past those catches and kills the transform.
        public virtual global::System.IO.Stream InputStream
        {
            get
            {
                if (_url == null)
                    return null;
                try
                {
                    if (IsFile)
                    {
                        return global::System.IO.File.OpenRead(_url.LocalPath);
                    }
                    // Guarded: the deadline is cooperative, so a server that trickles bytes would
                    // otherwise hold this thread long past the run's time limit (round AW).
                    return NetworkDeadline.Guard(Response().GetResponseStream());
                }
                catch (global::System.IO.IOException) { throw; }
                // Native I/O failures (FileNotFoundException/DirectoryNotFoundException) ARE subtypes of
                // System.IO.IOException, so the catch above propagates them to the resource-resolution
                // consumers (ResourceLoader/DirectResourceResolver) which "carry on" per the Java IOException
                // contract (unparsed-text-available false / FOUT1170). Non-IO failures wrap into IOException.
                catch (global::System.Exception e) { throw new global::System.IO.IOException(e.Message); }
            }
        }
        public virtual string ContentType { get { try { return _url == null || IsFile ? null : Response().ContentType; } catch { return null; } } }
        public URLConnection(global::System.Uri url) { _url = url; }
        protected global::System.Net.WebResponse Response()
        {
            if (_resp == null && _url != null)
            {
                try
                {
                    global::System.Net.WebRequest req = global::System.Net.WebRequest.Create(_url);
                    NetworkDeadline.Apply(req);   // a stalled connect must not outlive the run
                    HttpRequestDefaults.Apply(req, Cookies);
                    if (!followRedirects && req is global::System.Net.HttpWebRequest http)
                    {
                        http.AllowAutoRedirect = false;
                    }

                    _resp = req.GetResponse();
                }
                catch (global::System.Net.WebException we) when (!followRedirects && IsRedirect(we.Response))
                {
                    // Some stacks raise a 3xx as an error once auto-redirect is off; it is the answer here.
                    _resp = we.Response;
                }
                catch (global::System.Net.WebException we)
                {
                    // Java's URLConnection surfaces HTTP retrieval failures (including 4xx/5xx, which .NET raises
                    // as WebException from GetResponse) as java.io.IOException. Translate so the callers' existing
                    // IOException handlers fire — unparsed-text() -> FOUT1170, doc()/unparsed-text-available() ->
                    // not-available — instead of a raw WebException escaping as a code-less error and killing the query.
                    throw new global::System.IO.IOException(we.Message, we);
                }
            }

            return _resp;
        }
        // java.net.HttpURLConnection.disconnect(): release a response this connection opened but whose
        // body nobody will read. Needed because a redirect hop and a content-type probe each open a
        // response and abandon it - without this the socket stays checked out of the ServicePoint pool
        // until finalization, and DefaultConnectionLimit is 2, so a long-lived process starves on
        // connections long before any memory curve moves.
        public virtual void Disconnect()
        {
            global::System.Net.WebResponse r = _resp;
            _resp = null;
            if (r != null)
            {
                try { r.Close(); } catch (global::System.Exception) { }
            }
        }
        private static bool IsRedirect(global::System.Net.WebResponse response)
        {
            int status = response is global::System.Net.HttpWebResponse h ? (int)h.StatusCode : 0;
            return status >= 300 && status <= 399;
        }

        // Static helpers used by AbstractResourceCollection.
        public static string GuessContentTypeFromName(string name) => null;
        public static string GuessContentTypeFromStream(global::System.IO.Stream stream) => null;
    }
}
