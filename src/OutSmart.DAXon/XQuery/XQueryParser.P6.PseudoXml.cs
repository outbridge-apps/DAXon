////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Functions.Registry;
using OutSmart.DAXon.Values.Arrays;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Serialization;
using OutSmart.DAXon.Serialization.CharCodes;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Xslt;
using OutSmart.DAXon.Trees.Utilities;
using OutSmart.DAXon.Collections;
using OutSmart.DAXon.Internal.Net;
using OutSmart.DAXon.Internal.Regex;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Expressions.Flwor;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
using OutSmart.DAXon.Internal.Streams;
using System.IO;
namespace OutSmart.DAXon.XQuery
{
    // XQueryParser part: direct (pseudo-XML) constructors — tags, attribute/element content, CDATA,
    // entity references, string constructors; lexical helpers and nested detail types.
    public partial class XQueryParser
    {
        private Expression ParsePseudoXML(bool allowEndTag)
        {
            Expression exp;
            int offset = t.inputOffset;

            // we're reading raw characters, so we don't want the currentTokenStartOffset
            char c = t.NextChar();
            switch (c)
            {
                case '!':
                    c = t.NextChar();
                    if (c == '-')
                    {
                        exp = ParseCommentConstructor();
                    }
                    else if (c == '[')
                    {
                        Grumble("A CDATA section is allowed only in element content");
                        return null; // if CDATA were allowed here, we would have already read it
                    }
                    else
                    {
                        Grumble("Expected '--' or '[CDATA[' after '<!'");
                        return null;
                    }

                    break;
                case '?':
                    exp = ParsePIConstructor();
                    break;
                case '/':
                    if (allowEndTag)
                    {
                        StringBuilder sb = new StringBuilder(16);
                        while (true)
                        {
                            c = t.NextChar();
                            if (c == '>')
                            {
                                break;
                            }
                            else if (c == Tokenizer.NUL)
                            {
                                Grumble("Expected '>' after '/'; found end of input");
                            }

                            sb.Append(c);
                        }

                        return new StringLiteral(sb.ToString());
                    }

                    Grumble("Unmatched XML end tag");
                    return new ErrorExpression();
                case Tokenizer.NUL:
                    Grumble("End of input encountered while parsing direct constructor");
                    return new ErrorExpression();
                default:
                    t.UnreadChar();
                    exp = ParseDirectElementConstructor(allowEndTag);
                    break;
            }

            SetLocation(exp, offset);
            return exp;
        }

