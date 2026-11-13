////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Core;
using System;
using OutSmart.DAXon.Internal.Net;
using OutSmart.DAXon.Transformation;
using System.IO;
using System.Net;
using System.Text;

namespace OutSmart.DAXon.Lib
{
    internal sealed class StandardUnparsedTextResolver : IUnparsedTextURIResolver
    {
        public StandardUnparsedTextResolver() { }
        // Open a file as text honouring the F&O unparsed-text encoding rules: an explicit encoding wins;
        // otherwise a byte-order mark; otherwise, for a resource carrying an XML declaration, the encoding it
        // names; otherwise UTF-8. STREAMS the content (F1): the encoding is sniffed from the first 256 bytes
        // and the stream rewound — the old whole-file materialization tripled the memory of a large
        // unparsed-text (bytes + string + engine buffers). The consumer owns and closes the reader.
        // maxInput: the input-size cap, checked against the length of the open file (asking the file system by path
        // first cost a read of a small file a third of its time); uri names the resource in the error.
        private static TextReader OpenTextFile(string path, string encoding, long maxInput = long.MaxValue, string uri = null)
        {
            // Unbuffered, the reader's buffers the size of a small file are the only ones; a large file is read 64 KB at a time.
            System.IO.Stream fs = OutSmart.DAXon.Internal.Streams.FileNames.OpenRead(path, 1);
            try
            {
                if (maxInput != long.MaxValue && fs.CanSeek && fs.Length > maxInput)
                {
                    throw OutSmart.DAXon.Internal.Streams.InputSizeLimit.Oversized(fs.Length, maxInput, uri ?? path, "FOUT1170");
                }

                if (!fs.CanSeek || fs.Length > 4096)
                {
                    fs = new System.IO.BufferedStream(fs, 65536);
                }

                Encoding enc;
                if (!string.IsNullOrEmpty(encoding))
                {
                    enc = Strict(OutSmart.DAXon.Internal.PlatformEncodings.GetForReading(encoding));
                }
                else
                {
                    byte[] head = new byte[256];
                    int n = 0;
                    int got;
                    while (n < head.Length && (got = fs.Read(head, n, head.Length - n)) > 0)
                    {
                        n += got;
                    }

                    fs.Position = 0;
                    enc = Strict(InferEncoding(head, n));
                }

                // A mark wins over what was sniffed, not over an encoding that was asked for.
                return new OutSmart.DAXon.Internal.Streams.StrictTextReader(fs, enc, OutSmart.DAXon.Internal.Streams.StrictTextReader.MarksOf(encoding));
            }
            catch
            {
                fs.Dispose();
                throw;
            }
        }

        private static Encoding InferEncoding(byte[] bytes, int count)
        {
            // BOMs are handled by StreamReader; here infer from an XML declaration (its bytes are
            // ASCII-compatible in every XML encoding, so a Latin-1 decode of the prefix reads it).
            int n = Math.Min(count, 256);
            string head = Encoding.GetEncoding("ISO-8859-1").GetString(bytes, 0, n);
            var m = System.Text.RegularExpressions.Regex.Match(head, "^\\s*<\\?xml\\b[^>]*?encoding\\s*=\\s*[\"']([^\"']+)[\"']");
            if (m.Success)
            {
                try { return OutSmart.DAXon.Internal.PlatformEncodings.GetForReading(m.Groups[1].Value); }
                catch { }
            }

            return new UTF8Encoding(false);
        }

        public TextReader Resolve(URI absoluteURI, string encoding, Configuration config)
        {
            return Read(absoluteURI, encoding, config, "unparsed-text()");
        }

