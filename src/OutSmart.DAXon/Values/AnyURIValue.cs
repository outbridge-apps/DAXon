////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Types;
using System.Collections.Generic;
using System.Text;
using OutSmart.DAXon.Text;
namespace OutSmart.DAXon.Values
{
    internal sealed class AnyURIValue : StringValue
    {

        public override BuiltInAtomicType PrimitiveType => BuiltInAtomicType.ANY_URI;
        public AnyURIValue(UnicodeString value) : base(value == null ? EmptyUnicodeString.GetInstance() : Whitespace.CollapseWhitespace(value), BuiltInAtomicType.ANY_URI)
        {
        }

        public AnyURIValue(string value) : this(StringView.Tidy(value))
        {
        }

        public AnyURIValue(UnicodeString value, IAtomicType type) : base(value == null ? "" : Whitespace.CollapseWhitespace(value).ToString(), type)
        {
        }

        public override AtomicValue CopyAsSubType(IAtomicType typeLabel)
        {
            return new AnyURIValue(this.UnicodeStringValue, typeLabel);
        }

        public StringValue ConvertToString()
        {
            return new StringValue(Content, BuiltInAtomicType.STRING);
        }

        public static string Decode(string s)
        {

            // Evaluates all escapes in s, applying UTF-8 decoding if needed.  Assumes
            // that escapes are well-formed syntactically, i.e., of the form %XX.  If a
            // sequence of escaped octets is not valid UTF-8 then the erroneous octets
            // are replaced with '\uFFFD'.
            // Exception: any "%" found between "[]" is left alone. It is an IPv6 literal
            //            with a scope_id
            // A '%' with fewer than two characters after it is kept literally: the previous
            // version indexed past the end of the string, and its Java-shim decoder was a
            // hollow stub that turned EVERY escape into the empty string.
            if (s == null)
            {
                return null;
            }

            int n = s.Length;
            if (n == 0 || s.IndexOf('%') < 0)
            {
                return s;
            }

            StringBuilder sb = new StringBuilder(n);
            var octets = new List<byte>(8);
            bool betweenBrackets = false;
            for (int i = 0; i < n;)
            {
                char c = s[i];
                if (c == '[')
                {
                    betweenBrackets = true;
                }
                else if (betweenBrackets && c == ']')
                {
                    betweenBrackets = false;
                }

                if (c != '%' || betweenBrackets)
                {
                    sb.Append(c);
                    i++;
                    continue;
                }

                if (i + 2 >= n)
                {
                    // "%" or "%X" at the very end: keep it literally instead of reading past the string
                    sb.Append(s, i, n - i);
                    break;
                }

                // Collect the octets of consecutive %XX escapes, then UTF-8-decode them together
                // (a multi-byte character spans several escapes); invalid sequences become U+FFFD.
                octets.Clear();
                while (i + 2 < n && s[i] == '%')
                {
                    octets.Add(Hex(s[i + 1], s[i + 2]));
                    i += 3;
                }

                sb.Append(Encoding.UTF8.GetString(octets.ToArray()));
            }

            return sb.ToString();
        }

        //
        // Loop invariant
        private static byte Hex(char high, char low)
        {
            return (byte)((HexToDec(high) << 4) | HexToDec(low));
        }

        private static int HexToDec(char c)
        {
            if (c >= '0' && c <= '9')
            {
                return c - '0';
            }
            else if (c >= 'a' && c <= 'f')
            {
                return c - 'a' + 10;
            }
            else if (c >= 'A' && c <= 'F')
            {
                return c - 'A' + 10;
            }
            else
            {
                return 0;
            }
        }
    }
}