////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Serialization.CharCodes;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Collections;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Globalization;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Internal;
using System.IO;
namespace OutSmart.DAXon.Serialization
{
    public class XMLEmitter : Emitter
    {
        protected static bool[] specialInText; // lookup table for special characters in text
        protected static bool[] specialInAtt; // lookup table for special characters in attributes
        protected static bool[] specialInAttSingle; // lookup table for special characters in attributes with single-quote delimiter

        private static readonly byte[] XML_DECL_VERSION = StringConstants.Bytes("<?xml version=");
        private static readonly byte[] XML_DECL_ENCODING = StringConstants.Bytes("encoding=");
        private static readonly byte[] XML_DECL_STANDALONE = StringConstants.Bytes(" standalone=");
        protected static readonly byte[] DOCTYPE = StringConstants.Bytes("<!DOCTYPE ");
        private static readonly byte[] SYSTEM = StringConstants.Bytes("  SYSTEM ");
        private static readonly byte[] PUBLIC = StringConstants.Bytes("  PUBLIC ");
        protected static readonly byte[] RIGHT_ANGLE_NEWLINE = StringConstants.Bytes(">\n");
        protected bool canonical = false;
        protected bool started = false;
        protected bool startedElement = false;
        protected bool openStartTag = false;
        protected bool declarationIsWritten = false;
        protected INodeName elementCode;
        protected int indentForNextAttribute = -1;
        protected bool undeclareNamespaces = false;
        protected bool unfailing = false;
        protected string internalSubset = null;
        protected char delimiter = '"';
        protected bool[] attSpecials = specialInAtt;
        protected Stack<string> elementStack = new Stack<string>();
        private bool indenting = false;
        private bool requireWellFormed = false;
        protected ICharacterReferenceGenerator characterReferenceGenerator = HexCharacterReferenceGenerator.THE_INSTANCE;

        // True while the value being written carries the character map expander's marks: only there does U+0000
        // switch escaping off and on. In any other value it came with the data, and is no character of XML.
        private protected bool marked;

        // The XML written is 1.1: a control character is a character reference there, and no character at all in 1.0.
        private protected bool xml11;

        private static readonly Func<int, bool> noLiteral10 = (c) => IsNoLiteral(c, false);
        private static readonly Func<int, bool> noLiteral11 = (c) => IsNoLiteral(c, true);

        Func<int, bool> isSpecialInText;
        Func<int, bool> isSpecialInAttribute;
        static XMLEmitter()
        {
            specialInText = new bool[128];
            for (int i = 0; i <= 31; i++)
            {
                specialInText[i] = true; // allowed in XML 1.1 as character references
            }

            for (int i = 32; i <= 127; i++)
            {
                specialInText[i] = false;
            }


            //    note, 0 is used to switch escaping on and off for mapped characters
            specialInText['\n'] = false;
            specialInText['\t'] = false;
            specialInText['\r'] = true;
            specialInText['<'] = true;
            specialInText['>'] = true;
            specialInText['&'] = true;
            specialInAtt = new bool[128];
            for (int i = 0; i <= 31; i++)
            {
                specialInAtt[i] = true; // allowed in XML 1.1 as character references
            }

            for (int i = 32; i <= 127; i++)
            {
                specialInAtt[i] = false;
            }

            specialInAtt[(char)0] = true;

            // used to switch escaping on and off for mapped characters
            specialInAtt['\r'] = true;
            specialInAtt['\n'] = true;
            specialInAtt['\t'] = true;
            specialInAtt['<'] = true;
            specialInAtt['>'] = true;
            specialInAtt['&'] = true;
            specialInAtt['"'] = true;
            specialInAttSingle = ArrayTools.CopyOf(specialInAtt, 128);
            specialInAttSingle['"'] = false;
            specialInAttSingle['\''] = true;
        }
        public XMLEmitter()
        {
        }

        public virtual void SetCharacterReferenceGenerator(ICharacterReferenceGenerator generator)
        {
            this.characterReferenceGenerator = generator;
        }

        public virtual void SetEscapeNonAscii(bool escape)
        {
        }

        public override void Open()
        {
        }

        public override void StartDocument(int properties)
        {
        }

