////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Trees.Utilities;
using System;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Collections;
namespace OutSmart.DAXon.Expressions.Parsing
{
    // Split into partial files (XPathParser.Pn.*.cs); the Pn prefix fixes compile order:
    // member order across parts is the assembly metadata order — keep it byte-stable.
    public partial class XPathParser
    {
        private static readonly IntToIntHashMap operatorPrecedenceTable = new IntToIntHashMap(30);

        /* Must be in alphabetical order, since a binary search is used */
        private static readonly string[] reservedFunctionNames31 = new string[]
        {
            "array",
            "attribute",
            "comment",
            "document-node",
            "element",
            "empty-sequence",
            "function",
            "if",
            "item",
            "map",
            "namespace-node",
            "node",
            "processing-instruction",
            "schema-attribute",
            "schema-element",
            "switch",
            "text",
            "typeswitch"
        };
        private static readonly string[] reservedFunctionNames40 = new string[]
        {
            "attribute",
            "comment",
            "document-node",
            "element",
            "empty-sequence",
            "fn",
            "function",
            "if",
            "item",
            "namespace-node",
            "node",
            "processing-instruction",
            "schema-attribute",
            "schema-element",
            "switch",
            "text",
            "typeswitch"
        };
        protected Tokenizer t;
        protected IStaticContext env;
        protected IndexedStack<ILocalBinding> rangeVariables = new IndexedStack<ILocalBinding>();
        // The stack holds a list of range variables that are in scope.
        // Each entry on the stack is a IBinding object containing details
        // of the variable.
        public IndexedStack<InlineFunctionDetails> inlineFunctionStack = new IndexedStack<InlineFunctionDetails>();
        protected QNameParser qNameParser;
        protected ParserExtension parserExtension = new ParserExtension();
        protected IIntPredicateProxy charChecker;
        protected bool allowXPath30Syntax = false;
        protected bool allowXPath30XSLTExtensions = false;
        protected bool allowXPath31Syntax = false;
        protected bool allowXPath40Syntax = false;
        protected bool allowSaxonExtensions = false;
        protected bool scanOnly = false;
        // scanOnly is set to true while attributes in direct element constructors
        // are being processed. We need to parse enclosed expressions in the attribute
        // in order to find the end of the attribute value, but we don't yet know the
        // full namespace context at this stage.
        private bool allowAbsentExpression = false;
        // allowAbsentExpression is a flag that indicates that it is acceptable
        // for the expression to be empty (that is, to consist solely of whitespace and
        // comments). The result of parsing such an expression is equivalent to the
        // result of parsing an empty sequence literal, "()"
        protected ICodeInjector codeInjector = null;
        private IAccelerator accelerator = null;

        protected ParsedLanguage language = ParsedLanguage.XPATH; // know which language we are parsing, for diagnostics
        protected int languageVersion = 20;
        protected int catchDepth = 0;

        // .NET hardening (no upstream equivalent): Java's StackOverflowError is catchable, so upstream
        // relies on the JVM to turn a pathologically deep parse into a recoverable error. .NET's
        // StackOverflowException is uncatchable and kills the process, so we bound recursive-descent
        // nesting explicitly and raise XPST0003. The counter is the deterministic ceiling; the
        // StackGuard probe in ParseExprSingle covers threads too small to reach it (round AR).
        public const int MAX_EXPRESSION_NESTING = 3000;
        private int expressionDepth = 0;

        // Companion to the recursion guard above: the operator/postfix/path loops build a left-leaning
        // tree one level deeper per ITERATION without recursing, so a 300k-term chain (1+1+..., a[.][.]...,
        // a/b/...) escapes the recursion guard and burns O(n) work only to be rejected by the
        // static-analysis depth guard later. Capping loop iterations at the same bound rejects after
        // ~3000 tokens; N iterations imply a tree >= N deep, so anything rejected here would fail that
        // guard anyway — no new rejections.
        private void CheckIterativeDepth(int chainLength)
        {
            if (chainLength > MAX_EXPRESSION_NESTING)
            {
                Grumble("Expression is too deeply nested (exceeds the limit of " + MAX_EXPRESSION_NESTING + ")", "XPST0003");
            }
        }

        private ILocation mostRecentLocation = Loc.NONE;

        public virtual ICodeInjector CodeInjector
        {
            get => codeInjector; set
            {
                this.codeInjector = value;
            }
        }

        private int CurrentOperatorPrecedence => OperatorPrecedence(t.currentToken);

        // Routines for handling range variables
        public virtual IndexedStack<ILocalBinding> RangeVariables
        {
            get => rangeVariables; set
            {
                this.rangeVariables = value;
            }
        }
        static XPathParser()
        {
            InitializeOperatorPrecedenceTable();
        }