        /*clause.getRangeVariable()*/
        //
        //
        // OK
        // OK
        private Expression ParseDirectElementConstructor(bool isNested)
        {
            // Nested direct constructors (`<a><a>...`) are their own recursive descent: they read
            // raw characters and never pass through ParseExprSingle, so this production needs its
            // own guard or a deeply nested query kills the process.
            try
            {
                Internal.StackGuard.Probe();
            }
            catch (Internal.RecursionDepthError e) when (!e.Described)
            {
                throw e.Describe("Element constructors are too deeply nested (insufficient stack on this thread)", "XPST0003", null);
            }

            NamePool pool = env.GetConfiguration().GetNamePool();
            bool changesContext = false;
            int offset = t.inputOffset - 1;

            // we're reading raw characters, so we don't want the currentTokenStartOffset
            char c;
            StringBuilder buff = new StringBuilder(64);
            int namespaceCount = 0;
            while (true)
            {
                c = t.NextChar();
                if (c == ' ' || c == '\n' || c == '\r' || c == '\t' || c == '/' || c == '>')
                {
                    break;
                }
                else if (c == Tokenizer.NUL)
                {
                    Grumble("Found end of input while reading element name in XQuery element constructor");
                }

                buff.Append(c);
            }

            string elname = buff.ToString();
            if ((elname.Length == 0))
            {
                Grumble("Expected element name after '<'");
            }


            //Used LinkedHashMap because it is friendly to retain the order of attributes.
            Dictionary<string, AttributeDetails> attributes = new Dictionary<string, AttributeDetails>(10);
            while (true)
            {

                // loop through the attributes
                // We must process namespace declaration attributes first;
                // their scope applies to all preceding attribute names and values.
                // But finding the delimiting quote of an attribute value requires the
                // XPath expressions to be parsed, because they may contain nested quotes.
                // So we parse in "scanOnly" mode, which ignores any undeclared @namespace
                // prefixes, use the result of this parse to determine the length of the
                // attribute value, save the value, and reparse it when all the @namespace
                // declarations have been dealt with.
                c = SkipSpaces(c);
                if (c == '/' || c == '>')
                {
                    break;
                }
                else if (c == Tokenizer.NUL)
                {
                    Grumble("End of input encountered within element start tag");
                }

                int attOffset = t.inputOffset - 1;
                buff.Length = 0;

                // read the attribute name
                do
                {
                    buff.Append(c);
                    c = t.NextChar();
                }
                while (c != ' ' && c != '\n' && c != '\r' && c != '\t' && c != '=' && c != Tokenizer.NUL);
                string attName = buff.ToString();
                if (!NameChecker.IsQName(StringTool.CodePoints(attName)))
                {
                    Grumble("Invalid attribute name " + Err.Wrap(attName, Err.ATTRIBUTE));
                }

                c = SkipSpaces(c);
                ExpectChar(c, '=');
                c = t.NextChar();
                c = SkipSpaces(c);
                if (c != '"' && c != '\'')
                {
                    if (c == Tokenizer.NUL)
                    {
                        Grumble("End of input encountered within element start tag");
                    }
                    else
                    {
                        Grumble("Expected ' or \" as attribute delimiter - found '" + c + "'");
                    }
                }

                char delim = c;
                if (c != '"' && c != '\'')
                {
                    Grumble("Expected ' or \" as attribute delimiter - found '" + c + "'");
                }

                bool isNamespace = attName == "xmlns" || attName.StartsWith("xmlns:", StringComparison.Ordinal);
                int end;
                if (isNamespace)
                {
                    end = MakeNamespaceContent(t.input, t.inputOffset, delim);
                    changesContext = true;
                }
                else
                {
                    Expression avt;
                    try
                    {
                        avt = MakeAttributeContent(t.input, t.inputOffset, delim, true);
                    }
                    catch (XPathException err)
                    {
                        if (!err.HasBeenReported())
                        {
                            Grumble(err.Message);
                        }

                        throw;
                    }


                    // by convention, this returns the end position when called with scanOnly set
                    end = (int)((Int64Value)((Literal)avt).GroundedValue).LongValue();
                }

                if (end >= t.input.Length)
                {
                    Grumble("Reached end of input while processing attributes in start tag");
                }


                // save the value with its surrounding quotes
                string val = t.input.Substring(t.inputOffset - 1, end - t.inputOffset + 2) /*Java substring(begin,END) -> C# (start,LENGTH)*/;

                // and without
                string rval = t.input.Substring(t.inputOffset, end - t.inputOffset) /*Java substring(begin,END) -> C# (start,LENGTH)*/;

                // account for any newlines found in the value
                // (note, subexpressions between curlies will have been parsed using a different tokenizer)
                string tail = val;
                int pos;
                while ((pos = tail.IndexOf('\n')) >= 0)
                {
                    t.IncrementLineNumber(t.inputOffset - 1 + pos);
                    tail = tail.Substring(pos + 1);
                }

                t.inputOffset = end + 1;
                if (isNamespace)
                {

                    // Processing follows the resolution of bug 5083: doubled curly braces represent single
                    // curly braces, single curly braces are not allowed.
                    StringBuilder sb = new StringBuilder(rval.Length);
                    bool prevDelim = false;
                    bool prevOpenCurly = false;
                    bool prevCloseCurly = false;
                    for (int i = 0; i < rval.Length; i++)
                    {
                        char n = rval[i];
                        if (n == delim)
                        {
                            prevDelim = !prevDelim;
                            if (prevDelim)
                            {
                                continue;
                            }
                        }

                        if (n == '{')
                        {
                            prevOpenCurly = !prevOpenCurly;
                            if (prevOpenCurly)
                            {
                                continue;
                            }
                        }
                        else if (prevOpenCurly)
                        {
                            Grumble("Namespace must not contain an unescaped opening brace", "XQST0022");
                        }

                        if (n == '}')
                        {
                            prevCloseCurly = !prevCloseCurly;
                            if (prevCloseCurly)
                            {
                                continue;
                            }
                        }
                        else if (prevCloseCurly)
                        {
                            Grumble("Namespace must not contain an unescaped closing brace", "XPST0003");
                        }

                        sb.Append(n);
                    }

                    if (prevOpenCurly)
                    {
                        Grumble("Namespace must not contain an unescaped opening brace", "XQST0022");
                    }

                    if (prevCloseCurly)
                    {
                        Grumble("Namespace must not contain an unescaped closing brace", "XPST0003");
                    }

                    rval = sb.ToString();
                    NamespaceUri uri = NamespaceUri.Of(UriLiteral(rval));
                    if (!StandardURIChecker.GetInstance().IsValidURI(uri.ToString()))
                    {
                        Grumble("Namespace must be a valid URI value", "XQST0046");
                    }

                    string prefix;
                    if (attName == "xmlns")
                    {
                        prefix = "";
                        if (uri.Equals(NamespaceUri.XML))
                        {
                            Grumble("Cannot have the XML namespace as the default namespace", "XQST0070");
                        }
                    }
                    else
                    {
                        prefix = attName.Substring(6);
                        if (prefix.Equals("xml") && !uri.Equals(NamespaceUri.XML))
                        {
                            Grumble("Cannot bind the prefix 'xml' to a namespace other than the XML namespace", "XQST0070");
                        }
                        else if (uri.Equals(NamespaceUri.XML) && !prefix.Equals("xml"))
                        {
                            Grumble("Cannot bind a prefix other than 'xml' to the XML namespace", "XQST0070");
                        }
                        else if (prefix.Equals("xmlns"))
                        {
                            Grumble("Cannot use xmlns as a namespace prefix", "XQST0070");
                        }

                        if (uri.IsEmpty())
                        {
                            if (env.GetConfiguration().XMLVersion == Configuration.XML10)
                            {
                                Grumble("Namespace URI must not be empty", "XQST0085");
                            }
                        }
                    }

                    namespaceCount++;
                    ((QueryModule)env).DeclareActiveNamespace(prefix, uri);
                }

                if (attributes.GetOrDefault(attName) != null)
                {
                    if (isNamespace)
                    {
                        Grumble("Duplicate namespace declaration " + attName, "XQST0071", attOffset);
                    }
                    else
                    {
                        Grumble("Duplicate attribute name " + attName, "XQST0040", attOffset);
                    }
                }


                //                grumble("Value of xml:id must be a valid NCName", "XQST0082");
                //            }
                AttributeDetails a = new AttributeDetails();
                a.value = val;
                a.startOffset = attOffset;
                attributes[attName] = a;

                // on return, the current character is the closing quote
                c = t.NextChar();
                if (!(c == ' ' || c == '\n' || c == '\r' || c == '\t' || c == '/' || c == '>'))
                {
                    Grumble("There must be whitespace after every attribute except the last");
                }
            }

            StructuredQName qName = null;
            if (scanOnly)
            {
                qName = StandardNames.GetStructuredQName(StandardNames.XSL_ELEMENT); // any name will do
            }
            else
            {
                try
                {
                    string[] parts = NameChecker.GetQNameParts(elname);
                    NamespaceUri @namespace = ((QueryModule)env).CheckURIForPrefix(parts[0]);
                    if (@namespace == null)
                    {
                        Grumble("Undeclared prefix in element name " + Err.Wrap(elname, Err.ELEMENT), "XPST0081", offset);
                    }

                    qName = new StructuredQName(parts[0], @namespace, parts[1]);
                }
                catch (QNameException e)
                {
                    Grumble("Invalid element name " + Err.Wrap(elname, Err.ELEMENT), "XPST0003", offset);
                    qName = StandardNames.GetStructuredQName(StandardNames.XSL_ELEMENT); // any name will do
                }
            }

            int validationMode = ((QueryModule)env).ConstructionMode;
            FingerprintedQName fqn = new FingerprintedQName(qName.GetPrefix(), qName.GetNamespaceUri(), qName.GetLocalPart(), pool.AllocateFingerprint(qName.GetNamespaceUri(), qName.GetLocalPart()));
            FixedElement elInst = new FixedElement(fqn, ((QueryModule)env).ActiveNamespaceBindings, ((QueryModule)env).IsInheritNamespaces(), !isNested, null, validationMode);
            SetLocation(elInst, offset);
            IList<Expression> contents = new List<Expression>(10);
            IntHashSet attFingerprints = new IntHashSet(attributes.Count);

            // we've checked for duplicate lexical QNames, but not for duplicate expanded-QNames
            foreach (KeyValuePair<string, AttributeDetails> entry in attributes)
            {
                string attName = entry.Key;
                AttributeDetails a = entry.Value;
                string attValue = a.value;
                int attOffset = a.startOffset;
                if (attName == "xmlns" || attName.StartsWith("xmlns:", StringComparison.Ordinal))
                {
                }
                else if (scanOnly)
                {
                }
                else
                {
                    INodeName attributeName = null;
                    NamespaceUri attNamespace;
                    try
                    {
                        string[] parts = NameChecker.GetQNameParts(attName);
                        if ((parts[0].Length == 0))
                        {

                            // attributes don't use the default namespace
                            attNamespace = NamespaceUri.NULL;
                        }
                        else
                        {
                            attNamespace = ((QueryModule)env).CheckURIForPrefix(parts[0]);
                        }

                        if (attNamespace == null)
                        {
                            Grumble("Undeclared prefix in attribute name " + Err.Wrap(attName, Err.ATTRIBUTE), "XPST0081", attOffset);
                        }

                        attributeName = new FingerprintedQName(parts[0], attNamespace, parts[1]);
                        int key = attributeName.ObtainFingerprint(pool);
                        if (attFingerprints.Contains(key))
                        {
                            Grumble("Duplicate expanded attribute name " + attName, "XQST0040", attOffset);
                        }

                        attFingerprints.Add(key);
                    }
                    catch (QNameException e)
                    {
                        Grumble("Invalid attribute name " + Err.Wrap(attName, Err.ATTRIBUTE), "XPST0003", attOffset);
                    }

                    FixedAttribute attInst = new FixedAttribute(attributeName, Validation.STRIP, null);
                    SetLocation(attInst);
                    Expression select;
                    try
                    {
                        select = MakeAttributeContent(attValue, 1, attValue[0], false);
                    }
                    catch (XPathException err)
                    {
                        err.SetIsStaticError(true);
                        throw;
                    }

                    attInst.SetRetainedStaticContext(env.MakeRetainedStaticContext());
                    attInst.Select = select;
                    attInst.SetRejectDuplicates();
                    SetLocation(attInst);
                    contents.Add(MakeTracer(attInst, attributeName.GetStructuredQName()));
                }
            }

            if (c == '/')
            {

                // empty element tag
                ExpectChar(t.NextChar(), '>');
            }
            else
            {
                ReadElementContent(elname, contents);
            }

            Expression[] elk = new Expression[contents.Count];
            for (int i = 0; i < contents.Count; i++)
            {

                if (validationMode != Validation.STRIP)
                {
                    contents[i].SuppressValidation(validationMode);
                }

                elk[i] = contents[i];
            }

            Block block = new Block(elk);
            if (changesContext)
            {
                block.SetRetainedStaticContext(env.MakeRetainedStaticContext());
            }

            elInst.SetContentExpression(block);

            // reset the @in-scope namespaces to what they were before
            for (int n = 0; n < namespaceCount; n++)
            {
                ((QueryModule)env).UndeclareNamespace();
            }

            return MakeTracer(elInst, qName);
        }

