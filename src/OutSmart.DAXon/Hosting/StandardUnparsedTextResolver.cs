////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Core;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal.Net;
using OutSmart.DAXon.Transformation;
using System.IO;
using System.Net;
using System.Text;

namespace OutSmart.DAXon.Lib
{
    internal class StandardUnparsedTextResolver : IUnparsedTextURIResolver
    {
        public StandardUnparsedTextResolver() { }
        // Phase C 2026-06-09: real resolver (was => null, which made json-doc()/unparsed-text() fail with
        // "Unable to resolve URI"). Opens an absolute file:// or http(s):// URI and returns a Reader over its
        // content (the real StandardUnparsedTextResolver.cs is excluded). Errors -> null -> caller's FOUT1170.
        // Open a file as text honouring the F&O unparsed-text encoding rules: an explicit encoding wins;
        // otherwise a byte-order mark; otherwise, for a resource carrying an XML declaration, the encoding it
        // names; otherwise UTF-8. STREAMS the content (F1): the encoding is sniffed from the first 256 bytes
        // and the stream rewound — the old whole-file materialization tripled the memory of a large
        // unparsed-text (bytes + string + engine buffers). The consumer owns and closes the reader.
        private static TextReader OpenTextFile(string path, string encoding)
        {
            var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536);
            try
            {
                Encoding enc;
                if (!string.IsNullOrEmpty(encoding))
                {
                    enc = Encoding.GetEncoding(encoding);
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
                    enc = InferEncoding(head, n);
                }

                // A BOM, if present, still wins over the sniffed/default encoding.
                return new StreamReader(fs, enc, detectEncodingFromByteOrderMarks: true);
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
                try { return Encoding.GetEncoding(m.Groups[1].Value); }
                catch { }
            }

            return new UTF8Encoding(false);
        }

        public TextReader Resolve(URI absoluteURI, string encoding, Configuration config)
        {
            try
            {
                var sysUri = new Uri(absoluteURI.ToString());
                // The Processor's input-size cap applies here too (the http branch reads the whole
                // resource into memory; the file branch checks the on-disk length and then streams).
                long maxInput = OutSmart.DAXon.Internal.Streams.InputSizeLimit.MaxFor(config);
                string text;
                if (sysUri.IsFile)
                {
                    if (maxInput != long.MaxValue)
                    {
                        var info = new FileInfo(sysUri.LocalPath);
                        if (info.Exists && info.Length > maxInput)
                        {
                            throw OutSmart.DAXon.Internal.Streams.InputSizeLimit.Oversized(info.Length, maxInput, absoluteURI.ToString(), "FOUT1170");
                        }
                    }

                    return OpenTextFile(sysUri.LocalPath, encoding);
                }
                else
                {
                    // Remote fetch: no length known up front, so read through the counting wrapper.
                    // Encoding precedence (F&O rules; matches what DownloadString did before the
                    // cap): explicit argument, else the Content-Type header's charset
                    // (unparsed-text-2002 exercises exactly this), else BOM, else UTF-8.
                    using (var wc = new TimedWebClient())
                    {
                        var opened = NetworkDeadline.Guard(wc.OpenRead(sysUri));
                        string effective = encoding;
                        if (string.IsNullOrEmpty(effective) && wc.ResponseHeaders != null)
                        {
                            string contentType = wc.ResponseHeaders["Content-Type"];
                            if (contentType != null)
                            {
                                var cm = System.Text.RegularExpressions.Regex.Match(contentType, "charset\\s*=\\s*[\"']?([A-Za-z0-9._-]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                                if (cm.Success)
                                {
                                    effective = cm.Groups[1].Value;
                                }
                            }
                        }

                        using (var raw = OutSmart.DAXon.Internal.Streams.InputSizeLimit.Apply(opened, maxInput, absoluteURI.ToString(), "FOUT1170"))
                        using (var sr = string.IsNullOrEmpty(effective)
                            ? new StreamReader(raw, new UTF8Encoding(false), detectEncodingFromByteOrderMarks: true)
                            : new StreamReader(raw, Encoding.GetEncoding(effective)))
                        {
                            text = sr.ReadToEnd();
                        }
                    }
                }
                return new StringReader(text);
            }
            catch (XPathException) { throw; }   // the cap error must not degrade into null -> generic FOUT1170
            catch (Exception) { return null; }
        }
        // 2026-06-10: real UnparsedTextFunction.ReadFile falls back to this static when the resolver returns
        // null (ResourceRequest -> DirectResourceResolver -> StreamSource). Materializes via StringReader
        // (Java -1 EOF semantics), honoring whichever of reader/stream/systemId the source carries.
        public static TextReader GetReaderFromResolvedResource(ResolvedResource src, string encoding, Configuration config, bool isXml)
        {
            try
            {
                var tr = src.TextReader;
                if (tr != null)
                    return new StringReader(tr.ReadToEnd());
                var ins = src.Stream;
                if (ins != null)
                {
                    using (var sr = string.IsNullOrEmpty(encoding) ? new StreamReader(ins) : new StreamReader(ins, Encoding.GetEncoding(encoding)))
                    {
                        return new StringReader(sr.ReadToEnd());
                    }
                }
                var sysId = src.SystemId;
                if (sysId != null)
                {
                    var u = new Uri(sysId);
                    if (u.IsFile)
                    {
                        return OpenTextFile(u.LocalPath, encoding);
                    }
                    using (var wc = new TimedWebClient()) { return new StringReader(wc.DownloadString(u)); }
                }
            }
            // Upstream contract is `throws XPathException` (StandardUnparsedTextResolver.java:157) and the
            // UnparsedTextFunction.ReadFile call site sits OUTSIDE its IOException try - so translate all
            // native failures here: missing/unreadable resource -> FOUT1170, unknown encoding -> FOUT1190.
            catch (XPathException) { throw; }
            catch (ArgumentException e) { throw new XPathException("unparsed-text(): unknown encoding " + encoding + " (" + e.Message + ")", "FOUT1190"); }
            catch (Exception e) { throw new XPathException("unparsed-text(): cannot read " + (src.SystemId ?? "(anonymous source)") + ": " + e.Message, "FOUT1170"); }
            throw new XPathException("unparsed-text(): resource has no reader, stream or system ID", "FOUT1170");
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
                return request;
            }
        }
    }
}