        // The engine's own read of a text resource, once the resolvers declined it, and the one the resource policy
        // guards: a file or an http(s) URL, null for another scheme. A failure is final and names the reason.
        internal static TextReader Read(URI absoluteURI, string encoding, Configuration config, string function)
        {
            // Before FileInfo or any fetch, so a denied existing file and a missing one look the same.
            string denied = OutSmart.DAXon.Internal.ResourceGate.IsRestricted(config)
                ? OutSmart.DAXon.Internal.ResourceGate.CheckRead(config, absoluteURI?.ToString(), OutSmart.DAXon.Api.ResourceKind.Text)
                : null;
            if (denied != null)
            {
                throw new OutSmart.DAXon.Internal.ResourceDeniedException(denied, "FOUT1170");
            }

            if (!OutSmart.DAXon.Internal.Net.BidiUri.TryMake(absoluteURI.ToString(), UriKind.Absolute, out Uri sysUri)
                || !(sysUri.IsFile || sysUri.Scheme == Uri.UriSchemeHttp || sysUri.Scheme == Uri.UriSchemeHttps))
            {
                return null;
            }

            try
            {
                // The Processor's input-size cap applies here too (the http branch reads the whole
                // resource into memory; the file branch checks the on-disk length and then streams).
                long maxInput = OutSmart.DAXon.Internal.Streams.InputSizeLimit.MaxFor(config);
                string text;
                if (sysUri.IsFile)
                {
                    return OpenTextFile(OutSmart.DAXon.Internal.Net.BidiUri.Out(OutSmart.DAXon.Internal.Net.FileUris.LocalPathOf(sysUri)), encoding, maxInput, absoluteURI.ToString());
                }
                else if (OutSmart.DAXon.Internal.ResourceGate.IsRestricted(config))
                {
                    // WebClient follows redirects on its own; under a policy every hop is checked.
                    text = ReadRestricted(sysUri, encoding, config, maxInput, absoluteURI.ToString());
                }
                else
                {
                    text = ReadWeb(sysUri, encoding, maxInput, absoluteURI.ToString());
                }
                return new StringReader(text);
            }
            catch (Exception e) when (!(e is XPathException) && !(e is OutOfMemoryException))
            {
                throw Unreadable(e, function, absoluteURI.ToString(), encoding);
            }
        }
        // What a resolver returned (UnparsedTextFunction.OpenText), or the DirectResourceResolver for a scheme Read leaves
        // alone: materialized via StringReader (Java -1 EOF semantics), from whichever of reader/stream/systemId it carries.
        public static TextReader GetReaderFromResolvedResource(ResolvedResource src, string encoding, Configuration config, string function = "unparsed-text()")
        {
            try
            {
                var tr = src.TextReader;
                if (tr != null)
                {
                    // The host's reader is closed when it asks, as its stream is.
                    try
                    {
                        return new StringReader(tr.ReadToEnd());
                    }
                    finally
                    {
                        if (src.PleaseCloseAfterUse)
                        {
                            tr.Dispose();
                        }
                    }
                }
                var ins = src.Stream;
                if (ins != null)
                {
                    using (var sr = new OutSmart.DAXon.Internal.Streams.StrictTextReader(ins, string.IsNullOrEmpty(encoding) ? Strict(new UTF8Encoding(false)) : Strict(OutSmart.DAXon.Internal.PlatformEncodings.GetForReading(encoding)), OutSmart.DAXon.Internal.Streams.StrictTextReader.MarksOf(encoding)))
                    {
                        return new StringReader(sr.ReadToEnd());
                    }
                }
                var sysId = src.SystemId;
                if (sysId != null)
                {
                    var u = OutSmart.DAXon.Internal.Net.BidiUri.Make(sysId);
                    if (u.IsFile)
                    {
                        return OpenTextFile(OutSmart.DAXon.Internal.Net.BidiUri.Out(OutSmart.DAXon.Internal.Net.FileUris.LocalPathOf(u)), encoding);
                    }
                    if (OutSmart.DAXon.Internal.ResourceGate.IsRestricted(config))
                    {
                        return new StringReader(ReadRestricted(u, encoding, config, OutSmart.DAXon.Internal.Streams.InputSizeLimit.MaxFor(config), sysId));
                    }
                    return new StringReader(ReadWeb(u, encoding, OutSmart.DAXon.Internal.Streams.InputSizeLimit.MaxFor(config), sysId));
                }
            }
            // Upstream contract is `throws XPathException` (StandardUnparsedTextResolver.java:157) and the
            // UnparsedTextFunction.ReadFile call site sits OUTSIDE its IOException try - so translate all
            // native failures here: missing/unreadable resource -> FOUT1170, unknown encoding -> FOUT1190.
            catch (Exception e) when (!(e is XPathException) && !(e is OutOfMemoryException))
            {
                throw Unreadable(e, function, src.SystemId ?? "(anonymous source)", encoding);
            }

            throw new XPathException(function + ": resource has no reader, stream or system ID", "FOUT1170");
        }

