////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Globalization;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Expressions.Parsing
{
    public sealed class Tokenizer
    {
        public const char FULL_WIDTH_LT = '＜'; // xFF1C
        public const char FULL_WIDTH_GT = '＞'; // xFF1E
        public const char NUL = (char)0;
        // we may need to make this a stack at some time
        /// <summary>
        /// Initial default state of the Tokenizer
        /// </summary>
        public const int DEFAULT_STATE = 0;
        /// <summary>
        /// State in which a name is NOT to be merged with what comes next, for example "("
        /// </summary>
        public const int BARE_NAME_STATE = 1;
        /// <summary>
        /// State in which the next thing to be read is a SequenceType
        /// </summary>
        public const int SEQUENCE_TYPE_STATE = 2;
        /// <summary>
        /// State in which the next thing to be read is an @operator
        /// </summary>
        public const int OPERATOR_STATE = 3;
        private int state = DEFAULT_STATE;
        /// <summary>
        /// The number identifying the most recently read token
        /// </summary>
        public int currentToken = Token.EOF;
        /// <summary>
        /// The string value of the most recently read token
        /// </summary>
        public string currentTokenValue = null;
        /// <summary>
        /// The position in the input expression where the current token starts
        /// </summary>
        public int currentTokenStartOffset = 0;
        /// <summary>
        /// The number of the next token to be returned
        /// </summary>
        private int nextToken = Token.EOF;
        /// <summary>
        /// The string value of the next token to be returned
        /// </summary>
        private string nextTokenValue = null;
        /// <summary>
        /// The position in the expression of the start of the next token
        /// </summary>
        private int nextTokenStartOffset = 0;
        /// <summary>
        /// The string being parsed
        /// </summary>
        public string input;
        /// <summary>
        /// The current position within the input string
        /// </summary>
        public int inputOffset = 0;
        /// <summary>
        /// The length of the input string (in 2-byte chars)
        /// </summary>
        private int inputLength;
        /// <summary>
        /// The line number (within the expression) of the current token
        /// </summary>
        private int lineNumber = 1;
        /// <summary>
        /// The line number (within the expression) of the next token
        /// </summary>
        private int nextLineNumber = 1;
        private IList<int> newlineOffsets = null;
        /// <summary>
        /// The token number of the token that preceded the current token
        /// </summary>
        private int precedingToken = Token.UNKNOWN;
        /// <summary>
        /// The content of the preceding token
        /// </summary>
        private string precedingTokenValue = "";
        /// <summary>
        /// Flag to disallow "union" as a synonym for "|" when parsing XSLT 2.0 patterns
        /// </summary>
        public bool disallowUnionKeyword;
        /// <summary>
        /// Flag to indicate that this is XQuery as distinct from XPath
        /// </summary>
        public bool isXQuery = false;
        /// <summary>
        /// XPath language level: e.g. 2.0, 3.0, or 3.1
        /// </summary>
        public int languageLevel = 20;
        /// <summary>
        /// Flag to allow Saxon extensions
        /// </summary>
        public bool allowSaxonExtensions = false;

        public int State
        {
            get => state; set
            {
                this.state = value;
                if (value == DEFAULT_STATE)
                {

                    // force the followsOperator() test to return true
                    precedingToken = Token.UNKNOWN;
                    precedingTokenValue = "";
                    currentToken = Token.UNKNOWN;
                }
                else if (value == OPERATOR_STATE)
                {
                    precedingToken = Token.RPAR;
                    precedingTokenValue = ")";
                    currentToken = Token.RPAR;
                }
            }
        }

        private int Candidate
        {
            get
            {
                int candidate = -1;
                switch (currentTokenValue)
                {
                    case "element":
                        candidate = Token.ELEMENT_QNAME;
                        break;
                    case "attribute":
                        candidate = Token.ATTRIBUTE_QNAME;
                        break;
                    case "processing-instruction":
                        candidate = Token.PI_QNAME;
                        break;
                    case "namespace":
                        candidate = Token.NAMESPACE_QNAME;
                        break;
                }

                return candidate;
            }
        }
        public Tokenizer()
        {
        }

        //
        // Lexical analyser for expressions, queries, and XSLT patterns
        //
        public void Tokenize(string input, int start, int end)
        {
            nextToken = Token.EOF;
            nextTokenValue = null;
            nextTokenStartOffset = 0;
            inputOffset = start;
            this.input = input;
            this.lineNumber = 0;
            nextLineNumber = 0;
            if (end == -1)
            {
                inputLength = input.Length;
            }
            else
            {
                inputLength = end;
            }


            // The tokenizer actually reads one token ahead. The raw lexical analysis performed by
            // the lookAhead() method does not (in general) distinguish names used as QNames from names
            // used for operators, axes, and functions. The next() routine further refines names into the
            // correct category, by looking at the following token. In addition, it combines compound tokens
            // such as "instance of" and "cast as".
            LookAhead();
            Next();
        }

        //diagnostic version of next(): change real version to realnext()
        //
        //public void next() throws XPathException {
        public void Next()
        {
            precedingToken = currentToken;
            precedingTokenValue = currentTokenValue;
            currentToken = nextToken;
            currentTokenValue = nextTokenValue;
            if (currentTokenValue == null)
            {
                currentTokenValue = "";
            }

            currentTokenStartOffset = nextTokenStartOffset;
            lineNumber = nextLineNumber;

            // disambiguate the current token based on the tokenizer state
            switch (currentToken)
            {
                case Token.NAME:
                    int optype = GetBinaryOp(currentTokenValue);
                    if (optype != Token.UNKNOWN && !FollowsOperator(precedingToken))
                    {
                        currentToken = optype;
                    }

                    break;
                case Token.LT:
                    if (isXQuery && FollowsOperator(precedingToken) && !currentTokenValue.Equals("" + FULL_WIDTH_LT))
                    {
                        currentToken = Token.TAG;
                    }

                    break;
                case Token.STAR:
                    if (!FollowsOperator(precedingToken))
                    {
                        currentToken = Token.MULT;
                    }

                    break;
            }

            if (currentToken == Token.TAG || currentToken == Token.RCURLY || currentToken == Token.BACKTICK)
            {

                // No lookahead after encountering "<" at the start of an XML-like tag.
                // After an RCURLY, the parser must do an explicit lookahead() to continue
                // tokenizing; otherwise it can continue with direct character reading
                return;
            }

            int oldPrecedingToken = precedingToken;
            LookAhead();
            if (currentToken == Token.NAME)
            {
                if (state == BARE_NAME_STATE)
                {
                    return;
                }

                if (oldPrecedingToken == Token.DOLLAR)
                {
                    return;
                }

                HandleNextToken(oldPrecedingToken);
            }
        }

        public bool ThereMightBeAnArrowAhead()
        {
            return input.IndexOf("->", currentTokenStartOffset, StringComparison.Ordinal) >= 0 || input.IndexOf("-＞", currentTokenStartOffset, StringComparison.Ordinal) >= 0;
        }

        private void HandleNextToken(int oldPrecedingToken)
        {
            switch (nextToken)
            {
                case Token.LPAR:
                    HandleLPAR(oldPrecedingToken);
                    break;
                case Token.LCURLY:
                    HandleLCURLY();
                    break;
                case Token.COLONCOLON:
                    HandleCOLONCOLON();
                    break;
                case Token.HASH:
                    HandleHASH();
                    break;
                case Token.COLONSTAR:
                    HandleCOLONSTAR();
                    break;
                case Token.DOLLAR:
                    HandleDOLLAR();
                    break;
                case Token.PERCENT:
                    HandlePERCENT();
                    break;
                case Token.NAME:
                    int candidate = Candidate;
                    if (candidate != -1)
                    {

                        // <'element' QName '{'> constructor
                        // <'attribute' QName '{'> constructor
                        // <'processing-instruction' QName '{'> constructor
                        // <'namespace' QName '{'> constructor
                        string qname = nextTokenValue;
                        string saveTokenValue = currentTokenValue;
                        int savePosition = inputOffset;
                        LookAhead();
                        if (nextToken == Token.LCURLY)
                        {
                            currentToken = candidate;
                            currentTokenValue = qname;
                            LookAhead();
                            return;
                        }
                        else
                        {

                            // backtrack (we don't have 2-token lookahead; this is the
                            // only case where it's needed. So we backtrack instead.)
                            currentToken = Token.NAME;
                            currentTokenValue = saveTokenValue;
                            inputOffset = savePosition;
                            nextToken = Token.NAME;
                            nextTokenValue = qname;
                        }
                    }

                    string composite = currentTokenValue + ' ' + nextTokenValue;
                    int possibleToken = Token.doubleKeywords.GetOrDefault(composite, Token.UNKNOWN);
                    if (possibleToken == Token.UNKNOWN)
                    {
                        break;
                    }
                    else
                    {
                        HandleNotUnknown(composite, possibleToken);
                        return;
                    }

                default:
                    break;
            }
        }

        private void HandleLPAR(int oldPrecedingToken)
        {
            int op = GetBinaryOp(currentTokenValue);

            // the test on followsOperator() is to cater for an operator being used as a function name,
            // e.g. is(): see XQTS test K-FunctionProlog-66
            if (op == Token.UNKNOWN || FollowsOperator(oldPrecedingToken))
            {
                currentToken = GetFunctionType(currentTokenValue);
                LookAhead(); // swallow the "("
            }
            else
            {
                currentToken = op;
            }
        }

        private void HandleLCURLY()
        {
            if (state != SEQUENCE_TYPE_STATE)
            {
                currentToken = Token.KEYWORD_CURLY;
                LookAhead(); // swallow the "{"
            }
        }

        private void HandleCOLONCOLON()
        {
            LookAhead();
            currentToken = Token.AXIS;
        }

        private void HandleHASH()
        {
            LookAhead();
            currentToken = Token.NAMED_FUNCTION_REF;
        }

        private void HandleCOLONSTAR()
        {
            LookAhead();
            currentToken = Token.PREFIX;
        }

        private void HandleDOLLAR()
        {
            switch (currentTokenValue)
            {
                case "for":
                    currentToken = Token.FOR;
                    break;
                case "some":
                    currentToken = Token.SOME;
                    break;
                case "every":
                    currentToken = Token.EVERY;
                    break;
                case "let":
                    currentToken = Token.LET;
                    break;
                case "count":
                    currentToken = Token.COUNT;
                    break;
                case "copy":
                    currentToken = Token.COPY;
                    break;
            }
        }

        private void HandlePERCENT()
        {
            if (currentTokenValue.Equals("declare"))
            {
                currentToken = Token.DECLARE_ANNOTATED;
            }
        }

        private void HandleNotUnknown(string composite, int possibleToken)
        {
            currentToken = possibleToken;
            currentTokenValue = composite;

            // some tokens are actually triples
            if (currentToken == Token.REPLACE_VALUE)
            {

                // this one's a quadruplet - "replace value of node"
                LookAhead();
                if (nextToken != Token.NAME || !nextTokenValue.Equals("of"))
                {
                    throw new XPathException("After '" + composite + "', expected 'of'");
                }

                LookAhead();
                if (nextToken != Token.NAME || !nextTokenValue.Equals("node"))
                {
                    throw new XPathException("After 'replace value of', expected 'node'");
                }

                nextToken = currentToken; // to reestablish after-operator state
            }

            LookAhead();
        }

        /// <summary>
        /// Peek ahead at the next token
        /// </summary>
        public int PeekAhead()
        {
            return nextToken;
        }

        /// <summary>
        /// Force the current token to be treated as an operator if possible
        /// </summary>
        public void TreatCurrentAsOperator()
        {
            switch (currentToken)
            {
                case Token.NAME:
                    int optype = GetBinaryOp(currentTokenValue);
                    if (optype != Token.UNKNOWN)
                    {
                        currentToken = optype;
                    }

                    break;
                case Token.STAR:
                    currentToken = Token.MULT;
                    break;
            }
        }

        public void LookAhead()
        {
            precedingToken = nextToken;
            precedingTokenValue = nextTokenValue;
            nextTokenValue = null;
            nextTokenStartOffset = inputOffset;
            for (; ; )
            {
                if (inputOffset >= inputLength)
                {
                    nextToken = Token.EOF;
                    return;
                }

                char c = input[inputOffset++];
                switch (c)
                {
                    case '/':
                        if (inputOffset < inputLength && input[inputOffset] == '/')
                        {
                            inputOffset++;
                            nextToken = Token.SLASH_SLASH;
                            return;
                        }

                        nextToken = Token.SLASH;
                        return;
                    case ':':
                        if (inputOffset < inputLength)
                        {
                            if (input[inputOffset] == ':')
                            {
                                inputOffset++;
                                nextToken = Token.COLONCOLON;
                                return;
                            }
                            else if (input[inputOffset] == '=')
                            {
                                nextToken = Token.ASSIGN;
                                inputOffset++;
                                return;
                            } // if (input.charAt(inputOffset) == ' ') ??
                            else
                            {

                                // if (input.charAt(inputOffset) == ' ') ??
                                nextToken = Token.COLON;
                                return;
                            }
                        }

                        throw new XPathException("Unexpected colon at start of token");
                    case '@':
                        nextToken = Token.AT;
                        return;
                    case '?':
                        if (inputOffset < inputLength)
                        {
                            if (input[inputOffset] == '?')
                            {
                                inputOffset++;
                                nextToken = Token.QMARK_QMARK;
                                return;
                            }
                        }

                        nextToken = Token.QMARK;
                        return;
                    case '[':
                        nextToken = Token.LSQB;
                        return;
                    case ']':
                        nextToken = Token.RSQB;
                        return;
                    case '{':
                        nextToken = Token.LCURLY;
                        return;
                    case '}':
                        nextToken = Token.RCURLY;
                        return;
                    case ';':
                        nextToken = Token.SEMICOLON;
                        state = DEFAULT_STATE;
                        return;
                    case '%':
                        nextToken = Token.PERCENT;
                        return;
                    case '(':
                        if (inputOffset < inputLength && input[inputOffset] == '#')
                        {
                            inputOffset++;
                            int pragmaStart = inputOffset;
                            int nestingDepth = 1;
                            while (nestingDepth > 0 && inputOffset < (inputLength - 1))
                            {
                                if (input[inputOffset] == '\n')
                                {
                                    IncrementLineNumber();
                                }
                                else if (input[inputOffset] == '#' && input[inputOffset + 1] == ')')
                                {
                                    nestingDepth--;
                                    inputOffset++;
                                }
                                else if (input[inputOffset] == '(' && input[inputOffset + 1] == '#')
                                {
                                    nestingDepth++;
                                    inputOffset++;
                                }

                                inputOffset++;
                            }

                            if (nestingDepth > 0)
                            {
                                throw new XPathException("Unclosed XQuery pragma");
                            }

                            nextToken = Token.PRAGMA;
                            nextTokenValue = input.Substring(pragmaStart, inputOffset - 2 - pragmaStart);
                            return;
                        }

                        if (inputOffset < inputLength && input[inputOffset] == ':')
                        {

                            // XPath comment syntax is (: .... :)
                            // Comments may be nested, and may now be empty
                            inputOffset++;
                            int nestingDepth = 1;
                            while (nestingDepth > 0 && inputOffset < (inputLength - 1))
                            {
                                if (input[inputOffset] == '\n')
                                {
                                    IncrementLineNumber();
                                }
                                else if (input[inputOffset] == ':' && input[inputOffset + 1] == ')')
                                {
                                    nestingDepth--;
                                    inputOffset++;
                                }
                                else if (input[inputOffset] == '(' && input[inputOffset + 1] == ':')
                                {
                                    nestingDepth++;
                                    inputOffset++;
                                }

                                inputOffset++;
                            }

                            if (nestingDepth > 0)
                            {
                                throw new XPathException("Unclosed XPath comment");
                            }

                            // Restart the scan instead of recursing: the tail LookAhead() cost one
                            // stack frame per adjacent comment, so a long run of `(: :)` overflowed
                            // the stack with an uncatchable SOE at compile time. These four
                            // assignments reproduce the preamble the recursive call re-ran; continue
                            // re-enters the for(;;) scan.
                            precedingToken = nextToken;
                            precedingTokenValue = nextTokenValue;
                            nextTokenValue = null;
                            nextTokenStartOffset = inputOffset;
                            continue;
                        }
                        else
                        {
                            nextToken = Token.LPAR;
                        }

                        return;
                    case ')':
                        nextToken = Token.RPAR;
                        return;
                    case '+':
                        nextToken = Token.PLUS;
                        return;
                    case '-':
                        if (inputOffset < inputLength && IsGreaterThanChar(input[inputOffset]))
                        {
                            inputOffset++;
                            nextToken = Token.THIN_ARROW;
                            return;
                        }

                        nextToken = Token.MINUS; // not detected if part of a name
                        return;
                    case '=':
                        if (inputOffset < inputLength && IsGreaterThanChar(input[inputOffset]))
                        {
                            inputOffset++;
                            nextToken = Token.FAT_ARROW;
                            return;
                        }

                        if (inputOffset < inputLength - 1 && input[inputOffset] == '!' && IsGreaterThanChar(input[inputOffset + 1]))
                        {
                            inputOffset += 2;
                            nextToken = Token.MAPPING_ARROW; // Accepted in 4.0 only
                            return;
                        }

                        nextToken = Token.EQUALS;
                        return;
                    case '!':
                        if (inputOffset < inputLength)
                        {
                            if (input[inputOffset] == '=')
                            {
                                inputOffset++;
                                nextToken = Token.NE;
                                return;
                            }
                            else if (input[inputOffset] == '!')
                            {
                                inputOffset++;
                                nextToken = Token.BANG_BANG;
                                return;
                            }
                        }

                        nextToken = Token.BANG;
                        return;
                    case '*':

                        // disambiguation of MULT and STAR is now done later
                        if (inputOffset < inputLength && input[inputOffset] == ':' && inputOffset + 1 < inputLength && (input[inputOffset + 1] > 127 || NameChecker.IsNCNameStartChar(input[inputOffset + 1])))
                        {
                            inputOffset++;
                            nextToken = Token.SUFFIX;
                            return;
                        }

                        nextToken = Token.STAR;
                        return;
                    case '×':
                        if (languageLevel >= 40)
                        {
                            nextToken = Token.MATH_MULT;
                            return;
                        }
                        else
                        {
                            throw new XPathException("Multiply operator '×' is recognized only when XPath 4.0 is enabled");
                        }

                    case '÷':
                        if (languageLevel >= 40)
                        {
                            nextToken = Token.MATH_DIVIDE;
                            return;
                        }
                        else
                        {
                            throw new XPathException("Divide operator '÷' is recognized only when XPath 4.0 is enabled");
                        }

                    case ',':
                        nextToken = Token.COMMA;
                        return;
                    case '$':
                        nextToken = Token.DOLLAR;
                        return;
                    case FULL_WIDTH_LT:
                    case '<':
                        if (c == FULL_WIDTH_LT && languageLevel < 40)
                        {
                            throw new XPathException("Operator character FULL_WIDTH_LESS_THAN (xFF1C) requires XPath 4.0 to be enabled");
                        }

                        if (inputOffset < inputLength && input[inputOffset] == '=')
                        {
                            inputOffset++;
                            nextToken = Token.LE;
                            return;
                        }

                        if (inputOffset < inputLength && c == input[inputOffset])
                        {
                            inputOffset++;
                            nextToken = Token.PRECEDES;
                            return;
                        }

                        nextToken = Token.LT;
                        nextTokenValue = c + ""; // The parser needs to know which character was used
                        return;
                    case '|':
                        if (inputOffset < inputLength && input[inputOffset] == '|')
                        {
                            inputOffset++;
                            nextToken = Token.CONCAT;
                            return;
                        }

                        nextToken = Token.UNION;
                        return;
                    case '#':
                        nextToken = Token.HASH;
                        return;
                    case '>':
                    case FULL_WIDTH_GT:
                        if (c == FULL_WIDTH_GT && languageLevel < 40)
                        {
                            throw new XPathException("Operator character FULL_WIDTH_GREATER_THAN (xFF1E) requires XPath 4.0 to be enabled");
                        }

                        if (inputOffset < inputLength && input[inputOffset] == '=')
                        {
                            inputOffset++;
                            nextToken = Token.GE;
                            return;
                        }

                        if (inputOffset < inputLength && c == input[inputOffset])
                        {
                            inputOffset++;
                            nextToken = Token.FOLLOWS;
                            return;
                        }

                        nextToken = Token.GT;
                        return;
                    case '.':
                        if (inputOffset < inputLength && input[inputOffset] == '.')
                        {
                            inputOffset++;
                            nextToken = Token.DOTDOT;
                            return;
                        }


                        // TODO: drop this experimental syntax (.{expr} becomes ->{expr})
                        if (inputOffset < inputLength && input[inputOffset] == '{')
                        {
                            inputOffset++;
                            nextTokenValue = ".";
                            nextToken = Token.KEYWORD_CURLY;
                            return;
                        }

                        if (inputOffset == inputLength || input[inputOffset] < '0' || input[inputOffset] > '9')
                        {
                            nextToken = Token.DOT;
                            return;
                        }

                        goto case '0';
                    case '0':
                        if (inputOffset < inputLength && languageLevel >= 40)
                        {
                            if (input[inputOffset] == 'x')
                            {
                                inputOffset++;
                                while (inputOffset < inputLength && "0123456789abcdefABCDEF_".IndexOf(input[inputOffset]) >= 0)
                                {
                                    inputOffset++;
                                }

                                string body = input.Substring(nextTokenStartOffset + 2, inputOffset - nextTokenStartOffset - 2);
                                if (body.StartsWith("_", StringComparison.Ordinal) || body.EndsWith("_", StringComparison.Ordinal))
                                {
                                    throw new XPathException("Underscore not allowed at start or end of hex literal");
                                }

                                nextTokenValue = body.Replace("_", "");
                                nextToken = Token.HEX_INTEGER;
                                return;
                            }
                            else if (input[inputOffset] == 'b')
                            {
                                inputOffset++;
                                while (inputOffset < inputLength && "01_".IndexOf(input[inputOffset]) >= 0)
                                {
                                    inputOffset++;
                                }

                                string body = input.Substring(nextTokenStartOffset + 2, inputOffset - nextTokenStartOffset - 2);
                                if (body.StartsWith("_", StringComparison.Ordinal) || body.EndsWith("_", StringComparison.Ordinal))
                                {
                                    throw new XPathException("Underscore not allowed at start or end of binary literal");
                                }

                                nextTokenValue = body.Replace("_", "");
                                nextToken = Token.BINARY_INTEGER;
                                return;
                            }
                        }

                        goto case '1';
                    case '1':
                    case '2':
                    case '3':
                    case '4':
                    case '5':
                    case '6':
                    case '7':
                    case '8':
                    case '9':

                        // The logic here can return some tokens that are not legitimate numbers,
                        // for example "23e" or "1.0e+". However, this will only happen if the XPath
                        // expression as a whole is syntactically incorrect.
                        // These errors will be caught by the numeric constructor.
                        bool allowE = true;
                        bool allowSign = false;
                        bool allowDot = true;
                        bool keepGoing = true;
                        bool allowUnderscore = languageLevel >= 40;
                        while (true)
                        {
                            switch (c)
                            {
                                case '0':
                                case '1':
                                case '2':
                                case '3':
                                case '4':
                                case '5':
                                case '6':
                                case '7':
                                case '8':
                                case '9':
                                    allowSign = false;
                                    break;
                                case '.':
                                    if (allowDot)
                                    {
                                        allowDot = false;
                                        allowSign = false;
                                    }
                                    else
                                    {
                                        inputOffset--;
                                        keepGoing = false; //break numloop;
                                    }

                                    break;
                                case '_':
                                    if (allowUnderscore)
                                    {

                                        if (inputOffset >= inputLength || "0123456789_".IndexOf(input[inputOffset]) < 0)
                                        {
                                            throw new XPathException("Underscore must be followed by a digit (or another underscore)");
                                        }

                                        if (inputOffset < 2 || "0123456789_".IndexOf(input[inputOffset - 2]) < 0)
                                        {
                                            throw new XPathException("Underscore must be preceded by a digit (or another underscore)");
                                        }

                                        break;
                                    }
                                    else
                                    {
                                        throw new XPathException("Underscore is not allowed in numeric literal unless 4.0 is enabled");
                                    }

                                case 'E':
                                case 'e':
                                    if (allowE)
                                    {
                                        allowSign = true;
                                        allowE = false;
                                    }
                                    else
                                    {
                                        inputOffset--;
                                        keepGoing = false; //break numloop;
                                    }

                                    break;
                                case '+':
                                case '-':
                                    if (allowSign)
                                    {
                                        allowSign = false;
                                    }
                                    else
                                    {
                                        inputOffset--;
                                        keepGoing = false; //break numloop;
                                    }

                                    break;
                                default:
                                    if (('a' <= c && c <= 'z') || c > 127)
                                    {

                                        // this prevents the famous "10div 3"
                                        throw new XPathException("Separator needed after numeric literal");
                                    }

                                    inputOffset--;
                                    keepGoing = false;
                                    break;
                            }

                            if (!keepGoing || inputOffset >= inputLength)
                            {
                                break;
                            }

                            c = input[inputOffset++];
                        }

                        nextTokenValue = input.Substring(nextTokenStartOffset, inputOffset - nextTokenStartOffset).Replace("_", "");
                        nextToken = Token.NUMBER;
                        return;
                    case '"':
                    case '\'':
                        nextTokenValue = "";
                        while (true)
                        {
                            inputOffset = input.IndexOf(c, inputOffset);
                            if (inputOffset < 0)
                            {
                                inputOffset = nextTokenStartOffset + 1;
                                throw new XPathException("Unmatched quote in expression");
                            }

                            nextTokenValue += input.Substring(nextTokenStartOffset + 1, (inputOffset++) - nextTokenStartOffset - 1);
                            if (inputOffset < inputLength)
                            {
                                char n = input[inputOffset];
                                if (n == c)
                                {

                                    // Doubled delimiters
                                    nextTokenValue += c;
                                    nextTokenStartOffset = inputOffset;
                                    inputOffset++;
                                }
                                else
                                {
                                    break;
                                }
                            }
                            else
                            {
                                break;
                            }
                        }


                        // maintain line number if there are newlines in the string
                        if (nextTokenValue.IndexOf('\n') >= 0)
                        {
                            for (int i = 0; i < nextTokenValue.Length; i++)
                            {
                                if (nextTokenValue[i] == '\n')
                                {
                                    IncrementLineNumber(nextTokenStartOffset + i + 1);
                                }
                            }
                        }


                        //nextTokenValue = nextTokenValue.intern();
                        nextToken = Token.STRING_LITERAL;
                        return;
                    case '`':
                        if (inputOffset < inputLength - 1 && input[inputOffset] == '`' && input[inputOffset + 1] == '[')
                        {
                            if (!isXQuery)
                            {
                                throw new XPathException("String constructors (starting '``[') are allowed only in XQuery, not XPath");
                            }

                            inputOffset += 2;
                            int j = inputOffset;
                            int newlines = 0;
                            while (true)
                            {
                                if (j >= inputLength)
                                {
                                    throw new XPathException("Unclosed string template in expression");
                                }

                                if (input[j] == '\n')
                                {
                                    newlines++;
                                }
                                else if (input[j] == '`' && j + 1 < inputLength && input[j + 1] == '{')
                                {
                                    nextToken = Token.STRING_CONSTRUCTOR_INITIAL;
                                    nextTokenValue = input.Substring(inputOffset, j - inputOffset);
                                    inputOffset = j + 2;
                                    IncrementLineNumber(newlines);
                                    return;
                                }
                                else if (input[j] == ']' && j + 2 < inputLength && input[j + 1] == '`' && input[j + 2] == '`')
                                {
                                    nextToken = Token.STRING_LITERAL_BACKTICKED;

                                    // Can't return STRING_LITERAL because it's not accepted everywhere that a string literal @is, and
                                    // because it doesn't get unescaped (bug 5647)
                                    nextTokenValue = input.Substring(inputOffset, j - inputOffset);
                                    inputOffset = j + 3;
                                    IncrementLineNumber(newlines);
                                    return;
                                }

                                j++;
                            }
                        }
                        else
                        {
                            nextToken = Token.BACKTICK;
                            return;
                        }

                    case '\n':
                        IncrementLineNumber();
                        goto case ' ';
                    case ' ':
                    case '\t':
                    case '\r':
                        nextTokenStartOffset = inputOffset;
                        break;
                    case '¶':
                    case 'Q':
                        if (inputOffset < inputLength && input[inputOffset] == '{')
                        {

                            // EQName, revised syntax as per bug 15399
                            int close = input.IndexOf('}', inputOffset++);
                            if (close < inputOffset)
                            {
                                throw new XPathException("Missing closing brace in EQName");
                            }

                            string uri = input.Substring(inputOffset, close - inputOffset);
                            uri = Whitespace.CollapseWhitespace(uri); // Bug 29708
                            if (uri.Contains("{"))
                            {
                                throw new XPathException("EQName must not contain opening brace");
                            }

                            inputOffset = close + 1;
                            int start = inputOffset;
                            bool isStar = false;
                            while (inputOffset < inputLength)
                            {
                                char c2 = input[inputOffset];
                                if (c2 > 0x80 || char.IsLetterOrDigit(c2) || c2 == '_' || c2 == '.' || c2 == '-')
                                {
                                    inputOffset++;
                                }
                                else if (c2 == '*' && (start == inputOffset))
                                {
                                    inputOffset++;
                                    isStar = true;
                                    break;
                                }
                                else
                                {
                                    break;
                                }
                            }

                            string localName = input.Substring(start, inputOffset - start);
                            nextTokenValue = "Q{" + uri + "}" + localName;

                            // Reuse Token.NAME because EQName is allowed anywhere that QName is allowed
                            nextToken = isStar ? Token.PREFIX : Token.NAME;
                            return;
                        }

                        goto default;
                    default:
                        if (c < 0x80 && !char.IsLetter(c))
                        {
                            throw new XPathException("Invalid character '" + c + "' (x" + ((int)c).ToString("x", CultureInfo.InvariantCulture) + ") in expression");
                        }

                        goto case '_';
                        break;
                    case '_':
                        bool foundColon = false;
                        bool breakLoop = false;
                        for (; inputOffset < inputLength; inputOffset++)
                        {
                            c = input[inputOffset];
                            switch (c)
                            {
                                case ':':
                                    if (!foundColon)
                                    {
                                        if (precedingToken == Token.QMARK || precedingToken == Token.SUFFIX)
                                        {

                                            // only NCName allowed after "? in a lookup expression, or after *:
                                            nextTokenValue = input.Substring(nextTokenStartOffset, inputOffset - nextTokenStartOffset);
                                            nextToken = Token.NAME;
                                            return;
                                        }

                                        if (inputOffset + 1 < inputLength)
                                        {
                                            char nc = input[inputOffset + 1];
                                            if (nc == ':')
                                            {
                                                nextTokenValue = input.Substring(nextTokenStartOffset, inputOffset - nextTokenStartOffset);
                                                nextToken = Token.AXIS;
                                                inputOffset += 2;
                                                return;
                                            }
                                            else if (nc == '*')
                                            {
                                                nextTokenValue = input.Substring(nextTokenStartOffset, inputOffset - nextTokenStartOffset);
                                                nextToken = Token.PREFIX;
                                                inputOffset += 2;
                                                return;
                                            }
                                            else if (!(nc == '_' || nc > 127 || char.IsLetter(nc)))
                                            {

                                                // for example: "let $x:=2", "x:y:z", "x:2"
                                                // end the token before the colon
                                                nextTokenValue = input.Substring(nextTokenStartOffset, inputOffset - nextTokenStartOffset);
                                                nextToken = Token.NAME;
                                                return;
                                            }
                                        }

                                        foundColon = true;
                                    }
                                    else
                                    {
                                        breakLoop = true;
                                    }

                                    break;
                                case '.':
                                case '-':

                                    // If the name up to the "-" or "." is a valid @operator, and if the preceding token
                                    // is such that an operator is valid here and an NCName isn't, then quit here (bug 2715)
                                    if (precedingToken > Token.LAST_OPERATOR && !(precedingToken == Token.QMARK || precedingToken == Token.SUFFIX) && GetBinaryOp(input.Substring(nextTokenStartOffset, inputOffset - nextTokenStartOffset)) != Token.UNKNOWN && !(precedingToken == Token.NAME && GetBinaryOp(precedingTokenValue) != Token.UNKNOWN))
                                    {
                                        nextToken = GetBinaryOp(input.Substring(nextTokenStartOffset, inputOffset - nextTokenStartOffset));
                                        return;
                                    }

                                    goto case '_';
                                case '_':
                                    break;
                                default:
                                    if (c < 0x80 && !char.IsLetterOrDigit(c))
                                    {
                                        breakLoop = true;
                                    }

                                    break;
                            }

                            if (breakLoop)
                            {
                                break;
                            }
                        }

                        nextTokenValue = input.Substring(nextTokenStartOffset, inputOffset - nextTokenStartOffset);

                        //nextTokenValue = nextTokenValue.intern();
                        nextToken = Token.NAME;
                        return;
                }
            }
        }

        public int GetBinaryOp(string s)
        {
            switch (s)
            {
                case "after":
                    return Token.AFTER;
                case "and":
                    return Token.AND;
                case "as":
                    return Token.AS;
                case "before":
                    return Token.BEFORE;
                case "case":
                    return Token.CASE;
                case "default":
                    return Token.DEFAULT;
                case "div":
                    return Token.DIV;
                case "else":
                    return Token.ELSE;
                case "eq":
                    return Token.FEQ;
                case "except":
                    return Token.EXCEPT;
                case "ge":
                    return Token.FGE;
                case "gt":
                    return Token.FGT;
                case "idiv":
                    return Token.IDIV;
                case "in":
                    return Token.IN;
                case "intersect":
                    return Token.INTERSECT;
                case "into":
                    return Token.INTO;
                case "is":
                    return Token.IS;
                case "le":
                    return Token.FLE;
                case "lt":
                    return Token.FLT;
                case "mod":
                    return Token.MOD;
                case "modify":
                    return Token.MODIFY;
                case "ne":
                    return Token.FNE;
                case "or":
                    return Token.OR;
                case "otherwise":
                    return Token.OTHERWISE;
                case "return":
                    return Token.RETURN;
                case "satisfies":
                    return Token.SATISFIES;
                case "then":
                    return Token.THEN;
                case "to":
                    return Token.TO;
                case "union":
                    return Token.UNION;
                case "where":
                    return Token.WHERE;
                case "while":
                    return Token.WHILE;
                case "with":
                    return Token.WITH;
                case "orElse":
                    return allowSaxonExtensions ? Token.OR_ELSE : Token.UNKNOWN;
                case "andAlso":
                    return allowSaxonExtensions ? Token.AND_ALSO : Token.UNKNOWN;
                default:
                    return Token.UNKNOWN;
            }
        }

        private bool IsGreaterThanChar(char c)
        {
            return c == '>' || (languageLevel >= 40 && c == FULL_WIDTH_GT);
        }

        private int GetFunctionType(string s)
        {
            switch (s)
            {
                case "if":
                    return Token.IF;
                case "namespace-node":
                case "function":
                    return languageLevel == 20 ? Token.FUNCTION : Token.KEYWORD_LBRA;
                case "fn":
                    return languageLevel >= 40 ? Token.KEYWORD_LBRA : Token.FUNCTION;
                case "array":
                case "map":

                    // first reserved in 3.1, unreserved again in 4.0
                    return languageLevel == 31 ? Token.KEYWORD_LBRA : Token.FUNCTION;
                case "node":
                case "schema-attribute":
                case "schema-element":
                case "processing-instruction":
                case "empty-sequence":
                case "document-node":
                case "comment":
                case "element":
                case "item":
                case "text":
                case "attribute":
                    return Token.KEYWORD_LBRA;
                case "atomic":
                case "tuple":
                case "record":
                case "type":
                case "union":
                case "enum":
                    return allowSaxonExtensions ? Token.KEYWORD_LBRA : Token.FUNCTION; // Saxon extension types
                case "switch":

                    // Reserved in XPath 3.0, even though only used in XQuery
                    return languageLevel == 20 ? Token.FUNCTION : Token.SWITCH;
                case "otherwise":
                    return Token.OTHERWISE;
                case "typeswitch":
                    return Token.TYPESWITCH;
                default:
                    return Token.FUNCTION;
            }
        }

        private bool FollowsOperator(int precedingToken)
        {
            return precedingToken <= Token.LAST_OPERATOR;
        }

        public char NextChar()
        {
            if (inputOffset < inputLength)
            {
                char c = input[inputOffset++];

                //c = normalizeLineEnding(c);
                if (c == '\n')
                {
                    IncrementLineNumber();
                    lineNumber++;
                }

                return c;
            }
            else
            {
                inputOffset++; // in case of an unreadChar()
                return NUL;
            }
        }

        public char PeekChar()
        {
            if (inputOffset < inputLength)
            {
                return input[inputOffset];
            }
            else
            {
                return NUL;
            }
        }

        /// <summary>
        /// Increment the line number, making a record of where in the input string the newline character occurred.
        /// </summary>
        private void IncrementLineNumber()
        {
            nextLineNumber++;
            if (newlineOffsets == null)
            {
                newlineOffsets = new List<int>(20);
            }

            newlineOffsets.Add(inputOffset - 1);
        }

        public void IncrementLineNumber(int offset)
        {
            nextLineNumber++;
            if (newlineOffsets == null)
            {
                newlineOffsets = new List<int>(20);
            }

            newlineOffsets.Add(offset);
        }

        public void UnreadChar()
        {
            if (inputOffset > inputLength)
            {
                return;
            }

            if (input[--inputOffset] == '\n')
            {
                nextLineNumber--;
                lineNumber--;
                if (newlineOffsets != null)
                {
                    newlineOffsets.Remove(newlineOffsets.Count - 1);
                }
            }
        }

        public string RecentText(int offset)
        {
            if (offset == -1)
            {

                // if no offset was supplied, we want the text immediately before the current reading position
                if (inputOffset > inputLength)
                {
                    inputOffset = inputLength;
                }

                if (inputOffset < 34)
                {
                    return input.Substring(0, inputOffset);
                }
                else
                {
                    return Whitespace.CollapseWhitespace("..." + input.Substring(inputOffset - 30, 30));
                }
            }
            else
            {

                // if a specific offset was supplied, we want the text *starting* at that offset
                int end = offset + 30;
                if (end > inputLength)
                {
                    end = inputLength;
                }

                return Whitespace.CollapseWhitespace((offset > 0 ? "..." : "") + input.Substring(offset, end - offset));
            }
        }

        public void CopyTo(Tokenizer u)
        {
            u.currentToken = currentToken;
            u.currentTokenValue = currentTokenValue;
            u.precedingToken = precedingToken;
            u.precedingTokenValue = precedingTokenValue;
            u.nextToken = nextToken;
            u.nextTokenValue = nextTokenValue;
            u.inputOffset = inputOffset;
            u.lineNumber = lineNumber;
            u.nextLineNumber = nextLineNumber;
            if (newlineOffsets == null)
            {

                // written this way for transpilation reasons
                u.newlineOffsets = null;
            }
            else
            {
                u.newlineOffsets = new List<int>(newlineOffsets);
            }

            u.state = state;
        }

        public int GetLineNumber()
        {
            return lineNumber;
        }

        public int GetColumnNumber()
        {
            return (int)(GetLineAndColumn(currentTokenStartOffset) & 0x7fffffff);
        }

        private long GetLineAndColumn(int offset)
        {
            if (newlineOffsets == null)
            {
                return offset;
            }

            for (int line = newlineOffsets.Count - 1; line >= 0; line--)
            {
                int nloffset = newlineOffsets[line];
                if (offset > nloffset)
                {
                    return ((long)(line + 1) << 32) | (long)(offset - nloffset);
                }
            }

            return offset;
        }

        public int GetLineNumber(int offset)
        {
            return (int)(GetLineAndColumn(offset) >> 32);
        }

        public int GetColumnNumber(int offset)
        {
            return (int)(GetLineAndColumn(offset) & 0x7fffffff);
        }
    }
}