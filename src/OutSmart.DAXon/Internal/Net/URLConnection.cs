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
        protected readonly Uri _url;
        protected System.Net.WebResponse _resp;
        // false only on the resource-policy path, which follows redirects itself (ResourceLoader)
        protected bool followRedirects = true;
        // shared by the hops of one fetch when ResourceLoader follows the redirects itself
        internal System.Net.CookieContainer Cookies { get; set; }
        private bool IsFile => _url != null && _url.IsAbsoluteUri && _url.Scheme == Uri.UriSchemeFile;
        // Translate native I/O failures to the OutSmart.DAXon.Internal.IO family — transpiled callers
        // catch IOException per the Java contract (DirectResourceResolver "carry on", UnparsedTextFunction
        // HandleIOError -> FOUT1170); a native System.IO exception flies past those catches and kills the transform.
        public virtual System.IO.Stream InputStream
        {
            get
            {
                if (_url == null)
                    return null;
                try
                {
                    if (IsFile)
                    {
                        return System.IO.File.OpenRead(_url.LocalPath);
                    }
                    // Guarded: the deadline is cooperative, so a server that trickles bytes would
                    // otherwise hold this thread long past the run's time limit (round AW).
                    return NetworkDeadline.Guard(Response().GetResponseStream());
                }
                catch (System.IO.IOException)
                {
                    throw;
                }
                // Native I/O failures (FileNotFoundException/DirectoryNotFoundException) ARE subtypes of
                // System.IO.IOException, so the catch above propagates them to the resource-resolution
                // consumers (ResourceLoader/DirectResourceResolver) which "carry on" per the Java IOException
                // contract (unparsed-text-available false / FOUT1170). Non-IO failures wrap into IOException.
                catch (Exception e)
                {
                    throw new System.IO.IOException(e.Message, e);
                }
            }
        }
        public virtual string ContentType { get { try { return _url == null || IsFile ? null : Response().ContentType; } catch { return null; } } }

        // The body as the caller reads it: a gzip response decoded above the deadline guard, its
        // compressed bytes held to maxInput; the caller caps the decoded bytes.
        internal System.IO.Stream DecodedStream(long maxInput, string systemId, string errorCode)
        {
            System.IO.Stream body = InputStream;
            string contentEncoding = null;
            if (!IsFile && body != null)
            {
                try
                {
                    contentEncoding = (Response() as System.Net.HttpWebResponse)?.ContentEncoding;
                }
                catch (Exception)
                {
                }
            }

            return HttpContentDecoding.Decode(body, contentEncoding, maxInput, systemId, errorCode);
        }
        public URLConnection(Uri url) { _url = url; }
        protected System.Net.WebResponse Response()
        {
            if (_resp == null && _url != null)
            {
                try
                {
                    System.Net.WebRequest req = System.Net.WebRequest.Create(_url);
                    NetworkDeadline.Apply(req);   // a stalled connect must not outlive the run
                    HttpRequestDefaults.Apply(req, Cookies);
                    if (!followRedirects && req is System.Net.HttpWebRequest http)
                    {
                        http.AllowAutoRedirect = false;
                    }

                    _resp = req.GetResponse();
                }
                catch (System.Net.WebException we) when (!followRedirects && IsRedirect(we.Response))
                {
                    // Some stacks raise a 3xx as an error once auto-redirect is off; it is the answer here.
                    _resp = we.Response;
                }
                catch (System.Net.WebException we)
                {
                    // Java's URLConnection surfaces HTTP retrieval failures (including 4xx/5xx, which .NET raises
                    // as WebException from GetResponse) as java.io.IOException. Translate so the callers' existing
                    // IOException handlers fire — unparsed-text() -> FOUT1170, doc()/unparsed-text-available() ->
                    // not-available — instead of a raw WebException escaping as a code-less error and killing the query.
                    throw new System.IO.IOException(we.Message, we);
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
            System.Net.WebResponse r = _resp;
            _resp = null;
            if (r != null)
            {
                try { r.Close(); } catch (Exception) { }
            }
        }
        private static bool IsRedirect(System.Net.WebResponse response)
        {
            int status = response is System.Net.HttpWebResponse h ? (int)h.StatusCode : 0;
            return status >= 300 && status <= 399;
        }

        // The JDK's file-name map, reduced to entries whose media type has a resource factory; the
        // configuration's extension table answers for the rest, as it does after the JDK upstream.
        public static string GuessContentTypeFromName(string name)
        {
            int i = Math.Max(name.LastIndexOf('.'), Math.Max(name.LastIndexOf('/'), name.LastIndexOf('?')));
            if (i < 0 || name[i] != '.')
            {
                return null;
            }

            switch (name.Substring(i).ToLowerInvariant())
            {
                case ".text":
                case ".txt":
                case ".java":
                case ".c":
                case ".cc":
                case ".c++":
                case ".h":
                case ".pl":
                    return "text/plain";
                case ".htm":
                case ".html":
                    return "text/html";
                case ".xml":
                    return "application/xml";
                default:
                    return null;
            }
        }

        // Reads up to 16 bytes, restoring the position of a seekable stream.
        public static string GuessContentTypeFromStream(System.IO.Stream stream)
        {
            long start = stream.CanSeek ? stream.Position : 0;
            byte[] head = new byte[16];
            int n = 0;
            int got;
            while (n < head.Length && (got = stream.Read(head, n, head.Length - n)) > 0)
            {
                n += got;
            }

            if (stream.CanSeek)
            {
                stream.Position = start;
            }

            return GuessContentTypeFromBytes(head, n);
        }

        // The markup tests of the JDK's guessContentTypeFromStream. The binary formats it also knows
        // (images, audio) have no resource factory, so they end as binary resources either way.
        public static string GuessContentTypeFromBytes(byte[] bytes, int count)
        {
            int[] c = new int[16];
            for (int i = 0; i < c.Length; i++)
            {
                c[i] = i < count ? bytes[i] : -1;
            }

            if (c[0] == '<')
            {
                if (c[1] == '!'
                    || (c[1] == 'h' && ((c[2] == 't' && c[3] == 'm' && c[4] == 'l') || (c[2] == 'e' && c[3] == 'a' && c[4] == 'd')))
                    || (c[1] == 'b' && c[2] == 'o' && c[3] == 'd' && c[4] == 'y')
                    || (c[1] == 'H' && ((c[2] == 'T' && c[3] == 'M' && c[4] == 'L') || (c[2] == 'E' && c[3] == 'A' && c[4] == 'D')))
                    || (c[1] == 'B' && c[2] == 'O' && c[3] == 'D' && c[4] == 'Y'))
                {
                    return "text/html";
                }

                if (c[1] == '?' && c[2] == 'x' && c[3] == 'm' && c[4] == 'l' && c[5] == ' ')
                {
                    return "application/xml";
                }
            }

            // "<?x" after a UTF-8, UTF-16 or UTF-32 byte order mark
            if ((c[0] == 0xEF && c[1] == 0xBB && c[2] == 0xBF && c[3] == '<' && c[4] == '?' && c[5] == 'x')
                || (c[0] == 0xFE && c[1] == 0xFF && Units(c, 2, 2, false))
                || (c[0] == 0xFF && c[1] == 0xFE && Units(c, 2, 2, true))
                || (c[0] == 0 && c[1] == 0 && c[2] == 0xFE && c[3] == 0xFF && Units(c, 4, 4, false))
                || (c[0] == 0xFF && c[1] == 0xFE && c[2] == 0 && c[3] == 0 && Units(c, 4, 4, true)))
            {
                return "application/xml";
            }

            return null;
        }

        // "<?x" as three code units of the given width from position at.
        private static bool Units(int[] c, int at, int width, bool littleEndian)
        {
            string s = "<?x";
            for (int k = 0; k < s.Length; k++)
            {
                for (int b = 0; b < width; b++)
                {
                    int expected = (littleEndian ? b == 0 : b == width - 1) ? s[k] : 0;
                    if (c[at + k * width + b] != expected)
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
