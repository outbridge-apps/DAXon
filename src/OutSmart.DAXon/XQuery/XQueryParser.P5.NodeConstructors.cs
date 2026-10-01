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
    // XQueryParser part: computed node constructors (document/element/attribute/text/comment/PI/
    // namespace) and try/catch.
    public partial class XQueryParser
    {
        protected override Expression ParseConstructor()
        {
            int offset = t.currentTokenStartOffset;
            switch (t.currentToken)
            {
                case Token.TAG:
                    Expression tag = ParsePseudoXML(false);
                    LookAhead();
                    t.State = Tokenizer.OPERATOR_STATE;
                    NextToken();
                    return tag;
                case Token.KEYWORD_CURLY:
                    string keyword = t.currentTokenValue;
                    switch (keyword)
                    {
                        case "validate":
                            Grumble("A validate expression is not allowed within a path expression");

                            //if (nodeKind.equals("validate")) {
                            // this allows a validate{} expression to appear as an operand of '/', which the grammar does not allow
                            // return parseValidateExpression();
                            break;
                        case "ordered":
                        case "unordered":

                            // these are currently no-ops in Saxon
                            NextToken();
                            Expression content;
                            if (t.currentToken == Token.RCURLY && allowXPath31Syntax)
                            {
                                content = Literal.MakeEmptySequence();
                            }
                            else
                            {
                                content = ParseExpression();
                            }

                            Expect(Token.RCURLY);
                            LookAhead(); // must be done manually after an RCURLY
                            NextToken();
                            return content;
                        case "document":
                            return ParseDocumentConstructor(offset);
                        case "element":
                            return ParseComputedElementConstructor(offset);
                        case "attribute":
                            return ParseComputedAttributeConstructor(offset);
                        case "text":
                            return ParseTextNodeConstructor(offset);
                        case "comment":
                            return ParseCommentConstructor(offset);
                        case "processing-instruction":
                            return ParseProcessingInstructionConstructor(offset);
                        case "namespace":
                            return ParseNamespaceConstructor(offset);
                        case "switch":
                            return ParseSwitchExpression();
                        default:
                            Grumble("Unrecognized keyword '" + t.currentTokenValue + "' before {...} ");
                            break;
                    }

                    break;
                case Token.ELEMENT_QNAME:
                    return ParseNamedElementConstructor(offset);
                case Token.ATTRIBUTE_QNAME:
                    return ParseNamedAttributeConstructor(offset);
                case Token.NAMESPACE_QNAME:
                    return ParseNamedNamespaceConstructor(offset);
                case Token.PI_QNAME:
                    return ParseNamedProcessingInstructionConstructor(offset);
            }

            return new ErrorExpression();
        }

        /*clause.getRangeVariable()*/
        //
        //
        private Expression ParseDocumentConstructor(int offset)
        {
            NextToken();
            Expression content;
            if (t.currentToken == Token.RCURLY && allowXPath31Syntax)
            {
                content = Literal.MakeEmptySequence();
            }
            else
            {
                content = ParseExpression();
            }

            Expect(Token.RCURLY);
            LookAhead(); // must be done manually after an RCURLY
            NextToken();
            DocumentInstr doc = new DocumentInstr(false, null);
            if (!((QueryModule)env).IsPreserveNamespaces())
            {
                content = new CopyOf(content, false, Validation.PRESERVE, null, true);
            }

            doc.SetValidationAction(((QueryModule)env).ConstructionMode, null);
            doc.SetContentExpression(content);
            SetLocation(doc, offset);
            return doc;
        }

        /*clause.getRangeVariable()*/
        //
        //
        private Expression ParseComputedElementConstructor(int offset)
        {
            NextToken();

            // get the expression that yields the element name
            Expression name = ParseExpression();
            Expect(Token.RCURLY);
            LookAhead(); // must be done manually after an RCURLY
            NextToken();
            Expect(Token.LCURLY);
            t.State = Tokenizer.DEFAULT_STATE;
            NextToken();
            Expression content = null;
            if (t.currentToken != Token.RCURLY)
            {

                // get the expression that yields the element content
                content = ParseExpression();

                // if the child expression creates another element,
                // suppress validation, as the parent already takes care of it
                if (content is ElementCreator && ((ElementCreator)content).GetSchemaType() == null)
                {
                    ((ElementCreator)content).SetValidationAction(Validation.PRESERVE, null);
                }

                Expect(Token.RCURLY);
            }

            LookAhead(); // done manually after an RCURLY
            NextToken();
            Instruction inst;
            if (name is Literal)
            {
                IGroundedValue vName = ((Literal)name).GroundedValue;

                // if element name is supplied as a literal, treat it like a direct element constructor
                INodeName elemName;
                if (vName is StringValue && !(vName is AnyURIValue))
                {
                    string lex = ((StringValue)vName).GetStringValue();
                    try
                    {
                        QNameParser oldQP = GetQNameParser();
                        SetQNameParser(oldQP.WithUnescaper(null));
                        elemName = MakeNodeName(lex, true);
                        SetQNameParser(oldQP);
                        elemName.ObtainFingerprint(env.GetConfiguration().GetNamePool());
                    }
                    catch (XPathException staticError)
                    {
                        if (staticError.HasErrorCode("XPST0008", "XPST0081"))
                        {
                            staticError.SetErrorCode("XQDY0074");
                        }
                        else if (staticError.HasErrorCode("XPST0003"))
                        {

                            Grumble("Invalid QName in element constructor: " + lex, "XQDY0074", offset);
                            return new ErrorExpression();
                        }

                        staticError.SetLocator(MakeLocation());
                        staticError.SetIsStaticError(false);
                        return new ErrorExpression(new XmlProcessingException(staticError));
                    }
                }
                else if (vName is QualifiedNameValue)
                {
                    NamespaceUri uri = ((QualifiedNameValue)vName).GetNamespaceURI();
                    elemName = new FingerprintedQName("", uri, ((QualifiedNameValue)vName).LocalName);
                    elemName.ObtainFingerprint(env.GetConfiguration().GetNamePool());
                }
                else
                {
                    Grumble("Element name must be either a string or a QName", "XPTY0004", offset);
                    return new ErrorExpression();
                }

                inst = new FixedElement(elemName, ((QueryModule)env).ActiveNamespaceBindings, ((QueryModule)env).IsInheritNamespaces(), true, null, ((QueryModule)env).ConstructionMode);
                if (content == null)
                {
                    content = Literal.MakeEmptySequence();
                }

                if (!((QueryModule)env).IsPreserveNamespaces())
                {
                    content = new CopyOf(content, false, Validation.PRESERVE, null, true);
                }

                ((FixedElement)inst).SetContentExpression(content);
                SetLocation(inst, offset);

                return MakeTracer(inst, elemName.GetStructuredQName());
            }
            else
            {

                // it really is a computed element constructor: save the namespace context
                INamespaceResolver ns = new NamespaceResolverWithDefault(env.GetNamespaceResolver(), env.GetDefaultElementNamespace());
                inst = new ComputedElement(name, null, null, ((QueryModule)env).ConstructionMode, ((QueryModule)env).IsInheritNamespaces(), true);
                SetLocation(inst);
                if (content == null)
                {
                    content = Literal.MakeEmptySequence();
                }

                if (!((QueryModule)env).IsPreserveNamespaces())
                {
                    content = new CopyOf(content, false, Validation.PRESERVE, null, true);
                }

                ((ComputedElement)inst).SetContentExpression(content);
                SetLocation(inst, offset);

                return MakeTracer(inst, null);
            }
        }

        /*clause.getRangeVariable()*/
        //
        //
        private Expression ParseNamedElementConstructor(int offset)
        {
            INodeName nodeName = MakeNodeName(t.currentTokenValue, true);
            Expression content = null;
            NextToken();
            if (t.currentToken != Token.RCURLY)
            {
                content = ParseExpression();
                Expect(Token.RCURLY);
            }

            LookAhead(); // after an RCURLY
            NextToken();
            FixedElement el2 = new FixedElement(nodeName, ((QueryModule)env).ActiveNamespaceBindings, ((QueryModule)env).IsInheritNamespaces(), true, null, ((QueryModule)env).ConstructionMode);
            SetLocation(el2, offset);
            if (content == null)
            {
                content = Literal.MakeEmptySequence();
            }

            if (!((QueryModule)env).IsPreserveNamespaces())
            {
                content = new CopyOf(content, false, Validation.PRESERVE, null, true);
            }

            el2.SetContentExpression(content);
            return MakeTracer(el2, nodeName.GetStructuredQName());
        }

        /*clause.getRangeVariable()*/
        //
        //
        private Expression ParseComputedAttributeConstructor(int offset)
        {
            NextToken();
            Expression name = ParseExpression();
            Expect(Token.RCURLY);
            LookAhead(); // must be done manually after an RCURLY
            NextToken();
            Expect(Token.LCURLY);
            t.State = Tokenizer.DEFAULT_STATE;
            NextToken();
            Expression content = null;
            if (t.currentToken != Token.RCURLY)
            {
                content = ParseExpression();
                Expect(Token.RCURLY);
            }

            LookAhead(); // after an RCURLY
            NextToken();
            if (name is Literal)
            {
                IGroundedValue vName = ((Literal)name).GroundedValue;
                if (vName is StringValue && !(vName is AnyURIValue))
                {
                    string lex = ((StringValue)vName).GetStringValue();
                    if (lex.Equals("xmlns") || lex.StartsWith("xmlns:", StringComparison.Ordinal))
                    {
                        Grumble("Cannot create a namespace using an attribute constructor", "XQDY0044", offset);
                    }

                    INodeName attributeName;
                    try
                    {
                        QNameParser oldQP = GetQNameParser();
                        SetQNameParser(oldQP.WithUnescaper(null));
                        attributeName = MakeNodeName(lex, false);
                        SetQNameParser(oldQP);
                    }
                    catch (XPathException staticError)
                    {
                        staticError.SetLocator(MakeLocation());
                        if (staticError.HasErrorCode("XPST0008", "XPST0081"))
                        {
                            staticError.SetErrorCode("XQDY0074");
                        }
                        else if (staticError.HasErrorCode("XPST0003"))
                        {
                            Grumble("Invalid QName in attribute constructor: " + lex, "XQDY0074", offset);
                            return new ErrorExpression();
                        }

                        throw;
                    }

                    if ((attributeName.GetPrefix().Length == 0) && !attributeName.HasURI(NamespaceUri.NULL))
                    {
                        attributeName = new FingerprintedQName("_", attributeName.GetNamespaceUri(), attributeName.GetLocalPart(), attributeName.Fingerprint);
                    }

                    FixedAttribute fatt = new FixedAttribute(attributeName, Validation.STRIP, null);
                    fatt.SetRejectDuplicates();
                    MakeSimpleContent(content, fatt, offset);
                    return MakeTracer(fatt, null);
                }
                else if (vName is QNameValue)
                {
                    QNameValue qnv = (QNameValue)vName;
                    INodeName attributeName = new FingerprintedQName(qnv.GetPrefix(), qnv.GetNamespaceURI(), qnv.LocalName);
                    attributeName.ObtainFingerprint(env.GetConfiguration().GetNamePool());
                    FixedAttribute fatt = new FixedAttribute(attributeName, Validation.STRIP, null);
                    fatt.SetRejectDuplicates();
                    MakeSimpleContent(content, fatt, offset);
                    return MakeTracer(fatt, null);
                }
            }

            ComputedAttribute att = new ComputedAttribute(name, null, Validation.STRIP, null, true);
            att.SetRejectDuplicates();
            MakeSimpleContent(content, att, offset);
            return MakeTracer(att, null);
        }

        /*clause.getRangeVariable()*/
        //
        //
        private Expression ParseNamedAttributeConstructor(int offset)
        {
            string warningMessage = null;
            if (t.currentTokenValue.Equals("xmlns") || t.currentTokenValue.StartsWith("xmlns:", StringComparison.Ordinal))
            {
                warningMessage = "Cannot create a namespace declaration using an attribute constructor";
            }

            INodeName attributeName = MakeNodeName(t.currentTokenValue, false);
            if (!attributeName.HasURI(NamespaceUri.NULL) && (attributeName.GetPrefix().Length == 0))
            {

                // This must be because the name was given as Q{uri}local. Invent a prefix.
                attributeName = new FingerprintedQName("_", attributeName.GetNamespaceUri(), attributeName.GetLocalPart());
            }

            Expression attContent = null;
            NextToken();
            if (t.currentToken != Token.RCURLY)
            {
                attContent = ParseExpression();
                Expect(Token.RCURLY);
            }

            LookAhead(); // after an RCURLY
            NextToken();
            if (warningMessage == null)
            {
                FixedAttribute att2 = new FixedAttribute(attributeName, Validation.STRIP, null);
                att2.SetRejectDuplicates();
                att2.SetRetainedStaticContext(env.MakeRetainedStaticContext());
                MakeSimpleContent(attContent, att2, offset);
                return MakeTracer(att2, attributeName.GetStructuredQName());
            }
            else
            {
                Warning(warningMessage, "XQDY0044");
                return new ErrorExpression(warningMessage, "XQDY0044", false);
            }
        }

        /*clause.getRangeVariable()*/
        //
        //
        private Expression ParseTextNodeConstructor(int offset)
        {
            NextToken();
            Expression value;
            if (t.currentToken == Token.RCURLY && allowXPath31Syntax)
            {
                value = Literal.MakeEmptySequence();
            }
            else
            {
                value = ParseExpression();
            }

            Expect(Token.RCURLY);
            LookAhead(); // after an RCURLY
            NextToken();
            Expression select = Stringify(value, true, env);
            ValueOf vof = new ValueOf(select, false, true);
            SetLocation(vof, offset);
            return MakeTracer(vof, null);
        }

        /*clause.getRangeVariable()*/
        //
        //
        private Expression ParseCommentConstructor(int offset)
        {
            NextToken();
            Expression value;
            if (t.currentToken == Token.RCURLY && allowXPath31Syntax)
            {
                value = Literal.MakeEmptySequence();
            }
            else
            {
                value = ParseExpression();
            }

            Expect(Token.RCURLY);
            LookAhead(); // after an RCURLY
            NextToken();
            Comment com = new Comment();
            MakeSimpleContent(value, com, offset);
            return MakeTracer(com, null);
        }

        /*clause.getRangeVariable()*/
        //
        //
        private Expression ParseProcessingInstructionConstructor(int offset)
        {
            NextToken();
            Expression name = ParseExpression();
            Expect(Token.RCURLY);
            LookAhead(); // must be done manually after an RCURLY
            NextToken();
            Expect(Token.LCURLY);
            t.State = Tokenizer.DEFAULT_STATE;
            NextToken();
            Expression content = null;
            if (t.currentToken != Token.RCURLY)
            {
                content = ParseExpression();
                Expect(Token.RCURLY);
            }

            LookAhead(); // after an RCURLY
            NextToken();
            ProcessingInstruction pi = new ProcessingInstruction(name);
            MakeSimpleContent(content, pi, offset);
            return MakeTracer(pi, null);
        }

        /*clause.getRangeVariable()*/
        //
        //
        private Expression ParseNamedProcessingInstructionConstructor(int offset)
        {
            string target = t.currentTokenValue;
            string warningMessage = null;
            if (target.Equals("xml", StringComparison.OrdinalIgnoreCase))
            {
                warningMessage = "A processing instruction must not be named 'xml' in any combination of upper and lower case";
            }

            if (!NameChecker.IsValidNCName(target))
            {
                Grumble("Invalid processing instruction name " + Err.Wrap(target));
            }

            Expression piName = new StringLiteral(target);
            Expression piContent = null;
            NextToken();
            if (t.currentToken != Token.RCURLY)
            {
                piContent = ParseExpression();
                Expect(Token.RCURLY);
            }

            LookAhead(); // after an RCURLY
            NextToken();
            if (warningMessage == null)
            {
                ProcessingInstruction pi2 = new ProcessingInstruction(piName);
                MakeSimpleContent(piContent, pi2, offset);
                return MakeTracer(pi2, null);
            }
            else
            {
                Warning(warningMessage, "XQDY0064");
                return new ErrorExpression(warningMessage, "XQDY0064", false);
            }
        }

        /*clause.getRangeVariable()*/
        //
        //
        protected override Expression ParseTryCatchExpression()
        {
            if (!allowXPath30Syntax)
            {
                Grumble("try/catch requires XQuery 3.0");
            }

            int offset = t.currentTokenStartOffset;
            NextToken();
            Expression tryExpr;
            if (t.currentToken == Token.RCURLY && allowXPath31Syntax)
            {
                tryExpr = Literal.MakeEmptySequence();
            }
            else
            {
                tryExpr = ParseExpression();
            }

            TryCatch tryCatch = new TryCatch(tryExpr);
            SetLocation(tryCatch, offset);
            Expect(Token.RCURLY);
            LookAhead(); // must be done manually after an RCURLY
            NextToken();
            bool foundOneCatch = false;
            IList<IQNameTest> tests = new List<IQNameTest>();
            while (IsKeyword("catch"))
            {
                tests.Clear();
                foundOneCatch = true;
                bool seenCurly = false;
                do
                {
                    NextToken();
                    string tokv = t.currentTokenValue;
                    switch (t.currentToken)
                    {
                        case Token.NAME:
                            NextToken();
                            tests.Add(MakeQNameTest(Types.Type.ELEMENT, tokv));
                            break;
                        case Token.KEYWORD_CURLY:
                            NextToken();
                            tests.Add(MakeQNameTest(Types.Type.ELEMENT, tokv));
                            seenCurly = true;
                            break;
                        case Token.PREFIX:
                            NextToken();
                            tests.Add(MakeNamespaceTest(Types.Type.ELEMENT, tokv));
                            break;
                        case Token.SUFFIX:
                            NextToken();
                            tokv = t.currentTokenValue;
                            if (t.currentToken == Token.NAME)
                            {
                            }
                            else if (t.currentToken == Token.KEYWORD_CURLY)
                            {

                                // OK
                                seenCurly = true;
                            }
                            else
                            {
                                Grumble("Expected name after '*:'");
                            }

                            NextToken();
                            tests.Add(MakeLocalNameTest(Types.Type.ELEMENT, tokv));
                            break;
                        case Token.STAR:
                        case Token.MULT:
                            NextToken();
                            tests.Add(AnyNodeTest.GetInstance());
                            break;
                        default:
                            Grumble("Unrecognized name test in catch clause at " + Token.tokens[t.currentToken]);
                            return null;
                    }
                }
                while (t.currentToken == Token.UNION && !t.currentTokenValue.Equals("union")); // must be "|" not "union"!
                if (!seenCurly)
                {
                    Expect(Token.LCURLY);
                    NextToken();
                }

                IQNameTest test;
                if (tests.Count == 1)
                {
                    test = tests[0];
                }
                else
                {
                    test = (IQNameTest)new UnionQNameTest(tests);
                }

                catchDepth++;
                Expression catchExpr;
                if (t.currentToken == Token.RCURLY && allowXPath31Syntax)
                {
                    catchExpr = Literal.MakeEmptySequence();
                }
                else
                {
                    catchExpr = ParseExpression();
                }

                tryCatch.AddCatchExpression(test, catchExpr);
                Expect(Token.RCURLY);
                LookAhead(); // must be done manually after an RCURLY
                NextToken();
                catchDepth--;
            }

            if (!foundOneCatch)
            {
                Grumble("After try{}, expected 'catch'");
            }

            return tryCatch;
        }

        /*clause.getRangeVariable()*/
        //
        //
        // OK
        // OK
        private Expression ParseNamespaceConstructor(int offset)
        {
            if (!allowXPath30Syntax)
            {
                Grumble("Namespace node constructors require XQuery 3.0");
            }

            NextToken();
            Expression nameExpr = ParseExpression();
            Expect(Token.RCURLY);
            LookAhead(); // must be done manually after an RCURLY
            NextToken();
            Expect(Token.LCURLY);
            t.State = Tokenizer.DEFAULT_STATE;
            NextToken();
            Expression content = null;
            if (t.currentToken != Token.RCURLY)
            {
                content = ParseExpression();
                Expect(Token.RCURLY);
            }

            LookAhead(); // after an RCURLY
            NextToken();
            NamespaceConstructor instr = new NamespaceConstructor(nameExpr);
            SetLocation(instr);
            MakeSimpleContent(content, instr, offset);
            return MakeTracer(instr, null);
        }

        /*clause.getRangeVariable()*/
        //
        //
        // OK
        // OK
        private Expression ParseNamedNamespaceConstructor(int offset)
        {
            if (!allowXPath30Syntax)
            {
                Grumble("Namespace node constructors require XQuery 3.0");
            }

            string target = t.currentTokenValue;
            if (!NameChecker.IsValidNCName(target))
            {
                Grumble("Invalid namespace prefix " + Err.Wrap(target));
            }

            Expression nsName = new StringLiteral(target);
            Expression nsContent = null;
            NextToken();
            if (t.currentToken != Token.RCURLY)
            {
                nsContent = ParseExpression();
                Expect(Token.RCURLY);
            }

            LookAhead(); // after an RCURLY
            NextToken();
            NamespaceConstructor instr = new NamespaceConstructor(nsName);
            MakeSimpleContent(nsContent, instr, offset);
            return MakeTracer(instr, null);
        }

        /*clause.getRangeVariable()*/
        //
        //
        // OK
        // OK
        protected virtual void MakeSimpleContent(Expression content, SimpleNodeConstructor inst, int offset)
        {
            if (content == null)
            {
                inst.Select = new StringLiteral(StringValue.EMPTY_STRING);
            }
            else
            {
                inst.Select = Stringify(content, false, env);
            }

            SetLocation(inst, offset);
        }

    }
}