        /*clause.getRangeVariable()*/
        //
        //
        // OK
        // OK
        private Expression MakeAttributeContent(string avt, int start, char terminator, bool scanOnly)
        {
            ILocation loc = MakeLocation();
            IList<Expression> components = new List<Expression>(10);
            int i0, i1, i2, i8, i9, len, last;
            last = start;
            len = avt.Length;
            while (last < len)
            {
                i2 = avt.IndexOf(terminator, last);
                if (i2 < 0)
                {
                    XPathException e = new XPathException("Attribute constructor is not properly terminated");
                    e.SetIsStaticError(true);
                    throw e;
                }

                i0 = avt.IndexOf('{', last);
                i1 = avt.IndexOf("{{", last, StringComparison.Ordinal);
                i8 = avt.IndexOf('}', last);
                i9 = avt.IndexOf("}}", last, StringComparison.Ordinal);
                if ((i0 < 0 || i2 < i0) && (i8 < 0 || i2 < i8))
                {

                    // found end of string
                    AddStringComponent(components, avt, last, i2);

                    // look for doubled quotes, and skip them (for now)
                    if (i2 + 1 < avt.Length && avt[i2 + 1] == terminator)
                    {
                        components.Add(new StringLiteral(terminator + ""));
                        last = i2 + 2; //continue;
                    }
                    else
                    {
                        last = i2;
                        break;
                    }
                } // found a "}"
                else if (i8 >= 0 && (i0 < 0 || i8 < i0))
                {

                    // found a "}"
                    if (i8 != i9)
                    {

                        // a "}" that isn't a "}}"
                        XPathException e = new XPathException("Closing curly brace in attribute value template \"" + avt + "\" must be doubled");
                        e.SetIsStaticError(true);
                        throw e;
                    }

                    AddStringComponent(components, avt, last, i8 + 1);
                    last = i8 + 2;
                } // found a doubled "{{"
                else if (i1 >= 0 && i1 == i0)
                {

                    // found a doubled "{{"
                    AddStringComponent(components, avt, last, i1 + 1);
                    last = i1 + 2;
                } // found a single "{"
                else if (i0 >= 0)
                {

                    // found a single "{"
                    if (i0 > last)
                    {
                        AddStringComponent(components, avt, last, i0);
                    }

                    Expression exp;
                    XPathParser parser = NewParser();
                    ((XQueryParser)parser).executable = executable;
                    parser.SetAllowAbsentExpression(allowXPath31Syntax);
                    parser.SetScanOnly(scanOnly);
                    parser.SetRangeVariableStack(rangeVariables);
                    parser.SetCatchDepth(catchDepth);
                    exp = parser.Parse(avt, i0 + 1, Token.RCURLY, env);
                    if (!scanOnly)
                    {
                        exp = exp.Simplify();
                    }

                    last = parser.GetTokenizer().currentTokenStartOffset + 1;
                    components.Add(MakeStringJoin(exp, env));
                }
                else
                {
                    throw new InvalidOperationException("Internal error parsing direct attribute constructor");
                }
            }


            // if this is simply a prescan, return the position of the end of the
            // AVT, so we can parse it properly later
            if (scanOnly)
            {
                return Literal.MakeLiteral(Int64Value.MakeIntegerValue(last));
            }


            // is it empty?
            if (components.Count == 0)
            {
                return new StringLiteral(StringValue.EMPTY_STRING);
            }


            // is it a single component?
            if (components.Count == 1)
            {
                return components[0];
            }


            // otherwise, return an expression that concatenates the components
            Expression[] args = new Expression[components.Count];
            args = components.ToArray();
            RetainedStaticContext rsc = new RetainedStaticContext(env);
            Expression fn = SystemFunction.MakeCall("concat", rsc, args);
            fn.SetLocation(loc);
            return fn; //return visitor.simplify(fn);
        }