        public override void EndDocument()
        {
        }

        protected virtual void OpenDocument()
        {

            if (characterSet == null)
            {
                characterSet = UTF8CharacterSet.GetInstance();
                allCharactersEncodable = true;
            }

            if (outputProperties == null)
            {
                outputProperties = new Properties();
            }

            undeclareNamespaces = outputProperties.GetProperty(DAXonOutputKeys.UNDECLARE_PREFIXES) == "yes";
            canonical = outputProperties.GetProperty(DAXonOutputKeys.CANONICAL) == "yes";
            unfailing = outputProperties.GetProperty(DAXonOutputKeys.UNFAILING) == "yes";
            internalSubset = outputProperties.GetProperty(DAXonOutputKeys.INTERNAL_DTD_SUBSET);
            if (outputProperties.GetProperty(DAXonOutputKeys.SINGLE_QUOTES) == "yes")
            {
                delimiter = '\'';
                attSpecials = specialInAttSingle;
            }

            string version = outputProperties.GetProperty(DAXonOutputKeys.VERSION);
            xml11 = version == null ? GetConfiguration()?.XMLVersion == Configuration.XML11 : version.Equals("1.1");

            // Past U+2028 the scan also stops at what is no character of XML - half a surrogate pair, U+FFFE, U+FFFF.
            if (allCharactersEncodable)
            {
                isSpecialInText = (c) => (c < 127 ? specialInText[c] : (c < 160 || (c >= 0x2028 && (c == 0x2028 || (c >= 0xD800 && c <= 0xFFFF && (c <= 0xDFFF || c >= 0xFFFE))))));
                isSpecialInAttribute = (c) => (c < 127 ? attSpecials[c] : (c < 160 || (c >= 0x2028 && (c == 0x2028 || (c >= 0xD800 && c <= 0xFFFF && (c <= 0xDFFF || c >= 0xFFFE))))));
            }
            else
            {
                isSpecialInText = (c) => (c < 127 ? specialInText[c] : (c < 160 || c == 0x2028 || c > 65535 || !characterSet.InCharset(c) || (c >= 0xD800 && (c <= 0xDFFF || c >= 0xFFFE))));
                isSpecialInAttribute = (c) => (c < 127 ? attSpecials[c] : (c < 160 || c == 0x2028 || c > 65535 || !characterSet.InCharset(c) || (c >= 0xD800 && (c <= 0xDFFF || c >= 0xFFFE))));
            }

            WriteDeclaration();
        }

