////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Serialization;
using OutSmart.DAXon.Internal.Net;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System.IO;
namespace OutSmart.DAXon.XQuery
{
    // XQueryParser part: prolog — version/module declarations, schema/module imports, and all
    // prolog declarations (variables, functions, context item, options, output properties).
    public partial class XQueryParser
    {
        private void ParseVersionDeclaration()
        {
            if (t.currentToken == Token.XQUERY_VERSION)
            {
                NextToken();
                Expect(Token.STRING_LITERAL);
                string queryVersion = Unescape(t.currentTokenValue).ToString();
                string[] allowedVersions = new string[]
                {
                    "1.0",
                    "3.0",
                    "3.1",
                    "4.0"
                };
                if (Array.BinarySearch(allowedVersions, queryVersion, StringComparer.Ordinal) < 0)
                {
                    Grumble("Invalid XQuery version " + queryVersion, "XQST0031");
                }

                if (queryVersion.Equals("4.0"))
                {
                    languageVersion = 40;
                    allowXPath40Syntax = true;
                    t.languageLevel = 40;
                    env.GetPackageData().SetHostLanguage(HostLanguage.XQUERY, 40);
                }

                NextToken();
                if (t.currentTokenValue == "encoding")
                {
                    NextToken();
                    Expect(Token.STRING_LITERAL);
                    if (!encNamePattern.Matcher(Unescape(t.currentTokenValue)).Matches())
                    {
                        Grumble("Encoding name contains invalid characters", "XQST0087");
                    }


                    // we ignore the encoding now: it was handled earlier, while decoding the byte stream
                    NextToken();
                }

                Expect(Token.SEMICOLON);
                NextToken();
            }
            else
            {
                if (t.currentToken == Token.XQUERY_ENCODING)
                {
                    NextToken();
                    Expect(Token.STRING_LITERAL);
                    if (!encNamePattern.Matcher(t.currentTokenValue).Matches())
                    {
                        Grumble("Encoding name contains invalid characters", "XQST0087");
                    }


                    // we ignore the encoding now: it was handled earlier, while decoding the byte stream
                    NextToken();
                    Expect(Token.SEMICOLON);
                    NextToken();
                }
            }
        }

        private void ParseModuleDeclaration()
        {
            Expect(Token.MODULE_NAMESPACE);
            NextToken();
            Expect(Token.NAME);
            string prefix = t.currentTokenValue;
            NextToken();
            Expect(Token.EQUALS);
            NextToken();
            Expect(Token.STRING_LITERAL);
            NamespaceUri uri = NamespaceUri.Of(UriLiteral(t.currentTokenValue));
            CheckProhibitedPrefixes(prefix, uri);
            if (uri.IsEmpty())
            {
                Grumble("Module namespace cannot be \"\"", "XQST0088");
                uri = NamespaceUri.Of("http://saxon.fallback.namespace/"); // for error recovery
            }

            NextToken();
            Expect(Token.SEMICOLON);
            NextToken();
            try
            {
                ((QueryModule)env).ModuleNamespace = uri;
                ((QueryModule)env).DeclarePrologNamespace(prefix, uri);
                executable.AddQueryLibraryModule((QueryModule)env);
            }
            catch (XPathException err)
            {
                err.SetLocator(MakeLocation());
                ReportError(err);
            }
        }

        private void ParseProlog()
        {

            //boolean allowSetters = true;
            bool allowModuleDecl = true;
            bool allowDeclarations = true;
            while (true)
            {
                try
                {
                    if (t.currentToken == Token.MODULE_NAMESPACE)
                    {
                        NamespaceUri uri = ((QueryModule)env).ModuleNamespace;
                        if (uri == null)
                        {
                            Grumble("Module declaration must not be used in a main module");
                        }
                        else
                        {
                            Grumble("Module declaration appears more than once");
                        }

                        if (!allowModuleDecl)
                        {
                            Grumble("Module declaration must precede other declarations in the query prolog");
                        }
                    }

                    allowModuleDecl = false;
                    switch (t.currentToken)
                    {
                        case Token.DECLARE_NAMESPACE:
                            if (!allowDeclarations)
                            {
                                Grumble("Namespace declarations cannot follow variables, functions, or options");
                            }


                            //allowSetters = false;
                            ParseNamespaceDeclaration();
                            break;
                        case Token.DECLARE_ANNOTATED:

                            // we have read "declare %"
                            ProcessPreamble();
                            if (allowDeclarations)
                            {
                                SealNamespaces(namespacesToBeSealed, env.GetConfiguration());
                                allowDeclarations = false;
                            }

                            NextToken();
                            Expect(Token.PERCENT);
                            AnnotationList annotationList = ParseAnnotationsList();
                            if (IsKeyword("function"))
                            {
                                annotationList.Check(env.GetConfiguration(), "DF");
                                ParseFunctionDeclaration(annotationList);
                            }
                            else if (IsKeyword("variable"))
                            {
                                annotationList.Check(env.GetConfiguration(), "DV");
                                ParseVariableDeclaration(annotationList);
                            }
                            else if (IsKeyword("item-type"))
                            {
                                annotationList.Check(env.GetConfiguration(), "DI");
                                ParseItemTypeDeclaration(annotationList);
                            }
                            else
                            {
                                Grumble("Annotations can appear only in 'declare variable' and 'declare function'");
                            }

                            break;
                        case Token.DECLARE_FIXED:
                            CheckLanguageVersion40();
                            NextToken();
                            if (!IsKeyword("default"))
                            {
                                Grumble("expected 'default' after 'declare fixed");
                            }

                            NextToken();
                            Expect(Token.NAME);
                            switch (t.currentTokenValue)
                            {
                                case "element":
                                    if (!allowDeclarations)
                                    {
                                        Grumble("Namespace declarations cannot follow variables, functions, or options");
                                    }

                                    ParseDefaultElementNamespace(true);
                                    break;
                                case "function":
                                    if (!allowDeclarations)
                                    {
                                        Grumble("Namespace declarations cannot follow variables, functions, or options");
                                    }

                                    ParseDefaultFunctionNamespace();
                                    break;
                                default:
                                    Grumble("After 'declare fixed default', expected 'element' or 'function'");
                                    break;
                            }

                            break;
                        case Token.DECLARE_DEFAULT:
                            NextToken();
                            Expect(Token.NAME);
                            switch (t.currentTokenValue)
                            {
                                case "element":
                                    if (!allowDeclarations)
                                    {
                                        Grumble("Namespace declarations cannot follow variables, functions, or options");
                                    }


                                    //allowSetters = false;
                                    ParseDefaultElementNamespace(false);
                                    break;
                                case "function":
                                    if (!allowDeclarations)
                                    {
                                        Grumble("Namespace declarations cannot follow variables, functions, or options");
                                    }


                                    //allowSetters = false;
                                    ParseDefaultFunctionNamespace();
                                    break;
                                case "collation":
                                    if (!allowDeclarations)
                                    {
                                        Grumble("Collation declarations must appear earlier in the prolog");
                                    }

                                    ParseDefaultCollation();
                                    break;
                                case "order":
                                    if (!allowDeclarations)
                                    {
                                        Grumble("Order declarations must appear earlier in the prolog");
                                    }

                                    ParseDefaultOrder();
                                    break;
                                case "decimal-format":
                                    NextToken();
                                    ParseDefaultDecimalFormat();
                                    break;
                                default:
                                    Grumble("After 'declare default', expected 'element', 'function', or 'collation'");
                                    break;
                            }

                            break;
                        case Token.DECLARE_BOUNDARY_SPACE:
                            if (!allowDeclarations)
                            {
                                Grumble("'declare boundary-space' must appear earlier in the query prolog");
                            }

                            ParseBoundarySpaceDeclaration();
                            break;
                        case Token.DECLARE_ORDERING:
                            if (!allowDeclarations)
                            {
                                Grumble("'declare ordering' must appear earlier in the query prolog");
                            }

                            ParseOrderingDeclaration();
                            break;
                        case Token.DECLARE_COPY_NAMESPACES:
                            if (!allowDeclarations)
                            {
                                Grumble("'declare copy-namespaces' must appear earlier in the query prolog");
                            }

                            ParseCopyNamespacesDeclaration();
                            break;
                        case Token.DECLARE_BASEURI:
                            if (!allowDeclarations)
                            {
                                Grumble("'declare @base-uri' must appear earlier in the query prolog");
                            }

                            ParseBaseURIDeclaration();
                            break;
                        case Token.DECLARE_DECIMAL_FORMAT:
                            if (!allowDeclarations)
                            {
                                Grumble("'declare decimal-format' must appear earlier in the query prolog");
                            }

                            ParseDecimalFormatDeclaration();
                            break;
                        case Token.IMPORT_SCHEMA:

                            //allowSetters = false;
                            if (!allowDeclarations)
                            {
                                Grumble("Import schema must appear earlier in the prolog");
                            }

                            ParseSchemaImport();
                            break;
                        case Token.IMPORT_MODULE:

                            //allowSetters = false;
                            if (!allowDeclarations)
                            {
                                Grumble("Import module must appear earlier in the prolog");
                            }

                            ParseModuleImport();
                            break;
                        case Token.DECLARE_VARIABLE:

                            //allowSetters = false;
                            if (allowDeclarations)
                            {
                                SealNamespaces(namespacesToBeSealed, env.GetConfiguration());
                                allowDeclarations = false;
                            }

                            ProcessPreamble();
                            ParseVariableDeclaration(AnnotationList.EMPTY);
                            break;
                        case Token.DECLARE_CONTEXT:

                            //allowSetters = false;
                            if (allowDeclarations)
                            {
                                SealNamespaces(namespacesToBeSealed, env.GetConfiguration());
                                allowDeclarations = false;
                            }

                            ProcessPreamble();
                            ParseContextItemDeclaration();
                            break;
                        case Token.DECLARE_FUNCTION:
                            if (allowDeclarations)
                            {
                                SealNamespaces(namespacesToBeSealed, env.GetConfiguration());
                                allowDeclarations = false;
                            }

                            ProcessPreamble();
                            ParseFunctionDeclaration(AnnotationList.EMPTY);
                            break;
                        case Token.DECLARE_UPDATING:
                            NextToken();
                            if (!IsKeyword("function"))
                            {
                                Grumble("expected 'function' after 'declare updating");
                            }

                            if (allowDeclarations)
                            {
                                SealNamespaces(namespacesToBeSealed, env.GetConfiguration());
                                allowDeclarations = false;
                            }

                            ProcessPreamble();
                            parserExtension.ParseUpdatingFunctionDeclaration(this);
                            break;
                        case Token.DECLARE_OPTION:
                            if (allowDeclarations)
                            {
                                SealNamespaces(namespacesToBeSealed, env.GetConfiguration());
                                allowDeclarations = false;
                            }

                            ParseOptionDeclaration();
                            break;
                        case Token.DECLARE_ITEM_TYPE:
                            CheckSyntaxExtensions("declare item-type");
                            if (allowDeclarations)
                            {
                                SealNamespaces(namespacesToBeSealed, env.GetConfiguration());
                                allowDeclarations = false;
                            }

                            ParseItemTypeDeclaration(AnnotationList.EMPTY);
                            break;
                        case Token.DECLARE_CONSTRUCTION:
                            if (!allowDeclarations)
                            {
                                Grumble("'declare construction' must appear earlier in the query prolog");
                            }

                            ParseConstructionDeclaration();
                            break;
                        case Token.DECLARE_REVALIDATION:
                            if (!allowDeclarations)
                            {
                                Grumble("'declare revalidation' must appear earlier in the query prolog");
                            }

                            parserExtension.ParseRevalidationDeclaration(this);
                            break;
                        case Token.EOF:
                            NamespaceUri uri = ((QueryModule)env).ModuleNamespace;
                            if (uri == null)
                            {
                                Grumble("The main module must contain a query expression after any declarations in the prolog");
                            }
                            else
                            {
                                return;
                            }

                            break;
                        default:
                            return;
                    }

                    Expect(Token.SEMICOLON);
                    NextToken();
                }
                catch (XPathException err)
                {
                    if (err.GetLocator() == null)
                    {
                        err.SetLocator(MakeLocation());
                    }

                    if (!err.HasBeenReported())
                    {
                        errorCount++;
                        if (firstError == null)
                        {
                            firstError = err;
                        }

                        ReportError(err);
                    }


                    // we've reported an error, attempt to recover by skipping to the
                    // next semicolon
                    while (t.currentToken != Token.SEMICOLON)
                    {
                        NextToken();
                        if (t.currentToken == Token.EOF)
                        {
                            return;
                        }
                        else if (t.currentToken == Token.RCURLY)
                        {
                            t.LookAhead();
                        }
                        else if (t.currentToken == Token.TAG)
                        {
                            ParsePseudoXML(true);
                        }
                        else if (t.currentToken == Token.BACKTICK)
                        {
                            // Like RCURLY and TAG, Next() delivers a BACKTICK without refilling
                            // the lookahead; with no explicit LookAhead the same token repeats
                            // with the input offset frozen, and this skip never reaches ';'.
                            t.LookAhead();
                        }
                    }

                    NextToken();
                }
            }
        }

