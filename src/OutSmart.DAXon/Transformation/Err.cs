////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Serialization.CharCodes;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Collections;
using System;
using System.Globalization;
using System.Text;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Transformation
{
    /// <summary>
    /// Class containing utility methods for handling error messages
    /// </summary>
    internal sealed class Err
    {
        public const int ELEMENT = 1;
        public const int ATTRIBUTE = 2;
        public const int FUNCTION = 3;
        public const int VALUE = 4;
        public const int VARIABLE = 5;
        public const int GENERAL = 6;
        public const int URI = 7;
        public const int EQNAME = 8;
        public static string Wrap(UnicodeString cs)
        {
            return Wrap(cs, GENERAL);
        }

        public static string Wrap(string cs)
        {
            return Wrap(cs, GENERAL);
        }

        public static string Wrap(string cs, int valueType)
        {
            return Wrap(StringView.Of(cs), valueType);
        }

        public static string Wrap(UnicodeString cs, int valueType)
        {
            if (cs == null)
            {
                return "(NULL)";
            }

            StringBuilder sb = new StringBuilder(64);
            IIntIterator iter = cs.CodePoints();
            int len = 0;
            while (iter.MoveNext())
            {
                int c = iter.Current;
                len++;
                switch (c)
                {
                    case '\n':
                        sb.Append("\\n");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    case '\r':
                        sb.Append("\\r");
                        break;
                    default:
                        if (c < 32)
                        {
                            sb.Append("\\x");
                            sb.Append((c).ToString("x", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            sb.AppendCodePoint(c);
                        }

                        break;
                }
            }

            string s;
            if (valueType == ELEMENT || valueType == ATTRIBUTE)
            {
                s = sb.ToString();
                if (s.StartsWith("{", StringComparison.Ordinal))
                {
                    s = "Q" + s;
                }

                if (s.StartsWith("Q{", StringComparison.Ordinal))
                {
                    try
                    {
                        StructuredQName qn = StructuredQName.FromEQName(sb.ToString());
                        string uri = AbbreviateURI(qn.GetNamespaceUri());
                        s = "Q{" + uri + "}" + qn.GetLocalPart();
                    }
                    catch (Exception)
                    {
                        s = sb.ToString();
                    }
                }
            }
            else if (valueType == URI)
            {
                s = AbbreviateURI(sb.ToString());
            }
            else if (valueType == EQNAME)
            {
                s = AbbreviateEQName(sb.ToString());
            }
            else
            {
                s = len > 30 ? sb.ToString().Substring(0, 30) + "..." : sb.ToString();
            }

            switch (valueType)
            {
                case ELEMENT:
                    return "<" + s + ">";
                case ATTRIBUTE:
                    return "@" + s;
                case FUNCTION:
                    return s + "()";
                case VARIABLE:
                    return "$" + s;
                case VALUE:
                    return "\"" + s + "\"";
                case EQNAME:
                    return s;
                default:
                    return "{" + s + "}";
            }
        }

        // Depiction bounds BREADTH already (first few array members, maps over 5 entries elided)
        // but nested arrays and maps recurse, so it has to bound DEPTH too. A cap rather than a
        // stack probe: this runs while another error message is being built - MapItem.Atomize
        // embeds a depiction in FOTY0013 - so failing here would replace the real diagnosis with
        // a stack error. Well above anything a legible message shows.
        private const int MaxDepictDepth = 20;

        [ThreadStatic]
        private static int depictDepth;

        /// <summary>Enter one level of value depiction. Pair with <see cref="LeaveDepiction"/>
        /// in a finally: an abandoned counter would elide every later message on this thread.</summary>
        internal static void EnterDepiction()
        {
            depictDepth++;
        }

        internal static void LeaveDepiction()
        {
            depictDepth--;
        }

        internal static bool DepictionTooDeep => depictDepth > MaxDepictDepth;

        /// <summary>
        /// Create a string representation of an item for use in an error message
        /// </summary>
        public static string Depict(IItem item)
        {
            if (item == null)
            {
                return "(*null*)";
            }

            EnterDepiction();
            try
            {
                return DepictItem(item);
            }
            finally
            {
                LeaveDepiction();
            }
        }

        private static string DepictItem(IItem item)
        {
            if (DepictionTooDeep)
            {
                return "...";
            }

            if (item is NodeInfo)
            {
                NodeInfo node = (NodeInfo)item;
                switch (node.GetNodeKind())
                {
                    case Types.Type.DOCUMENT:
                        return "doc(" + AbbreviateURI(node.GetSystemId()) + ')';
                    case Types.Type.ELEMENT:
                        return '<' + node.DisplayName + '>';
                    case Types.Type.ATTRIBUTE:
                        return '@' + node.DisplayName + "=\"" + node.UnicodeStringValue + '"';
                    case Types.Type.TEXT:
                        return "text{" + Truncate30(node.UnicodeStringValue) + "}";
                    case Types.Type.COMMENT:
                        return "<!--...-->";
                    case Types.Type.PROCESSING_INSTRUCTION:
                        return "<?" + node.GetLocalPart() + "...?>";
                    case Types.Type.NAMESPACE:
                        return "xmlns:" + node.GetLocalPart() + "=" + AbbreviateURI(node.GetStringValue());
                    default:
                        return "";
                }
            }
            else
            {
                return item.ToShortString();
            }
        }

        public static string DepictCodepoint(int cp)
        {
            string hexCode = "#x" + (cp).ToString("x", CultureInfo.InvariantCulture);
            if (cp >= 20 && cp < UTF16CharacterSet.SURROGATE1_MIN)
            {
                return "'" + (char)cp + "'(" + hexCode + ")";
            }
            else
            {
                return hexCode;
            }
        }

        public static string DepictSequence(ISequence seq)
        {
            if (seq == null)
            {
                return "(*null*)";
            }

            try
            {
                if (seq is IGroundedValue)
                {
                    IGroundedValue val = (IGroundedValue)seq;
                    if (val.GetLength() == 0)
                    {
                        return "()";
                    }
                    else if (val.GetLength() == 1)
                    {
                        return Depict(seq.Head());
                    }
                    else
                    {
                        return DepictSequenceStart(val.Iterate(), 3, val.GetLength());
                    }
                }
                else if (seq is SingletonClosure)
                {
                    SingletonClosure sc = (SingletonClosure)seq;
                    if (sc.IsBuilt())
                    {
                        return sc.AsItem() == null ? "()" : Depict(sc.AsItem());
                    }
                    else
                    {
                        return "(*not-yet-evaluated singleton*)";
                    }
                }
                else if (seq is MemoClosure)
                {
                    MemoClosure mc = (MemoClosure)seq;
                    seq = mc.SequenceAsIs;
                    if (seq == null)
                    {
                        return "(*not-yet-evaluated sequence*)";
                    }
                    else
                    {
                        return DepictSequence(seq);
                    }
                }
                else
                {
                    return "(*lazily evaluated*)";
                }
            }
            catch (Exception)
            {
                // Swallows everything DELIBERATELY, RecursionDepthError included: a depiction
                // runs while another error is being built, so degrading here beats replacing
                // the real diagnosis - and the guard's margin lets that error finish building.
                return "(*unreadable*)";
            }
        }

        public static string DepictSequenceStart(ISequenceIterator seq, int max, int actual)
        {
            StringBuilder sb = new StringBuilder(64);
            int count = 0;
            sb.Append(" (");
            IItem next;
            while ((next = seq.Next()) != null)
            {
                if (count++ > 0)
                {
                    sb.Append(", ");
                }

                if (count > max)
                {
                    sb.Append("... [" + actual + "])");
                    return sb.ToString();
                }

                sb.Append(Err.Depict(next));
            }

            sb.Append(") ");
            return sb.ToString();
        }

        public static UnicodeString Truncate30(UnicodeString cs)
        {
            if (cs.Length() <= 30)
            {
                return Whitespace.CollapseWhitespace(cs);
            }
            else
            {
                return Whitespace.CollapseWhitespace(cs.Substring(0, 30)).Concat(BMPString.Of("..."));
            }
        }

        public static string AbbreviateURI(string uri)
        {
            if (uri == null)
            {
                return "";
            }

            int lastSlash = (uri.EndsWith("/", StringComparison.Ordinal) ? uri.Substring(0, uri.Length - 1) : uri).LastIndexOf('/');
            if (lastSlash < 0)
            {
                if (uri.Length > 15)
                {
                    uri = "..." + uri.Substring(uri.Length - 15);
                }

                return uri;
            }
            else
            {
                return "..." + uri.Substring(lastSlash);
            }
        }

        public static string AbbreviateURI(NamespaceUri uri)
        {
            return AbbreviateURI(uri.ToString());
        }

        public static string AbbreviateEQName(string eqName)
        {
            try
            {
                if (eqName.StartsWith("{", StringComparison.Ordinal))
                {
                    eqName = "Q" + eqName;
                }

                StructuredQName sq = StructuredQName.FromEQName(eqName);
                return "Q{" + AbbreviateURI(sq.GetNamespaceUri()) + "}" + sq.GetLocalPart();
            }
            catch (Exception)
            {
                return eqName;
            }
        }

        public static string DescribeGenre(Genre genre)
        {
            switch (genre)
            {
                case Genre.ANY:
                    return "any item";
                case Genre.ATOMIC:
                    return "an atomic value";
                case Genre.NODE:
                    return "a node";
                case Genre.FUNCTION:
                    return "a function";
                case Genre.MAP:
                    return "a map";
                case Genre.ARRAY:
                    return "an array";
                case Genre.EXTERNAL:
                default:
                    return "an external object";
            }
        }

        public static string DescribeVisibility(Visibility vis)
        {
            return vis.ToString().ToLowerInvariant();
        }

        public static string Show(ILocation loc)
        {
            return AbbreviateURI(loc.GetSystemId()) + "#" + loc.GetLineNumber();
        }

        public static string IndefiniteArticleFor(string s, bool caps)
        {
            if ("aeioux".IndexOf(s[0]) >= 0)
            {
                return (caps ? "An" : "an");
            }
            else
            {
                return (caps ? "A" : "a");
            }
        }
    }
}