////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Net;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Text;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Xslt
{
    /// <summary>
    /// An xsl:output element in the stylesheet.
    /// </summary>
    internal sealed class XSLOutput : StyleElement
    {
        private StructuredQName outputFormatName;
        private readonly string method;
        private string useCharacterMaps;
        private readonly Dictionary<string, string> serializationAttributes = new Dictionary<string, string>(10);
        private Dictionary<string, string> userAttributes;

        public StructuredQName FormatQName => outputFormatName;
        public override bool IsDeclaration()
        {
            return true;
        }

        public override void PrepareAttributes()
        {
            string nameAtt = null;
            foreach (AttributeInfo att in Attributes())
            {
                INodeName attName = att.GetNodeName();
                string value = att.Value;
                string f = attName.GetStructuredQName().ClarkName;
                if (f.Equals("name"))
                {
                    nameAtt = Whitespace.Trim(value);
                }
                else if (f.Equals("version"))
                {
                    string outputVersion = Whitespace.Trim(value);
                    serializationAttributes[f] = outputVersion;
                }
                else if (f.Equals("use-character-maps"))
                {
                    useCharacterMaps = value;
                }
                else if (f.Equals("parameter-document"))
                {
                    string val = Whitespace.Trim(value);
                    try
                    {
                        val = ResolveURI.MakeAbsolute(val, GetBaseURI()).ToASCIIString();
                    }
                    catch (URISyntaxException e)
                    {
                        CompileError(XPathException.MakeXPathException(e));
                    }

                    serializationAttributes[f] = val;
                }
                else if (XSLResultDocument.fans.Contains(f) && !f.Equals("output-version"))
                {
                    string val = value;
                    if (f.Equals(DAXonOutputKeys.ESCAPE_SOLIDUS))
                    {
                        RequireXslt40Attribute(f);
                    }

                    if (!f.Equals(DAXonOutputKeys.ITEM_SEPARATOR) && !f.Equals(DAXonOutputKeys.NEWLINE))
                    {
                        val = Whitespace.Trim(val);
                    }

                    serializationAttributes[f] = val;
                }
                else
                {
                    NamespaceUri attributeURI = attName.GetNamespaceUri();
                    if (NamespaceUri.NULL.Equals(attributeURI) || NamespaceUri.XSLT.Equals(attributeURI) || NamespaceUri.SAXON.Equals(attributeURI))
                    {
                        CheckUnknownAttribute(attName);
                    }
                    else
                    {
                        string name = "{" + attributeURI + "}" + attName.GetLocalPart();
                        if (userAttributes == null)
                        {
                            userAttributes = new Dictionary<string, string>(5);
                        }

                        userAttributes[name] = value;
                    }
                }
            }

            if (nameAtt != null)
            {
                outputFormatName = MakeQName(nameAtt, "XTSE1570", "name");
            }
        }

        public override void Validate(ComponentDeclaration decl)
        {
            CheckTopLevel("XTSE0010", false);
            CheckEmpty();
        }

        public override void CompileDeclaration(Compilation compilation, ComponentDeclaration decl)
        {
        }

        protected internal override void ProcessVersionAttribute(NamespaceUri ns)
        {
            version = ((StyleElement)GetParent()).EffectiveVersion;
        }

        public void GatherOutputProperties(Properties details, Dictionary<string, int> precedences, int thisPrecedence)
        {
            SerializerFactory sf = GetConfiguration().SerializerFactory;
            if (method != null)
            {
                if (method == "xml" || method == "html" || method == "text" || method == "xhtml" || method == "json" || method == "adaptive")
                {
                    CheckAndPut(sf, DAXonOutputKeys.METHOD, method, details, precedences, thisPrecedence); //details.put(DAXonOutputKeys.METHOD, method);
                }
                else
                {
                    string[] parts;
                    try
                    {
                        parts = NameChecker.GetQNameParts(method);
                        string prefix = parts[0];
                        if ((prefix.Length == 0))
                        {
                            CompileError("method must be xml, html, xhtml, text, json, adaptive, or a prefixed name", "XTSE1570");
                        }
                        else
                        {
                            NamespaceUri uri = GetURIForPrefix(prefix, false);
                            if (uri == null)
                            {
                                UndeclaredNamespaceError(prefix, "XTSE0280", "method");
                            }

                            CheckAndPut(sf, DAXonOutputKeys.METHOD, "{" + uri + "}" + parts[1], details, precedences, thisPrecedence); //details.put(DAXonOutputKeys.METHOD, '{' + uri + '}' + parts[1] );
                        }
                    }
                    catch (QNameException e)
                    {
                        CompileError("Invalid method name. " + e.GetMessage(), "XTSE1570");
                    }
                }
            }

            foreach (KeyValuePair<string, string> entry in serializationAttributes)
            {
                CheckAndPut(sf, entry.Key, entry.Value, details, precedences, thisPrecedence);
            }

            if (serializationAttributes.ContainsKey(DAXonOutputKeys.NEXT_IN_CHAIN))
            {
                CheckAndPut(sf, DAXonOutputKeys.NEXT_IN_CHAIN_BASE_URI, GetSystemId(), details, precedences, thisPrecedence);
            }

            if (useCharacterMaps != null)
            {
                string s = PrepareCharacterMaps(this, useCharacterMaps, details);
                details.SetProperty(DAXonOutputKeys.USE_CHARACTER_MAPS, s);
            }


            // deal with user-defined attributes
            if (userAttributes != null)
            {
                foreach (KeyValuePair<string, string> e in userAttributes)
                {
                    details.SetProperty(e.Key, e.Value);
                }
            }
        }

        private void CheckAndPut(SerializerFactory sf, string property, string value, Properties props, Dictionary<string, int> precedences, int thisPrecedence)
        {
            try
            {
                if (IsListOfNames(property))
                {
                    bool useDefaultNS = !property.Equals(DAXonOutputKeys.ATTRIBUTE_ORDER);
                    bool allowStar = property.Equals(DAXonOutputKeys.ATTRIBUTE_ORDER);
                    value = DAXonOutputKeys.ParseListOfNodeNames(value, this, useDefaultNS, false, allowStar, "XTSE0280");
                }

                if (IsQName(property) && value.Contains(":"))
                {
                    value = ResolveQName.ResolveQNameFn(value, this).EQName;
                }

                value = sf.CheckOutputProperty(property, value);
            }
            catch (XPathException err)
            {
                string code = property.Equals("method") ? "XTSE1570" : "XTSE0020";
                if (property.Contains("{"))
                {
                    CompileError(err.Message, code);
                }
                else
                {
                    CompileErrorInAttribute(err.Message, code, property);
                }

                return;
            }

            string old = props.GetProperty(property);
            if (old == null)
            {
                props.SetProperty(property, value);
                precedences[property] = thisPrecedence;
            }
            else if (old.Equals(value))
            {
            }
            else if (IsListOfNames(property))
            {
                props.SetProperty(property, old + " " + value);
                precedences[property] = thisPrecedence;
            }
            else
            {
                int oldPrec = precedences.GetOrDefault(property, int.MinValue);
                if (oldPrec == int.MinValue)
                {
                    return; // shouldn't happen but ignore it
                }

                if (oldPrec > thisPrecedence)
                {
                }
                else if (oldPrec == thisPrecedence)
                {
                    CompileError("Conflicting values for output property " + property, "XTSE1560");
                }
                else
                {

                    // this has higher precedence: can't happen
                    throw new InvalidOperationException("Output properties must be processed in decreasing precedence order");
                }
            }
        }

        // do nothing
        // ignore this value, the other has higher precedence
        private static bool IsListOfNames(string property)
        {
            return property.Equals(DAXonOutputKeys.CDATA_SECTION_ELEMENTS) || property.Equals(DAXonOutputKeys.SUPPRESS_INDENTATION) || property.Equals(DAXonOutputKeys.ATTRIBUTE_ORDER) || property.Equals(DAXonOutputKeys.DOUBLE_SPACE);
        }

        private static bool IsQName(string property)
        {
            return property.Equals(DAXonOutputKeys.METHOD) || property.Equals(DAXonOutputKeys.JSON_NODE_OUTPUT_METHOD);
        }

        public static string PrepareCharacterMaps(StyleElement element, string useCharacterMaps, Properties details)
        {
            PrincipalStylesheetModule psm = element.GetPrincipalStylesheetModule();
            string existing = details.GetProperty(DAXonOutputKeys.USE_CHARACTER_MAPS);
            if (existing == null)
            {
                existing = "";
            }

            StringBuilder s = new StringBuilder();
            foreach (string displayname in useCharacterMaps.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                StructuredQName qName = element.MakeQName(displayname, null, "use-character-maps");
                ComponentDeclaration decl = psm.GetCharacterMap(qName);
                if (decl == null)
                {
                    element.CompileErrorInAttribute("No character-map named '" + displayname + "' has been defined", "XTSE1590", "use-character-maps");
                }

                s.Append(' ').Append(qName.ClarkName);
            }

            existing = s + existing;
            return existing;
        }
    }
}