        /*clause.getRangeVariable()*/
        //
        //
        // OK
        // OK
        private void AddStringComponent(IList<Expression> components, string avt, int start, int end)
        {

            // analyze fixed text within the value of a direct attribute constructor.
            if (start < end)
            {
                StringBuilder sb = new StringBuilder(end - start);
                for (int i = start; i < end; i++)
                {
                    char c = avt[i];
                    switch (c)
                    {
                        case '&':
                            {
                                int semic = avt.IndexOf(';', i);
                                if (semic < 0)
                                {
                                    Grumble("No closing ';' found for entity or character reference");
                                }
                                else
                                {
                                    string entity = avt.Substring(i + 1, semic - i - 1) /*Java substring(begin,END) -> C# (start,LENGTH)*/;
                                    sb.Append(new Unescaper(env.GetConfiguration().ValidCharacterChecker).AnalyzeEntityReference(entity));
                                    i = semic;
                                }

                                break;
                            }

                        case '<':
                            Grumble("The < character must not appear in attribute content");
                            break;
                        case '\n':
                        case '\t':
                            sb.Append(' ');
                            break;
                        case '\r':
                            sb.Append(' ');
                            if (i + 1 < end && avt[i + 1] == '\n')
                            {
                                i++;
                            }

                            break;
                        default:
                            sb.Append(c);
                            break;
                    }
                }

                components.Add(new StringLiteral(sb.ToString()));
            }
        }