        protected override AnnotationList ParseAnnotationsList()
        {

            // we have read "declare" and have seen "%" as lookahead
            List<Annotation> annotations = new List<Annotation>();
            int options = 0;
            while (true)
            {
                t.State = Tokenizer.BARE_NAME_STATE;
                NextToken();
                Expect(Token.NAME);
                t.State = Tokenizer.DEFAULT_STATE;
                StructuredQName qName;
                NamespaceUri uri;

                qName = MakeStructuredQName(t.currentTokenValue, NamespaceUri.XQUERY);
                uri = qName.GetNamespaceUri();

                //            }
                Annotation annotation = new Annotation(qName);
                if (uri.Equals(NamespaceUri.XQUERY))
                {
                    if (!qName.Equals(Annotation.PRIVATE) && !qName.Equals(Annotation.PUBLIC) && !qName.Equals(Annotation.UPDATING) && !qName.Equals(Annotation.SIMPLE))
                    {
                        Grumble("Unrecognized variable or function annotation " + qName.DisplayName, "XQST0045");
                    }

                    annotation.AddAnnotationParameter(new Int64Value(options));
                }
                else if (IsReservedInQuery(uri))
                {
                    Grumble("The annotation " + t.currentTokenValue + " is in a reserved namespace", "XQST0045"); //            } else if (uri.isEmpty()) {
                    //                grumble("The annotation " + t.currentTokenValue + " is in no namespace", "XQST0045");
                }
                else
                {
                }

                NextToken();
                if (t.currentToken == Token.LPAR)
                {
                    NextToken();
                    if (t.currentToken == Token.RPAR)
                    {
                        Grumble("Annotation parameter list cannot be empty");
                    }

                    while (true)
                    {
                        bool negative = t.currentToken == Token.MINUS;
                        if (negative)
                        {
                            if (!allowXPath40Syntax)
                            {
                                Grumble("Minus sign in annotation value requires 4.0 to be enabled");
                                return null;
                            }

                            NextToken();
                        }

                        Literal arg;
                        switch (t.currentToken)
                        {
                            case Token.STRING_LITERAL:
                                arg = (Literal)ParseStringLiteral(false);
                                break;
                            case Token.NUMBER:
                                arg = (Literal)ParseNumericLiteral(false);
                                break;
                            case Token.HEX_INTEGER:
                                arg = (Literal)ParseHexLiteral(false);
                                break;
                            case Token.BINARY_INTEGER:
                                arg = (Literal)ParseBinaryLiteral(false);
                                break;
                            case Token.FUNCTION:

                                // true() and folse() allowed in 4.0
                                if (t.currentTokenValue.Equals("true"))
                                {
                                    arg = Literal.MakeLiteral(BooleanValue.TRUE);
                                }
                                else if (t.currentTokenValue.Equals("false"))
                                {
                                    arg = Literal.MakeLiteral(BooleanValue.FALSE);
                                }
                                else
                                {
                                    Grumble("The only function calls allowed in an annotation are true() and false()");
                                    return null;
                                }

                                if (!allowXPath40Syntax)
                                {
                                    Grumble("Annotation values true() and false() require 4.0 to be enabled");
                                    return null;
                                }

                                NextToken();
                                Expect(Token.RPAR);
                                NextToken();
                                break;
                            default:
                                Grumble("Annotation parameter must be a literal");
                                return null;
                        }

                        IGroundedValue val = arg.GroundedValue;
                        if (negative)
                        {
                            if (val is NumericValue)
                            {
                                val = ((NumericValue)val).Negate();
                            }
                            else
                            {
                                Grumble("Minus sign in annotation parameter must be followed by a numeric literal");
                            }
                        }

                        if (val is StringValue || val is NumericValue || val is BooleanValue)
                        {
                            annotation.AddAnnotationParameter((AtomicValue)val);
                        }
                        else
                        {
                            Grumble("Annotation parameter must be a string or number");
                        }

                        if (t.currentToken == Token.RPAR)
                        {
                            NextToken();
                            break;
                        }

                        Expect(Token.COMMA);
                        NextToken();
                    }
                }

                annotations.Add(annotation);
                if (t.currentToken != Token.PERCENT)
                {
                    return new AnnotationList(annotations);
                }
            }
        }