        /// <summary>
        /// Create an expression parser
        /// </summary>
        public XPathParser(IStaticContext env)
        {
            this.env = env;
        }

        /// <summary>
        /// Initialize the static operator precedence table
        /// </summary>
        private static void InitializeOperatorPrecedenceTable()
        {
            operatorPrecedenceTable.DefaultValue = -1;
            IntToIntHashMap m = operatorPrecedenceTable;
            m.Put(Token.QMARK_QMARK, 3);
            m.Put(Token.BANG_BANG, 3);
            m.Put(Token.OR, 4);
            m.Put(Token.OR_ELSE, 4);
            m.Put(Token.AND, 5);
            m.Put(Token.AND_ALSO, 5);
            m.Put(Token.FEQ, 6);
            m.Put(Token.FNE, 6);
            m.Put(Token.FLT, 6);
            m.Put(Token.FGT, 6);
            m.Put(Token.FLE, 6);
            m.Put(Token.FGE, 6);
            m.Put(Token.EQUALS, 6);
            m.Put(Token.NE, 6);
            m.Put(Token.LT, 6);
            m.Put(Token.LE, 6);
            m.Put(Token.GT, 6);
            m.Put(Token.GE, 6);
            m.Put(Token.IS, 6);
            m.Put(Token.PRECEDES, 6);
            m.Put(Token.FOLLOWS, 6);
            m.Put(Token.CONCAT, 7);
            m.Put(Token.TO, 9);
            m.Put(Token.PLUS, 10);
            m.Put(Token.MINUS, 10);
            m.Put(Token.MULT, 11);
            m.Put(Token.MATH_MULT, 11);
            m.Put(Token.DIV, 11);
            m.Put(Token.MATH_DIVIDE, 11);
            m.Put(Token.IDIV, 11);
            m.Put(Token.MOD, 11);
            m.Put(Token.OTHERWISE, 12);
            m.Put(Token.UNION, 13);
            m.Put(Token.INTERSECT, 14);
            m.Put(Token.EXCEPT, 14);
            m.Put(Token.INSTANCE_OF, 15);
            m.Put(Token.TREAT_AS, 16);
            m.Put(Token.CASTABLE_AS, 17);
            m.Put(Token.CAST_AS, 18);
            m.Put(Token.FAT_ARROW, 19);
            m.Put(Token.MAPPING_ARROW, 19); // remainder commented out because not used in precedence parsing (but perhaps they could be)
        }

        public virtual void SetAccelerator(IAccelerator accelerator)
        {
            this.accelerator = accelerator;
        }

        public virtual Tokenizer GetTokenizer()
        {
            return t;
        }

        public virtual IStaticContext GetStaticContext()
        {
            return env;
        }

        public virtual void SetParserExtension(ParserExtension extension)
        {
            this.parserExtension = extension;
        }

        public virtual void SetCatchDepth(int depth)
        {
            catchDepth = depth;
        }

        public virtual void NextToken()
        {
            try
            {
                t.Next();
            }
            catch (XPathException e)
            {
                Grumble(e.Message);
            }
        }

        public virtual void Expect(int token)
        {
            if (t.currentToken != token)
            {
                Grumble("expected \"" + Token.tokens[token] + "\", found " + CurrentTokenDisplay());
            }
        }

        public virtual void Grumble(string message)
        {
            Grumble(message, language == ParsedLanguage.XSLT_PATTERN ? "XTSE0340" : "XPST0003");
        }

        public virtual void Grumble(string message, string errorCode)
        {
            Grumble(message, new StructuredQName("", NamespaceUri.ERR, errorCode), -1);
        }

        public virtual void Grumble(string message, string errorCode, int offset)
        {
            Grumble(message, new StructuredQName("", NamespaceUri.ERR, errorCode), offset);
        }

        protected virtual void Grumble(string message, StructuredQName errorCode, int offset)
        {
            if (errorCode == null)
            {
                errorCode = new StructuredQName("err", NamespaceUri.ERR, "XPST0003");
            }

            string nearbyText = null;
            int line = -1;
            int column = -1;
            if (t != null)
            {
                nearbyText = t.RecentText(-1);
                if (offset == -1)
                {
                    line = t.GetLineNumber();
                    column = t.GetColumnNumber();
                }
                else
                {
                    line = t.GetLineNumber(offset);
                    column = t.GetColumnNumber(offset);
                }
            }

            ILocation loc = MakeNestedLocation(env.GetContainingLocation(), line, column, nearbyText);
            XPathException err = new XPathException(message).WithLocation(loc).AsStaticError().WithErrorCode(errorCode);
            err.SetIsSyntaxError(errorCode.GetLocalPart() == "XPST0003");
            err.SetHostLanguage(GetLanguage());
            throw err;
        }