        /*clause.getRangeVariable()*/
        //
        //
        // OK
        // OK
        private int MakeNamespaceContent(string avt, int start, char terminator)
        {
            int i2, len, last;
            last = start;
            len = avt.Length;
            while (last < len)
            {
                i2 = avt.IndexOf(terminator, last);
                if (i2 < 0)
                {
                    XPathException e = new XPathException("Namespace declaration is not properly terminated");
                    e.SetIsStaticError(true);
                    throw e;
                }


                // look for doubled quotes, and skip them (for now)
                if (i2 + 1 < avt.Length && avt[i2 + 1] == terminator)
                {
                    last = i2 + 2; //continue;
                }
                else
                {
                    last = i2;
                    break;
                }
            }


            // return the position of the end of the literal
            return last;
        }

        /*clause.getRangeVariable()*/
        //
        //
        // OK
        // OK
        private void ReadElementContent(string startTag, IList<Expression> components)
        {
            bool afterEnclosedExpr = false;
            while (true)
            {

                // read all the components of the element value
                StringBuilder text = new StringBuilder(64);
                char c;
                bool containsEntities = false;
                while (true)
                {
                    c = t.NextChar();
                    if (c == '<')
                    {

                        // See if we've got a CDATA section
                        if (t.NextChar() == '!')
                        {
                            if (t.NextChar() == '[')
                            {
                                ReadCDATASection(text);
                                containsEntities = true;
                                continue;
                            }
                            else
                            {
                                t.UnreadChar();
                                t.UnreadChar();
                            }
                        }
                        else
                        {
                            t.UnreadChar();
                        }

                        break;
                    }
                    else if (c == '&')
                    {
                        text.Append(ReadEntityReference());
                        containsEntities = true;
                    }
                    else if (c == '}')
                    {
                        c = t.NextChar();
                        if (c != '}')
                        {
                            Grumble("'}' must be written as '}}' within element content");
                        }

                        text.Append(c);
                    }
                    else if (c == '{')
                    {
                        c = t.NextChar();
                        if (c != '{')
                        {
                            c = '{';
                            break;
                        }

                        text.Append(c);
                    }
                    else if (c == Tokenizer.NUL)
                    {
                        Grumble("Reached end of input while reading XQuery element content");
                    }
                    else
                    {
                        if (!charChecker.Test(c) && !UTF16CharacterSet.IsSurrogate(c))
                        {
                            Grumble("Character code " + c + " is not a valid XML character");
                        }

                        text.Append(c);
                    }
                }

                string textStr = text.ToString();
                if (!(textStr.Length == 0) && (containsEntities | ((QueryModule)env).IsPreserveBoundarySpace() || !Whitespace.IsAllWhite(StringView.Of(textStr))))
                {
                    ValueOf inst = new ValueOf(new StringLiteral(new StringValue(textStr)), false, false);
                    SetLocation(inst);
                    components.Add(inst);
                    afterEnclosedExpr = false;
                }

                if (c == '<')
                {
                    Expression exp = ParsePseudoXML(true);

                    // An end tag can appear here, and is returned as a string value
                    if (exp is StringLiteral)
                    {
                        string endTag = ((StringLiteral)exp).GetString().ToString();
                        if (Whitespace.IsWhite(endTag[0]))
                        {
                            Grumble("End tag contains whitespace before the name");
                        }

                        endTag = Whitespace.Trim(endTag);
                        if (endTag.Equals(startTag))
                        {
                            return;
                        }
                        else
                        {
                            Grumble("End tag </" + endTag + "> does not match start tag <" + startTag + '>', "XQST0118"); // error code allocated by spec bug 11609
                        }
                    }
                    else
                    {
                        components.Add(exp);
                    }
                }
                else
                {

                    // we read an '{' indicating an enclosed expression
                    if (afterEnclosedExpr)
                    {
                        Expression previousComponent = components[components.Count - 1];
                        bool previousComponentIsNodeTest = true;
                        UType previousItemType = previousComponent.GetStaticUType(UType.ANY);
                        previousComponentIsNodeTest = UType.ANY_NODE.Subsumes(previousItemType);
                        if (!previousComponentIsNodeTest)
                        {

                            // Add a zero-length text node, to prevent {"a"}{"b"} generating an intervening space
                            // See tests (qxmp132, qxmp261)
                            ValueOf inst = new ValueOf(new StringLiteral(StringValue.EMPTY_STRING), false, false);
                            SetLocation(inst);
                            components.Add(inst);
                        }
                    }

                    t.UnreadChar();
                    t.State = Tokenizer.DEFAULT_STATE;
                    LookAhead();
                    NextToken();
                    if (t.currentToken == Token.RCURLY && allowXPath31Syntax)
                    {
                        components.Add(Literal.MakeEmptySequence());
                    }
                    else
                    {
                        Expression exp = ParseExpression();
                        if (!((QueryModule)env).IsPreserveNamespaces())
                        {
                            exp = new CopyOf(exp, false, Validation.PRESERVE, null, true);
                        }

                        components.Add(exp);
                        Expect(Token.RCURLY);
                    }

                    afterEnclosedExpr = true;
                }
            }
        }

