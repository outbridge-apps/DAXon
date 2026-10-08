////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Text;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Expressions.Parsing
{
    // XPathParser part: node tests — kind tests, element/attribute tests, name-test unions, and
    // syntax-version checks.
    public partial class XPathParser
    {
        protected virtual NodeTest ParseNodeTest(short nodeType)
        {
            int tok = t.currentToken;
            string tokv = t.currentTokenValue;
            switch (tok)
            {
                case Token.LPAR:
                    CheckLanguageVersion40();
                    return ParseUnionNodeTest(nodeType);
                case Token.NAME:
                    NextToken();
                    return MakeNameTest(nodeType, tokv, nodeType == Types.Type.ELEMENT);
                case Token.PREFIX:
                    NextToken();
                    return MakeNamespaceTest(nodeType, tokv);
                case Token.SUFFIX:
                    NextToken();
                    tokv = t.currentTokenValue;
                    Expect(Token.NAME);
                    NextToken();
                    return MakeLocalNameTest(nodeType, tokv);
                case Token.STAR:
                    NextToken();
                    return NodeKindTest.MakeNodeKindTest(nodeType);
                case Token.KEYWORD_LBRA:
                    return ParseKindTest();
                default:
                    Grumble("Unrecognized node test");
                    throw new XPathException(""); // unreachable instruction
            }
        }

        protected virtual NodeTest ParseUnionNodeTest(short nodeType)
        {
            NextToken();
            NodeTest test = ParseNodeTest(nodeType);
            while (t.currentToken == Token.UNION && !t.currentTokenValue.Equals("union"))
            {
                NextToken();
                test = new CombinedNodeTest(test, Token.UNION, ParseNodeTest(nodeType));
            }

            Expect(Token.RPAR);
            NextToken();
            return test;
        }

        private NodeTest ParseKindTest()
        {
            NamePool pool = env.GetConfiguration().GetNamePool();
            string typeName = t.currentTokenValue;
            bool empty = false;
            NextToken();
            if (t.currentToken == Token.RPAR)
            {
                empty = true;
                NextToken();
            }

            switch (typeName)
            {
                case "item":
                    Grumble("item() is not allowed in a path expression");
                    return null;
                case "node":
                    if (empty)
                    {
                        return AnyNodeTest.GetInstance();
                    }
                    else
                    {
                        Grumble("Expected ')': no arguments are allowed in node()");
                        return null;
                    }

                case "text":
                    if (empty)
                    {
                        return NodeKindTest.TEXT;
                    }
                    else
                    {
                        Grumble("Expected ')': no arguments are allowed in text()");
                        return null;
                    }

                case "comment":
                    if (empty)
                    {
                        return NodeKindTest.COMMENT;
                    }
                    else
                    {
                        Grumble("Expected ')': no arguments are allowed in comment()");
                        return null;
                    }

                case "namespace-node":
                    if (empty)
                    {
                        if (!IsNamespaceTestAllowed())
                        {
                            Grumble("namespace-node() test is not allowed in XPath 2.0/XQuery 1.0");
                        }

                        return NodeKindTest.NAMESPACE;
                    }
                    else
                    {
                        if (language == ParsedLanguage.EXTENDED_ITEM_TYPE && t.currentToken == Token.NAME)
                        {
                            string nsName = t.currentTokenValue;
                            NextToken();
                            Expect(Token.RPAR);
                            NextToken();
                            return new NameTest(Types.Type.NAMESPACE, NamespaceUri.NULL, nsName, pool);
                        }
                        else
                        {
                            Grumble("No arguments are allowed in namespace-node()");
                            return null;
                        }
                    }

                case "document-node":
                    if (empty)
                    {
                        return NodeKindTest.DOCUMENT;
                    }
                    else
                    {
                        if (!(t.currentTokenValue.Equals("element") || t.currentTokenValue.Equals("schema-element")))
                        {
                            Grumble("Argument to document-node() must be element(...) or schema-element(...)");
                        }

                        NodeTest inner = ParseKindTest();
                        Expect(Token.RPAR);
                        NextToken();
                        return new DocumentNodeTest(inner);
                    }

                case "processing-instruction":
                    int fp = -1;
                    if (empty)
                    {
                        return NodeKindTest.PROCESSING_INSTRUCTION;
                    }
                    else if (t.currentToken == Token.STRING_LITERAL)
                    {
                        string piName = Whitespace.Trim(Unescape(t.currentTokenValue));
                        if (!NameChecker.IsValidNCName(StringTool.CodePoints(piName)))
                        {

                            // Became an error as a result of XPath erratum XP.E7
                            Grumble("Processing instruction name must be a valid NCName", "XPTY0004");
                        }
                        else
                        {
                            fp = pool.AllocateFingerprint(NamespaceUri.NULL, piName);
                        }
                    }
                    else if (t.currentToken == Token.NAME)
                    {
                        try
                        {
                            string[] parts = NameChecker.GetQNameParts(t.currentTokenValue);
                            if ((parts[0].Length == 0))
                            {
                                fp = pool.AllocateFingerprint(NamespaceUri.NULL, parts[1]);
                            }
                            else
                            {
                                Grumble("Processing instruction name must not contain a colon");
                            }
                        }
                        catch (QNameException e)
                        {
                            Grumble("Invalid processing instruction name. " + e.GetMessage());
                        }
                    }
                    else
                    {
                        Grumble("Processing instruction name must be a QName or a string literal");
                    }

                    NextToken();
                    Expect(Token.RPAR);
                    NextToken();
                    return new NameTest(Types.Type.PROCESSING_INSTRUCTION, fp, pool);
                case "schema-attribute":
                    if (empty)
                    {
                        Grumble(typeName + "schema-attribute() requires a name to be supplied");
                        return null;
                    }
                    else
                    {
                        Expect(Token.NAME);
                        string name = t.currentTokenValue;
                        fp = MakeFingerprint(name, false);
                        NextToken();
                        Expect(Token.RPAR);
                        NextToken();
                        if (!env.IsImportedSchema(pool.GetURI(fp)))
                        {
                            Grumble("No schema has been imported for namespace '" + pool.GetURI(fp) + '\'', "XPST0008");
                        }

                        ISchemaDeclaration decl = env.GetConfiguration().GetAttributeDeclaration(fp);
                        if (decl == null)
                        {
                            Grumble("There is no declaration for attribute @" + name + " in an imported schema", "XPST0008");
                            return null;
                        }
                        else
                        {
                            return decl.MakeSchemaNodeTest();
                        }
                    }

                case "schema-element":
                    if (empty)
                    {
                        Grumble(typeName + "schema-element() requires a name to be supplied");
                        return null;
                    }
                    else
                    {
                        Expect(Token.NAME);
                        string name = t.currentTokenValue;
                        fp = MakeFingerprint(name, true);
                        NextToken();
                        Expect(Token.RPAR);
                        NextToken();
                        if (!env.IsImportedSchema(pool.GetURI(fp)))
                        {
                            Grumble("No schema has been imported for namespace '" + pool.GetURI(fp) + '\'', "XPST0008");
                        }

                        ISchemaDeclaration decl = env.GetConfiguration().GetElementDeclaration(fp);
                        if (decl == null)
                        {
                            Grumble("There is no declaration for element " + name + " in an imported schema", "XPST0008");
                            return null;
                        }
                        else
                        {
                            return decl.MakeSchemaNodeTest();
                        }
                    }

                case "attribute":
                case "element":
                    return ParseElementOrAttributeTest(typeName, empty);
                default:

                    // can't happen!
                    Grumble("Unknown node kind " + typeName);
                    return null;
            }
        }

        // Parse an element(...) or attribute(...) kind test (the name-and-type forms), already past the
        // opening keyword; empty says whether the parentheses were empty.
        private NodeTest ParseElementOrAttributeTest(string typeName, bool empty)
        {
            bool isElementTest = typeName.Equals("element");
            int nodeKind = isElementTest ? Types.Type.ELEMENT : Types.Type.ATTRIBUTE;
            NodeTest nodeTest;
            if (empty)
            {
                return isElementTest ? NodeKindTest.ELEMENT : NodeKindTest.ATTRIBUTE;
            }

            IList<NodeTest> tests = ParseNameTestUnion(nodeKind);
            if (tests.Count == 1)
            {
                nodeTest = tests[0];
                if (!allowXPath40Syntax && (nodeTest is NamespaceTest || nodeTest is LocalNameTest))
                {
                    Grumble("Wildcard syntax in item types requires 4.0 to be enabled");
                }
            }
            else
            {
                if (!allowXPath40Syntax)
                {
                    Grumble("NameTestUnion syntax requires 4.0 to be enabled");
                }

                nodeTest = NameTestUnion.WithTests(tests);
            }

            if (t.currentToken == Token.RPAR)
            {
                NextToken();
                return nodeTest;
            }
            else if (t.currentToken == Token.COMMA)
            {
                NextToken();
                NodeTest result;
                if (t.currentToken == Token.NAME)
                {
                    StructuredQName contentType = MakeStructuredQName(t.currentTokenValue, env.GetDefaultElementNamespace());
                    NamespaceUri uri = contentType.GetNamespaceUri();
                    if (!(uri.Equals(NamespaceUri.SCHEMA) || env.IsImportedSchema(uri)))
                    {
                        Grumble("No schema has been imported for namespace '" + uri + '\'', "XPST0008");
                    }

                    ISchemaType schemaType = env.GetConfiguration().GetSchemaType(contentType);
                    if (schemaType == null)
                    {
                        Grumble("Unknown type name " + contentType.EQName, "XPST0008");
                        return null;
                    }

                    if (nodeKind == Types.Type.ATTRIBUTE && schemaType.IsComplexType())
                    {
                        Warning("An attribute cannot have a complex type", DAXonErrorCode.SXWN9041);
                    }

                    NextToken();
                    bool nillable = false;
                    if (t.currentToken == Token.QMARK)
                    {
                        nillable = true;
                        if (nodeKind == Types.Type.ATTRIBUTE)
                        {
                            Grumble("attribute() tests must not be nillable");
                        }

                        NextToken();
                    }

                    if ((schemaType == AnyType.INSTANCE && nillable) || (nodeKind == Types.Type.ATTRIBUTE && schemaType == AnySimpleType.INSTANCE))
                    {
                        result = nodeTest;
                    }
                    else
                    {
                        NodeTest typeTest = new ContentTypeTest(nodeKind, schemaType, env.GetConfiguration(), nillable);
                        if (nodeTest is NodeKindTest)
                        {

                            // this represents element(*,T) or attribute(*,T)
                            result = typeTest;
                        }
                        else
                        {
                            result = new CombinedNodeTest(nodeTest, Token.INTERSECT, typeTest);
                        }
                    }
                }
                else
                {
                    Grumble("Unexpected " + Token.tokens[t.currentToken] + " after ',' in SequenceType");
                    return null;
                }

                Expect(Token.RPAR);
                NextToken();
                return result;
            }
            else
            {
                Grumble("Expected ')' or ',' in SequenceType");
            }

            return null;
        }

        public virtual IList<NodeTest> ParseNameTestUnion(int nodeKind)
        {
            IList<NodeTest> tests = new List<NodeTest>();
            bool matchesAll = false;
            while (true)
            {
                string tokv = t.currentTokenValue;
                switch (t.currentToken)
                {
                    case Token.NAME:
                        NextToken();
                        tests.Add(MakeNameTest(nodeKind, tokv, true));
                        break;
                    case Token.PREFIX:
                        NextToken();
                        tests.Add(MakeNamespaceTest(nodeKind, tokv));
                        break;
                    case Token.SUFFIX:
                        NextToken();
                        tokv = t.currentTokenValue;
                        if (t.currentToken == Token.NAME)
                        {
                        }
                        else
                        {
                            Grumble("Expected name after '*:'");
                        }

                        NextToken();
                        tests.Add(MakeLocalNameTest(nodeKind, tokv));
                        break;
                    case Token.STAR:
                    case Token.MULT:
                        NextToken();
                        matchesAll = true;
                        break;
                    default:
                        Grumble("Unrecognized name test at " + Token.tokens[t.currentToken]);
                        return null;
                }

                if (t.currentToken == Token.UNION && !t.currentTokenValue.Equals("union"))
                {

                    // must be "|" not "union"!
                    NextToken();
                }
                else
                {
                    break;
                }
            }

            if (matchesAll)
            {

                // If there's a "*" in the list, then anything else gets swallowed
                tests.Clear();
                tests.Add(NodeKindTest.MakeNodeKindTest(nodeKind));
            }

            return tests;
        }

        protected virtual bool IsNamespaceTestAllowed()
        {
            return allowXPath30Syntax;
        }

        protected virtual void CheckLanguageVersion30()
        {
            if (!allowXPath30Syntax)
            {
                Grumble("To use XPath 3.0 syntax, you must configure the XPath parser to handle it");
            }
        }

        protected virtual void CheckLanguageVersion31()
        {
            if (!allowXPath31Syntax)
            {
                Grumble("The XPath parser is not configured to allow use of XPath 3.1 syntax");
            }
        }

        protected virtual void CheckLanguageVersion40()
        {
            string lang = GetLanguage();
            if (!allowXPath40Syntax)
            {
                Grumble("The parser is not configured to allow use of " + lang + " 4.0 syntax");
            }
        }

        protected virtual void CheckMapExtensions()
        {
            if (!(allowXPath31Syntax || allowXPath30XSLTExtensions))
            {
                Grumble("The XPath parser is not configured to allow use of the map syntax from XSLT 3.0 or XPath 3.1");
            }
        }

        public virtual void CheckSyntaxExtensions(string construct)
        {
            if (!allowXPath40Syntax)
            {
                Grumble("Saxon XPath syntax extensions have not been enabled: " + construct + " is not allowed");
            }
        }

    }
}
