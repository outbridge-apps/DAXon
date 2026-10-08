////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Text;
using System.IO;
namespace OutSmart.DAXon.Serialization
{
    /// <summary>
    /// This class generates HTML output
    /// </summary>
    internal abstract class HTMLEmitter : XMLEmitter
    {

        static readonly HTMLTagHashSet emptyTags = new HTMLTagHashSet(31);

        private static readonly HTMLTagHashSet booleanAttributes = new HTMLTagHashSet(43);
        private static readonly HTMLTagHashSet booleanCombinations = new HTMLTagHashSet(57);
        private int inScript;
        protected int version = 5;
        private string parentElement;
        private bool escapeNonAscii = false;
        private readonly Stack<INodeName> nodeNameStack = new Stack<INodeName>();
        static HTMLEmitter()
        {
            SetBooleanAttribute("*", "hidden"); // HTML5
            SetBooleanAttribute("area", "nohref");
            SetBooleanAttribute("audio", "autoplay"); // HTML5
            SetBooleanAttribute("audio", "controls"); // HTML5
            SetBooleanAttribute("audio", "loop"); // HTML5
            SetBooleanAttribute("audio", "muted"); // HTML5
            SetBooleanAttribute("button", "disabled");
            SetBooleanAttribute("button", "autofocus"); // HTML5
            SetBooleanAttribute("button", "formnovalidate"); //HTML5
            SetBooleanAttribute("details", "open"); // HTML5
            SetBooleanAttribute("dialog", "open"); // HTML5
            SetBooleanAttribute("dir", "compact");
            SetBooleanAttribute("dl", "compact");
            SetBooleanAttribute("fieldset", "disabled"); //HTML5
            SetBooleanAttribute("form", "novalidate"); // HTML5
            SetBooleanAttribute("frame", "noresize");
            SetBooleanAttribute("hr", "noshade");
            SetBooleanAttribute("img", "ismap");
            SetBooleanAttribute("input", "checked");
            SetBooleanAttribute("input", "disabled");
            SetBooleanAttribute("input", "multiple"); //HTML5
            SetBooleanAttribute("input", "readonly");
            SetBooleanAttribute("input", "required"); //HTML5
            SetBooleanAttribute("input", "autofocus"); // HTML5
            SetBooleanAttribute("input", "formnovalidate"); //HTML5
            SetBooleanAttribute("iframe", "seamless"); // HTML5
            SetBooleanAttribute("keygen", "autofocus"); // HTML5
            SetBooleanAttribute("keygen", "disabled"); //HTML5
            SetBooleanAttribute("menu", "compact");
            SetBooleanAttribute("object", "declare");
            SetBooleanAttribute("object", "typemustmatch"); // HTML5
            SetBooleanAttribute("ol", "compact");
            SetBooleanAttribute("ol", "reversed"); // HTML5
            SetBooleanAttribute("optgroup", "disabled");
            SetBooleanAttribute("option", "selected");
            SetBooleanAttribute("option", "disabled");
            SetBooleanAttribute("script", "defer");
            SetBooleanAttribute("script", "async"); // HTML5
            SetBooleanAttribute("select", "multiple");
            SetBooleanAttribute("select", "disabled");
            SetBooleanAttribute("select", "autofocus"); // HTML5
            SetBooleanAttribute("select", "required"); // HTML5
            SetBooleanAttribute("style", "scoped"); // HTML5
            SetBooleanAttribute("td", "nowrap");
            SetBooleanAttribute("textarea", "disabled");
            SetBooleanAttribute("textarea", "readonly");
            SetBooleanAttribute("textarea", "autofocus"); // HTML5
            SetBooleanAttribute("textarea", "required"); // HTML5
            SetBooleanAttribute("th", "nowrap");
            SetBooleanAttribute("track", "default"); // HTML5
            SetBooleanAttribute("ul", "compact");
            SetBooleanAttribute("video", "autoplay"); // HTML5
            SetBooleanAttribute("video", "controls"); // HTML5
            SetBooleanAttribute("video", "loop"); // HTML5
            SetBooleanAttribute("video", "muted"); // HTML5
        }
        protected static void SetEmptyTag(string tag)
        {
            emptyTags.Add(tag);
        }

        protected static bool IsEmptyTag(string tag)
        {
            return emptyTags.Contains(tag);
        }

        private static void SetBooleanAttribute(string element, string attribute)
        {
            booleanAttributes.Add(attribute);
            booleanCombinations.Add(element + '+' + attribute);
        }

        private static bool IsBooleanAttribute(string element, string attribute, string value)
        {
            return attribute.Equals(value, StringComparison.OrdinalIgnoreCase) && booleanAttributes.Contains(attribute) && (booleanCombinations.Contains(element + '+' + attribute) || booleanCombinations.Contains("*+" + attribute));
        }

        public override void SetEscapeNonAscii(bool escape)
        {
            escapeNonAscii = escape;
        }

        protected abstract bool IsHTMLElement(INodeName name);
        public override void Open()
        {
        }