        private void SealNamespaces(IList<NamespaceUri> namespacesToBeSealed, Configuration config)
        {
            foreach (NamespaceUri ns in namespacesToBeSealed)
            {
                config.SealNamespace(ns);
            }
        }

        private void ProcessPreamble()
        {
            if (preambleProcessed)
            {
                return;
            }

            preambleProcessed = true;
            if (foundDefaultCollation)
            {
                string collationName = env.GetDefaultCollationName();
                URI collationURI;
                try
                {
                    collationURI = new URI(collationName);
                    if (!collationURI.IsAbsolute())
                    {
                        URI @base = new URI(env.StaticBaseURI);
                        collationURI = @base.Resolve(collationURI);
                        collationName = collationURI.ToString();
                    }
                }
                catch (URISyntaxException)
                {
                    Grumble("Default collation name '" + collationName + "' is not a valid URI", "XQST0046");
                    collationName = NamespaceConstant.CODEPOINT_COLLATION_URI;
                }

                if (env.GetConfiguration().GetCollation(collationName) == null)
                {
                    Grumble("Default collation name '" + collationName + "' is not a recognized collation", "XQST0038");
                    collationName = NamespaceConstant.CODEPOINT_COLLATION_URI;
                }

                ((QueryModule)env).SetDefaultCollationName(collationName);
            }

            foreach (Import imp in schemaImports)
            {
                try
                {
                    ApplySchemaImport(imp);
                }
                catch (XPathException err)
                {
                    if (!err.HasBeenReported())
                    {
                        throw err.MaybeWithLocation(MakeLocation(imp.offset));
                    }
                }
            }

            foreach (Import imp in moduleImports)
            {
                try
                {
                    ApplyModuleImport(imp);
                }
                catch (XPathException err)
                {
                    if (!err.HasBeenReported())
                    {
                        throw err.MaybeWithLocation(MakeLocation(imp.offset));
                    }
                }
            }
        }

        private void ParseDefaultCollation()
        {

            // <"default" "collation"> StringLiteral
            if (foundDefaultCollation)
            {
                Grumble("default collation appears more than once", "XQST0038");
            }

            foundDefaultCollation = true;
            NextToken();
            Expect(Token.STRING_LITERAL);
            string uri = UriLiteral(t.currentTokenValue);
            ((QueryModule)env).SetDefaultCollationName(uri);
            NextToken();
        }

        private void ParseDefaultOrder()
        {
            if (foundEmptyOrderingDeclaration)
            {
                Grumble("empty ordering declaration appears more than once", "XQST0069");
            }

            foundEmptyOrderingDeclaration = true;
            NextToken();
            if (!IsKeyword("empty"))
            {
                Grumble("After 'declare default order', expected keyword 'empty'");
            }

            NextToken();
            if (IsKeyword("least"))
            {
                ((QueryModule)env).SetEmptyLeast(true);
            }
            else if (IsKeyword("greatest"))
            {
                ((QueryModule)env).SetEmptyLeast(false);
            }
            else
            {
                Grumble("After 'declare default order empty', expected keyword 'least' or 'greatest'");
            }

            NextToken();
        }

        private void ParseBoundarySpaceDeclaration()
        {
            if (foundBoundarySpaceDeclaration)
            {
                Grumble("'declare boundary-space' appears more than once", "XQST0068");
            }

            foundBoundarySpaceDeclaration = true;
            NextToken();
            Expect(Token.NAME);
            if (t.currentTokenValue == "preserve")
            {
                ((QueryModule)env).SetPreserveBoundarySpace(true);
            }
            else if (t.currentTokenValue == "strip")
            {
                ((QueryModule)env).SetPreserveBoundarySpace(false);
            }
            else
            {
                Grumble("boundary-space must be 'preserve' or 'strip'");
            }

            NextToken();
        }

        private void ParseOrderingDeclaration()
        {
            if (foundOrderingDeclaration)
            {
                Grumble("ordering mode declaration appears more than once", "XQST0065");
            }

            foundOrderingDeclaration = true;
            NextToken();
            Expect(Token.NAME);
            if (t.currentTokenValue != "ordered" && t.currentTokenValue != "unordered")
            {
                Grumble("ordering mode must be 'ordered' or 'unordered'");
            }

            NextToken();
        }

        private void ParseCopyNamespacesDeclaration()
        {
            if (foundCopyNamespaces)
            {
                Grumble("declare copy-namespaces appears more than once", "XQST0055");
            }

            foundCopyNamespaces = true;
            NextToken();
            Expect(Token.NAME);
            if (t.currentTokenValue == "preserve")
            {
                ((QueryModule)env).SetPreserveNamespaces(true);
            }
            else if (t.currentTokenValue == "no-preserve")
            {
                ((QueryModule)env).SetPreserveNamespaces(false);
            }
            else
            {
                Grumble("copy-namespaces must be followed by 'preserve' or 'no-preserve'");
            }

            NextToken();
            Expect(Token.COMMA);
            NextToken();
            Expect(Token.NAME);
            if (t.currentTokenValue == "inherit")
            {
                ((QueryModule)env).SetInheritNamespaces(true);
            }
            else if (t.currentTokenValue == "no-inherit")
            {
                ((QueryModule)env).SetInheritNamespaces(false);
            }
            else
            {
                Grumble("After the comma in the copy-namespaces declaration, expected 'inherit' or 'no-inherit'");
            }

            NextToken();
        }

        private void ParseConstructionDeclaration()
        {
            if (foundConstructionDeclaration)
            {
                Grumble("declare construction appears more than once", "XQST0067");
            }

            foundConstructionDeclaration = true;
            NextToken();
            Expect(Token.NAME);
            int val;
            if (t.currentTokenValue == "preserve")
            {
                val = Validation.PRESERVE; //            if (!env.getExecutable().isSchemaAware()) {
                //                grumble("construction mode preserve is allowed only with a schema-aware query");
                //            }
            }
            else if (t.currentTokenValue == "strip")
            {
                val = Validation.STRIP;
            }
            else
            {
                Grumble("construction mode must be 'preserve' or 'strip'");
                val = Validation.STRIP;
            }

            ((QueryModule)env).ConstructionMode = val;
            NextToken();
        }

        protected virtual void ParseRevalidationDeclaration()
        {
            Grumble("declare revalidation is allowed only in XQuery Update");
        }