        /*clause.getRangeVariable()*/
        //
        //
        // OK
        // OK
        private Expression ParsePIConstructor()
        {
            StringBuilder pi = new StringBuilder(64);
            int firstSpace = -1;
            // Tail check on the builder itself: ToString().EndsWith per appended character
            // re-copies the whole body every iteration, which is quadratic in the body length
            // (a 200 KB constructor took 16 s to compile; same in the CDATA and comment loops).
            while (!(pi.Length >= 2 && pi[pi.Length - 2] == '?' && pi[pi.Length - 1] == '>'))
            {
                char c = t.NextChar();
                if (c == Tokenizer.NUL)
                {
                    Grumble("Found end of input while reading processing instruction constructor");
                }

                if (firstSpace < 0 && " \t\r\n".IndexOf(c) >= 0)
                {
                    firstSpace = pi.Length;
                }

                pi.Append(c);
            }

            pi.Length = pi.Length - 2;
            string target;
            string data = "";
            if (firstSpace < 0)
            {

                // there is no data part
                target = pi.ToString();
            }
            else
            {

                // trim leading space from the data part, but not trailing space
                target = pi.ToString().Substring(0, firstSpace);
                firstSpace++;
                while (firstSpace < pi.Length && " \t\r\n".IndexOf(pi[firstSpace]) >= 0)
                {
                    firstSpace++;
                }

                data = pi.ToString().Substring(firstSpace);
            }

            if (!NameChecker.IsValidNCName(target))
            {
                Grumble("Invalid processing instruction name " + Err.Wrap(target));
            }

            if (target.Equals("xml", StringComparison.OrdinalIgnoreCase))
            {
                Grumble("A processing instruction must not be named 'xml' in any combination of upper and lower case");
            }

            ProcessingInstruction instruction = new ProcessingInstruction(new StringLiteral(target));
            instruction.Select = new StringLiteral(data);
            SetLocation(instruction);
            return instruction;
        }

        /*clause.getRangeVariable()*/
        //
        //
        // OK
        // OK
        private void ReadCDATASection(StringBuilder cdata)
        {
            char c;

            // CDATA section
            c = t.NextChar();
            ExpectChar(c, 'C');
            c = t.NextChar();
            ExpectChar(c, 'D');
            c = t.NextChar();
            ExpectChar(c, 'A');
            c = t.NextChar();
            ExpectChar(c, 'T');
            c = t.NextChar();
            ExpectChar(c, 'A');
            c = t.NextChar();
            ExpectChar(c, '[');
            // Tail check on the builder: see ParsePIConstructor for why not ToString().EndsWith.
            while (!(cdata.Length >= 3 && cdata[cdata.Length - 3] == ']' && cdata[cdata.Length - 2] == ']' && cdata[cdata.Length - 1] == '>'))
            {
                char cc = t.NextChar();
                if (cc == Tokenizer.NUL)
                {
                    Grumble("No closing ']]>' found for CDATA section");
                }

                cdata.Append(cc);
            }

            cdata.Length = cdata.Length - 3;
        }

        /*clause.getRangeVariable()*/
        //
        //
        // OK
        // OK
        private Expression ParseCommentConstructor()
        {
            char c = t.NextChar();

            // XML-like comment
            ExpectChar(c, '-');
            StringBuilder comment = new StringBuilder(256);
            // Tail check on the builder: see ParsePIConstructor for why not ToString().EndsWith.
            while (!(comment.Length >= 2 && comment[comment.Length - 2] == '-' && comment[comment.Length - 1] == '-'))
            {
                char cc = t.NextChar();
                if (cc == Tokenizer.NUL)
                {
                    Grumble("Reached end of input while reading XML comment constructor");
                }

                comment.Append(cc);
            }

            if (t.NextChar() != '>')
            {
                Grumble("'--' is not permitted in an XML comment");
            }

            string commentText = comment.ToString(0, (comment.Length - 2) - (0));
            Comment instruction = new Comment();
            instruction.Select = new StringLiteral(new StringValue(commentText.ToString()));
            SetLocation(instruction);
            return instruction;
        }

        /*clause.getRangeVariable()*/
        //
        //
        // OK
        // OK
        public static Expression Stringify(Expression exp, bool noNodeIfEmpty, IStaticContext env)
        {

            // Compare with XSLLeafNodeConstructor.makeSimpleContentConstructor
            // Fast path if given a string literal
            if (exp is StringLiteral)
            {
                return exp;
            }

            if (exp.LocalRetainedStaticContext == null)
            {
                exp.SetRetainedStaticContext(env.MakeRetainedStaticContext());
            }


            // Atomize the result
            exp = Atomizer.MakeAtomizer(exp, null);

            // Convert each atomic value to a string
            exp = new AtomicSequenceConverter(exp, BuiltInAtomicType.STRING);

            //((AtomicSequenceConverter) exp).allocateConverter(config, false);
            // Join the resulting strings with a separator
            exp = SystemFunction.MakeCall("string-join", exp.GetRetainedStaticContext(), exp, new StringLiteral(StringValue.SINGLE_SPACE));
            if (noNodeIfEmpty)
            {
                ((StringJoin)((SystemFunctionCall)exp).TargetFunction).SetReturnEmptyIfEmpty(true);
            }


            // All that's left for the instruction to do is to construct the right kind of node
            return exp;
        }

