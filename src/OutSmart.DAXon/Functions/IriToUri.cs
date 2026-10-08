////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Collections;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// This class supports the functions encode-for-uri() and iri-to-uri()
    /// </summary>
    internal sealed class IriToUri : ScalarSystemFunction
    {
        public static bool[] allowedASCII = new bool[128];
        static IriToUri()
        {
            ArrayTools.Fill(allowedASCII, 0, 32, false);
            ArrayTools.Fill(allowedASCII, 33, 127, true);
            allowedASCII[(int)'"'] = false;
            allowedASCII[(int)'<'] = false;
            allowedASCII[(int)'>'] = false;
            allowedASCII[(int)'\\'] = false;
            allowedASCII[(int)'^'] = false;
            allowedASCII[(int)'`'] = false;
            allowedASCII[(int)'{'] = false;
            allowedASCII[(int)'|'] = false;
            allowedASCII[(int)'}'] = false;
        }

        public override AtomicValue Evaluate(IItem arg, IXPathContext context)
        {
            return new StringValue(IriToUriFn(arg.UnicodeStringValue));
        }

        public override ISequence ResultWhenEmpty()
        {
            return StringValue.EMPTY_STRING;
        }

        public static UnicodeString IriToUriFn(UnicodeString s)
        {

            // NOTE: implements a late spec change which says that characters that are illegal in an IRI,
            // for example "\", must be %-encoded.
            if (AllAllowedAscii(s.CodePoints()))
            {

                // it's worth doing a prescan to avoid the cost of copying in the common all-ASCII case
                return s;
            }

            UnicodeBuilder sb = new UnicodeBuilder(s.Length32() + 20);
            IIntIterator iter = s.CodePoints();
            while (iter.MoveNext())
            {
                int c = iter.Current;
                if (c >= 0x7f || !allowedASCII[(int)c])
                {
                    EncodeForUri.EscapeChar(c, sb);
                }
                else
                {
                    sb.Append(c);
                }
            }

            return sb.ToUnicodeString();
        }

        private static bool AllAllowedAscii(IIntIterator codePoints)
        {
            while (codePoints.MoveNext())
            {
                int c = codePoints.Current;
                if (c >= 0x7f || !allowedASCII[(int)c])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
