////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.Text;
namespace OutSmart.DAXon.Text
{
    internal sealed class StringConstants
    {

        public static readonly UnicodeString SINGLE_SPACE = new Twine8(Bytes(" "));
        public static readonly UnicodeString TRUE = new Twine8(Bytes("true"));
        public static readonly UnicodeString FALSE = new Twine8(Bytes("false"));
        public static readonly UnicodeString ZERO_TO_NINE = new Twine8(Bytes("0123456789"));
        public static readonly UnicodeString MIN_LONG = new Twine8(Bytes("-9223372036854775808"));
        public static readonly UnicodeString POINT_ZERO = new Twine8(Bytes(".0"));
        public static readonly UnicodeString ASTERISK = new Twine8(Bytes("*"));
        public static readonly byte[] COMMENT_START = Bytes("<!--");
        public static readonly byte[] COMMENT_END = Bytes("-->");
        public static readonly byte[] TWO_HYPHENS = Bytes("--");
        public static readonly byte[] PI_START = Bytes("<?");
        public static readonly byte[] PI_END = Bytes("?>");
        public static readonly byte[] EMPTY_TAG_MIDDLE = Bytes("></");
        public static readonly byte[] EMPTY_TAG_END = Bytes("/>");
        public static readonly byte[] EMPTY_TAG_END_XHTML = Bytes(" />");
        public static readonly byte[] END_TAG_START = Bytes("</");
        public static readonly byte[] ESCAPE_LT = Bytes("&lt;");
        public static readonly byte[] ESCAPE_GT = Bytes("&gt;");
        public static readonly byte[] ESCAPE_AMP = Bytes("&amp;");
        public static readonly byte[] ESCAPE_NL = Bytes("&#xA;");
        public static readonly byte[] ESCAPE_CR = Bytes("&#xD;");
        public static readonly byte[] ESCAPE_TAB = Bytes("&#x9;");
        public static readonly byte[] ESCAPE_QUOT = Bytes("&#34;");
        public static readonly byte[] ESCAPE_APOS = Bytes("&#39;");
        public static readonly byte[] ESCAPE_NBSP = Bytes("&nbsp;");
        public static byte[] Bytes(string s)
        {
            return Encoding.ASCII.GetBytes(s);
        }
    }
}