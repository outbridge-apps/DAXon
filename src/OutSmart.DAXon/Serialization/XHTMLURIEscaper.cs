////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System.Collections.Generic;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Api;

namespace OutSmart.DAXon.Serialization
{
    /// <summary>
    /// This class performs URI escaping for the XHTML output method. The logic for performing escaping
    /// is the same as the HTML output method, but the way in which attributes are identified for escaping
    /// is different, because XHTML is case-sensitive.
    /// </summary>
    internal sealed class XHTMLURIEscaper : HTMLURIEscaper
    {
        /// <summary>
        /// Table of attributes whose value is a URL
        /// </summary>
        private static readonly HashSet<string> urlTable = new HashSet<string>();
        private static readonly HashSet<string> attTable = new HashSet<string>();

        static XHTMLURIEscaper()
        {
            SetUrlAttributeX("form", "action");
            SetUrlAttributeX("object", "archive");
            SetUrlAttributeX("body", "background");
            SetUrlAttributeX("q", "cite");
            SetUrlAttributeX("blockquote", "cite");
            SetUrlAttributeX("del", "cite");
            SetUrlAttributeX("ins", "cite");
            SetUrlAttributeX("object", "classid");
            SetUrlAttributeX("object", "codebase");
            SetUrlAttributeX("applet", "codebase");
            SetUrlAttributeX("object", "data");
            SetUrlAttributeX("button", "datasrc");
            SetUrlAttributeX("div", "datasrc");
            SetUrlAttributeX("input", "datasrc");
            SetUrlAttributeX("object", "datasrc");
            SetUrlAttributeX("select", "datasrc");
            SetUrlAttributeX("span", "datasrc");
            SetUrlAttributeX("table", "datasrc");
            SetUrlAttributeX("textarea", "datasrc");
            SetUrlAttributeX("script", "for");
            SetUrlAttributeX("a", "href");
            SetUrlAttributeX("a", "name");       // see second note in section B.2.1 of HTML 4 specification
            SetUrlAttributeX("area", "href");
            SetUrlAttributeX("link", "href");
            SetUrlAttributeX("base", "href");
            SetUrlAttributeX("img", "longdesc");
            SetUrlAttributeX("frame", "longdesc");
            SetUrlAttributeX("iframe", "longdesc");
            SetUrlAttributeX("head", "profile");
            SetUrlAttributeX("script", "src");
            SetUrlAttributeX("input", "src");
            SetUrlAttributeX("frame", "src");
            SetUrlAttributeX("iframe", "src");
            SetUrlAttributeX("img", "src");
            SetUrlAttributeX("img", "usemap");
            SetUrlAttributeX("input", "usemap");
            SetUrlAttributeX("object", "usemap");
        }

        public XHTMLURIEscaper(IReceiver next) : base(next)
        {
        }

        private static void SetUrlAttributeX(string element, string attribute)
        {
            attTable.Add(attribute);
            urlTable.Add(element + "+" + attribute);
        }

        /// <summary>
        /// Notify the start of an element
        /// </summary>
        public override void StartElement(INodeName nameCode, ISchemaType type, IAttributeMap attributes, NamespaceMap namespaces, ILocation location, int properties)
        {
            currentElement = nameCode;
            IAttributeMap atts2 = attributes;
            if (escapeURIAttributes)
            {
                try
                {
                    atts2 = attributes.Apply((att) =>
                    {
                        if (!ReceiverOption.Contains(att.GetProperties(), ReceiverOption.DISABLE_ESCAPING))
                        {
                            INodeName attName = att.GetNodeName();
                            if (IsUrlAttribute(nameCode, attName))
                            {
                                string value = att.Value;
                                try
                                {
                                    string normalized = IsAllAscii(value)
                                        ? value
                                        : OutSmart.DAXon.Functions.NormalizeUnicode.Normalize(value, NormalizationForm.FormC);
                                    return new AttributeInfo(
                                        attName,
                                        att.GetType(),
                                        EscapeURL(normalized, true, GetConfiguration()),
                                        att.GetLocation(),
                                        att.GetProperties() | ReceiverOption.DISABLE_CHARACTER_MAPS);
                                }
                                catch (XPathException e)
                                {
                                    throw new UncheckedXPathException(e);
                                }
                            }
                            else
                            {
                                return att;
                            }
                        }
                        else
                        {
                            return att;
                        }
                    });
                }
                catch (UncheckedXPathException e)
                {
                    throw e.GetXPathException();
                }
            }

            nextReceiver.StartElement(nameCode, type, atts2, namespaces, location, properties);
        }

        private static bool IsAllAscii(string value)
        {
            foreach (char c in value)
            {
                if (c > 127)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
