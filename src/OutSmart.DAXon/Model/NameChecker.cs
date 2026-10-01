////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Serialization.CharCodes;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Model
{
    internal abstract class NameChecker
    {
        public static bool IsQName(IIntIterator codePoints)
        {
            bool atStart = true;
            bool foundColon = false;
            while (codePoints.MoveNext())
            {
                int ch = codePoints.Current;
                if (ch == ':')
                {
                    if (atStart || foundColon)
                    {
                        return false;
                    }

                    atStart = true;
                    foundColon = true;
                }
                else
                {
                    if (atStart)
                    {
                        if (!IsNCNameStartChar(ch))
                        {
                            return false;
                        }

                        atStart = false;
                    }
                    else
                    {
                        if (!IsNCNameChar(ch))
                        {
                            return false;
                        }
                    }
                }
            }

            return !atStart;
        }

        public static String[] GetQNameParts(string qname)
        {
            string[] parts = new string[2];
            int len = qname.Length;
            int colon = qname.IndexOf(':', 0);
            if (colon < 0)
            {
                parts[0] = "";
                parts[1] = qname;
                if (!IsValidNCName(StringTool.CodePoints(qname)))
                {
                    throw new QNameException("Invalid QName " + Err.Wrap(qname));
                }
            }
            else
            {
                if (colon == 0)
                {
                    throw new QNameException("QName cannot start with colon: " + Err.Wrap(qname));
                }

                if (colon == len - 1)
                {
                    throw new QNameException("QName cannot end with colon: " + Err.Wrap(qname));
                }

                parts[0] = qname.Substring(0, colon);
                parts[1] = qname.Substring(colon + 1);
                if (!IsValidNCName(parts[1]))
                {
                    if (!IsValidNCName(parts[0]))
                    {
                        throw new QNameException("Both the prefix " + Err.Wrap(parts[0]) + " and the local part " + Err.Wrap(parts[1]) + " are invalid");
                    }

                    throw new QNameException("Invalid QName local part " + Err.Wrap(parts[1]));
                }
            }

            return parts;
        }

        // GetQNameParts without the exception, null where it throws: `castable as xs:QName` on text
        // that is not a QName cost ~31 us a test through QNameException.
        public static string[] TryGetQNameParts(string qname)
        {
            int colon = qname.IndexOf(':');
            if (colon < 0)
            {
                return IsValidNCName(qname) ? new[] { "", qname } : null;
            }

            if (colon == 0 || colon == qname.Length - 1)
            {
                return null;
            }

            string local = qname.Substring(colon + 1);
            return IsValidNCName(local) ? new[] { qname.Substring(0, colon), local } : null;
        }

        public static String[] CheckQNameParts(string qname)
        {
            try
            {
                string[] parts = GetQNameParts(qname);
                if (parts[0].Length > 0 && !IsValidNCName(parts[0]))
                {
                    throw new XPathException("Invalid QName prefix " + Err.Wrap(parts[0]));
                }

                return parts;
            }
            catch (QNameException e)
            {
                throw new XPathException(e.GetMessage(), "FORG0001");
            }
        }

        public static bool IsValidNCName(IIntIterator codePoints)
        {
            bool first = true;
            while (codePoints.MoveNext())
            {
                int ch = codePoints.Current;
                if (first)
                {
                    if (!IsNCNameStartChar(ch))
                    {
                        return false;
                    }

                    first = false;
                }
                else
                {
                    if (!IsNCNameChar(ch))
                    {
                        return false;
                    }
                }
            }

            return !first;
        }

        public static bool IsValidNCName(string str)
        {
            return IsValidNCName(StringTool.CodePoints(str));
        }

        public static bool IsValidNmtoken(UnicodeString @in)
        {
            IIntIterator codePoints = @in.CodePoints();
            bool empty = true;
            while (codePoints.MoveNext())
            {
                int ch = codePoints.Current;
                empty = false;
                if (ch != ':' && !IsNCNameChar(ch))
                {
                    return false;
                }
            }

            return !empty;
        }

        public static bool IsNCNameChar(int ch)
        {
            return XMLCharacterData.IsNCName11(ch);
        }

        public static bool IsNCNameStartChar(int ch)
        {
            return XMLCharacterData.IsNCNameStart11(ch);
        }
    }
}