        private void ParseSchemaImport()
        {
            // NB: the "schema-aware not supported" check (XQST0009) is deferred to the END of this method so
            // that a malformed schema-import clause reports its own more-specific static error first, matching
            // Saxon/other processors: `namespace NCName := …` -> XPST0003, reserved prefix -> XQST0070, empty
            // target namespace with a prefix -> XQST0057 (K-CombinedErrorCodes-7, XQST0057, XQST0070_1). For a
            // well-formed import the result is unchanged (still XQST0009).
            Import sImport = new Import();
            string prefix = null;
            sImport.namespaceURI = null;
            sImport.locationURIs = new List<string>(5);
            sImport.offset = t.currentTokenStartOffset;
            NextToken();
            bool fixedDefault = false;
            if (IsKeyword("namespace"))
            {
                prefix = ReadNamespaceBinding();
            }
            else
            {
                if (IsKeyword("fixed"))
                {
                    CheckLanguageVersion40();
                    fixedDefault = true;
                    NextToken();
                }

                if (IsKeyword("default") || t.currentToken == Token.DEFAULT)
                {
                    NextToken();
                    if (!IsKeyword("element"))
                    {
                        Grumble("In 'import schema', expected 'element namespace'");
                    }

                    NextToken();
                    if (!IsKeyword("namespace"))
                    {
                        Grumble("In 'import schema', expected keyword 'namespace'");
                    }

                    NextToken();
                    prefix = "";
                }
            }

            if (t.currentToken == Token.STRING_LITERAL)
            {
                NamespaceUri uri = NamespaceUri.Of(UriLiteral(t.currentTokenValue));
                CheckProhibitedPrefixes(prefix, uri);
                sImport.namespaceURI = uri;
                NextToken();
                if (IsKeyword("at"))
                {
                    NextToken();
                    Expect(Token.STRING_LITERAL);
                    sImport.locationURIs.Add(UriLiteral(t.currentTokenValue));
                    NextToken();
                    while (t.currentToken == Token.COMMA)
                    {
                        NextToken();
                        Expect(Token.STRING_LITERAL);
                        sImport.locationURIs.Add(UriLiteral(t.currentTokenValue));
                        NextToken();
                    }
                }
                else if (t.currentToken != Token.SEMICOLON)
                {
                    Grumble("After the target namespace URI, expected 'at' or ';'");
                }
            }
            else
            {
                Grumble("After 'import schema', expected 'namespace', 'default', or a string-literal");
            }

            if (prefix != null)
            {
                try
                {
                    if ((prefix.Length == 0))
                    {
                        ((QueryModule)env).SetDefaultElementNamespace(sImport.namespaceURI, fixedDefault);
                    }
                    else
                    {
                        if (sImport.namespaceURI == null || sImport.namespaceURI.IsEmpty())
                        {
                            Grumble("A prefix cannot be bound to the null namespace", "XQST0057");
                        }

                        ((QueryModule)env).DeclarePrologNamespace(prefix, sImport.namespaceURI);
                    }
                }
                catch (XPathException err)
                {
                    err.SetLocator(MakeLocation());
                    ReportError(err);
                }
            }

            foreach (Import schemaImport in schemaImports)
            {
                if (schemaImport.namespaceURI.Equals(sImport.namespaceURI))
                {
                    Grumble("Schema namespace '" + sImport.namespaceURI + "' is imported more than once", "XQST0058");
                    break;
                }
            }

            // java.net.URI would reject these during schema loading (XQST0046); the port's lenient URI
            // accepts them, and on HE the load never happens anyway, so screen the locations here.
            foreach (string location in sImport.locationURIs)
            {
                if (!ResolveURI.IsValidUriSyntax(location))
                {
                    Grumble("Invalid schema location URI " + location, "XQST0046", sImport.offset);
                }
            }

            // Deferred from the top of the method (see note there): the clause is now fully validated, so a
            // schema-unaware processor reports XQST0009 only if no more-specific error was raised first.
            EnsureSchemaAware("import schema");
            schemaImports.Add(sImport);
        }

        private string ReadNamespaceBinding()
        {
            t.State = Tokenizer.DEFAULT_STATE;
            NextToken();
            Expect(Token.NAME);
            string prefix = t.currentTokenValue;
            NextToken();
            Expect(Token.EQUALS);
            NextToken();
            return prefix;
        }

        protected virtual void EnsureSchemaAware(string featureName, string errorCode = "XQST0009")
        {
            if (!env.GetConfiguration().IsLicensedFeature(Configuration.LicenseFeature.ENTERPRISE_XQUERY))
            {
                throw new XPathException("This Saxon version and license does not allow use of '" + featureName + "'", errorCode);
            }

            env.GetConfiguration().CheckLicensedFeature(Configuration.LicenseFeature.ENTERPRISE_XQUERY, featureName, -1);
            GetExecutable().SetSchemaAware(true);
            GetStaticContext().GetPackageData().SetSchemaAware(true);
        }

        private void ApplySchemaImport(Import sImport)
        {

            // Do the importing
            Configuration config = env.GetConfiguration();

            lock (config.syncLock)
            {
                if (!config.IsSchemaAvailable(sImport.namespaceURI))
                {
                    if (sImport.locationURIs.Count > 0)
                    {
                        try
                        {
                            PipelineConfiguration pipe = config.MakePipelineConfiguration();
                            config.ReadMultipleSchemas(pipe, env.StaticBaseURI, sImport.locationURIs, sImport.namespaceURI);
                            namespacesToBeSealed.Add(sImport.namespaceURI);
                        }
                        catch (SchemaException err)
                        {
                            Grumble("Error in schema " + sImport.namespaceURI + ": " + err.Message, "XQST0059", sImport.offset);
                        }
                    }
                    else if (sImport.namespaceURI.Equals(NamespaceUri.XML) || sImport.namespaceURI.Equals(NamespaceUri.FN) || sImport.namespaceURI.Equals(NamespaceUri.SCHEMA_INSTANCE))
                    {
                        config.AddSchemaForBuiltInNamespace(sImport.namespaceURI);
                    }
                    else
                    {
                        Grumble("Unable to locate requested schema " + sImport.namespaceURI, "XQST0059", sImport.offset);
                    }
                }

                ((QueryModule)env).AddImportedSchema(sImport.namespaceURI, env.StaticBaseURI, sImport.locationURIs);
            }
        }

        private void ParseModuleImport()
        {
            QueryModule thisModule = (QueryModule)env;
            Import mImport = new Import();
            string prefix = null;
            mImport.namespaceURI = null;
            mImport.locationURIs = new List<string>(5);
            mImport.offset = t.currentTokenStartOffset;
            NextToken();
            if (t.currentToken == Token.NAME && t.currentTokenValue.Equals("namespace"))
            {
                prefix = ReadNamespaceBinding();
            }

            if (t.currentToken == Token.STRING_LITERAL)
            {
                NamespaceUri uri = NamespaceUri.Of(UriLiteral(t.currentTokenValue));
                CheckProhibitedPrefixes(prefix, uri);
                mImport.namespaceURI = uri;
                if (mImport.namespaceURI.IsEmpty())
                {
                    Grumble("Imported module namespace cannot be \"\"", "XQST0088");
                    mImport.namespaceURI = NamespaceUri.Of("http://saxon.fallback.namespace/line" + t.GetLineNumber()); // for error recovery
                }

                if (importedModules.Contains(mImport.namespaceURI))
                {
                    Grumble("Two 'import module' declarations specify the same module namespace", "XQST0047");
                }

                importedModules.Add(mImport.namespaceURI);
                ((QueryModule)env).AddImportedNamespace(mImport.namespaceURI);
                NextToken();
                if (IsKeyword("at"))
                {
                    do
                    {
                        NextToken();
                        Expect(Token.STRING_LITERAL);
                        mImport.locationURIs.Add(UriLiteral(t.currentTokenValue));
                        NextToken();
                    }
                    while (t.currentToken == Token.COMMA);
                }
            }
            else
            {
                Grumble("After 'import module', expected 'namespace' or a string-literal");
            }

            if (prefix != null)
            {
                try
                {
                    if (mImport.namespaceURI.Equals(thisModule.ModuleNamespace) && mImport.namespaceURI.Equals(thisModule.CheckURIForPrefix(prefix)))
                    {
                    }
                    else
                    {
                        thisModule.DeclarePrologNamespace(prefix, mImport.namespaceURI);
                    }
                }
                catch (XPathException err)
                {
                    err.SetLocator(MakeLocation());
                    ReportError(err);
                }
            }

            moduleImports.Add(mImport);
        }