        /*clause.getRangeVariable()*/
        //
        //
        // OK
        // OK
        protected override
        Literal MakeStringLiteral(string token, bool doUnescaping)
        {
            if (doUnescaping)
            {
                StringLiteral lit;
                if (token.IndexOf('&') == -1)
                {
                    lit = new StringLiteral(token);
                }
                else
                {
                    string sb = Unescape(token);
                    lit = new StringLiteral(StringValue.MakeStringValue(sb));
                }

                SetLocation(lit);
                return lit;
            }
            else
            {
                return base.MakeStringLiteral(token, doUnescaping);
            }
        }

        /*clause.getRangeVariable()*/
        protected override string Unescape(string token)
        {
            return new Unescaper(env.GetConfiguration().ValidCharacterChecker).Unescape(token);
        }

        /*clause.getRangeVariable()*/
        //
        //
        // OK
        // OK
        private string ReadEntityReference()
        {
            StringBuilder sb = new StringBuilder(64);
            while (true)
            {
                char c = t.NextChar();
                if (c == ';')
                {
                    break;
                }
                else if (c == Tokenizer.NUL)
                {
                    Grumble("No closing ';' found for entity or character reference");
                    return ""; // to keep the Java compiler happy
                }

                sb.Append(c);
            }

            string entity = sb.ToString();
            return new Unescaper(env.GetConfiguration().ValidCharacterChecker).AnalyzeEntityReference(entity);
        }

        /*clause.getRangeVariable()*/
        //
        //
        // OK
        // OK
        protected override Expression ParseStringConstructor()
        {

            // For legacy reasons (see bug 4208) parsing of string constructors is split
            // rather arbitrarily between the parser and tokenizer. This method is called
            // when the tokenizer has seen the sequence ``[xxxx`{ which it reports as
            // a STRING_CONSTRUCTOR_INITIAL token. At this point it hands over to the parser,
            // which continues by parsing the enclosed expression, and then reading
            // character-by-character to get the literal content outside the enclosed expressions.
            int offset = t.currentTokenStartOffset;
            if (!allowXPath31Syntax)
            {
                throw new XPathException("String constructor expressions require XQuery 3.1");
            }

            IList<Expression> components = new List<Expression>();
            components.Add(new StringLiteral(t.currentTokenValue));
            t.Next();
            while (true)
            {
                bool emptyExpression = t.currentToken == Token.RCURLY;
                if (emptyExpression)
                {
                    components.Add(new StringLiteral(StringValue.EMPTY_STRING));
                }
                else
                {
                    Expression enclosed = ParseExpression();
                    Expression stringJoin = SystemFunction.MakeCall("string-join", env.MakeRetainedStaticContext(), enclosed, new StringLiteral(" "));
                    components.Add(stringJoin);
                }

                if (t.currentToken != Token.RCURLY)
                {
                    Grumble("Expected '}' after enclosed expression in string constructor");
                }

                StringBuilder sb = new StringBuilder(256);
                char c = t.NextChar();
                if (c != '`')
                {
                    Grumble("Expected '}`' after enclosed expression in string constructor");
                }

                char prior = (char)0;
                char penult = (char)0;
                bool continueOuter = false;
                while (true)
                {
                    c = t.NextChar();
                    if (c == Tokenizer.NUL)
                    {
                        Grumble("Reached end of input while reading string constructor");
                    }

                    if (prior == '`' && c == '{')
                    {
                        sb.Length = sb.Length - 1;
                        components.Add(new StringLiteral(sb.ToString()));
                        t.LookAhead();
                        t.Next();
                        if (t.currentToken == Token.RCURLY)
                        {
                            components.Add(Literal.MakeEmptySequence());
                            sb.Length = 0;
                            continue;
                        }
                        else
                        {
                            continueOuter = true;
                            break;
                        }
                    }
                    else if (penult == ']' && prior == '`' && c == '`')
                    {
                        sb.Length = sb.Length - 2;
                        components.Add(new StringLiteral(sb.ToString()));
                        t.LookAhead();
                        t.Next();
                        continueOuter = false;
                        break;
                    }

                    sb.Append(c);
                    penult = prior;
                    prior = c;
                }

                if (!continueOuter)
                {
                    break;
                }
            }

            Expression[] args = components.ToArray();
            Expression result = SystemFunction.MakeCall("concat", env.MakeRetainedStaticContext(), args);
            SetLocation(result, offset);
            return result;
        }

        /*clause.getRangeVariable()*/
        //
        //
        // OK
        // OK
        public virtual string UriLiteral(string @in)
        {
            return Whitespace.Collapse(Unescape(@in)).ToString();
        }

        /*clause.getRangeVariable()*/
        //
        //
        // OK
        // OK
        protected virtual void LookAhead()
        {
            try
            {
                t.LookAhead();
            }
            catch (XPathException err)
            {
                Grumble(err.Message);
            }
        }

        /*clause.getRangeVariable()*/
        //
        //
        // OK
        // OK
        protected override bool AtStartOfRelativePath()
        {
            return t.currentToken == Token.TAG || base.AtStartOfRelativePath(); // "<" after "/" is recognized in XQuery but not in XPath.
        }