        // A decoder that refuses bytes it cannot decode (FOUT1190, or FOUT1200 when the encoding was inferred), where
        // .NET's default writes U+FFFD and the text was changed silently.
        internal static Encoding Strict(Encoding e)
        {
            if (e.CodePage == 65001)
            {
                return StrictUtf8;   // nearly every read: no copy per file (a reader skips a mark itself)
            }

            var strict = (Encoding)e.Clone();
            strict.DecoderFallback = DecoderFallback.ExceptionFallback;
            return strict;
        }

        private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

        // An argument the file system did not refuse is the encoding's: an unknown name.
        private static XPathException Unreadable(Exception e, string function, string systemId, string encoding)
        {
            if (e is DecoderFallbackException)
            {
                return new XPathException(function + ": " + systemId + " cannot be decoded as " + (encoding ?? "UTF-8") + ": " + e.Message, e)
                    .WithErrorCode(string.IsNullOrEmpty(encoding) ? "FOUT1200" : "FOUT1190");
            }

            if ((e is ArgumentException || e is NotSupportedException) && !OutSmart.DAXon.Api.DAXonApiException.IsIO(e))   // UTF-7 on .NET 5 and later
            {
                return new XPathException(function + ": unknown encoding " + encoding + " (" + e.Message + ")", "FOUT1190");
            }

            return new XPathException(function + ": cannot read " + systemId + ": " + e.Message, e).WithErrorCode("FOUT1170");
        }

        // The policy path of a remote fetch: redirects followed by ResourceLoader, hop by hop. Same
        // encoding precedence as the WebClient path: argument, Content-Type charset, BOM, UTF-8.
        private static string ReadRestricted(Uri uri, string encoding, Configuration config, long maxInput, string systemId)
        {
            OutSmart.DAXon.Internal.Net.URLConnection conn = OutSmart.DAXon.Resources.ResourceLoader.UrlConnection(uri, config, OutSmart.DAXon.Api.ResourceKind.Text);
            string effective = string.IsNullOrEmpty(encoding) ? CharsetOf(conn.ContentType) : encoding;
            return ReadCapped(conn.DecodedStream(maxInput, systemId, "FOUT1170"), effective, encoding, maxInput, systemId);
        }

        // The default path of a remote fetch; WebClient follows redirects itself.
        private static string ReadWeb(Uri uri, string encoding, long maxInput, string systemId)
        {
            using (var wc = new TimedWebClient())
            {
                Stream body = NetworkDeadline.Guard(wc.OpenRead(uri));
                try
                {
                    WebHeaderCollection headers = wc.ResponseHeaders;
                    string effective = string.IsNullOrEmpty(encoding) ? CharsetOf(headers?["Content-Type"]) : encoding;
                    body = HttpContentDecoding.Decode(body, headers?["Content-Encoding"], maxInput, systemId, "FOUT1170");
                    return ReadCapped(body, effective, encoding, maxInput, systemId);
                }
                finally
                {
                    body.Dispose();
                }
            }
        }

        // Decoded bytes against the cap. Encoding precedence (F&O rules): the argument, else the
        // Content-Type charset (unparsed-text-2002), else a BOM, else UTF-8.
        private static string ReadCapped(Stream body, string encoding, string asked, long maxInput, string systemId)
        {
            using (var raw = OutSmart.DAXon.Internal.Streams.InputSizeLimit.Apply(body, maxInput, systemId, "FOUT1170"))
            using (var sr = new OutSmart.DAXon.Internal.Streams.StrictTextReader(raw, string.IsNullOrEmpty(encoding) ? Strict(new UTF8Encoding(false)) : Strict(OutSmart.DAXon.Internal.PlatformEncodings.GetForReading(encoding)), OutSmart.DAXon.Internal.Streams.StrictTextReader.MarksOf(asked)))
            {
                return sr.ReadToEnd();
            }
        }

        private static string CharsetOf(string contentType)
        {
            if (contentType == null)
            {
                return null;
            }

            var cm = System.Text.RegularExpressions.Regex.Match(contentType, "charset\\s*=\\s*[\"']?([A-Za-z0-9._-]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return cm.Success ? cm.Groups[1].Value : null;
        }

        // WebClient builds its request internally, so this is the only place its timeouts can be
        // set: a fetch must not outlive the run's deadline (round AW). Unlimited runs keep the
        // platform defaults.
        private sealed class TimedWebClient : WebClient
        {
            protected override WebRequest GetWebRequest(Uri address)
            {
                WebRequest request = base.GetWebRequest(address);
                NetworkDeadline.Apply(request);
                HttpRequestDefaults.Apply(request, null);
                return request;
            }
        }
    }
}
