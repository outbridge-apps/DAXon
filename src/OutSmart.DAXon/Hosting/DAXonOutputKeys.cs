////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Numerics;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Core;
namespace OutSmart.DAXon.Lib
{
    internal sealed class DAXonOutputKeys
    {
        // The W3C serialization property names (ex-JAXP OutputKeys).
        public const string METHOD = "method";
        public const string VERSION = "version";
        public const string ENCODING = "encoding";
        public const string OMIT_XML_DECLARATION = "omit-xml-declaration";
        public const string STANDALONE = "standalone";
        public const string DOCTYPE_PUBLIC = "doctype-public";
        public const string DOCTYPE_SYSTEM = "doctype-system";
        public const string CDATA_SECTION_ELEMENTS = "cdata-section-elements";
        public const string INDENT = "indent";
        public const string MEDIA_TYPE = "media-type";
        public const string ALLOW_DUPLICATE_NAMES = "allow-duplicate-names";
        public const string ESCAPE_SOLIDUS = "escape-solidus";
        public const string BUILD_TREE = "build-tree";
        public const string INDENT_SPACES = "{http://saxon.sf.net/}indent-spaces";
        public const string INTERNAL_DTD_SUBSET = "{http://saxon.sf.net/}internal-dtd-subset";
        public const string LINE_LENGTH = "{http://saxon.sf.net/}line-length";
        public const string SINGLE_QUOTES = "{http://saxon.sf.net/}single-quotes";
        public const string SUPPRESS_INDENTATION = "suppress-indentation";
        public const string HTML_VERSION = "html-version";
        public const string ITEM_SEPARATOR = "item-separator";
        public const string JSON_NODE_OUTPUT_METHOD = "json-node-output-method";
        public const string ATTRIBUTE_ORDER = "{http://saxon.sf.net/}attribute-order";
        public const string CANONICAL = "{http://saxon.sf.net/}canonical";
        public const string PROPERTY_ORDER = "{http://saxon.sf.net/}property-order";
        public const string DOUBLE_SPACE = "{http://saxon.sf.net/}double-space";
        public const string NEWLINE = "{http://saxon.sf.net/}newline";
        public const string STYLESHEET_VERSION = "{http://saxon.sf.net/}stylesheet-version";
        public const string USE_CHARACTER_MAPS = "use-character-maps";
        public const string INCLUDE_CONTENT_TYPE = "include-content-type";
        public const string UNDECLARE_PREFIXES = "undeclare-prefixes";
        public const string ESCAPE_URI_ATTRIBUTES = "escape-uri-attributes";
        public const string CHARACTER_REPRESENTATION = "{http://saxon.sf.net/}character-representation";
        public const string NEXT_IN_CHAIN = "{http://saxon.sf.net/}next-in-chain";
        public const string NEXT_IN_CHAIN_BASE_URI = "{http://saxon.sf.net/}next-in-chain-base-uri";
        public const string PARAMETER_DOCUMENT = "parameter-document";
        public const string PARAMETER_DOCUMENT_BASE_URI = "{http://saxon.sf.net/}parameter-document-base-uri";
        public const string BYTE_ORDER_MARK = "byte-order-mark";
        public const string NORMALIZATION_FORM = "normalization-form";
        public const string RECOGNIZE_BINARY = "{http://saxon.sf.net/}recognize-binary";
        public const string REQUIRE_WELL_FORMED = "{http://saxon.sf.net/}require-well-formed";
        public const string SUPPLY_SOURCE_LOCATOR = "{http://saxon.sf.net/}supply-source-locator";
        public const string UNFAILING = "{http://saxon.sf.net/}unfailing";
        public static string ParseListOfNodeNames(string value, INamespaceResolver nsResolver, bool useDefaultNS, bool prevalidated, bool allowStar, string errorCode)
        {
            StringBuilder s = new StringBuilder();
            foreach (string displayname in value.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (allowStar && displayname == "*")
                {
                    s.Append(' ').Append(displayname);
                }
                else if (prevalidated || (nsResolver == null))
                {
                    s.Append(' ').Append(displayname);
                }
                else if (displayname.StartsWith("Q{", StringComparison.Ordinal))
                {
                    s.Append(' ').Append(displayname);
                }
                else
                {
                    try
                    {
                        string[] parts = NameChecker.GetQNameParts(displayname);
                        NamespaceUri muri = nsResolver.GetURIForPrefix(parts[0], useDefaultNS);
                        if (muri == null)
                        {
                            throw new XPathException("Namespace prefix '" + parts[0] + "' has not been declared", errorCode);
                        }

                        s.Append(" Q{").Append(muri).Append('}').Append(parts[1]);
                    }
                    catch (QNameException err)
                    {
                        throw new XPathException("Invalid QName. " + err.GetMessage(), errorCode);
                    }
                }
            }

            return s.ToString();
        }

        public static bool IsUnstrippedProperty(string key)
        {
            return ITEM_SEPARATOR.Equals(key) || NEWLINE.Equals(key);
        }

        // A version is a decimal, and is that number however it is written: "4", "4.0", "4.00".
        internal static bool IsDecimal(string value, string number)
        {
            try
            {
                return ((DecimalValue)BigDecimalValue.MakeDecimalValue(value, false).AsAtomic()).GetDecimalValue().Equals(new BigDecimal(number));
            }
            catch (ValidationException)
            {
                return false;
            }
        }

        public static bool IsXhtmlHtmlVersion5(Properties properties)
        {
            string htmlVersion = properties.GetProperty(DAXonOutputKeys.HTML_VERSION);
            try
            {
                return htmlVersion != null && ((DecimalValue)BigDecimalValue.MakeDecimalValue(htmlVersion, false).AsAtomic()).GetDecimalValue().Equals(BigDecimal.ValueOf(5));
            }
            catch (ValidationException e)
            {
                return false;
            }
        }

        public static bool IsHtmlVersion5(Properties properties)
        {
            string htmlVersion = properties.GetProperty(DAXonOutputKeys.HTML_VERSION);
            if (htmlVersion == null)
            {
                htmlVersion = properties.GetProperty(VERSION);
            }

            if (htmlVersion != null)
            {
                try
                {
                    return ((DecimalValue)BigDecimalValue.MakeDecimalValue(htmlVersion, false).AsAtomic()).GetDecimalValue().Equals(BigDecimal.ValueOf(5));
                }
                catch (ValidationException e)
                {
                    return false;
                }
            }
            else
            {
                return true; // Change in 10.0 to make HTML5 the default
            }
        }
    }
}