        /*clause.getRangeVariable()*/
        //
        //
        // OK
        // OK
        protected override void TestPermittedAxis(int axis, string errorCode)
        {
            base.TestPermittedAxis(axis, errorCode);
            if (axis == AxisInfo.NAMESPACE && language == ParsedLanguage.XQUERY)
            {
                Grumble("The namespace axis is not available in XQuery", errorCode);
            }
        }

        /*clause.getRangeVariable()*/
        //
        //
        // OK
        // OK
        private char SkipSpaces(char c)
        {
            while (c == ' ' || c == '\n' || c == '\r' || c == '\t')
            {
                c = t.NextChar();
            }

            return c;
        }

        /*clause.getRangeVariable()*/
        //
        //
        // OK
        // OK
        private void ExpectChar(char actual, char expected)
        {
            if (actual != expected)
            {
                Grumble("Expected '" + expected + "', found " + (actual == Tokenizer.NUL ? "end of input" : "'" + actual + "'"));
            }
        }

        /*clause.getRangeVariable()*/
        //
        //
        // OK
        // OK
        protected override string GetLanguage()
        {
            return "XQuery";
        }

        /*clause.getRangeVariable()*/
        private sealed class SortSpec
        {
            public Expression sortKey;
            public bool ascending;
            public bool emptyLeast;
            public string collation;
        }

        /*clause.getRangeVariable()*/
        public class Unescaper
        {
            private readonly IIntPredicateProxy characterChecker;
            public Unescaper(IIntPredicateProxy characterChecker)
            {
                this.characterChecker = characterChecker;
            }

            public virtual string Unescape(string token)
            {
                StringBuilder sb = new StringBuilder(token.Length);
                for (int i = 0; i < token.Length; i++)
                {
                    char c = token[i];
                    if (c == '&')
                    {
                        int semic = token.IndexOf(';', i);
                        if (semic < 0)
                        {
                            throw new XPathException("No closing ';' found for entity or character reference", "XPST0003");
                        }
                        else
                        {
                            string entity = token.Substring(i + 1, semic - i - 1) /*Java substring(begin,END) -> C# (start,LENGTH)*/;
                            sb.Append(AnalyzeEntityReference(entity));
                            i = semic;
                        }
                    }
                    else
                    {
                        sb.Append(c);
                    }
                }

                return sb.ToString();
            }

            public virtual string AnalyzeEntityReference(string entity)
            {
                if (entity == "lt")
                {
                    return "<";
                }
                else if (entity == "gt")
                {
                    return ">";
                }
                else if (entity == "amp")
                {
                    return "&";
                }
                else if (entity == "quot")
                {
                    return "\"";
                }
                else if (entity == "apos")
                {
                    return "'";
                }
                else if (entity.Length < 2 || entity[0] != '#')
                {
                    throw new XPathException("invalid character reference &" + entity + ';', "XPST0003");
                }
                else
                {

                    //entity = entity.toLowerCase();
                    return ParseCharacterReference(entity);
                }
            }

            private string ParseCharacterReference(string entity)
            {
                int value = 0;
                if (entity[1] == 'x')
                {
                    if (entity.Length < 3)
                    {
                        throw new XPathException("No hex digits in hexadecimal character reference", "XPST0003");
                    }

                    entity = entity.ToLowerInvariant();
                    for (int i = 2; i < entity.Length; i++)
                    {
                        int digit = "0123456789abcdef".IndexOf(entity[i]);
                        if (digit < 0)
                        {
                            throw new XPathException("Invalid hex digit '" + entity[i] + "' in character reference", "XPST0003");
                        }

                        value = (value * 16) + digit;
                        if (value > UTF16CharacterSet.NONBMP_MAX)
                        {
                            throw new XPathException("Character reference exceeds Unicode codepoint limit", "XQST0090");
                        }
                    }
                }
                else
                {
                    for (int i = 1; i < entity.Length; i++)
                    {
                        int digit = "0123456789".IndexOf(entity[i]);
                        if (digit < 0)
                        {
                            throw new XPathException("Invalid digit '" + entity[i] + "' in decimal character reference", "XPST0003");
                        }

                        value = (value * 10) + digit;
                        if (value > UTF16CharacterSet.NONBMP_MAX)
                        {
                            throw new XPathException("Character reference exceeds Unicode codepoint limit", "XQST0090");
                        }
                    }
                }

                if (!characterChecker.Test(value))
                {
                    throw new XPathException("Invalid XML character reference x" + (value).ToString("x"), "XQST0090");
                }


                // following code borrowed from AElfred
                // Check for surrogates: 00000000 0000xxxx yyyyyyyy zzzzzzzz
                //  (1101|10xx|xxyy|yyyy + 1101|11yy|zzzz|zzzz:
                if (value <= 0x0000ffff)
                {

                    // no surrogates needed
                    return "" + (char)value;
                }
                else
                {
                    value -= 0x10000;

                    // > 16 bits, surrogate needed
                    return "" + (char)(0xd800 | (value >> 10)) + (char)(0xdc00 | (value & 0x0003ff));
                }
            }
        }

        /*clause.getRangeVariable()*/
        //
        //
        // OK
        // OK
        private sealed class AttributeDetails
        {
            public string value;
            public int startOffset;
        }

        /*clause.getRangeVariable()*/
        //
        //
        // OK
        // OK
        private sealed class Import
        {
            public NamespaceUri namespaceURI;
            public IList<string> locationURIs;
            public int offset;
        }
    }
}