        protected virtual void Grumble(string message, StructuredQName errorCode)
        {
            Grumble(message, errorCode, -1);
        }

        protected virtual void Warning(string message, string errorCode)
        {
            if (!env.GetConfiguration().GetBooleanProperty(Feature<bool>.SUPPRESS_XPATH_WARNINGS))
            {
                string s = t.RecentText(-1);
                string prefix = (message.StartsWith("...", StringComparison.Ordinal) ? "near" : "in") + ' ' + Err.Wrap(s) + ":\n    ";
                env.IssueWarning(prefix + message, errorCode, MakeLocation());
            }
        }

        protected virtual void SetLanguage(ParsedLanguage language, int version)
        {
            if (version == 0)
            {
                version = 30; // default
            }

            if (version == 305)
            {
                version = 30;
                allowXPath30XSLTExtensions = true;
            }

            if (version == 40)
            {
                GetStaticContext().GetConfiguration().CheckLicensedFeature(Configuration.LicenseFeature.PROFESSIONAL_EDITION, "XPath 4.0 Syntax", -1);
            }

            switch (language)
            {
                case ParsedLanguage.XPATH:
                    if (!(version == 20 || version == 30 || version == 31 || version == 40))
                    {
                        throw new ArgumentException("Unsupported language version " + version);
                    }

                    break;
                case ParsedLanguage.XSLT_PATTERN:
                case ParsedLanguage.SEQUENCE_TYPE:
                case ParsedLanguage.EXTENDED_ITEM_TYPE:
                    if (!(version == 20 || version == 30 || version == 31 || version == 40))
                    {
                        throw new ArgumentException("Unsupported language version " + version);
                    }

                    break;
                case ParsedLanguage.XQUERY:
                    if (!(version == 10 || version == 30 || version == 31 || version == 40))
                    {
                        throw new ArgumentException("Unsupported language version " + version);
                    }

                    break;
                default:
                    throw new ArgumentException("Unknown language " + language);
            }

            this.language = language;
            this.languageVersion = version;
            this.allowXPath30Syntax = languageVersion >= 30;
            this.allowXPath31Syntax = languageVersion >= 31;
            this.allowXPath40Syntax = languageVersion >= 40;
        }

        protected virtual string GetLanguage()
        {
            switch (language)
            {
                case ParsedLanguage.XPATH:
                    return "XPath";
                case ParsedLanguage.XSLT_PATTERN:
                    return "XSLT Pattern";
                case ParsedLanguage.SEQUENCE_TYPE:
                    return "SequenceType";
                case ParsedLanguage.XQUERY:
                    return "XQuery";
                case ParsedLanguage.EXTENDED_ITEM_TYPE:
                    return "Extended ItemType";
                default:
                    return "XPath";
            }
        }

        public virtual bool IsAllowXPath31Syntax()
        {
            return allowXPath31Syntax;
        }

        public virtual void SetQNameParser(QNameParser qp)
        {
            this.qNameParser = qp;
        }

        public virtual QNameParser GetQNameParser()
        {
            return qNameParser;
        }

        protected virtual string CurrentTokenDisplay()
        {
            if (t.currentToken == Token.NAME)
            {
                return "name \"" + t.currentTokenValue + '"';
            }
            else if (t.currentToken == Token.UNKNOWN)
            {
                return "(unknown token)";
            }
            else
            {
                return '"' + Token.tokens[t.currentToken] + '"';
            }
        }