        public virtual void WriteDeclaration()
        {
            if (declarationIsWritten)
            {
                return;
            }

            declarationIsWritten = true;
            try
            {
                indenting = outputProperties.GetProperty(DAXonOutputKeys.INDENT) == "yes";
                string byteOrderMark = outputProperties.GetProperty(DAXonOutputKeys.BYTE_ORDER_MARK);
                string encoding = outputProperties.GetProperty(DAXonOutputKeys.ENCODING);
                if (encoding == null || encoding.Equals("utf8", StringComparison.OrdinalIgnoreCase) || canonical)
                {
                    encoding = "UTF-8";
                }

                if (byteOrderMark == "yes" && !canonical && ("UTF-8".Equals(encoding, StringComparison.OrdinalIgnoreCase) || "UTF-16LE".Equals(encoding, StringComparison.OrdinalIgnoreCase) || "UTF-16BE".Equals(encoding, StringComparison.OrdinalIgnoreCase)))
                {
                    writer.WriteCodePoint(0xFEFF);
                }

                string omitXMLDeclaration = outputProperties.GetProperty(DAXonOutputKeys.OMIT_XML_DECLARATION);
                if (omitXMLDeclaration == null)
                {
                    omitXMLDeclaration = "no";
                }

                if (canonical)
                {
                    omitXMLDeclaration = "yes";
                }

                string version = outputProperties.GetProperty(DAXonOutputKeys.VERSION);
                if (version == null)
                {
                    version = GetConfiguration().XMLVersion == Configuration.XML10 ? "1.0" : "1.1";
                }
                else
                {
                    if (!version.Equals("1.0") && !version.Equals("1.1"))
                    {
                        if (unfailing)
                        {
                            version = "1.0";
                        }
                        else
                        {
                            throw new XPathException("XML version must be 1.0 or 1.1").WithErrorCode("SESU0013");
                        }
                    }

                    if (!version.Equals("1.0") && omitXMLDeclaration.Equals("yes") && outputProperties.GetProperty(DAXonOutputKeys.DOCTYPE_SYSTEM) != null)
                    {
                        if (!unfailing)
                        {
                            throw new XPathException("Values of 'version', 'omit-xml-declaration', and 'doctype-system' conflict").WithErrorCode("SEPM0009");
                        }
                    }
                }

                string undeclare = outputProperties.GetProperty(DAXonOutputKeys.UNDECLARE_PREFIXES);
                if (undeclare == "yes")
                {
                    undeclareNamespaces = true;
                }

                if (version.Equals("1.0") && undeclareNamespaces)
                {
                    if (unfailing)
                    {
                        undeclareNamespaces = false;
                    }
                    else
                    {
                        throw new XPathException("Cannot undeclare namespaces with XML version 1.0").WithErrorCode("SEPM0010");
                    }
                }

                string standalone = outputProperties.GetProperty(DAXonOutputKeys.STANDALONE);
                if (standalone == "omit")
                {
                    standalone = null;
                }

                if (standalone != null)
                {
                    requireWellFormed = true;
                    if (omitXMLDeclaration.Equals("yes") && !unfailing)
                    {
                        throw new XPathException("Values of 'standalone' and 'omit-xml-declaration' conflict").WithErrorCode("SEPM0009");
                    }
                }

                string systemId = outputProperties.GetProperty(DAXonOutputKeys.DOCTYPE_SYSTEM);
                if (systemId != null && systemId != "")
                {
                    requireWellFormed = true;
                }

                if (omitXMLDeclaration.Equals("no"))
                {
                    writer.WriteAscii(XML_DECL_VERSION);
                    writer.WriteCodePoint(delimiter);
                    writer.Write(version);
                    writer.WriteCodePoint(delimiter);
                    writer.WriteCodePoint(' ');
                    writer.WriteAscii(XML_DECL_ENCODING);
                    writer.WriteCodePoint(delimiter);
                    writer.Write(encoding);
                    writer.WriteCodePoint(delimiter);
                    if (standalone != null)
                    {
                        writer.WriteAscii(XML_DECL_STANDALONE);
                        writer.WriteCodePoint(delimiter);
                        writer.Write(standalone);
                        writer.WriteCodePoint(delimiter);
                    }

                    writer.WriteAscii(StringConstants.PI_END); //                writer.write("<?xml version=\"" + version + "\" " + "encoding=\"" + encoding + '\"' +
                    //                                     (standalone != null ? " standalone=\"" + standalone + '\"' : "") + "?>");
                    // don't write a newline character: it's wrong if the output is an
                    // external general parsed entity
                }
            }
            catch (IOException err)
            {
                throw WriteFailure(err);
            }
        }
        protected virtual void WriteDocType(INodeName name, string displayName, string systemId, string publicId)
        {
            try
            {
                if (!canonical)
                {
                    if (declarationIsWritten && !indenting)
                    {

                        // don't add a newline if indenting, because the indenter will already have done so
                        writer.WriteCodePoint(0x0A);
                    }

                    writer.WriteAscii(DOCTYPE);
                    writer.Write(displayName);
                    writer.WriteCodePoint(0x0A);
                    systemId = systemId == null ? null : Literal(StringView.Of(systemId)).ToString();
                    string quotedSystemId = null;
                    if (systemId != null)
                    {
                        if (systemId.Contains("\""))
                        {
                            quotedSystemId = "'" + systemId + "'";
                        }
                        else if (systemId.Contains("'"))
                        {
                            quotedSystemId = '"' + systemId + '"';
                        }
                        else
                        {
                            quotedSystemId = delimiter + systemId + delimiter;
                        }
                    }

                    if (systemId != null && publicId == null)
                    {
                        writer.WriteAscii(SYSTEM);
                        writer.Write(quotedSystemId);
                    } // handles the HTML case
                    else if (systemId == null && publicId != null)
                    {

                        // handles the HTML case
                        writer.WriteAscii(PUBLIC);
                        writer.WriteCodePoint(delimiter);
                        writer.Write(publicId);
                        writer.WriteCodePoint(delimiter);
                    }
                    else if (publicId != null)
                    {
                        writer.WriteAscii(PUBLIC);
                        writer.WriteCodePoint(delimiter);
                        writer.Write(publicId);
                        writer.WriteCodePoint(delimiter);
                        writer.WriteCodePoint(' ');
                        writer.Write(quotedSystemId);
                    }

                    if (internalSubset != null)
                    {
                        writer.WriteCodePoint('[');
                        writer.WriteCodePoint(0x0A);
                        writer.Write(internalSubset);
                        writer.WriteCodePoint(0x0A);
                        writer.WriteCodePoint(']');
                    }

                    writer.WriteAscii(RIGHT_ANGLE_NEWLINE);
                }
            }
            catch (IOException err)
            {
                throw WriteFailure(err);
            }
        }

