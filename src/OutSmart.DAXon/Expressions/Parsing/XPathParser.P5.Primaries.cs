////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Collections;
using OutSmart.DAXon.Internal;
using System.Numerics;
namespace OutSmart.DAXon.Expressions.Parsing
{
    // XPathParser part: primary expressions — parenthesized/lambda, numeric/string literals, string
    // templates, variable references, dynamic function calls, lookups.
    public partial class XPathParser
    {
        public virtual Expression ParseParenthesizedExpression()
        {
            NextToken();
            if (t.currentToken == Token.RPAR)
            {
                NextToken();
                return MakeTracer(Literal.MakeEmptySequence(), null);
            }

            Expression seq = ParseExpression();
            Expect(Token.RPAR);
            NextToken();
            return seq;
        }

        public virtual IList<StructuredQName> ParseLambdaParams()
        {
            IList<StructuredQName> result = new List<StructuredQName>(4);
            NextToken();
            if (t.currentToken == Token.RPAR)
            {
                NextToken();
                if (t.currentToken == Token.THIN_ARROW)
                {
                    NextToken();
                    return result;
                }
                else
                {
                    return null;
                }
            }

            while (true)
            {
                if (t.currentToken != Token.DOLLAR)
                {
                    return null;
                }

                NextToken();
                if (t.currentToken == Token.NAME)
                {
                    result.Add(MakeStructuredQName(t.currentTokenValue, NamespaceUri.NULL));
                }
                else
                {
                    return null;
                }

                NextToken();
                if (t.currentToken == Token.COMMA)
                {
                    NextToken();
                }
                else if (t.currentToken == Token.RPAR)
                {
                    NextToken();
                    if (t.currentToken == Token.THIN_ARROW)
                    {
                        NextToken();
                        return result;
                    }
                    else
                    {
                        return null;
                    }
                }
                else
                {
                    return null;
                }
            }
        }

        protected virtual void TestPermittedAxis(int axis, string errorCode)
        {
            if (axis == AxisInfo.PRECEDING_OR_ANCESTOR)
            {
                Grumble("The preceding-or-ancestor axis is for internal use only", errorCode);
            }
        }

        public virtual Expression ParseNumericLiteral(bool traceable)
        {
            int offset = t.currentTokenStartOffset;
            NumericValue number = NumericValue.ParseNumber(t.currentTokenValue);
            if (number.IsNaN())
            {
                Grumble("Invalid numeric literal " + Err.Wrap(t.currentTokenValue, Err.VALUE));
            }

            NextToken();
            Literal lit = Literal.MakeLiteral(number);
            SetLocation(lit, offset);

            return traceable ? MakeTracer(lit, null) : lit;
        }

        public virtual Expression ParseHexLiteral(bool traceable)
        {
            try
            {
                int offset = t.currentTokenStartOffset;
                IntegerValue val;
                if (t.currentTokenValue.Length < 16)
                {
                    if ((t.currentTokenValue.Length == 0))
                    {

                        // Handled specially because .NET code otherwise crashes
                        Grumble("Empty hex literal");
                    }

                    long parsed = Convert.ToInt64(t.currentTokenValue, 16);
                    val = new Int64Value(parsed);
                }
                else
                {
                    BigInteger big = BigIntegers.FromString(t.currentTokenValue, 16);
                    val = new BigIntegerValue(big);
                }

                NextToken();
                Literal lit = Literal.MakeLiteral(val);
                SetLocation(lit, offset);
                return traceable ? MakeTracer(lit, null) : lit;
            }
            catch (FormatException)
            {
                Grumble("Invalid hex literal");
                return null;
            }
        }

        public virtual Expression ParseBinaryLiteral(bool traceable)
        {
            try
            {
                int offset = t.currentTokenStartOffset;
                IntegerValue val;
                if (t.currentTokenValue.Length < 64)
                {
                    if ((t.currentTokenValue.Length == 0))
                    {

                        // Handled specially because .NET code otherwise crashes
                        Grumble("Empty binary literal");
                    }

                    long parsed = BinaryStringToLong(t.currentTokenValue);
                    val = new Int64Value(parsed);
                }
                else
                {
                    BigInteger big = BigIntegers.FromString(t.currentTokenValue, 2);
                    val = new BigIntegerValue(big);
                }

                NextToken();
                Literal lit = Literal.MakeLiteral(val);
                SetLocation(lit, offset);
                return traceable ? MakeTracer(lit, null) : lit;
            }
            catch (FormatException)
            {
                Grumble("Invalid binary literal");
                return null;
            }
        }

        private long BinaryStringToLong(string input)
        {
            return Convert.ToInt64(input, 2);
        }

        protected virtual Expression ParseStringLiteral(bool traceable)
        {
            Literal literal = MakeStringLiteral(t.currentTokenValue, true);
            NextToken();
            return traceable ? MakeTracer(literal, null) : literal;
        }

