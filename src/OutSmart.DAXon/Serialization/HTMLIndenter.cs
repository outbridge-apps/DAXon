////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Collections;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Serialization
{
    internal sealed class HTMLIndenter : ProxyReceiver
    {
        private const int IS_INLINE = 1;
        private const int IS_FORMATTED = 2;
        private const int IS_SUPPRESSED = 4;
        private static readonly string[] formattedTags = new[]
        {
            "pre",
            "script",
            "style",
            "textarea",
            "title",
            "xmp"
        };
        // "xmp" is obsolete but still encountered!
        // When elements are classified as inline, indenting whitespace is not added adjacent to the element.
        // See Saxon bug 3839 and W3C bug 30276. We use a list of inline elements that is the union of
        // the HTML4 and HTML5 lists, on the basis that no harm is done treating an element as inline
        // even if the spec doesn't require us to do so. This also means we include elements such as
        // "ins", "del", and "area" that are sometimes inline and sometimes not.
        // "link" is excluded from inlineTags, see Saxon bug 3877
        private static readonly string[] inlineTags = new[]
        {
            "a",
            "abbr",
            "acronym",
            "applet",
            "area",
            "audio",
            "b",
            "basefont",
            "bdi",
            "bdo",
            "big",
            "br",
            "button",
            "canvas",
            "cite",
            "code",
            "data",
            "datalist",
            "del",
            "dfn",
            "em",
            "embed",
            "font",
            "i",
            "iframe",
            "img",
            "input",
            "ins",
            "kbd",
            "label",
            "map",
            "mark",
            "math",
            "meter",
            "noscript",
            "object",
            "output",
            "picture",
            "progress",
            "q",
            "ruby",
            "s",
            "samp",
            "script",
            "select",
            "small",
            "span",
            "strike",
            "strong",
            "sub",
            "sup",
            "svg",
            "template",
            "textarea",
            "time",
            "tt",
            "u",
            "var",
            "video",
            "wbr"
        };
        private static readonly HashSet<string> inlineTable = new HashSet<string>();
        private static readonly HashSet<string> formattedTable = new HashSet<string>();
        private int level;
        private bool sameLine;
        private bool inFormattedTag;
        private bool afterInline;
        private bool afterEndElement;
        private int[] propertyStack = new int[20];
        private HashSet<string> suppressed;

        /*!afterFormatted &&*/
        protected int LineLength => 80;

        /*!afterFormatted &&*/
        protected int Indentation => 3;
        static HTMLIndenter()
        {
            inlineTable.UnionWith(inlineTags);
            formattedTable.UnionWith(formattedTags);
        }
        public HTMLIndenter(IReceiver next, string method) : base(next)
        {
        }

        public void SetOutputProperties(Properties props)
        {
            string s = props.GetProperty(DAXonOutputKeys.SUPPRESS_INDENTATION);
            if (s != null)
            {
                suppressed = new HashSet<string>();
                foreach (string eqName in s.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    suppressed.Add(FingerprintedQName.FromEQName(eqName).GetLocalPart().ToLowerInvariant());
                }
            }
        }

        public int ClassifyTag(INodeName name)
        {
            int r = 0;
            if (inlineTable.Contains(name.GetLocalPart().ToLowerInvariant()))
            {
                r |= IS_INLINE;
            }

            if (formattedTable.Contains(name.GetLocalPart().ToLowerInvariant()))
            {
                r |= IS_FORMATTED;
            }

            if (suppressed != null && suppressed.Contains(name.GetLocalPart().ToLowerInvariant()))
            {
                r |= IS_SUPPRESSED;
            }

            return r;
        }

        public override void StartElement(INodeName elemName, ISchemaType type, IAttributeMap attributes, NamespaceMap namespaces, ILocation location, int properties)
        {
            int withinSuppressed = level == 0 ? 0 : (propertyStack[level - 1] & IS_SUPPRESSED);
            int tagProps = ClassifyTag(elemName) | withinSuppressed;
            if (level >= propertyStack.Length)
            {
                Array.Resize(ref propertyStack, level * 2);
            }

            propertyStack[level] = tagProps;
            bool inlineTag = (tagProps & IS_INLINE) != 0;
            if (!inlineTag && !inFormattedTag && !afterInline && withinSuppressed == 0 && level != 0)
            {
                Indent();
            }

            nextReceiver.StartElement(elemName, type, attributes, namespaces, location, properties);
            inFormattedTag = inFormattedTag || ((tagProps & IS_FORMATTED) != 0);
            level++;
            sameLine = true;
            afterInline = false;

            //afterFormatted = false;
            afterEndElement = false;
        }

        /*!afterFormatted &&*/
        /// <summary>
        /// Output element end tag
        /// </summary>
        public override void EndElement()
        {
            level--;
            bool thisInline = (propertyStack[level] & IS_INLINE) != 0;
            bool thisFormatted = (propertyStack[level] & IS_FORMATTED) != 0;
            bool thisSuppressed = (propertyStack[level] & IS_SUPPRESSED) != 0;
            if (afterEndElement && !thisInline && !thisSuppressed && !afterInline && !sameLine && !inFormattedTag)
            {
                Indent();
                afterInline = false; //afterFormatted = false;
            }
            else
            {
                afterInline = thisInline; //afterFormatted = thisFormatted;
            }

            nextReceiver.EndElement();
            inFormattedTag = inFormattedTag && !thisFormatted;
            sameLine = false;
            afterEndElement = true;
        }

        /*!afterFormatted &&*/
        /// <summary>
        /// Output character data
        /// </summary>
        public override void Characters(UnicodeString chars, ILocation locationId, int properties)
        {
            int withinSuppressed = level == 0 ? 0 : (propertyStack[level - 1] & IS_SUPPRESSED);
            if (inFormattedTag || withinSuppressed > 0 || ReceiverOption.Contains(properties, ReceiverOption.USE_NULL_MARKERS) || ReceiverOption.Contains(properties, ReceiverOption.DISABLE_ESCAPING))
            {

                // don't split the text if in a tag such as <pre>, or if the text contains the result of
                // expanding a character map or was produced using disable-output-escaping
                nextReceiver.Characters(chars, locationId, properties);
            }
            else
            {

                // otherwise try to split long lines into multiple lines
                UnicodeString t = chars.Tidy();
                int lastNL = 0;
                IIntIterator iter = t.CodePoints();
                int i = 0;
                while (iter.MoveNext())
                {
                    int ch = iter.Current;
                    if (ch == '\n' || (i - lastNL > LineLength && ch == ' '))
                    {
                        sameLine = false;
                        nextReceiver.Characters(t.Substring(lastNL, i), locationId, properties);
                        Indent();
                        lastNL = i + 1;
                        while (lastNL < t.Length() && t.CodePointAt(lastNL) == ' ')
                        {
                            lastNL++;
                        }
                    }

                    i++;
                }

                if (lastNL < t.Length())
                {
                    nextReceiver.Characters(t.Substring(lastNL, t.Length()), locationId, properties);
                }
            }

            afterInline = false;
            afterEndElement = false;
        }

        /*!afterFormatted &&*/
        /// <summary>
        /// Output a processing instruction
        /// </summary>
        public override void ProcessingInstruction(string target, UnicodeString data, ILocation locationId, int properties)
        {
            if (afterEndElement && level != 0 && (propertyStack[level - 1] & IS_INLINE) == 0)
            {
                Indent();
            }

            nextReceiver.ProcessingInstruction(target, data, locationId, properties);
            afterEndElement = false;
        }

        /*!afterFormatted &&*/
        public override void Comment(UnicodeString chars, ILocation locationId, int properties)
        {
            if (afterEndElement && level != 0 && (propertyStack[level - 1] & IS_INLINE) == 0)
            {
                Indent();
            }

            nextReceiver.Comment(chars, locationId, properties);
            afterEndElement = false;
        }

        /*!afterFormatted &&*/
        private void Indent()
        {
            int spaces = level * Indentation;

            //                increment += spaces + 1;
            //            indentChars = c2;
            //        }
            //        nextReceiver.characters(new Twine16(indentChars, 0, spaces + 1),
            //                                Loc.NONE, ReceiverOption.NONE);
            nextReceiver.Characters(IndentWhitespace.Of(1, spaces), Loc.NONE, ReceiverOption.NONE);
            sameLine = false;
        }
    }
}