        public virtual Expression Parse(string expression, int start, int terminator, IStaticContext env)
        {
            this.env = env;
            int languageVersion = env.GetXPathVersion();
            if (languageVersion == 20 && language == ParsedLanguage.XQUERY)
            {
                languageVersion = 10;
            }

            SetLanguage(language, languageVersion);
            Expression exp = null;
            int offset;
            if (accelerator != null && env.GetUnprefixedElementMatchingPolicy() == UnprefixedElementMatchingPolicy.DEFAULT_NAMESPACE && terminator != Token.IMPLICIT_EOF && (expression.Length - start < 30 || terminator == Token.RCURLY))
            {

                // We need the tokenizer to be visible so that the caller can ask
                // about where the expression ended within the input string
                t = new Tokenizer();
                t.languageLevel = env.GetXPathVersion();
                exp = accelerator.Parse(t, env, expression, start, terminator);
            }

            if (exp == null)
            {
                qNameParser = new QNameParser(env.GetNamespaceResolver()).WithAcceptEQName(allowXPath30Syntax).WithErrorOnBadSyntax(language == ParsedLanguage.XSLT_PATTERN ? "XTSE0340" : "XPST0003").WithErrorOnUnresolvedPrefix("XPST0081");
                charChecker = env.GetConfiguration().ValidCharacterChecker;
                t = new Tokenizer();
                t.languageLevel = env.GetXPathVersion();
                allowXPath40Syntax = t.allowSaxonExtensions = env.GetConfiguration().GetBooleanProperty(Feature<bool>.ALLOW_SYNTAX_EXTENSIONS) || t.languageLevel == 40;
                offset = t.currentTokenStartOffset;
                CustomizeTokenizer(t);
                try
                {
                    t.Tokenize(expression, start, -1);
                }
                catch (XPathException err)
                {
                    Grumble(err.Message);
                }

                if (t.currentToken == terminator)
                {
                    if (allowAbsentExpression)
                    {
                        Expression result = Literal.MakeEmptySequence();
                        result.SetRetainedStaticContext(env.MakeRetainedStaticContext());
                        SetLocation(result);
                        return result;
                    }
                    else
                    {
                        Grumble("The expression is empty");
                    }
                }

                exp = ParseExpression();
                if (t.currentToken != terminator && terminator != Token.IMPLICIT_EOF)
                {
                    if (t.currentToken == Token.EOF && terminator == Token.RCURLY)
                    {
                        Grumble("Missing curly brace after expression in value template", "XTSE0350");
                    }
                    else
                    {
                        Grumble("Unexpected token " + CurrentTokenDisplay() + " beyond end of expression");
                    }
                }

                SetLocation(exp, offset);
            }

            exp.SetRetainedStaticContextThoroughly(env.MakeRetainedStaticContext());

            return exp;
        }

        protected virtual void CustomizeTokenizer(Tokenizer t)
        {
        }

        public virtual Values.SequenceType ParseSequenceType(string input, IStaticContext env)
        {
            this.env = env;
            SetLanguage(ParsedLanguage.SEQUENCE_TYPE, env.GetXPathVersion());
            if (qNameParser == null)
            {
                qNameParser = new QNameParser(env.GetNamespaceResolver());
                if (languageVersion >= 30)
                {
                    qNameParser = qNameParser.WithAcceptEQName(true);
                }
            }

            language = ParsedLanguage.SEQUENCE_TYPE;
            t = new Tokenizer();
            t.languageLevel = languageVersion;
            allowXPath40Syntax = t.allowSaxonExtensions = env.GetConfiguration().GetBooleanProperty(Feature<bool>.ALLOW_SYNTAX_EXTENSIONS) || t.languageLevel == 40;
            try
            {
                t.Tokenize(input, 0, -1);
            }
            catch (XPathException err)
            {
                Grumble(err.Message);
            }

            Values.SequenceType req = ParseSequenceType();
            if (t.currentToken != Token.EOF)
            {
                Grumble("Unexpected token " + CurrentTokenDisplay() + " beyond end of SequenceType");
            }

            return req;
        }

        public virtual Types.ItemType ParseExtendedItemType(string input, IStaticContext env)
        {
            this.env = env;
            SetLanguage(ParsedLanguage.EXTENDED_ITEM_TYPE, env.GetXPathVersion());
            t = new Tokenizer();
            t.languageLevel = env.GetXPathVersion();
            allowSaxonExtensions = t.allowSaxonExtensions = true;
            try
            {
                t.Tokenize(input, 0, -1);
            }
            catch (XPathException err)
            {
                Grumble(err.Message);
            }

            Types.ItemType req = ParseItemType();
            if (t.currentToken != Token.EOF)
            {
                Grumble("Unexpected token " + CurrentTokenDisplay() + " beyond end of ItemType");
            }

            return req;
        }

        public virtual Values.SequenceType ParseExtendedSequenceType(string input, IStaticContext env)
        {
            this.env = env;
            language = ParsedLanguage.EXTENDED_ITEM_TYPE;
            t = new Tokenizer();
            t.languageLevel = languageVersion = 40;
            allowSaxonExtensions = t.allowSaxonExtensions = true;
            allowXPath30Syntax = allowXPath31Syntax = allowXPath40Syntax = true;
            try
            {
                t.Tokenize(input, 0, -1);
            }
            catch (XPathException err)
            {
                Grumble(err.Message);
            }

            Values.SequenceType req = ParseSequenceType();
            if (t.currentToken != Token.EOF)
            {
                Grumble("Unexpected token " + CurrentTokenDisplay() + " beyond end of SequenceType");
            }

            return req;
        }

    }
}