        public override void Close()
        {

            // if nothing has been written, we should still create the file and write an XML declaration
            if (!started)
            {
                OpenDocument();
            }

            try
            {
                if (writer != null)
                {
                    writer.Flush();
                }
            }
            catch (IOException err)
            {
                throw WriteFailure(err);
            }

            base.Close();
        }

        public override void StartElement(INodeName elemName, ISchemaType type, IAttributeMap attributes, NamespaceMap namespaces, ILocation location, int properties)
        {
            previousAtomic = false;
            if (!started)
            {
                OpenDocument();
            }
            else if (requireWellFormed && elementStack.Count == 0 && startedElement && !unfailing)
            {
                throw new XPathException("When 'standalone' or 'doctype-system' is specified, " + "the document must be well-formed; but this document contains more than one top-level element").WithErrorCode("SEPM0004");
            }

            startedElement = true;
            string displayName = elemName.DisplayName;
            if (!allCharactersEncodable)
            {
                int badchar = TestCharacters(StringView.Of(displayName));
                if (badchar != 0)
                {
                    throw new XPathException("Element name contains a character (decimal + " + badchar + ") not available in the selected encoding").WithErrorCode("SERE0008");
                }
            }

            elementStack.Push(displayName);
            elementCode = elemName;
            try
            {
                if (!started)
                {
                    string systemId = outputProperties.GetProperty(DAXonOutputKeys.DOCTYPE_SYSTEM);
                    string publicId = outputProperties.GetProperty(DAXonOutputKeys.DOCTYPE_PUBLIC);

                    // Treat "" as equivalent to absent. This goes beyond what the spec strictly allows.
                    if (systemId == "")
                    {
                        systemId = null;
                    }

                    if (publicId == "")
                    {
                        publicId = null;
                    }

                    if (systemId != null)
                    {
                        requireWellFormed = true;
                        WriteDocType(elemName, displayName, systemId, publicId);
                    }
                    else if (WriteDocTypeWithNullSystemId())
                    {
                        WriteDocType(elemName, displayName, null, publicId);
                    }

                    started = true;
                }

                if (openStartTag)
                {
                    CloseStartTag();
                }

                writer.WriteCodePoint('<');
                writer.Write(displayName);
                if (indentForNextAttribute >= 0)
                {
                    indentForNextAttribute += displayName.Length;
                }

                // index loops: the NamespaceMap enumerator allocates a state machine plus a
                // NamespaceBinding per binding, and IAttributeMap enumerators allocate per
                // element - this runs for every element serialized
                bool isFirst = true;
                string[] nsPrefixes = namespaces.PrefixArray;
                NamespaceUri[] nsUris = namespaces.URIsAsArray;
                for (int i = 0; i < nsPrefixes.Length; i++)
                {
                    Namespace(nsPrefixes[i], nsUris[i], isFirst);
                    isFirst = false;
                }

                int attCount = attributes.Size();
                for (int i = 0; i < attCount; i++)
                {
                    AttributeInfo att = attributes.ItemAt(i);
                    Attribute(att.GetNodeName(), att.Value, att.GetProperties(), isFirst);
                    isFirst = false;
                }

                openStartTag = true;
                indentForNextAttribute = -1;
            }
            catch (IOException err)
            {
                throw WriteFailure(err);
            }
        }

