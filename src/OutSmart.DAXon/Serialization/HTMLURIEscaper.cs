////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Serialization.CharCodes;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Serialization
{
    internal class HTMLURIEscaper : ProxyReceiver
    {
        private static readonly HTMLTagHashSet urlAttributes = new HTMLTagHashSet(47);
        private static readonly HTMLTagHashSet urlCombinations = new HTMLTagHashSet(101);

        protected INodeName currentElement;
        protected bool escapeURIAttributes = true;
        protected NamePool pool;
        static HTMLURIEscaper()
        {
            SetUrlAttribute("form", "action");
            SetUrlAttribute("object", "archive");
            SetUrlAttribute("body", "background");
            SetUrlAttribute("q", "cite");
            SetUrlAttribute("blockquote", "cite");
            SetUrlAttribute("del", "cite");
            SetUrlAttribute("ins", "cite");
            SetUrlAttribute("object", "classid");
            SetUrlAttribute("object", "codebase");
            SetUrlAttribute("applet", "codebase");
            SetUrlAttribute("object", "data");
            SetUrlAttribute("button", "datasrc");
            SetUrlAttribute("div", "datasrc");
            SetUrlAttribute("input", "datasrc");
            SetUrlAttribute("object", "datasrc");
            SetUrlAttribute("select", "datasrc");
            SetUrlAttribute("span", "datasrc");
            SetUrlAttribute("table", "datasrc");
            SetUrlAttribute("textarea", "datasrc");
            SetUrlAttribute("script", "for");
            SetUrlAttribute("a", "href");
            SetUrlAttribute("a", "name"); // see second note in section B.2.1 of HTML 4 specification
            SetUrlAttribute("area", "href");
            SetUrlAttribute("link", "href");
            SetUrlAttribute("base", "href");
            SetUrlAttribute("img", "longdesc");
            SetUrlAttribute("frame", "longdesc");
            SetUrlAttribute("iframe", "longdesc");
            SetUrlAttribute("head", "profile");
            SetUrlAttribute("script", "src");
            SetUrlAttribute("input", "src");
            SetUrlAttribute("frame", "src");
            SetUrlAttribute("iframe", "src");
            SetUrlAttribute("img", "src");
            SetUrlAttribute("img", "usemap");
            SetUrlAttribute("input", "usemap");
            SetUrlAttribute("object", "usemap");
        }
        public HTMLURIEscaper(IReceiver nextReceiver) : base(nextReceiver)
        {
        }

        private static void SetUrlAttribute(string element, string attribute)
        {
            urlAttributes.Add(attribute);
            urlCombinations.Add(element + '+' + attribute);
        }

        public virtual bool IsUrlAttribute(INodeName element, INodeName attribute)
        {
            if (pool == null)
            {
                pool = GetNamePool();
            }

            string attributeName = attribute.DisplayName;
            if (!urlAttributes.Contains(attributeName))
            {
                return false;
            }

            string elementName = element.DisplayName;
            return urlCombinations.Contains(elementName + '+' + attributeName);
        }

        public override void StartDocument(int properties)
        {
            nextReceiver.StartDocument(properties);
            pool = GetPipelineConfiguration().GetConfiguration().GetNamePool();
        }

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
                                    return new AttributeInfo(att.GetNodeName(), att.GetType(), EscapeURL(value, true, GetConfiguration()), att.GetLocation(), att.GetProperties() | ReceiverOption.DISABLE_CHARACTER_MAPS);
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

        public static string EscapeURL(string url, bool normalize, Configuration config)
        {

            // optimize for the common case where the string is all ASCII characters
            IIntIterator iter = StringTool.CodePoints(url);
            while (iter.MoveNext())
            {
                int ch = iter.Current;
                if (ch < 32 || ch > 126)
                {
                    if (normalize)
                    {
                        string normalized = NormalizeUnicode.Normalize(url, NormalizationForm.FormC);
                        return ReallyEscapeURL(normalized).ToString();
                    }
                    else
                    {
                        return ReallyEscapeURL(url).ToString();
                    }
                }
            }

            return url;
        }

        private static UnicodeString ReallyEscapeURL(string url)
        {
            UnicodeBuilder ub = new UnicodeBuilder(url.Length + 20);
            string hex = "0123456789ABCDEF";
            byte[] array;
            IIntIterator iter = StringTool.CodePoints(url);
            while (iter.MoveNext())
            {
                int ch = iter.Current;
                if (ch < 32 || ch > 126)
                {
                    array = UTF8CharacterSet.Encode(new IntSingletonIterator(ch));
                    foreach (byte value in array)
                    {
                        int v = ((int)value) & 0xff;
                        ub.Append('%').Append(hex[v / 16]).Append(hex[v % 16]);
                    }
                }
                else
                {
                    ub.Append(ch);
                }
            }

            return ub.ToUnicodeString();
        }
    }
}