////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Values;
using System.Collections.Generic;

namespace OutSmart.DAXon.Serialization
{
    /// <summary>
    /// Generates XHTML 5 output: like the XML emitter, but follows the legacy HTML browser-compatibility
    /// rules — empty elements such as &lt;br /&gt; and explicit end tags for non-void elements (&lt;p&gt;&lt;/p&gt;).
    /// (Was a hollow stub extending the bare Emitter, so method=xhtml + html-version=5 hit StartDocument stubs.)
    /// </summary>
    internal sealed class XHTML5Emitter : XMLEmitter
    {
        private static readonly HashSet<string> html5Elements = new HashSet<string>
        {
            "a", "abbr", "address", "area", "article", "aside", "audio",
            "b", "base", "bdi", "bdo", "blockquote", "body", "br", "button",
            "canvas", "caption", "cite", "code", "col", "colgroup",
            "datalist", "dd", "del", "details", "dfn", "dialog", "div", "dl", "dt",
            "em", "embed",
            "fieldset", "figcaption", "figure", "footer", "form",
            "h1", "h2", "h3", "h4", "h5", "h6", "head", "header", "hgroup", "hr", "html",
            "i", "iframe", "img", "input", "ins",
            "kbd", "keygen",
            "label", "legend", "li", "link",
            "map", "mark", "menu", "meta", "meter",
            "nav", "noscript",
            "object", "ol", "optgroup", "option", "output",
            "p", "param", "pre", "progress",
            "q",
            "rp", "rt", "ruby",
            "s", "samp", "script", "section", "select", "small", "source", "span", "strong", "style", "sub", "summary", "sup",
            "table", "tbody", "td", "textarea", "tfoot", "th", "thead", "time", "title", "tr", "track",
            "u", "ul",
            "var", "video",
            "wbr"
        };

        private static readonly HashSet<string> emptyTags5 = new HashSet<string>
        {
            "area", "base", "br", "col", "embed", "hr", "img", "input", "keygen", "link", "meta", "param",
            "source", "track", "wbr"
        };

        private bool IsRecognizedHtmlElement(INodeName name)
        {
            return name.HasURI(NamespaceUri.XHTML) ||
                    (name.HasURI(NamespaceUri.NULL) && html5Elements.Contains(name.GetLocalPart().ToLowerInvariant()));
        }

        protected override void WriteDocType(INodeName name, string displayName, string systemId, string publicId)
        {
            if (systemId == null && IsRecognizedHtmlElement(name) && name.GetLocalPart().ToLowerInvariant().Equals("html"))
            {
                writer.WriteAscii(DOCTYPE);
                writer.Write(displayName);
                writer.WriteCodePoint('>');
            }
            else if (systemId != null)
            {
                base.WriteDocType(name, displayName, systemId, publicId);
            }
        }

        protected override bool WriteDocTypeWithNullSystemId()
        {
            return true;
        }

        protected override void WriteEmptyElementTagCloser(string displayName, INodeName name)
        {
            if (IsRecognizedHtmlElement(name) && emptyTags5.Contains(name.GetLocalPart()))
            {
                writer.WriteAscii(StringConstants.EMPTY_TAG_END);
            }
            else
            {
                writer.WriteAscii(StringConstants.EMPTY_TAG_MIDDLE);
                writer.Write(displayName);
                writer.WriteCodePoint('>');
            }
        }

        public override void Characters(UnicodeString chars, ILocation locationId, int properties)
        {
            // Ignore whitespace before the first start tag: we buffer nothing, but need the first element's
            // name to emit the DOCTYPE, so leading whitespace is dropped (matches upstream).
            if (!started && Whitespace.IsAllWhite(chars))
            {
                // no action
            }
            else
            {
                base.Characters(chars, locationId, properties);
            }
        }
    }
}