        protected virtual bool WriteDocTypeWithNullSystemId()
        {
            return internalSubset != null;
        }

        public virtual void Namespace(string nsprefix, NamespaceUri nsuri, bool isFirst)
        {
            try
            {
                if ((nsprefix.Length == 0))
                {
                    if (isFirst)
                    {
                        writer.WriteCodePoint(' ');
                    }
                    else
                    {
                        WriteAttributeIndentString();
                    }

                    WriteAttribute(elementCode, "xmlns", nsuri.ToString(), ReceiverOption.NONE);
                } //noinspection StatementWithEmptyBody
                else if (nsprefix.Equals("xml"))
                {
                }
                else
                {
                    int badchar = TestCharacters(StringView.Of(nsprefix));
                    if (badchar != 0)
                    {
                        throw new XPathException("Namespace prefix contains a character (decimal + " + badchar + ") not available in the selected encoding").WithErrorCode("SERE0008");
                    }

                    if (undeclareNamespaces || !nsuri.IsEmpty())
                    {
                        if (isFirst)
                        {
                            writer.WriteCodePoint(' ');
                        }
                        else
                        {
                            WriteAttributeIndentString();
                        }

                        WriteAttribute(elementCode, "xmlns:" + nsprefix, nsuri.ToString(), ReceiverOption.NONE);
                    }
                }
            }
            catch (IOException err)
            {
                throw WriteFailure(err);
            }
        }

        //return;
        public virtual void SetIndentForNextAttribute(int indent)
        {
            indentForNextAttribute = indent;
        }

        //return;
        private void Attribute(INodeName nameCode, string value, int properties, bool isFirst)
        {
            string displayName = nameCode.DisplayName;
            if (!allCharactersEncodable)
            {
                int badchar = TestCharacters(StringView.Of(displayName));
                if (badchar != 0)
                {
                    if (unfailing)
                    {
                        displayName = ConvertToAscii(StringView.Of(displayName)).ToString();
                    }
                    else
                    {
                        throw new XPathException("Attribute name contains a character (decimal + " + badchar + ") not available in the selected encoding").WithErrorCode("SERE0008");
                    }
                }
            }

            try
            {
                if (isFirst)
                {
                    writer.WriteCodePoint(' ');
                }
                else
                {
                    WriteAttributeIndentString();
                }
            }
            catch (IOException err)
            {
                throw WriteFailure(err);
            }

            WriteAttribute(elementCode, displayName, value, properties);
        }

        //return;
        protected virtual void WriteAttributeIndentString()
        {
            if (indentForNextAttribute < 0)
            {
                writer.WriteCodePoint(' ');
            }
            else
            {
                writer.WriteCodePoint('\n');
                writer.WriteRepeatedAscii((byte)0x20, indentForNextAttribute);
            }
        }

        //return;
        public virtual void CloseStartTag()
        {
            try
            {
                if (openStartTag)
                {
                    writer.WriteCodePoint('>');
                    openStartTag = false;
                }
            }
            catch (IOException err)
            {
                throw WriteFailure(err);
            }
        }

        //return;
        protected virtual void WriteEmptyElementTagCloser(string displayName, INodeName nameCode)
        {
            if (canonical)
            {
                writer.WriteCodePoint('>');
                writer.WriteAscii(StringConstants.END_TAG_START);
                writer.Write(displayName);
                writer.WriteCodePoint('>');
            }
            else
            {
                writer.WriteAscii(StringConstants.EMPTY_TAG_END);
            }
        }