        protected override void OpenDocument()
        {

            if (started)
            {
                return;
            }

            string byteOrderMark = outputProperties.GetProperty(DAXonOutputKeys.BYTE_ORDER_MARK);
            string encoding = outputProperties.GetProperty(DAXonOutputKeys.ENCODING);
            if (encoding == null || encoding.Equals("utf8", StringComparison.OrdinalIgnoreCase))
            {
                // no encoding named is UTF-8, as for the XML and text methods: the mark asked for was left out for it
                encoding = "UTF-8";
            }

            if (byteOrderMark == "yes" && ("UTF-8".Equals(encoding, StringComparison.OrdinalIgnoreCase) || "UTF-16LE".Equals(encoding, StringComparison.OrdinalIgnoreCase) || "UTF-16BE".Equals(encoding, StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    WriteByteOrderMark();
                }
                catch (IOException)
                {
                }
            }

            if (outputProperties.GetProperty(DAXonOutputKeys.SINGLE_QUOTES) == "yes")
            {
                delimiter = '\'';
                attSpecials = specialInAttSingle;
            }

            inScript = -1000000;
        }

        protected override void WriteDocType(INodeName name, string displayName, string systemId, string publicId)
        {
            base.WriteDocType(name, displayName, systemId, publicId);
        }

        public override void StartElement(INodeName elemName, ISchemaType type, IAttributeMap attributes, NamespaceMap namespaces, ILocation location, int properties)
        {
            base.StartElement(elemName, type, attributes, namespaces, location, properties);
            parentElement = elementStack.Peek();
            if (IsHTMLElement(elemName) && (parentElement.Equals("script", StringComparison.OrdinalIgnoreCase) || parentElement.Equals("style", StringComparison.OrdinalIgnoreCase)))
            {
                inScript = 0;
            }

            inScript++;
            nodeNameStack.Push(elemName);
        }
        protected override void WriteAttribute(INodeName elCode, string attname, string value, int properties)
        {
            try
            {
                if (IsHTMLElement(elCode))
                {
                    if (IsBooleanAttribute(elCode.GetLocalPart(), attname, value))
                    {
                        writer.Write(attname);
                        return;
                    }
                }

                if (inScript > 0)
                {
                    properties |= ReceiverOption.DISABLE_ESCAPING;
                }

                base.WriteAttribute(elCode, attname, value, properties);
            }
            catch (IOException err)
            {
                throw WriteFailure(err);
            }
        }

        protected override void WriteEscape(UnicodeString chars, bool inAttribute)
        {
            int segstart = 0;
            bool[] specialChars = inAttribute ? attSpecials : specialInText;
            if (chars is WhitespaceString)
            {
                ((WhitespaceString)chars).WriteEscape(specialChars, writer);
                return;
            }

            bool disabled = false;
            chars = chars.Tidy();
            int[] codePoints = StringTool.Expand(chars);
            while (segstart < codePoints.Length)
            {
                int i = segstart;

                // find a maximal sequence of "ordinary" characters
                if (escapeNonAscii)
                {
                    int c;
                    while (i < codePoints.Length && (c = codePoints[i]) < 127 && !specialChars[c])
                    {
                        i++;
                    }
                }
                else
                {
                    int c;
                    while (i < codePoints.Length && ((c = codePoints[i]) < 127 ? !specialChars[c] : (characterSet.InCharset(c) && c > 160)))
                    {
                        i++;
                    }
                }


                // if this was the whole string, output the string and quit
                if (i == codePoints.Length)
                {
                    writer.Write(chars.Substring(segstart));
                    return;
                }


                // otherwise, output this sequence and continue
                if (i > segstart)
                {
                    writer.Write(chars.Substring(segstart, i));
                }

                int ch = codePoints[i];
                if (ch == 0)
                {

                    // the expander's mark switches escaping on and off; U+0000 of the data is U+FFFD, which is
                    // what an HTML parser makes of one
                    if (marked)
                    {
                        disabled = !disabled;
                    }
                    else if (escapeNonAscii)
                    {
                        characterReferenceGenerator.OutputCharacterReference(0xFFFD, writer);
                    }
                    else
                    {
                        WriteReplacement();
                    }
                }
                else if (disabled)
                {
                    WriteCodePoint(ch);
                }
                else if (ch <= 127)
                {

                    // handle a special ASCII character
                    if (inAttribute)
                    {
                        if (ch == '<')
                        {
                            writer.WriteCodePoint('<'); // not escaped
                        }
                        else if (ch == '>')
                        {
                            writer.WriteAscii(StringConstants.ESCAPE_GT); // recommended for older browsers
                        }
                        else if (ch == '&')
                        {
                            if (i + 1 < codePoints.Length && codePoints[i + 1] == '{')
                            {
                                writer.WriteCodePoint('&'); // not escaped if followed by '{'
                            }
                            else
                            {
                                writer.WriteAscii(StringConstants.ESCAPE_AMP);
                            }
                        }
                        else if (ch == '"')
                        {
                            writer.WriteAscii(StringConstants.ESCAPE_QUOT);
                        }
                        else if (ch == '\'')
                        {
                            writer.WriteAscii(StringConstants.ESCAPE_APOS);
                        }
                        else if (ch == '\n')
                        {
                            writer.WriteAscii(StringConstants.ESCAPE_NL);
                        }
                        else if (ch == '\t')
                        {
                            writer.WriteAscii(StringConstants.ESCAPE_TAB);
                        }
                        else if (ch == '\r')
                        {
                            writer.WriteAscii(StringConstants.ESCAPE_CR);
                        }
                    }
                    else
                    {
                        if (ch == '<')
                        {
                            writer.WriteAscii(StringConstants.ESCAPE_LT);
                        }
                        else if (ch == '>')
                        {
                            writer.WriteAscii(StringConstants.ESCAPE_GT);
                        }
                        else if (ch == '&')
                        {
                            writer.WriteAscii(StringConstants.ESCAPE_AMP);
                        }
                        else if (ch == '\r')
                        {
                            writer.WriteAscii(StringConstants.ESCAPE_CR);
                        }
                    }
                }
                else if (ch < 160)
                {
                    if (RejectControlCharacters())
                    {

                        // these control characters are illegal in HTML
                        throw new XPathException("Illegal HTML character: decimal " + ch, "SERE0014");
                    }
                    else
                    {
                        characterReferenceGenerator.OutputCharacterReference(ch, writer);
                    }
                }
                else if (ch == 160)
                {

                    // always output NBSP as an entity reference
                    writer.WriteAscii(StringConstants.ESCAPE_NBSP);
                }
                else if (ch > 65535 || escapeNonAscii || !characterSet.InCharset(ch))
                {
                    characterReferenceGenerator.OutputCharacterReference(ch, writer);
                }
                else
                {
                    writer.WriteCodePoint(ch);
                }

                segstart = ++i;
            }
        }

        protected abstract bool RejectControlCharacters();

        private static readonly Func<int, bool> isNul = (c) => c == 0;

        // HTML takes a comment, an instruction and an identifier as they are given, but for U+0000: a parser reads
        // U+FFFD for one wherever it stands, and a file that holds one is no text to the tools that meet it first.
        private protected override UnicodeString Literal(UnicodeString chars)
        {
            return chars.IndexOf(0, 0) < 0 ? chars : Replaced(chars, isNul, false);
        }
        protected override void WriteEmptyElementTagCloser(string displayName, INodeName nameCode)
        {
            if (IsHTMLElement(nameCode))
            {
                writer.WriteAscii(StringConstants.EMPTY_TAG_MIDDLE);
                writer.Write(displayName);
                writer.WriteCodePoint('>');
            }
            else
            {
                writer.WriteAscii(StringConstants.EMPTY_TAG_END);
            }
        }

        /// <summary>
        /// Output an element end tag.
        /// </summary>
        public override void EndElement()
        {
            INodeName nodeName = nodeNameStack.Pop();
            string name = elementStack.Peek();
            inScript--;
            if (inScript == 0)
            {
                inScript = -1000000;
            }

            if (IsEmptyTag(name) && IsHTMLElement(nodeName))
            {
                if (openStartTag)
                {
                    CloseStartTag();
                }


                // no end tag required
                elementStack.Pop();
            }
            else
            {
                base.EndElement();
            }
        }

        /// <summary>
        /// Character data.
        /// </summary>
        public override void Characters(UnicodeString chars, ILocation locationId, int properties)
        {
            if (inScript > 0)
            {
                // The content of a script or a style goes out as it stands - but for U+0000 of the data, as in a
                // comment. Where the stylesheet itself disabled escaping it asked for every character; the marks of
                // a character map are taken out further on.
                if (!ReceiverOption.Contains(properties, ReceiverOption.DISABLE_ESCAPING) && !ReceiverOption.Contains(properties, ReceiverOption.USE_NULL_MARKERS) && chars.IndexOf(0, 0) >= 0)
                {
                    chars = Replaced(chars, isNul, true);
                }

                properties |= ReceiverOption.DISABLE_ESCAPING;
            }

            base.Characters(chars, locationId, properties);
        }

        /// <summary>
        /// Handle a processing instruction.
        /// </summary>
        public override void ProcessingInstruction(string target, UnicodeString data, ILocation locationId, int properties)
        {
            if (!started)
            {
                OpenDocument();
            }

            UnicodeString t = Literal(data.Tidy());
            if (t.IndexOf('>') >= 0)
            {
                throw new XPathException("A processing instruction in HTML must not contain a > character", "SERE0015");
            }

            try
            {
                if (openStartTag)
                {
                    CloseStartTag();
                }

                writer.WriteAscii(StringConstants.PI_START);
                writer.Write(target);
                writer.WriteCodePoint(' ');
                writer.Write(t);
                writer.WriteCodePoint('>');
            }
            catch (IOException err)
            {
                throw WriteFailure(err);
            }
        }
    }
}