        private void ApplyModuleImport(Import mImport)
        {
            IList<QueryModule> existingModules;

            // resolve the location URIs against the base URI
            for (int i = 0; i < mImport.locationURIs.Count; i++)
            {
                try
                {
                    string uri = mImport.locationURIs[i];
                    // java.net.URI raises URISyntaxException for these inside MakeAbsolute; the port's
                    // lenient URI does not, so screen explicitly to reach the same XQST0046.
                    if (!ResolveURI.IsValidUriSyntax(uri))
                    {
                        Grumble("Invalid URI " + uri, "XQST0046", mImport.offset);
                    }
                    URI abs = ResolveURI.MakeAbsolute(uri, env.StaticBaseURI);
                    mImport.locationURIs[i] = abs.ToString();
                }
                catch (URISyntaxException e)
                {
                    Grumble("Invalid URI " + mImport.locationURIs[i] + ": " + e.Message, "XQST0046", mImport.offset);
                }
            }


            // See if the URI is that of a separately-compiled query library
            QueryLibrary lib = ((QueryModule)env).UserQueryContext.GetCompiledLibrary(mImport.namespaceURI);
            if (lib != null)
            {
                executable.AddQueryLibraryModule(lib);
                existingModules = new List<QueryModule>();
                existingModules.Add(lib);
                lib.Link((QueryModule)env);
            }
            else if (!env.GetConfiguration().GetBooleanProperty(Feature<bool>.XQUERY_MULTIPLE_MODULE_IMPORTS))
            {

                // Unless this configuration option is set, if we already know a module with the right module URI, then we
                // use it irrespective of its location URI.
                IList<QueryModule> list = executable.GetQueryLibraryModules(mImport.namespaceURI);
                if (list != null && list.Count > 0)
                {
                    ((QueryModule)env).AddImportedModule(list[0]);
                    return;
                }
            }
            else
            {
                for (int h = mImport.locationURIs.Count - 1; h >= 0; h--)
                {
                    if (executable.IsQueryLocationHintProcessed(mImport.locationURIs[h]))
                    {
                        mImport.locationURIs.RemoveAt(h);
                    }
                }
            }


            // If there are no location URIs left, and we already know a module with the right module URI.
            if (mImport.locationURIs.Count == 0)
            {
                IList<QueryModule> list = executable.GetQueryLibraryModules(mImport.namespaceURI);
                if (list != null && list.Count > 0)
                {
                    foreach (QueryModule target in list)
                    {
                        ((QueryModule)env).AddImportedModule(target);
                    }

                    return;
                }
            }


            // Call the module URI resolver to find the remaining modules
            IModuleURIResolver resolver = ((QueryModule)env).UserQueryContext.ModuleURIResolver;
            string[] hints = new string[mImport.locationURIs.Count];
            for (int h = 0; h < hints.Length; h++)
            {
                hints[h] = mImport.locationURIs[h];
            }

            ResolvedResource[] sources = null;
            if (resolver != null)
            {
                try
                {
                    sources = resolver.Resolve(mImport.namespaceURI.ToString(), env.StaticBaseURI, hints);
                }
                catch (XPathException err)
                {
                    Grumble("Failed to resolve URI of imported module: " + err.Message, "XQST0059", mImport.offset);
                }
            }

            if (sources == null)
            {
                resolver = env.GetConfiguration().GetStandardModuleURIResolver();
                sources = resolver.Resolve(mImport.namespaceURI.ToString(), env.StaticBaseURI, hints);
            }

            foreach (string hint in mImport.locationURIs)
            {
                executable.AddQueryLocationHintProcessed(hint);
            }

            for (int m = 0; m < sources.Length; m++)
            {
                ResolvedResource ss = sources[m];
                string baseURI = ss.SystemId;
                if (baseURI == null)
                {
                    if (m < hints.Length)
                    {
                        baseURI = hints[m];
                    }
                    else
                    {
                        baseURI = env.StaticBaseURI; //grumble("No base URI available for imported module", "XQST0059", mImport.offset);
                    }

                    ss.SystemId = baseURI;
                }


                // Although the module hadn't been loaded when we started, it might have been loaded since, as
                // a result of a reference from another imported module.
                // TODO: use similar logic when loading schema modules
                existingModules = executable.GetQueryLibraryModules(mImport.namespaceURI);
                bool loaded = false;
                if (existingModules != null && m < hints.Length)
                {
                    foreach (QueryModule existingModule in existingModules)
                    {
                        URI uri = existingModule.LocationURI;
                        if (uri != null && uri.ToString().Equals(mImport.locationURIs[m]))
                        {
                            loaded = true;
                            break;
                        }
                    }
                }

                if (loaded)
                {
                    break;
                }

                try
                {
                    string queryText = QueryReader.ReadSourceQuery(env.GetConfiguration(), ss, charChecker);
                    try
                    {
                        if (ss.Stream != null)
                        {
                            ss.Stream.Dispose();
                        }
                        else if (ss.TextReader != null)
                        {
                            ss.TextReader.Dispose();
                        }
                    }
                    catch (IOException)
                    {
                        throw new XPathException("Failure while closing file for imported query module");
                    }

                    QueryModule.MakeQueryModule(baseURI, executable, (QueryModule)env, queryText, mImport.namespaceURI);
                }
                catch (XPathException err)
                {
                    // Java surfaces an unreadable module location as XQST0059 (its resolver opens the
                    // resource eagerly); the port reads lazily, so the raw I/O failure from QueryReader
                    // lands here without a code. Errors from inside the module keep their own codes.
                    if (err.ErrorCodeQName == null)
                    {
                        err.SetErrorCode("XQST0059");
                    }
                    ReportError(err.MaybeWithLocation(MakeLocation()));
                }
            }
        }

        private void ParseBaseURIDeclaration()
        {
            if (foundBaseURIDeclaration)
            {
                Grumble("Base URI IDeclaration may only appear once", "XQST0032");
            }

            foundBaseURIDeclaration = true;
            NextToken();
            Expect(Token.STRING_LITERAL);
            string uri = UriLiteral(t.currentTokenValue);
            try
            {

                // if the supplied URI is relative, try to resolve it
                URI baseURI = new URI(uri);
                if (!baseURI.IsAbsolute())
                {
                    string oldBase = env.StaticBaseURI;
                    uri = ResolveURI.MakeAbsolute(uri, oldBase).ToString();
                }

                ((QueryModule)env).SetBaseURI(uri);
            }
            catch (URISyntaxException)
            {

                // The spec says this "is not intrinsically an error", but can cause a failure later
                ((QueryModule)env).SetBaseURI(uri);
            }

            NextToken();
        }

        private void ParseDecimalFormatDeclaration()
        {
            NextToken();
            Expect(Token.NAME);
            StructuredQName formatName = MakeStructuredQName(t.currentTokenValue, NamespaceUri.NULL);
            if (env.GetDecimalFormatManager().GetNamedDecimalFormat(formatName) != null)
            {
                Grumble("Duplicate declaration of decimal-format " + formatName.DisplayName, "XQST0111");
            }

            NextToken();
            ParseDecimalFormatProperties(formatName);
        }

        private void ParseDefaultDecimalFormat()
        {
            if (foundDefaultDecimalFormat)
            {
                Grumble("Duplicate declaration of default decimal-format", "XQST0111");
            }

            foundDefaultDecimalFormat = true;
            ParseDecimalFormatProperties(null);
        }

        private void ParseDecimalFormatProperties(StructuredQName formatName)
        {
            int outerOffset = t.currentTokenStartOffset;
            DecimalFormatManager dfm = env.GetDecimalFormatManager();
            DecimalSymbols dfs = formatName == null ? dfm.DefaultDecimalFormat : dfm.ObtainNamedDecimalFormat(formatName);
            dfs.SetHostLanguage(HostLanguage.XQUERY, 31);
            HashSet<string> propertyNames = new HashSet<string>();
            while (t.currentToken != Token.SEMICOLON)
            {
                int offset = t.currentTokenStartOffset;
                string propertyName = t.currentTokenValue;
                if (propertyNames.Contains(propertyName))
                {
                    Grumble("Property name " + propertyName + " is defined more than once", "XQST0114", offset);
                }

                NextToken();
                Expect(Token.EQUALS);
                NextToken();
                Expect(Token.STRING_LITERAL);
                string propertyValue = Unescape(t.currentTokenValue).ToString();
                NextToken();
                propertyNames.Add(propertyName);
                switch (propertyName)
                {
                    case "decimal-separator":
                        dfs.SetDecimalSeparator(propertyValue);
                        break;
                    case "grouping-separator":
                        dfs.SetGroupingSeparator(propertyValue);
                        break;
                    case "infinity":
                        dfs.Infinity = propertyValue;
                        break;
                    case "minus-sign":
                        dfs.SetMinusSign(propertyValue);
                        break;
                    case "NaN":
                        dfs.NaN = propertyValue;
                        break;
                    case "percent":
                        dfs.SetPercent(propertyValue);
                        break;
                    case "per-mille":
                        dfs.SetPerMille(propertyValue);
                        break;
                    case "zero-digit":
                        try
                        {
                            dfs.SetZeroDigit(propertyValue);
                        }
                        catch (XPathException err)
                        {
                            throw err.WithErrorCode("XQST0097");
                        }

                        break;
                    case "digit":
                        dfs.SetDigit(propertyValue);
                        break;
                    case "pattern-separator":
                        dfs.SetPatternSeparator(propertyValue);
                        break;
                    case "exponent-separator":
                        dfs.SetExponentSeparator(propertyValue);
                        break;
                    default:
                        Grumble("Unknown decimal-format property: " + propertyName, "XPST0003", offset);
                        break;
                }
            }

            try
            {
                dfs.CheckConsistency(formatName);
            }
            catch (XPathException err)
            {
                Grumble(err.Message, "XQST0098", outerOffset);
            }
        }