        //return;
        protected virtual void WriteAttribute(INodeName elCode, string attname, string value, int properties)
        {
            try
            {
                writer.Write(attname);
                if (ReceiverOption.Contains(properties, ReceiverOption.NO_SPECIAL_CHARS))
                {
                    writer.WriteCodePoint('=');
                    writer.WriteCodePoint(delimiter);
                    writer.Write(value);
                    writer.WriteCodePoint(delimiter);
                }
                else if (ReceiverOption.Contains(properties, ReceiverOption.USE_NULL_MARKERS))
                {

                    // null (0) characters will be used before and after any section of
                    // the value generated from a character map
                    writer.WriteCodePoint('=');
                    char delim = value.IndexOf('"') >= 0 && value.IndexOf('\'') < 0 ? '\'' : delimiter;
                    writer.WriteCodePoint(delim);
                    marked = true;
                    WriteEscape(StringView.Tidy(value), true);
                    marked = false;
                    writer.WriteCodePoint(delim);
                }
                else
                {
                    writer.WriteCodePoint('=');
                    writer.WriteCodePoint(delimiter);
                    if (ReceiverOption.Contains(properties, ReceiverOption.DISABLE_ESCAPING))
                    {
                        writer.Write(value);
                    }
                    else
                    {
                        WriteEscape(StringView.Tidy(value), true);
                    }

                    writer.WriteCodePoint(delimiter);
                }
            }
            catch (IOException err)
            {
                throw WriteFailure(err);
            }
        }

        //return;
        protected virtual int TestCharacters(UnicodeString chars)
        {
            // UTF-8/16 encode every code point: skip the per-character delegate scan
            if (allCharactersEncodable)
            {
                return 0;
            }

            long foundInvalid = chars.IndexWhere((ch) => ch > 127 && !characterSet.InCharset(ch), 0);
            if (foundInvalid >= 0)
            {
                return chars.CodePointAt(foundInvalid);
            }

            return 0;
        }

        //return;
        protected virtual UnicodeString ConvertToAscii(UnicodeString chars)
        {
            UnicodeBuilder buff = new UnicodeBuilder();
            IIntIterator iter = chars.CodePoints();
            while (iter.MoveNext())
            {
                int c = iter.Current;
                if (c >= 20 && c < 127)
                {
                    buff.Append(c);
                }
                else
                {
                    buff.Append("_" + c + "_");
                }
            }

            return buff.ToUnicodeString();
        }

        //return;
        /// <summary>
        /// End of an element.
        /// </summary>
        public override void EndElement()
        {
            string displayName = elementStack.Pop();
            try
            {
                if (openStartTag)
                {
                    WriteEmptyElementTagCloser(displayName, elementCode);
                    openStartTag = false;
                }
                else
                {
                    writer.WriteAscii(StringConstants.END_TAG_START);
                    writer.Write(displayName);
                    writer.WriteCodePoint('>');
                }
            }
            catch (IOException err)
            {
                throw WriteFailure(err);
            }
        }

        //return;
        /// <summary>
        /// Character data.
        /// </summary>
        public override void Characters(UnicodeString chars, ILocation locationId, int properties)
        {
            if (!started)
            {
                OpenDocument();
            }

            if (requireWellFormed && elementStack.Count == 0 && !Whitespace.IsAllWhite(chars) && !unfailing)
            {
                throw new XPathException("When 'standalone' or 'doctype-system' is specified, " + "the document must be well-formed; but this document contains a top-level text node").WithErrorCode("SEPM0004");
            }

            try
            {
                if (openStartTag)
                {
                    CloseStartTag();
                }

                if (chars is WhitespaceString)
                {
                    ((WhitespaceString)chars).Write(writer);
                }
                else if (ReceiverOption.Contains(properties, ReceiverOption.NO_SPECIAL_CHARS))
                {
                    writer.Write(chars);
                }
                else if (!ReceiverOption.Contains(properties, ReceiverOption.DISABLE_ESCAPING))
                {
                    marked = ReceiverOption.Contains(properties, ReceiverOption.USE_NULL_MARKERS);
                    WriteEscape(chars, false);
                    marked = false;
                }
                else
                {

                    // disable-output-escaping="yes"
                    if (TestCharacters(chars) == 0)
                    {
                        if (!ReceiverOption.Contains(properties, ReceiverOption.USE_NULL_MARKERS))
                        {

                            // null (0) characters will be used before and after any section of
                            // the value generated from a character map
                            writer.Write(chars);
                        }
                        else
                        {

                            // Need to strip out any null markers. See test output-html109
                            IIntIterator iter = chars.CodePoints();
                            while (iter.MoveNext())
                            {
                                int c = iter.Current;
                                if (c != 0)
                                {
                                    writer.WriteCodePoint(c);
                                }
                            }
                        }
                    }
                    else
                    {

                        // Using disable output escaping with characters
                        // that are not available in the target encoding
                        // The required action is to ignore d-o-e in respect of those characters that are
                        // not available in the encoding. This is slow...
                        IIntIterator iter = chars.CodePoints();
                        while (iter.MoveNext())
                        {
                            int c = iter.Current;
                            if (c != 0)
                            {
                                if (characterSet.InCharset(c))
                                {
                                    writer.WriteCodePoint(c);
                                }
                                else
                                {
                                    WriteEscape(new UnicodeChar(c), false);
                                }
                            }
                        }
                    }
                }
            }
            catch (IOException err)
            {
                throw WriteFailure(err);
            }
        }

