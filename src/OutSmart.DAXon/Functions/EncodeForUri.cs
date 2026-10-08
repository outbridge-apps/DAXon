////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Serialization.CharCodes;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Collections;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// This class supports the function fn:encode-for-uri()
    /// </summary>
    internal sealed class EncodeForUri : ScalarSystemFunction
    {

        private static readonly string hex = "0123456789ABCDEF";

        // Length of a UTF8 byte sequence, as a function of the first nibble
        private static readonly int[] UTF8RepresentationLength = new[]
        {
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            1,
            -1,
            -1,
            -1,
            -1,
            2,
            2,
            3,
            4
        };
        public override AtomicValue Evaluate(IItem arg, IXPathContext context)
        {
            UnicodeString s = arg.UnicodeStringValue;
            return Escape(s, "-_.~");
        }

        public override ISequence ResultWhenEmpty()
        {
            return StringValue.EMPTY_STRING;
        }

        public static StringValue Escape(UnicodeString s, string allowedPunctuation)
        {
            s = s.Tidy();
            UnicodeBuilder sb = new UnicodeBuilder(s.Length32() + 20);
            IIntIterator iter = s.CodePoints();
            while (iter.MoveNext())
            {
                int c = iter.Current;
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
                {
                    sb.Append(c);
                }
                else if (c <= 0x20 || c >= 0x7f)
                {
                    EscapeChar(c, sb);
                }
                else if (allowedPunctuation.IndexOf((char)c) >= 0)
                {
                    sb.Append(c);
                }
                else
                {
                    EscapeChar(c, sb);
                }
            }

            return new StringValue(sb.ToUnicodeString());
        }
        public static void EscapeChar(int cp, UnicodeBuilder sb)
        {
            byte[] array = UTF8CharacterSet.Encode(new IntSingletonIterator(cp));
            foreach (byte value in array)
            {
                int v = (int)value & 0xff;
                sb.Append('%').Append(hex[v / 16]).Append(hex[v % 16]);
            }
        }

        // Only reached from UnparsedTextFunction.GetAbsoluteURI (unparsed-text / json-doc): a malformed
        // %-escape is the unparsed-text "invalid URI" error FOUT1170 (json-doc-error-029).
        public static void CheckPercentEncoding(string uri)
        {
            string hexDigits = "0123456789abcdefABCDEF";
            for (int i = 0; i < uri.Length;)
            {
                char c = uri[i];
                byte[] bytes;

                // Note: we're translating the UTF-8 byte sequence but then not using the value
                int expectedOctets;
                if (c == '%')
                {
                    if (i + 2 >= uri.Length)
                    {
                        throw new XPathException("% sign in URI must be followed by two hex digits" + Err.Wrap(uri), "FOUT1170");
                    }

                    int h1 = hexDigits.IndexOf(uri[i + 1]);
                    if (h1 > 15)
                    {
                        h1 -= 6;
                    }

                    int h2 = hexDigits.IndexOf(uri[i + 2]);
                    if (h2 > 15)
                    {
                        h2 -= 6;
                    }

                    if (h1 >= 0 && h2 >= 0)
                    {
                        int b = h1 << 4 | h2;
                        expectedOctets = UTF8RepresentationLength[h1];
                        if (expectedOctets == -1)
                        {
                            throw new XPathException("First %-encoded octet in URI is not valid as the start of a UTF-8 " + "character: first two bits must not be '10'" + Err.Wrap(uri), "FOUT1170");
                        }

                        bytes = new byte[expectedOctets];
                        bytes[0] = (byte)b;
                        i += 3;
                        for (int q = 1; q < expectedOctets; q++)
                        {
                            if (i + 2 > uri.Length || uri[i] != '%')
                            {
                                throw new XPathException("Incomplete %-encoded UTF-8 octet sequence in URI " + Err.Wrap(uri), "FOUT1170");
                            }

                            h1 = hexDigits.IndexOf(uri[i + 1]);
                            if (h1 > 15)
                            {
                                h1 -= 6;
                            }

                            h2 = hexDigits.IndexOf(uri[i + 2]);
                            if (h2 > 15)
                            {
                                h2 -= 6;
                            }

                            if (h1 < 0 || h2 < 0)
                            {
                                throw new XPathException("Invalid %-encoded UTF-8 octet sequence in URI" + Err.Wrap(uri), "FOUT1170");
                            }

                            if (UTF8RepresentationLength[h1] != -1)
                            {
                                throw new XPathException("In a URI, a %-encoded UTF-8 octet after the first " + "must have '10' as the first two bits" + Err.Wrap(uri), "FOUT1170");
                            }

                            b = h1 << 4 | h2;
                            bytes[q] = (byte)b;
                            i += 3;
                        }
                    }
                    else
                    {
                        throw new XPathException("% sign in URI must be followed by two hex digits" + Err.Wrap(uri), "FOUT1170");
                    }
                }
                else
                {
                    i++;
                }
            }
        }
    }
}