        protected virtual Expression ParseBackTickedStringLiteral()
        {
            Literal literal = MakeStringLiteral(t.currentTokenValue, false);
            NextToken();
            return MakeTracer(literal, null);
        }

        protected virtual Expression ParseStringConstructor()
        {
            Grumble("String constructor expressions are allowed only in XQuery");
            return null;
        }

        public virtual Expression ParseStringTemplate()
        {

            // we're reading raw characters
            //t.nextChar(); // lose this one, it's the initial backtick
            IList<Expression> components = new List<Expression>();
            StringBuilder currentPart = new StringBuilder();
            bool finished = false;
            do
            {
                char c = t.NextChar();
                switch (c)
                {
                    case (char)0:
                        Grumble("Unclosed string template");
                        return null;
                    case '`':
                        c = t.NextChar();
                        if (c == '`')
                        {
                            currentPart.Append('`');
                        }
                        else
                        {
                            Expression @fixed = new StringLiteral(currentPart.ToString());
                            components.Add(@fixed);
                            finished = true;
                            t.UnreadChar();
                            t.LookAhead();
                            NextToken();
                        }

                        break;
                    case '{':
                        c = t.NextChar();
                        if (c == '{')
                        {
                            currentPart.Append('{');
                        }
                        else
                        {
                            Expression @fixed = new StringLiteral(currentPart.ToString());
                            components.Add(@fixed);
                            currentPart.Length = 0;
                            t.UnreadChar();
                            t.State = Tokenizer.DEFAULT_STATE;
                            t.LookAhead();
                            NextToken();
                            if (t.currentToken == Token.RCURLY)
                            {
                            }
                            else
                            {
                                Expression exp = ParseExpression();
                                RetainedStaticContext rscSJ = new RetainedStaticContext(env);
                                Expression fnSJ = SystemFunction.MakeCall("string-join", rscSJ, exp, new StringLiteral(StringValue.SINGLE_SPACE));
                                ExpressionTool.CopyLocationInfo(exp, fnSJ);
                                components.Add(fnSJ);
                                Expect(Token.RCURLY);
                            }
                        }

                        break;
                    case '}':
                        c = t.NextChar();
                        if (c == '}')
                        {
                            currentPart.Append('}');
                        }
                        else
                        {
                            Grumble("Closing brace ('}') in string template must be doubled");
                        }

                        break;
                    default:
                        currentPart.Append(c);
                        break;
                }
            }
            while (!finished);
            RetainedStaticContext rsc = new RetainedStaticContext(env);
            Block block = new Block(components.ToArray());
            SetLocation(block);
            Expression fn = SystemFunction.MakeCall("string-join", rsc, block, new StringLiteral(StringValue.EMPTY_STRING));
            ExpressionTool.CopyLocationInfo(block, fn);
            components.Add(fn);
            return fn;
        }

        public virtual StructuredQName ParseVariableName()
        {
            NextToken();
            Expect(Token.NAME);
            string var = t.currentTokenValue;
            NextToken();
            if (scanOnly)
            {
                return new StructuredQName("", NamespaceUri.SAXON_GENERATED_VARIABLE, "dummy");
            }

            StructuredQName vtest = MakeStructuredQName(var, NamespaceUri.NULL);
            return vtest;
        }

        public virtual Expression ResolveVariableReference(int offset, StructuredQName vtest)
        {

            // See if it's a range variable or a variable in the context
            if (scanOnly)
            {
                return Literal.MakeEmptySequence();
            }

            ILocalBinding b = FindRangeVariable(vtest);
            Expression @ref;
            if (b != null)
            {
                @ref = new LocalVariableReference(b);
            }
            else
            {
                if (catchDepth > 0)
                {
                    foreach (StructuredQName errorVariable in StandardNames.errorVariables)
                    {
                        if (errorVariable.GetLocalPart().Equals(vtest.GetLocalPart()))
                        {
                            // Build saxon:dynamic-error-info(<name>) the same way the XSLT xsl:catch path does
                            // (ExpressionContext.BindVariable): MakeFunction + MakeFunctionCall. The BuiltInFunctionSet
                            // .Bind overload returned a node the parser then treated as a bound reference but which had
                            // no wired argument operand → SetDeepRetainedStaticContext walked a null child (NRE).
                            SystemFunction f = VendorFunctionSetHE.GetInstance().MakeFunction("dynamic-error-info", 1);
                            Expression call = f.MakeFunctionCall(new StringLiteral(vtest.GetLocalPart()));
                            SetLocation(call, offset);
                            return call;
                        }
                    }
                }

                try
                {
                    @ref = env.BindVariable(vtest);
                }
                catch (XPathException err)
                {
                    throw err.MaybeWithLocation(MakeLocation());
                }
            }

            SetLocation(@ref, offset);
            return @ref;
        }