        //return;
        public override void ProcessingInstruction(string target, UnicodeString data, ILocation locationId, int properties)
        {
            if (!started)
            {
                OpenDocument();
            }

            int x = TestCharacters(StringView.Of(target));
            if (x != 0)
            {
                if (unfailing)
                {
                    target = ConvertToAscii(StringView.Of(target)).ToString();
                }
                else
                {
                    throw new XPathException("Character in processing instruction name cannot be represented " + "in the selected encoding (code " + x + ')').WithErrorCode("SERE0008");
                }
            }

            data = Literal(data);
            x = TestCharacters(data);
            if (x != 0)
            {
                if (unfailing)
                {
                    data = ConvertToAscii(data);
                }
                else
                {
                    throw new XPathException("Character in processing instruction data cannot be represented " + "in the selected encoding (code " + x + ')').WithErrorCode("SERE0008");
                }
            }

            try
            {
                if (openStartTag)
                {
                    CloseStartTag();
                }

                writer.WriteAscii(StringConstants.PI_START);
                writer.Write(target);
                if (!data.IsEmpty())
                {
                    writer.WriteCodePoint(0x20);
                    writer.Write(data);
                }

                writer.WriteAscii(StringConstants.PI_END);
            }
            catch (IOException err)
            {
                throw WriteFailure(err);
            }
        }

        //return;
        protected virtual void WriteEscape(UnicodeString chars, bool inAttribute)
        {
            long segstart = 0;
            bool disabled = false;
            bool[] specialChars = inAttribute ? attSpecials : specialInText;
            if (chars is WhitespaceString)
            {
                ((WhitespaceString)chars).WriteEscape(specialChars, writer);
                return;
            }

            Func<int, bool> special = inAttribute ? isSpecialInAttribute : isSpecialInText;
            long clength = chars.Length();
            while (segstart < clength)
            {

                // find a maximal sequence of "ordinary" characters
                long found = chars.IndexWhere(special, segstart);
                long i = found == -1 ? clength : found;

                // if this was the whole (or remainder of the) string write it out and exit
                if (found < 0)
                {
                    if (segstart == 0)
                    {
                        writer.Write(chars);
                    }
                    else
                    {
                        writer.Write(chars.Substring(segstart, clength));
                    }

                    return;
                }


                // otherwise write out this sequence
                if (i > segstart)
                {
                    writer.Write(chars.Substring(segstart, i));
                }


                // examine the special character that interrupted the scan
                int c = chars.CodePointAt(i);
                if (c == 0)
                {

                    // the expander's mark switches escaping on and off; U+0000 of the data is no character
                    if (marked)
                    {
                        disabled = !disabled;
                    }
                    else
                    {
                        WriteReplacement();
                    }
                }
                else if (disabled)
                {
                    if (c > 127 && !characterSet.InCharset(c))
                    {
                        throw new XPathException("Character " + c + " (x" + (c).ToString("x", CultureInfo.InvariantCulture) + ") is not available in the chosen encoding").WithErrorCode("SERE0008");
                    }

                    WriteCodePoint(c);
                }
                else if (c < 127)
                {

                    // process special ASCII characters
                    switch (c)
                    {
                        case '<':
                            writer.WriteAscii(StringConstants.ESCAPE_LT);
                            break;
                        case '>':
                            writer.WriteAscii(StringConstants.ESCAPE_GT);
                            break;
                        case '&':
                            writer.WriteAscii(StringConstants.ESCAPE_AMP);
                            break;
                        case '"':
                            writer.WriteAscii(StringConstants.ESCAPE_QUOT);
                            break;
                        case '\'':
                            writer.WriteAscii(StringConstants.ESCAPE_APOS);
                            break;
                        case '\n':
                            writer.WriteAscii(StringConstants.ESCAPE_NL);
                            break;
                        case '\r':
                            writer.WriteAscii(StringConstants.ESCAPE_CR);
                            break;
                        case '\t':
                            writer.WriteAscii(StringConstants.ESCAPE_TAB);
                            break;
                        default:

                            // C0 control characters: a reference in XML 1.1, which has them; XML 1.0 does not
                            if (xml11)
                            {
                                characterReferenceGenerator.OutputCharacterReference(c, writer);
                            }
                            else
                            {
                                WriteReplacement();
                            }

                            break;
                    }
                }
                else if (c < 160 || c == 0x2028)
                {

                    // XML 1.1 requires these characters to be written as character references
                    characterReferenceGenerator.OutputCharacterReference(c, writer);
                }
                else if (c > 65535)
                {
                    if (characterSet.InCharset(c))
                    {
                        WriteCodePoint(c);
                    }
                    else
                    {
                        characterReferenceGenerator.OutputCharacterReference(c, writer);
                    }
                }
                else if (IsNoCharacter(c))
                {
                    WriteReplacement();
                }
                else
                {

                    // process characters not available in the current encoding
                    characterReferenceGenerator.OutputCharacterReference(c, writer);
                }

                segstart = ++i;
            }
        }

