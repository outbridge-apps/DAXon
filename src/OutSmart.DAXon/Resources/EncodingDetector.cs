////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Transformation;
using System;
using System.Text;

namespace OutSmart.DAXon.Resources
{
    // Infers the encoding of a text resource from its first bytes: a byte order mark, the encoding
    // named by an XML declaration, a UTF-16 zero-byte pattern, else the default.
    internal static class EncodingDetector
    {
        private const int Peek = 100;

        // Names are ones .NET resolves: upstream's "UTF-16" means big-endian to Java, little-endian here.
        public static string InferEncoding(byte[] start, int read, string defaultEncoding)
        {
            read = Math.Min(read, Math.Min(start.Length, Peek));
            if (read >= 2)
            {
                if (start[0] == 0xFE && start[1] == 0xFF)
                {
                    return "UTF-16BE";
                }
                else if (start[0] == 0xFF && start[1] == 0xFE)
                {
                    return "UTF-16LE";
                }
            }

            if (read >= 3 && start[0] == 0xEF && start[1] == 0xBB && start[2] == 0xBF)
            {
                return "UTF-8";
            }

            if (read >= 5 && start[0] == '<' && start[1] == '?' && start[2] == 'x' && start[3] == 'm' && start[4] == 'l')
            {
                string p = Encoding.GetEncoding("ISO-8859-1").GetString(start, 0, read);
                int v = p.IndexOf("encoding", StringComparison.Ordinal);
                if (v >= 0)
                {
                    v += 8;
                    while (v < p.Length && " \n\r\t=\"'".IndexOf(p[v]) >= 0)
                    {
                        v++;
                    }

                    int end = v;
                    while (end < p.Length && p[end] != '"' && p[end] != '\'')
                    {
                        end++;
                    }

                    return p.Substring(v, end - v);
                }
            }
            else if (read >= 8)
            {
                // Upstream tests these only when fewer than 4 bytes were read, so they never fire there.
                if (start[0] == 0 && start[2] == 0 && start[4] == 0 && start[6] == 0)
                {
                    return "UTF-16BE";
                }
                else if (start[1] == 0 && start[3] == 0 && start[5] == 0 && start[7] == 0)
                {
                    return "UTF-16LE";
                }
            }

            return defaultEncoding;
        }

        // The platform encoding for a name, as an XPathException when the name is unknown.
        public static Encoding Lookup(string name, bool strict)
        {
            try
            {
                return strict
                    ? Encoding.GetEncoding(name, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback)
                    : Encoding.GetEncoding(name);
            }
            catch (ArgumentException)
            {
                throw new XPathException("Unsupported encoding " + name, "FODC0002");
            }
        }

        // A byte order mark is not content, as for unparsed-text(); Java's decoders keep a UTF-8 one.
        public static int BomLength(byte[] bytes, int offset, int len, Encoding encoding)
        {
            byte[] bom = encoding.GetPreamble();
            if (bom.Length == 0 || len < bom.Length)
            {
                return 0;
            }

            for (int i = 0; i < bom.Length; i++)
            {
                if (bytes[offset + i] != bom[i])
                {
                    return 0;
                }
            }

            return bom.Length;
        }
    }
}