        protected virtual Literal MakeStringLiteral(string currentTokenValue, bool unescape)
        {
            StringLiteral literal = new StringLiteral(currentTokenValue);
            SetLocation(literal);
            return literal;
        }

        protected virtual string Unescape(string token)
        {
            return token;
        }

        protected virtual Expression ParseConstructor()
        {
            Grumble("Node constructor expressions are allowed only in XQuery, not in XPath");
            return new ErrorExpression();
        }

        public virtual Expression ParseDynamicFunctionCall(Expression functionItem, Expression prefixArgument)
        {
            CheckLanguageVersion30();
            List<Expression> args = new List<Expression>(10);
            if (prefixArgument != null)
            {
                args.Add(prefixArgument);
            }

            IntSet placeMarkers = null;

            // the "(" has already been read by the Tokenizer: now parse the arguments
            NextToken();
            if (t.currentToken != Token.RPAR)
            {
                while (true)
                {
                    Expression arg;
                    int peek = t.PeekAhead();
                    if (t.currentToken == Token.QMARK && (peek == Token.COMMA || peek == Token.RPAR))
                    {
                        NextToken();

                        // this is a "?" placemarker
                        if (placeMarkers == null)
                        {
                            placeMarkers = new IntArraySet();
                        }

                        placeMarkers.Add(args.Count);
                        arg = Literal.MakeEmptySequence(); // a convenient fiction
                    }
                    else
                    {
                        arg = ParseFunctionArgument();
                    }

                    args.Add(arg);
                    if (t.currentToken == Token.COMMA)
                    {
                        NextToken();
                    }
                    else if (t.currentToken == Token.COLON)
                    {
                        Grumble("Keyword arguments are not allowed in a dynamic function call");
                    }
                    else
                    {
                        break;
                    }
                }

                Expect(Token.RPAR);
            }

            NextToken();
            if (placeMarkers == null)
            {
                return GenerateApplyCall(functionItem, args);
            }
            else
            {
                return CreateDynamicCurriedFunction(this, functionItem, args, placeMarkers);
            }
        }

        protected virtual Expression GenerateApplyCall(Expression functionItem, List<Expression> args)
        {
            DynamicFunctionCall call = new DynamicFunctionCall(functionItem, args);
            SetLocation(call, t.currentTokenStartOffset);
            return call;
        }

        protected virtual Expression ParseLookup(Expression lhs)
        {
            CheckLanguageVersion31();
            Tokenizer t = GetTokenizer();
            int offset = t.currentTokenStartOffset;
            t.State = Tokenizer.BARE_NAME_STATE; // Prevent mis-recognition of x?f(2)
            t.currentToken = Token.LPAR; // Hack to force following symbol to be recognised in post-operator mode
            NextToken();
            int token = t.currentToken;
            t.State = Tokenizer.OPERATOR_STATE;
            Expression result;
            if (token == Token.NAME)
            {
                string name = t.currentTokenValue;
                if (!NameChecker.IsValidNCName(StringTool.CodePoints(name)))
                {
                    Grumble("The name following '?' must be a valid NCName");
                }

                NextToken();
                result = LookupName(lhs, name);
            }
            else if (token == Token.NUMBER)
            {
                NumericValue number = NumericValue.ParseNumber(t.currentTokenValue);
                if (!(number is IntegerValue))
                {
                    Grumble("Number following '?' must be an integer");
                }

                NextToken();
                result = new LookupExpression(lhs, Literal.MakeLiteral(number));
            }
            else if (token == Token.MULT || token == Token.STAR)
            {
                NextToken();
                result = LookupStar(lhs);
            }
            else if (token == Token.LPAR)
            {
                t.State = Tokenizer.DEFAULT_STATE;
                result = new LookupExpression(lhs, ParseParenthesizedExpression());
            }
            else if (token == Token.STRING_LITERAL)
            {
                CheckLanguageVersion40();
                result = LookupName(lhs, t.currentTokenValue);
                NextToken();
            }
            else if (token == Token.DOLLAR)
            {
                CheckLanguageVersion40();
                offset = t.currentTokenStartOffset;
                StructuredQName varName = ParseVariableName();
                result = new LookupExpression(lhs, ResolveVariableReference(offset, varName));
            }
            else
            {
                Grumble("Unexpected " + Token.tokens[token] + " after '?'");
                return null;
            }

            SetLocation(result, offset);
            return result;
        }

        private Expression LookupName(Expression lhs, string rhs)
        {
            return new LookupExpression(lhs, new StringLiteral(rhs));
        }

        private static Expression LookupStar(Expression lhs)
        {
            return new LookupAllExpression(lhs);
        }

    }
}