        //return;
        protected virtual void WriteCodePoint(int c)
        {
            writer.WriteCodePoint(c);
        }

        // U+FFFD, for what is no character of the XML being written.
        private protected void WriteReplacement()
        {
            if (characterSet.InCharset(0xFFFD))
            {
                writer.WriteCodePoint(0xFFFD);
            }
            else
            {
                characterReferenceGenerator.OutputCharacterReference(0xFFFD, writer);
            }
        }

        // The text as a comment, a processing instruction or a system identifier holds it, where no character
        // reference can stand for a character: U+FFFD for what the output cannot have ('?' where the encoding has no
        // U+FFFD either). The same text comes back when there is nothing of the kind in it.
        private protected virtual UnicodeString Literal(UnicodeString chars)
        {
            // one of two delegates made once: every comment and instruction of the output is scanned here
            Func<int, bool> unfit = xml11 ? noLiteral11 : noLiteral10;
            return chars.IndexWhere(unfit, 0) < 0 ? chars : Replaced(chars, unfit, false);
        }

        // The text with U+FFFD for each character a test picks out - or '?', where no reference can stand for a
        // character and the encoding has no U+FFFD.
        private protected UnicodeString Replaced(UnicodeString chars, Func<int, bool> unfit, bool referable)
        {
            int replacement = referable || characterSet == null || characterSet.InCharset(0xFFFD) ? 0xFFFD : '?';
            UnicodeBuilder clean = new UnicodeBuilder();
            long length = chars.Length();
            for (long i = 0; i < length; i++)
            {
                int c = chars.CodePointAt(i);
                clean.Append(unfit(c) ? replacement : c);
            }

            return clean.ToUnicodeString();
        }

        //return;
        public override void Comment(UnicodeString chars, ILocation locationId, int properties)
        {
            if (!started)
            {
                OpenDocument();
            }

            chars = Literal(chars);
            int x = TestCharacters(chars);
            if (x != 0)
            {
                if (unfailing)
                {
                    chars = ConvertToAscii(chars);
                }
                else
                {
                    throw new XPathException("Character in comment cannot be represented " + "in the selected encoding (code " + x + ')').WithErrorCode("SERE0008");
                }
            }

            try
            {
                if (openStartTag)
                {
                    CloseStartTag();
                }

                writer.WriteAscii(StringConstants.COMMENT_START);
                writer.Write(chars);
                writer.WriteAscii(StringConstants.COMMENT_END);
            }
            catch (IOException err)
            {
                throw WriteFailure(err);
            }
        }

        //return;
        public override bool UsesTypeAnnotations()
        {
            return false;
        }

        //return;
        public virtual bool IsStarted()
        {
            return started;
        }
    }
}
