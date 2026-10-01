////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System.Collections.Generic;
using System.Text;

namespace OutSmart.DAXon.Serialization.CharCodes
{
    // Faithful port of net.sf.saxon.serialize.charcode.JavaCharacterSet (Saxon 12.9). Was a hollow stub, so
    // serializing with ANY encoding outside the built-in table (us-ascii/iso-8859-1/utf) crashed.
    // Probes the platform encoder for encodability, caching per-BMP-character results.
    internal sealed class JavaCharacterSet : ICharacterSet
    {
        private const byte GOOD = 1;
        private const byte BAD = 2;
        private static Dictionary<string, JavaCharacterSet> map;

        private readonly Encoding encoder;
        // This class is written on the assumption that the encodability probe may be expensive. For BMP
        // characters it remembers the results so each character is only looked up the first time.
        private readonly byte[] charinfo = new byte[65536];

        public string CanonicalName => encoder.WebName;

        private JavaCharacterSet(Encoding encoding)
        {
            encoder = (Encoding)encoding.Clone();
            encoder.EncoderFallback = EncoderFallback.ExceptionFallback;
        }

        public static JavaCharacterSet MakeCharSet(Encoding encoding)
        {
            lock (typeof(JavaCharacterSet))
            {
                if (map == null)
                {
                    map = new Dictionary<string, JavaCharacterSet>(10);
                }

                JavaCharacterSet c;
                if (!map.TryGetValue(encoding.WebName, out c))
                {
                    c = new JavaCharacterSet(encoding);
                    map[encoding.WebName] = c;
                }

                return c;
            }
        }

        private bool CanEncode(string s)
        {
            try
            {
                encoder.GetByteCount(s);
                return true;
            }
            catch (EncoderFallbackException)
            {
                return false;
            }
            catch (System.ArgumentException)
            {
                return false;
            }
        }

        public bool InCharset(int c)
        {
            // Assume ASCII chars are always OK
            if (c <= 127)
            {
                return true;
            }

            if (c <= 65535)
            {
                if (charinfo[c] == GOOD)
                {
                    return true;
                }
                else if (charinfo[c] == BAD)
                {
                    return false;
                }
                else
                {
                    bool ok = CanEncode(((char)c).ToString());
                    charinfo[c] = ok ? GOOD : BAD;
                    return ok;
                }
            }
            else
            {
                return CanEncode(char.ConvertFromUtf32(c));
            }
        }
    }
}