        private void ParseDefaultFunctionNamespace()
        {
            if (foundDefaultFunctionNamespace)
            {
                Grumble("default function namespace appears more than once", "XQST0066");
            }

            foundDefaultFunctionNamespace = true;
            NextToken();
            Expect(Token.NAME);
            if (t.currentTokenValue != "namespace")
            {
                Grumble("After 'declare default function', expected 'namespace'");
            }

            NextToken();
            Expect(Token.STRING_LITERAL);
            NamespaceUri uri = NamespaceUri.Of(UriLiteral(t.currentTokenValue));
            if (uri.Equals(NamespaceUri.XML) || uri.Equals(NamespaceUri.XMLNS))
            {
                Grumble("Reserved namespace used as default element/type namespace", "XQST0070");
            }

            ((QueryModule)env).SetDefaultFunctionNamespace(uri);
            NextToken();
        }

        private void ParseDefaultElementNamespace(bool isFixedDefault)
        {
            if (foundDefaultElementNamespace)
            {
                Grumble("default element namespace appears more than once", "XQST0066");
            }

            foundDefaultElementNamespace = true;
            NextToken();
            Expect(Token.NAME);
            if (t.currentTokenValue != "namespace")
            {
                Grumble("After 'declare default element', expected 'namespace'");
            }

            NextToken();
            Expect(Token.STRING_LITERAL);
            NamespaceUri uri = NamespaceUri.Of(UriLiteral(t.currentTokenValue));
            if (uri.Equals(NamespaceUri.XML) || uri.Equals(NamespaceUri.XMLNS))
            {
                Grumble("Reserved namespace used as default element/type namespace", "XQST0070");
            }

            ((QueryModule)env).SetDefaultElementNamespace(uri, isFixedDefault);
            NextToken();
        }

        private void ParseNamespaceDeclaration()
        {
            NextToken();
            Expect(Token.NAME);
            string prefix = t.currentTokenValue;
            if (!NameChecker.IsValidNCName(prefix))
            {
                Grumble("Invalid namespace prefix " + Err.Wrap(prefix));
            }

            NextToken();
            Expect(Token.EQUALS);
            NextToken();
            Expect(Token.STRING_LITERAL);
            NamespaceUri uri = NamespaceUri.Of(UriLiteral(t.currentTokenValue));
            CheckProhibitedPrefixes(prefix, uri);
            if (prefix == "xml")
            {

                // disallowed here even if bound to the correct @namespace - erratum XQ.E19
                Grumble("Namespace prefix 'xml' cannot be declared", "XQST0070");
            }

            try
            {
                ((QueryModule)env).DeclarePrologNamespace(prefix, uri);
            }
            catch (XPathException err)
            {
                err.SetLocator(MakeLocation());
                ReportError(err);
            }

            NextToken();
        }

        private void CheckProhibitedPrefixes(string prefix, NamespaceUri uri)
        {
            if (prefix != null && !(prefix.Length == 0) && !NameChecker.IsValidNCName(prefix))
            {
                Grumble("The namespace prefix " + Err.Wrap(prefix) + " is not a valid NCName");
            }

            if (prefix == null)
            {
                prefix = "";
            }

            if (uri == null)
            {
                uri = NamespaceUri.NULL;
            }

            if (prefix == "xmlns")
            {
                Grumble("The namespace prefix 'xmlns' cannot be redeclared", "XQST0070");
            }

            if (uri.Equals(NamespaceUri.XMLNS))
            {
                Grumble("The xmlns namespace URI is reserved", "XQST0070");
            }

            if (uri.Equals(NamespaceUri.XML) && !prefix.Equals("xml"))
            {
                Grumble("The XML namespace cannot be bound to any prefix other than 'xml'", "XQST0070");
            }

            if (prefix.Equals("xml") && !uri.Equals(NamespaceUri.XML))
            {
                Grumble("The prefix 'xml' cannot be bound to any namespace other than " + NamespaceConstant.XML, "XQST0070");
            }
        }

        private void ParseVariableDeclaration(AnnotationList annotations)
        {
            int offset = t.currentTokenStartOffset;
            GlobalVariable var = new GlobalVariable();
            var.SetPackageData(env.GetPackageData());
            var.SetLineNumber(t.GetLineNumber() + 1);
            var.SetColumnNumber(t.GetColumnNumber() + 1);
            var.SetSystemId(env.GetSystemId());
            if (annotations != null)
            {
                CheckPublicPrivateAnnotations(annotations, "XQST0116");
                var.SetPrivate(annotations.Includes(Annotation.PRIVATE));
            }

            NextToken();
            Expect(Token.DOLLAR);
            t.State = Tokenizer.BARE_NAME_STATE;
            NextToken();
            Expect(Token.NAME);
            string varName = t.currentTokenValue;
            StructuredQName varQName = MakeStructuredQName(t.currentTokenValue, NamespaceUri.NULL);
            var.SetVariableQName(varQName);
            NamespaceUri uri = varQName.GetNamespaceUri();
            NamespaceUri moduleURI = ((QueryModule)env).ModuleNamespace;
            if (moduleURI != null && !moduleURI.Equals(uri))
            {
                Grumble("A variable declared in a library module must be in the module namespace", "XQST0048", offset);
            }

            NextToken();
            Values.SequenceType requiredType = Values.SequenceType.ANY_SEQUENCE;
            if (t.currentToken == Token.AS)
            {
                t.State = Tokenizer.SEQUENCE_TYPE_STATE;
                NextToken();
                requiredType = ParseSequenceType();
            }

            var.SetRequiredType(requiredType);
            if (t.currentToken == Token.ASSIGN)
            {
                t.State = Tokenizer.DEFAULT_STATE;
                NextToken();
                int refs = ((QueryModule)env).GetForwardReferenceCount(varQName);
                Expression exp = ParseExprSingle();
                if (((QueryModule)env).GetForwardReferenceCount(varQName) > refs)
                {
                    Grumble("Variable $" + var.GetVariableQName().DisplayName + " is referenced within its own declaration", "XPST0008");
                }

                exp = MakeTracer(exp, varQName);
                if (allowXPath40Syntax && requiredType != Values.SequenceType.ANY_SEQUENCE)
                {
                    TypeChecker checker = env.GetConfiguration().GetTypeChecker(false);
                    ExpressionVisitor visitor = ExpressionVisitor.Make(env);
                    Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.VARIABLE, varName, 0);
                    exp = checker.StaticTypeCheck(exp, requiredType, role, visitor);
                }

                var.SetBody(exp);
            }
            else if (t.currentToken == Token.NAME)
            {
                if (t.currentTokenValue == "external")
                {
                    GlobalParam par = new GlobalParam();
                    par.SetPackageData(env.GetPackageData());

                    par.SetLineNumber(var.GetLineNumber());
                    par.SetColumnNumber(var.GetColumnNumber());
                    par.SetSystemId(var.GetSystemId());
                    par.SetVariableQName(var.GetVariableQName());
                    par.SetRequiredType(var.GetRequiredType());
                    var = par;
                    NextToken();
                    if (t.currentToken == Token.ASSIGN)
                    {
                        t.State = Tokenizer.DEFAULT_STATE;
                        NextToken();
                        Expression exp = ParseExprSingle();
                        exp = MakeTracer(exp, varQName);
                        if (allowXPath40Syntax && requiredType != Values.SequenceType.ANY_SEQUENCE)
                        {
                            TypeChecker checker = env.GetConfiguration().GetTypeChecker(false);
                            ExpressionVisitor visitor = ExpressionVisitor.Make(env);
                            Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.VARIABLE, varName, 0);
                            exp = checker.StaticTypeCheck(exp, requiredType, role, visitor);
                        }

                        var.SetBody(exp);
                    }
                }
                else
                {
                    Grumble("Variable must either be initialized or be declared as external");
                }
            }
            else
            {
                Grumble("Expected ':=' or 'external' in variable declaration");
            }

            QueryModule qenv = (QueryModule)env;
            RetainedStaticContext rsc = env.MakeRetainedStaticContext();
            var.SetRetainedStaticContext(rsc);
            if (var.GetBody() != null)
            {
                ExpressionTool.SetDeepRetainedStaticContext(var.GetBody(), rsc);
            }

