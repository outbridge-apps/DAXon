////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Collections;
using System;
using System.IO;
using System.Text;


namespace OutSmart.DAXon.XQuery
{
    /// <summary>
    /// Utility for reading an XQuery module from a byte/character stream, honouring BOM detection and an
    /// explicit `xquery ... encoding "..."` declaration, and validating XML characters. (Port of upstream
    /// net.sf.saxon.query.QueryReader; the Source hierarchy is gone from this port, so the primary overload
    /// takes a <see cref="ResolvedResource"/>.)
    /// </summary>
    internal static class QueryReader
    {
        // Read a query module supplied as a ResolvedResource (byte stream, char reader, or systemId).
        public static string ReadSourceQuery(Configuration config, ResolvedResource ss, IIntPredicateProxy charChecker)
        {
            if (ss == null)
            {
                throw new XPathException("Module URI Resolver supplied no resource");
            }

            if (ss.TextReader != null)
            {
                return ReadQueryFromReader(ss.TextReader, charChecker);
            }

            Stream stream = ss.Stream;
            if (stream == null && ss.SystemId != null)
            {
                try
                {
                    Uri u = new Uri(ss.SystemId);
                    if (u.IsFile)
                    {
                        stream = OutSmart.DAXon.Internal.Streams.FileNames.OpenRead(u.LocalPath);
                    }
                }
                catch (Exception e)
                {
                    throw new XPathException("I/O Error reading input stream from " + ss.SystemId + ": " + e.Message);
                }
            }

            if (stream == null)
            {
                throw new XPathException("Module URI Resolver must supply either an InputStream or a Reader");
            }

            return ReadInputStream(stream, null, charChecker);
        }

        // Read a query module from a byte stream, inferring the encoding when none is supplied.
        public static string ReadInputStream(Stream source, string encoding, IIntPredicateProxy charChecker)
        {
            byte[] bytes;
            using (var ms = new MemoryStream())
            {
                source.CopyTo(ms);
                bytes = ms.ToArray();
            }

            if (bytes.Length == 0)
            {
                throw new XPathException("Query source file is empty");
            }

            int start = 0;
            Encoding enc = null;
            // BOM detection wins over any declared/supplied encoding.
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            { enc = new UTF8Encoding(false); start = 3; }
            else if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            { enc = new UnicodeEncoding(false, false); start = 2; }
            else if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            { enc = new UnicodeEncoding(true, false); start = 2; }

            if (enc == null && !string.IsNullOrEmpty(encoding))
            {
                enc = TryGetEncoding(encoding);
            }

            if (enc == null)
            {
                // Look for an `xquery version "..." encoding "..."` declaration in the ASCII prolog head.
                string head = Encoding.ASCII.GetString(bytes, start, Math.Min(bytes.Length - start, 200));
                System.Text.RegularExpressions.Match m = System.Text.RegularExpressions.Regex.Match(head, "encoding\\s+[\"']([^\"']+)[\"']");
                enc = m.Success ? TryGetEncoding(m.Groups[1].Value) : new UTF8Encoding(false);
            }

            string text = enc.GetString(bytes, start, bytes.Length - start);
            return ReadQueryFromReader(new StringReader(text), charChecker);
        }

        private static Encoding TryGetEncoding(string encoding)
        {
            try { return Encoding.GetEncoding(encoding); }
            catch (ArgumentException)
            {
                throw new XPathException("Unknown encoding " + encoding, "XQST0087");
            }
        }

        // Read the query text from a Reader, validating XML characters (XPST0003 on a bad character).
        private static string ReadQueryFromReader(TextReader reader, IIntPredicateProxy charChecker)
        {
            try
            {
                UnicodeString content = UnparsedTextFunction.ReadFile(charChecker, reader);
                return content.ToString();
            }
            catch (XPathException err)
            {
                err.SetErrorCode("XPST0003");
                err.SetIsStaticError(true);
                throw;
            }
            catch (IOException ioErr)
            {
                throw new XPathException("Failed to read supplied query file: " + ioErr.Message);
            }
        }
    }
}