            if (qenv.ModuleNamespace != null && !uri.Equals(qenv.ModuleNamespace))
            {
                Grumble("Variable " + Err.Wrap(varName, Err.VARIABLE) + " is not defined in the module namespace");
            }

            try
            {
                qenv.DeclareVariable(var);
            }
            catch (XPathException e)
            {
                Grumble(e.Message, e.ErrorCodeQName, -1);
            }
        }

        private void ParseContextItemDeclaration()
        {
            int offset = t.currentTokenStartOffset;
            NextToken();
            if (!IsKeyword("item"))
            {
                Grumble("After 'declare context', expected 'item'");
            }

            if (foundContextItemDeclaration)
            {
                Grumble("More than one context item declaration found", "XQST0099", offset);
            }

            foundContextItemDeclaration = true;
            GlobalContextRequirement req = new GlobalContextRequirement();
            req.SetAbsentFocus(false);
            t.State = Tokenizer.BARE_NAME_STATE;
            NextToken();
            Types.ItemType requiredType = AnyItemType.GetInstance();
            if (t.currentToken == Token.AS)
            {
                t.State = Tokenizer.SEQUENCE_TYPE_STATE;
                NextToken();
                requiredType = ParseItemType();
            }

            req.AddRequiredItemType(requiredType);
            if (t.currentToken == Token.ASSIGN)
            {
                if (!((QueryModule)env).IsMainModule())
                {
                    Grumble("The context item must not be initialized in a library module", "XQST0113");
                }

                t.State = Tokenizer.DEFAULT_STATE;
                NextToken();
                Expression exp = ParseExprSingle();
                exp.SetRetainedStaticContext(env.MakeRetainedStaticContext());
                Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.CONTEXT_ITEM, "context item declaration", 0);
                exp = CardinalityChecker.MakeCardinalityChecker(exp, StaticProperty.EXACTLY_ONE, role);
                ExpressionVisitor visitor = ExpressionVisitor.Make(env);
                exp = exp.Simplify();
                ContextItemStaticInfo info = env.GetConfiguration().MakeContextItemStaticInfo(AnyItemType.GetInstance(), true);
                exp.SetRetainedStaticContext(env.MakeRetainedStaticContext());
                exp = exp.TypeCheck(visitor, info);
                req.DefaultValue = exp;
                req.SetExternal(false);
            }
            else if (t.currentToken == Token.NAME && t.currentTokenValue == "external")
            {
                req.SetAbsentFocus(false);
                req.SetExternal(true);
                NextToken();
                if (t.currentToken == Token.ASSIGN)
                {
                    if (!((QueryModule)env).IsMainModule())
                    {
                        Grumble("The context item must not be initialized in a library module", "XQST0113");
                    }

                    t.State = Tokenizer.DEFAULT_STATE;
                    NextToken();
                    Expression exp = ParseExprSingle();
                    Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.CONTEXT_ITEM, "context item declaration", 0);
                    exp = CardinalityChecker.MakeCardinalityChecker(exp, StaticProperty.EXACTLY_ONE, role);
                    exp.SetRetainedStaticContext(env.MakeRetainedStaticContext());
                    req.DefaultValue = exp;
                }
            }
            else
            {
                Grumble("Expected ':=' or 'external' in context item declaration");
            }

            Executable exec = GetExecutable();
            if (exec.GlobalContextRequirement != null)
            {

                // the context item is already declared in another module. Compare the required types
                GlobalContextRequirement gcr = exec.GlobalContextRequirement;
                if (gcr.DefaultValue == null && req.DefaultValue != null)
                {
                    gcr.DefaultValue = req.DefaultValue;
                }

                foreach (Types.ItemType otherType in gcr.RequiredItemTypes)
                {
                    if (otherType != AnyItemType.GetInstance())
                    {
                        TypeHierarchy th = env.GetConfiguration().GetTypeHierarchy();
                        Affinity rel = th.Relationship(requiredType, otherType);
                        if (rel == Affinity.DISJOINT)
                        {

                            // the two types are incompatible: fail now
                            Grumble("Different modules specify incompatible requirements for the type of the initial context item", "XPTY0004");
                        }
                    }
                }

                gcr.AddRequiredItemType(requiredType);
            }
            else
            {
                exec.GlobalContextRequirement = req;
            }
        }

        // XQST0106 (function) / XQST0116 (variable): a declaration must not carry more than one %public or
        // %private annotation (whether duplicated or conflicting).
        private void CheckPublicPrivateAnnotations(AnnotationList annotations, string errorCode)
        {
            int n = 0;
            foreach (Annotation a in annotations)
            {
                StructuredQName q = a.AnnotationQName;
                if (q.Equals(Annotation.PUBLIC) || q.Equals(Annotation.PRIVATE))
                {
                    n++;
                }
            }

            if (n > 1)
            {
                Grumble("A declaration must not have more than one %public or %private annotation", errorCode);
            }
        }

        public virtual void ParseFunctionDeclaration(AnnotationList annotations)
        {
            CheckPublicPrivateAnnotations(annotations, "XQST0106");
            if (annotations.Includes(SAXON_MEMO_FUNCTION))
            {
                if (env.GetConfiguration().EditionCode.Equals("HE"))
                {
                    Warning("saxon:memo-function option is ignored under Saxon-HE", DAXonErrorCode.SXJX0001);
                }
                else
                {
                    memoFunction = true;
                }
            }


            // the next token should be the < QNAME "("> pair
            int offset = t.currentTokenStartOffset;
            t.State = Tokenizer.DEFAULT_STATE;
            NextToken();
            Expect(Token.FUNCTION);
            NamespaceUri uri;
            StructuredQName qName;
            if (t.currentTokenValue.IndexOf(':') < 0)
            {
                uri = env.GetDefaultFunctionNamespace();
                qName = new StructuredQName("", uri, t.currentTokenValue);
            }
            else
            {
                qName = MakeStructuredQName(t.currentTokenValue, NamespaceUri.NULL);
                uri = qName.GetNamespaceUri();
            }

            if (uri.IsEmpty())
            {
                Grumble("The function must be in a namespace", "XQST0060");
            }

            NamespaceUri moduleURI = ((QueryModule)env).ModuleNamespace;
            if (moduleURI != null && !moduleURI.Equals(uri))
            {
                Grumble("A function in a library module must be in the module namespace", "XQST0048");
            }

            if (IsReservedInQuery(uri))
            {
                Grumble("The function name " + t.currentTokenValue + " is in a reserved namespace", "XQST0045");
            }

            XQueryFunction func = new XQueryFunction();
            func.SetFunctionName(qName);
            func.ResultType = Values.SequenceType.ANY_SEQUENCE;
            func.Body = null;
            ILocation loc = MakeNestedLocation(env.GetContainingLocation(), t.GetLineNumber(offset), t.GetColumnNumber(offset), null);
            func.SetLocation(loc);
            func.SetStaticContext((QueryModule)env);
            func.SetMemoFunction(memoFunction);
            func.SetUpdating(annotations.Includes(Annotation.UPDATING));
            func.Annotations = annotations;
            NextToken();
            HashSet<StructuredQName> paramNames = new HashSet<StructuredQName>();
            bool external = false;
            bool foundDefault = false;
            if (t.currentToken != Token.RPAR)
            {
                while (true)
                {

                    //     ParamList   ::=     Param ("," Param)*
                    //     Param       ::=     "$" VarName  TypeDeclaration?
                    Expect(Token.DOLLAR);
                    NextToken();
                    Expect(Token.NAME);
                    StructuredQName argQName = MakeStructuredQName(t.currentTokenValue, NamespaceUri.NULL);
                    if (paramNames.Contains(argQName))
                    {
                        Grumble("Duplicate parameter name " + Err.Wrap(t.currentTokenValue, Err.VARIABLE), "XQST0039");
                    }

                    paramNames.Add(argQName);
                    Values.SequenceType paramType = Values.SequenceType.ANY_SEQUENCE;
                    NextToken();
                    if (t.currentToken == Token.AS)
                    {
                        NextToken();
                        paramType = ParseSequenceType();
                    }

                    UserFunctionParameter arg = new UserFunctionParameter();
                    arg.SetRequiredType(paramType);
                    arg.SetVariableQName(argQName);
                    if (t.currentToken == Token.ASSIGN)
                    {
                        if (!allowXPath40Syntax)
                        {
                            Grumble("Default values for function parameters require XQuery 4.0 to be enabled");
                        }

                        foundDefault = true;
                        NextToken();
                        Expression defaultValue = ParseExprSingle();
                        if (!(defaultValue is Literal || defaultValue is ContextItemExpression))
                        {
                            Grumble("The default value for a function parameter must be either a constant, or '.' (temporary Saxon restriction)");
                        }

                        defaultValue.SetRetainedStaticContext(env.MakeRetainedStaticContext());
                        arg.DefaultValueExpression = defaultValue;
                        arg.SetRequired(false);
                    }
                    else if (foundDefault)
                    {
                        Grumble("If a parameter in a function declaration has a default value, " + "all subsequent parameters must also have default values");
                    }

                    func.AddParameter(arg);
                    if (t.currentToken == Token.RPAR)
                    {
                        break;
                    }
                    else if (t.currentToken == Token.COMMA)
                    {
                        NextToken();
                    }
                    else
                    {
                        Grumble("Expected ',' or ')' after function argument, found '" + Token.tokens[t.currentToken] + '\'');
                    }
                }


                // Don't declare the variables until the end, to prevent one parameter being referenced
                // as the default value of another
                foreach (UserFunctionParameter p in func.GetParameterDefinitions())
                {
                    DeclareRangeVariable(p);
                }
            }

            t.State = Tokenizer.BARE_NAME_STATE;
            NextToken();
            if (t.currentToken == Token.AS)
            {
                if (func.IsUpdating())
                {
                    Grumble("Cannot specify a return type for an updating function", "XUST0028");
                }

                t.State = Tokenizer.SEQUENCE_TYPE_STATE;
                NextToken();
                func.ResultType = ParseSequenceType();
            }

            if (IsKeyword("external"))
            {
                external = true;
            }
            else
            {
                Expect(Token.LCURLY);
                t.State = Tokenizer.DEFAULT_STATE;
                NextToken();
                if (t.currentToken == Token.RCURLY)
                {
                    Expression body = Literal.MakeEmptySequence();
                    body.SetRetainedStaticContext(env.MakeRetainedStaticContext());
                    SetLocation(body);
                    func.Body = body;
                }
                else
                {
                    Expression body = ParseExpression();
                    func.Body = body;
                    ExpressionTool.SetDeepRetainedStaticContext(body, env.MakeRetainedStaticContext());
                }

                Expect(Token.RCURLY);
                LookAhead(); // must be done manually after an RCURLY
            }

            UserFunctionParameter[] @params = func.GetParameterDefinitions();

            foreach (UserFunctionParameter param in @params)
            {
                UndeclareRangeVariable();
            }

            t.State = Tokenizer.DEFAULT_STATE;
            NextToken();
            QueryModule qenv = (QueryModule)env;
            if (external)
            {
                parserExtension.HandleExternalFunctionDeclaration(this, func);
            }
            else
            {
                try
                {
                    qenv.DeclareFunction(func);
                }
                catch (XPathException e)
                {
                    Grumble(e.Message, e.ErrorCodeQName, -1);
                }
            }

            memoFunction = false;
        }
        protected virtual void ParseItemTypeDeclaration(AnnotationList annotations)
        {
            parserExtension.ParseItemTypeDeclaration(this);
        }

        private void ParseOptionDeclaration()
        {
            NextToken();
            Expect(Token.NAME);
            NamespaceUri defaultUri = NamespaceUri.XQUERY;
            StructuredQName varName = MakeStructuredQName(t.currentTokenValue, defaultUri);
            NamespaceUri uri = varName.GetNamespaceUri();
            if (uri.IsEmpty())
            {
                Grumble("The QName identifying an option declaration must be prefixed", "XPST0081");
                return;
            }

            NextToken();
            Expect(Token.STRING_LITERAL);

            //String value = URILiteral(t.currentTokenValue).trim();
            string value = Unescape(t.currentTokenValue).ToString();
            if (uri.Equals(NamespaceUri.OUTPUT))
            {
                ParseOutputDeclaration(varName, value);
            }
            else if (uri.Equals(NamespaceUri.SAXON))
            {
                string localName = varName.GetLocalPart();
                switch (localName)
                {
                    case "output":
                        SetOutputProperty(value);
                        break;
                    case "memo-function":
                        value = value.Trim();
                        switch (value)
                        {
                            case "true":
                                memoFunction = true;
                                if (env.GetConfiguration().EditionCode.Equals("HE"))
                                {
                                    Warning("saxon:memo-function option is ignored under Saxon-HE", DAXonErrorCode.SXJX0001);
                                }

                                break;
                            case "false":
                                memoFunction = false;
                                break;
                            default:
                                Warning("Value of saxon:memo-function must be 'true' or 'false'", DAXonErrorCode.SXWN9042);
                                break;
                        }

                        break;
                    case "allow-cycles":
                        Warning("Value of saxon:allow-cycles is ignored", DAXonErrorCode.SXWN9042);
                        break;
                    default:
                        Warning("Unknown Saxon option declaration: " + varName.DisplayName, DAXonErrorCode.SXWN9042);
                        break;
                }
            }

            NextToken();
        }

        protected virtual void ParseOutputDeclaration(StructuredQName varName, string value)
        {
            if (!((QueryModule)env).IsMainModule())
            {
                Grumble("Output declarations must not appear in a library module", "XQST0108");
            }

            string localName = varName.GetLocalPart();
            if (outputPropertiesSeen.Contains(varName))
            {
                Grumble("Duplicate output declaration (" + varName + ")", "XQST0110");
            }

            outputPropertiesSeen.Add(varName);
            switch (localName)
            {
                case "parameter-document":
                    {
                        Configuration config = env.GetConfiguration();
                        ResourceRequest rr = new ResourceRequest();
                        rr.relativeUri = value;
                        rr.baseUri = env.StaticBaseURI;
                        try
                        {
                            rr.uri = ResolveURI.MakeAbsolute(value, env.StaticBaseURI).ToString();
                        }
                        catch (URISyntaxException err)
                        {
                            throw XPathException.MakeXPathException(err);
                        }

                        rr.nature = NamespaceConstant.OUTPUT;
                        rr.purpose = ResourceRequest.ANY_PURPOSE;
                        ResolvedResource source = rr.Resolve(config.GetResourceResolver(), new DirectResourceResolver(config));
                        ITreeInfo doc = config.BuildDocumentTree(source);
                        SerializationParamsHandler ph = new SerializationParamsHandler(parameterDocProperties);
                        ph.SetSerializationParams(doc.GetRootNode());
                        CharacterMap characterMap = ph.GetCharacterMap();
                        if (characterMap != null)
                        {
                            CharacterMapIndex index = new CharacterMapIndex();
                            index.PutCharacterMap(characterMap.Name, characterMap);
                            GetExecutable().SetCharacterMapIndex(index);
                            parameterDocProperties.SetProperty(DAXonOutputKeys.USE_CHARACTER_MAPS, characterMap.Name.ClarkName);
                        }

                        break;
                    }

                case "use-character-maps":
                    Grumble("Output declaration use-character-maps cannot appear except in a parameter file", "XQST0109");
                    break;
                default:
                    {
                        Properties props = GetExecutable().PrimarySerializationProperties.GetProperties();
                        ResultDocument.SetSerializationProperty(props, NamespaceUri.NULL, localName, value, env.GetNamespaceResolver(), false, env.GetConfiguration());
                        break;
                    }
            }
        }

        private void SetOutputProperty(string property)
        {
            int equals = property.IndexOf('=');
            if (equals < 0)
            {
                BadOutputProperty("no equals sign");
            }
            else if (equals == 0)
            {
                BadOutputProperty("starts with '=");
            }

            string keyword = Whitespace.Trim(property.Substring(0, equals));
            string value = equals == property.Length - 1 ? "" : Whitespace.Trim(property.Substring(equals + 1));
            Properties props = GetExecutable().PrimarySerializationProperties.GetProperties();
            try
            {
                StructuredQName name = MakeStructuredQName(keyword, NamespaceUri.NULL);
                string lname = name.GetLocalPart();
                NamespaceUri uri = name.GetNamespaceUri();
                ResultDocument.SetSerializationProperty(props, uri, lname, value, env.GetNamespaceResolver(), false, env.GetConfiguration());
            }
            catch (XPathException e)
            {
                BadOutputProperty(e.Message);
            }
        }

        private void BadOutputProperty(string s)
        {
            Warning("Invalid serialization property (" + s + ")", DAXonErrorCode.SXWN9043);
        }

    }
}
