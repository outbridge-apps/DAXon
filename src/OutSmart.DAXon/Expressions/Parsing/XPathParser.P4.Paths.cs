////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.XQuery;
using System.Collections.Generic;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Expressions.Parsing
{
    // XPathParser part: unary and path grammar — steps, axes, predicates, arrow postfix.
    public partial class XPathParser
    {
        private Expression ParseUnaryExpression()
        {
            Expression exp;
            switch (t.currentToken)
            {
                case Token.MINUS:
                    {
                        // Direct recursion bypassing the ParseExprSingle guard: an unguarded ----...-1
                        // sign chain overflows the uncatchable .NET stack (~3M signs). Same idiom:
                        // increment before the try so the finally pairs only with a successful entry.
                        if (++expressionDepth > MAX_EXPRESSION_NESTING)
                        {
                            expressionDepth--;
                            Grumble("Expression is too deeply nested (exceeds the limit of " + MAX_EXPRESSION_NESTING + ")", "XPST0003");
                        }

                        try
                        {
                            NextToken();
                            Expression operand = ParseUnaryExpression();
                            exp = MakeUnaryExpression(Token.NEGATE, operand);
                        }
                        finally
                        {
                            expressionDepth--;
                        }

                        break;
                    }

                case Token.PLUS:
                    {
                        // Same direct-recursion guard as MINUS above.
                        if (++expressionDepth > MAX_EXPRESSION_NESTING)
                        {
                            expressionDepth--;
                            Grumble("Expression is too deeply nested (exceeds the limit of " + MAX_EXPRESSION_NESTING + ")", "XPST0003");
                        }

                        try
                        {
                            NextToken();

                            // Unary plus: can't ignore it completely, it might be a type error, or it might
                            // force conversion to a number which would affect operations such as "=".
                            Expression operand = ParseUnaryExpression();
                            exp = MakeUnaryExpression(Token.PLUS, operand);
                        }
                        finally
                        {
                            expressionDepth--;
                        }

                        break;
                    }

                case Token.VALIDATE:
                case Token.VALIDATE_STRICT:
                case Token.VALIDATE_LAX:
                case Token.VALIDATE_TYPE:
                    exp = ParseValidateExpression();
                    break;
                case Token.PRAGMA:
                    exp = ParseExtensionExpression();
                    break;
                case Token.KEYWORD_CURLY:
                    if (t.currentTokenValue.Equals("validate"))
                    {
                        exp = ParseValidateExpression();
                        break;
                    }

                    goto default;
                default:
                    exp = ParseSimpleMappingExpression();
                    break;
            }

            SetLocation(exp);
            return exp;
        }

        private Expression MakeUnaryExpression(int @operator, Expression operand)
        {
            if (Literal.IsAtomic(operand))
            {

                // very early evaluation of expressions like "-1", so they are treated as numeric literals
                AtomicValue val = (AtomicValue)((Literal)operand).GroundedValue;
                if (val is NumericValue)
                {
                    if (env.IsInBackwardsCompatibleMode())
                    {
                        val = new DoubleValue(((NumericValue)val).GetDoubleValue());
                    }

                    AtomicValue value = @operator == Token.NEGATE ? ((NumericValue)val).Negate() : (NumericValue)val;
                    return Literal.MakeLiteral(value);
                }
            }

            return env.GetConfiguration().GetTypeChecker(env.IsInBackwardsCompatibleMode()).MakeArithmeticExpression(Literal.MakeLiteral(Int64Value.ZERO), @operator, operand);
        }

        protected virtual bool AtStartOfRelativePath()
        {
            switch (t.currentToken)
            {
                case Token.AXIS:
                case Token.AT:
                case Token.NAME:
                case Token.PREFIX:
                case Token.SUFFIX:
                case Token.STAR:
                case Token.KEYWORD_LBRA:
                case Token.DOT:
                case Token.DOTDOT:
                case Token.FUNCTION:
                case Token.STRING_LITERAL:
                case Token.NUMBER:
                case Token.HEX_INTEGER:
                case Token.BINARY_INTEGER:
                case Token.LPAR:
                case Token.DOLLAR:
                case Token.PRAGMA:
                case Token.ELEMENT_QNAME:
                case Token.ATTRIBUTE_QNAME:
                case Token.PI_QNAME:
                case Token.NAMESPACE_QNAME:
                case Token.NAMED_FUNCTION_REF:
                case Token.LSQB:
                    return true;
                case Token.KEYWORD_CURLY:
                    return t.currentTokenValue.Equals("ordered") || t.currentTokenValue.Equals("unordered") || t.currentTokenValue.Equals("map") || t.currentTokenValue.Equals("array");
                default:
                    return false;
            }
        }

        protected virtual bool DisallowedAtStartOfRelativePath()
        {
            switch (t.currentToken)
            {
                case Token.CAST_AS:
                case Token.CASTABLE_AS:
                case Token.INSTANCE_OF:
                case Token.TREAT_AS:
                    return true;
                default:
                    return false;
            }
        }

        protected virtual Expression ParsePathExpression()
        {
            int offset = t.currentTokenStartOffset;
            switch (t.currentToken)
            {
                case Token.SLASH:
                    NextToken();
                    RootExpression start = new RootExpression();
                    SetLocation(start);
                    if (DisallowedAtStartOfRelativePath())
                    {
                        Grumble("Operator '" + Token.tokens[t.currentToken] + "' is not allowed after '/'");
                    }

                    if (AtStartOfRelativePath())
                    {
                        Expression path = ParseRemainingPath(start);
                        SetLocation(path, offset);
                        return path;
                    }
                    else
                    {
                        return start;
                    }

                case Token.SLASH_SLASH:
                    NextToken();
                    RootExpression start2 = new RootExpression();
                    SetLocation(start2, offset);
                    AxisExpression axisExp = new AxisExpression(AxisInfo.DESCENDANT_OR_SELF, null);
                    SetLocation(axisExp, offset);
                    Expression slashExp = ExpressionTool.MakePathExpression(start2, axisExp);
                    SetLocation(slashExp, offset);
                    Expression exp = ParseRemainingPath(slashExp);
                    SetLocation(exp, offset);
                    return exp;
                default:
                    if (t.currentToken == Token.NAME && (t.currentTokenValue.Equals("true") || t.currentTokenValue.Equals("false")))
                    {
                        Warning("The expression is looking for a child element named '" + t.currentTokenValue + "' - perhaps " + t.currentTokenValue + "() was intended? To avoid this warning, use child::" + t.currentTokenValue + " or ./" + t.currentTokenValue + ".", DAXonErrorCode.SXWN9040);
                    }

                    if (t.currentToken == Token.NAME && t.GetBinaryOp(t.currentTokenValue) != Token.UNKNOWN && language != ParsedLanguage.XSLT_PATTERN && (offset > 0 || t.PeekAhead() != Token.EOF))
                    {
                        string s = t.currentTokenValue;
                        Warning("The keyword '" + s + "' in this context means 'child::" + s + "'. If this was intended, use 'child::" + s + "' or './" + s + "' to avoid this warning.", DAXonErrorCode.SXWN9040);
                    }

                    return ParseRelativePath();
            }
        }

        protected virtual Expression ParseSimpleMappingExpression()
        {
            int offset = t.currentTokenStartOffset;
            Expression exp = ParsePathExpression();
            int chain = 0;
            while (t.currentToken == Token.BANG)
            {
                CheckIterativeDepth(++chain);
                if (!allowXPath30Syntax)
                {
                    Grumble("XPath '!' operator requires XPath 3.0 to be enabled");
                }

                NextToken();
                Expression next = ParsePathExpression();
                exp = new ForEach(exp, next);
                SetLocation(exp, offset);
            }

            return exp;
        }

        protected virtual Expression ParseRelativePath()
        {
            int offset = t.currentTokenStartOffset;
            Expression exp = ParseStepExpression(language == ParsedLanguage.XSLT_PATTERN);
            int chain = 0;
            while (t.currentToken == Token.SLASH || t.currentToken == Token.SLASH_SLASH)
            {
                CheckIterativeDepth(++chain);
                int op = t.currentToken;
                NextToken();
                Expression next = ParseStepExpression(false);
                if (op == Token.SLASH)
                {

                    //return new RawSlashExpression(start, step);
                    exp = new HomogeneityChecker(new SlashExpression(exp, next));
                } /* (op == Token.SLASH_SLASH)*/
                else
                {

                    // add implicit descendant-or-self.node() step
                    AxisExpression ae = new AxisExpression(AxisInfo.DESCENDANT_OR_SELF, null);
                    SetLocation(ae, offset);
                    Expression one = ExpressionTool.MakePathExpression(exp, ae);
                    SetLocation(one, offset);
                    exp = ExpressionTool.MakePathExpression(one, next);
                    exp = new HomogeneityChecker(exp);
                }

                SetLocation(exp, offset);
            }

            return exp;
        }

        protected virtual Expression ParseRemainingPath(Expression start)
        {
            int offset = t.currentTokenStartOffset;
            Expression exp = start;
            int op = Token.SLASH;
            int chain = 0;
            while (true)
            {
                CheckIterativeDepth(++chain);
                Expression next = ParseStepExpression(false);
                if (op == Token.SLASH)
                {

                    //return new RawSlashExpression(start, step);
                    exp = new HomogeneityChecker(new SlashExpression(exp, next));
                }
                else if (op == Token.SLASH_SLASH)
                {

                    // add implicit descendant-or-self.node() step
                    AxisExpression descOrSelf = new AxisExpression(AxisInfo.DESCENDANT_OR_SELF, null);
                    SetLocation(descOrSelf);
                    Expression step = ExpressionTool.MakePathExpression(descOrSelf, next);
                    SetLocation(step);
                    exp = ExpressionTool.MakePathExpression(exp, step);
                    exp = new HomogeneityChecker(exp);
                } /*if (op == Token.BANG)*/
                else
                {
                    if (!allowXPath30Syntax)
                    {
                        Grumble("XPath '!' operator requires XPath 3.0 to be enabled");
                    }

                    exp = new ForEach(exp, next);
                }

                SetLocation(exp, offset);
                op = t.currentToken;
                if (op != Token.SLASH && op != Token.SLASH_SLASH && op != Token.BANG)
                {
                    break;
                }

                NextToken();
            }

            return exp;
        }

        protected virtual Expression ParseStepExpression(bool firstInPattern)
        {
            Expression step = ParseBasicStep(firstInPattern);

            // When the filter is applied to an Axis step, the nodes are considered in
            // axis order. In all other cases they are considered in document order
            bool reverse = (step is AxisExpression) && !AxisInfo.isForwards[((AxisExpression)step).Axis];
            int chain = 0;
            while (true)
            {
                CheckIterativeDepth(++chain);
                if (t.currentToken == Token.LSQB)
                {
                    step = ParsePredicate(step);
                }
                else if (t.currentToken == Token.LPAR)
                {

                    // dynamic function call (XQuery 3.0/XPath 3.0 syntax)
                    step = ParseDynamicFunctionCall(step, null);
                    SetLocation(step);
                }
                else if (t.currentToken == Token.QMARK)
                {
                    step = ParseLookup(step);
                    SetLocation(step);
                }
                else
                {
                    break;
                }
            }

            if (reverse)
            {

                // An AxisExpression such as preceding-sibling.x delivers nodes in axis
                // order, so that positional predicate like preceding-sibling.x[1] work
                // correctly. To satisfy the XPath semantics we turn preceding-sibling.x
                // into reverse(preceding-sibling.x), and preceding-sibling.x[3] into
                // reverse(preceding-sibling.x[3]). The call on reverse() will be eliminated
                // later in the case where the predicate selects a singleton.
                RetainedStaticContext rsc = env.MakeRetainedStaticContext();
                step = SystemFunction.MakeCall("reverse", rsc, step);
            }

            return step;
        }

        protected virtual Expression ParsePredicate(Expression step)
        {
            NextToken();
            Expression predicate = ParsePredicate();
            if (Literal.IsConstantZero(predicate))
            {
                Warning("Positions are numbered from one; the predicate [0] selects nothing", DAXonErrorCode.SXWN9046);
            }

            Expect(Token.RSQB);
            NextToken();
            step = new FilterExpression(step, predicate);
            SetLocation(step);
            return step;
        }

        protected virtual Expression ParseArrowPostfix(Expression lhs)
        {
            CheckLanguageVersion31();
            NextToken();
            int token = GetTokenizer().currentToken;
            if (token == Token.NAME || token == Token.FUNCTION)
            {
                return ParseFunctionCall(lhs);
            }
            else if (token == Token.DOLLAR)
            {
                int offset = t.currentTokenStartOffset;
                StructuredQName varName = ParseVariableName();
                Expression var = ResolveVariableReference(offset, varName);
                Expect(Token.LPAR);
                return ParseDynamicFunctionCall(var, lhs);
            }
            else if (token == Token.LPAR)
            {
                Expression var = ParseParenthesizedExpression();
                Expect(Token.LPAR);
                return ParseDynamicFunctionCall(var, lhs);
            }
            else if (token == Token.KEYWORD_LBRA && t.currentTokenValue.Equals("function"))
            {
                Expression fn = ParseInlineFunction(AnnotationList.EMPTY);
                Expect(Token.LPAR);
                return ParseDynamicFunctionCall(fn, lhs);
            }
            else if (token == Token.KEYWORD_CURLY && (t.currentTokenValue.Equals("function") || t.currentTokenValue.Equals("fn")))
            {
                Expression fn = ParseFocusFunction(AnnotationList.EMPTY);
                Expect(Token.LPAR);
                return ParseDynamicFunctionCall(fn, lhs);
            }
            else
            {
                Grumble("Unexpected " + Token.tokens[token] + " after '=>'");
                return null;
            }
        }

        protected virtual Expression ParseMappingArrowPostfix(Expression lhs)
        {
            CheckLanguageVersion40();
            NextToken();
            ForExpression forExpr = new ForExpression();
            forExpr.Sequence = lhs;
            StructuredQName varName = new StructuredQName("vv", NamespaceUri.SAXON_GENERATED_VARIABLE, "" + lhs.GetHashCode());
            forExpr.SetVariableQName(varName);
            VariableReference varRef = new LocalVariableReference(forExpr);
            int token = GetTokenizer().currentToken;
            Expression rhs;
            if (token == Token.NAME || token == Token.FUNCTION)
            {
                rhs = ParseFunctionCall(varRef);
            }
            else if (token == Token.DOLLAR)
            {
                int offset = t.currentTokenStartOffset;
                StructuredQName variableName = ParseVariableName();
                Expression var = ResolveVariableReference(offset, variableName);
                Expect(Token.LPAR);
                rhs = ParseDynamicFunctionCall(var, varRef);
            }
            else if (token == Token.LPAR)
            {
                Expression var = ParseParenthesizedExpression();
                Expect(Token.LPAR);
                rhs = ParseDynamicFunctionCall(var, varRef);
            }
            else if (token == Token.KEYWORD_LBRA && t.currentTokenValue.Equals("function"))
            {
                Expression fn = ParseInlineFunction(AnnotationList.EMPTY);
                Expect(Token.LPAR);
                rhs = ParseDynamicFunctionCall(fn, varRef);
            }
            else if (token == Token.KEYWORD_CURLY && (t.currentTokenValue.Equals("function") || t.currentTokenValue.Equals("fn")))
            {
                Expression fn = ParseFocusFunction(AnnotationList.EMPTY);
                Expect(Token.LPAR);
                rhs = ParseDynamicFunctionCall(fn, varRef);
            }
            else
            {
                Grumble("Unexpected " + Token.tokens[token] + " after '=!>'");
                return null;
            }

            forExpr.SetAction(rhs);
            return forExpr;
        }

        protected virtual Expression ParsePredicate()
        {
            return ParseExpression();
        }

        protected virtual bool IsReservedInQuery(NamespaceUri uri)
        {
            return NamespaceUri.IsReservedInQuery31(uri);
        }

        protected virtual Expression ParseBasicStep(bool firstInPattern)
        {
            switch (t.currentToken)
            {
                case Token.DOLLAR:
                    int offset = t.currentTokenStartOffset;
                    StructuredQName variableName = ParseVariableName();
                    return ResolveVariableReference(offset, variableName);
                case Token.LPAR:
                    if (allowXPath40Syntax && t.ThereMightBeAnArrowAhead() && (t.PeekAhead() == Token.DOLLAR || t.PeekAhead() == Token.RPAR))
                    {

                        // Possible lambda expression.
                        Tokenizer checkpoint = new Tokenizer();
                        t.CopyTo(checkpoint);
                        IList<StructuredQName> lambdaParams = ParseLambdaParams();
                        if (lambdaParams == null)
                        {

                            // backtrack and resume a normal parse
                            checkpoint.CopyTo(t);
                        }
                        else
                        {

                            IList<UserFunctionParameter> @params = new List<UserFunctionParameter>(lambdaParams.Count);
                            int slotNumber = 0;
                            foreach (StructuredQName paramName in lambdaParams)
                            {
                                UserFunctionParameter arg = new UserFunctionParameter();
                                arg.SetRequiredType(Values.SequenceType.ANY_SEQUENCE);
                                arg.SetVariableQName(paramName);
                                arg.SetSlotNumber(slotNumber++);
                                @params.Add(arg);
                            }

                            return ParseInlineFunctionBody(AnnotationList.EMPTY, @params, Values.SequenceType.ANY_SEQUENCE);
                        }
                    }

                    return ParseParenthesizedExpression();
                case Token.LSQB:
                    return ParseArraySquareConstructor();
                case Token.STRING_LITERAL:
                    return ParseStringLiteral(true);
                case Token.STRING_LITERAL_BACKTICKED:
                    return ParseBackTickedStringLiteral();
                case Token.STRING_CONSTRUCTOR_INITIAL:
                    return ParseStringConstructor();
                case Token.BACKTICK:
                    CheckLanguageVersion40();
                    return ParseStringTemplate();
                case Token.NUMBER:
                    return ParseNumericLiteral(true);
                case Token.HEX_INTEGER:
                    return ParseHexLiteral(true);
                case Token.BINARY_INTEGER:
                    return ParseBinaryLiteral(true);
                case Token.FUNCTION:
                    return ParseFunctionCall(null);
                case Token.QMARK:
                    return ParseLookup(new ContextItemExpression());
                case Token.DOT:
                    NextToken();
                    Expression cie = new ContextItemExpression();
                    SetLocation(cie);
                    return cie;
                case Token.DOTDOT:
                    NextToken();
                    Expression pne = new AxisExpression(AxisInfo.PARENT, null);
                    SetLocation(pne);
                    return pne;
                case Token.PERCENT:
                    {
                        AnnotationList annotations = ParseAnnotationsList();
                        if (t.currentToken == Token.THIN_ARROW)
                        {
                            CheckLanguageVersion40();
                            NextToken();
                            annotations.Check(env.GetConfiguration(), "IF");
                            if (t.currentToken == Token.LPAR)
                            {
                                return ParseInlineFunction(annotations);
                            }
                            else if (t.currentToken == Token.LCURLY)
                            {
                                return ParseFocusFunction(annotations);
                            }
                            else
                            {
                                Grumble("Expected '(' or '{' after '->'");
                            }
                        }

                        if (!(t.currentTokenValue.Equals("function") || t.currentTokenValue.Equals("fn")))
                        {
                            Grumble("Expected 'function' to follow the annotation assertion");
                        }

                        annotations.Check(env.GetConfiguration(), "IF");
                        if (t.currentToken == Token.KEYWORD_CURLY)
                        {
                            return ParseFocusFunction(annotations);
                        }
                        else
                        {
                            return ParseInlineFunction(annotations);
                        }
                    }

                case Token.THIN_ARROW:
                    {
                        CheckLanguageVersion40();
                        NextToken();
                        if (t.currentToken == Token.LPAR)
                        {
                            AnnotationList annotations = AnnotationList.EMPTY;
                            return ParseInlineFunction(annotations);
                        }
                        else if (t.currentToken == Token.LCURLY)
                        {
                            AnnotationList annotations = AnnotationList.EMPTY;
                            return ParseFocusFunction(annotations);
                        }
                        else
                        {
                            Grumble("Expected '(' or '{' after '->'");
                        }

                        break;
                    }

                case Token.KEYWORD_LBRA:
                    if (t.currentTokenValue.Equals("function") || (t.currentTokenValue.Equals("fn")))
                    {
                        AnnotationList annotations = AnnotationList.EMPTY;
                        return ParseInlineFunction(annotations);
                    }

                    goto case Token.NAME;
                case Token.NAME:
                case Token.PREFIX:
                case Token.SUFFIX:
                case Token.STAR:
                    byte defaultAxis = AxisInfo.CHILD;
                    if (t.currentToken == Token.KEYWORD_LBRA && (t.currentTokenValue.Equals("attribute") || t.currentTokenValue.Equals("schema-attribute")))
                    {
                        defaultAxis = AxisInfo.ATTRIBUTE;
                    }
                    else if (t.currentToken == Token.KEYWORD_LBRA && t.currentTokenValue.Equals("namespace-node"))
                    {
                        defaultAxis = AxisInfo.NAMESPACE;
                        TestPermittedAxis(AxisInfo.NAMESPACE, "XQST0134");
                    }
                    else if (firstInPattern && t.currentToken == Token.KEYWORD_LBRA && t.currentTokenValue.Equals("document-node"))
                    {
                        defaultAxis = AxisInfo.SELF;
                    }

                    NodeTest test = ParseNodeTest(Types.Type.ELEMENT);
                    if (test is AnyNodeTest)
                    {

                        // handles patterns of the form match="node()"
                        if (defaultAxis == AxisInfo.CHILD)
                        {
                            test = MultipleNodeKindTest.CHILD_NODE;
                        }
                        else
                        {
                            test = NodeKindTest.ATTRIBUTE;
                        }
                    }

                    AxisExpression ae = new AxisExpression(defaultAxis, test);
                    SetLocation(ae);
                    return ae;
                case Token.AT:
                    NextToken();
                    switch (t.currentToken)
                    {
                        case Token.NAME:
                        case Token.PREFIX:
                        case Token.SUFFIX:
                        case Token.STAR:
                        case Token.KEYWORD_LBRA:
                        case Token.LPAR:
                            AxisExpression ae2 = new AxisExpression(AxisInfo.ATTRIBUTE, ParseNodeTest(Types.Type.ATTRIBUTE));
                            SetLocation(ae2);
                            return ae2;
                        default:
                            Grumble("@ must be followed by a NodeTest");
                            break;
                    }

                    break;
                case Token.AXIS:
                    int axis;
                    try
                    {
                        axis = AxisInfo.GetAxisNumber(t.currentTokenValue);
                    }
                    catch (XPathException err)
                    {
                        Grumble(err.Message);
                        axis = AxisInfo.CHILD; // error recovery
                    }

                    TestPermittedAxis(axis, "XPST0003");
                    short principalNodeType = AxisInfo.principalNodeType[axis];
                    NextToken();
                    switch (t.currentToken)
                    {
                        case Token.NAME:
                        case Token.PREFIX:
                        case Token.SUFFIX:
                        case Token.STAR:
                        case Token.KEYWORD_LBRA:
                        case Token.LPAR:
                            Expression ax = new AxisExpression(axis, ParseNodeTest(principalNodeType));
                            SetLocation(ax);
                            return ax;
                        default:
                            Grumble("Unexpected token " + CurrentTokenDisplay() + " after axis name");
                            break;
                    }

                    break;
                case Token.KEYWORD_CURLY:
                    switch (t.currentTokenValue)
                    {
                        case "map":
                            return ParseMapExpression();
                        case "array":
                            return ParseArrayCurlyConstructor();
                        case "function":
                        case "fn":
                            return ParseFocusFunction(null);
                    }

                    goto case Token.ELEMENT_QNAME;
                case Token.ELEMENT_QNAME:
                case Token.ATTRIBUTE_QNAME:
                case Token.NAMESPACE_QNAME:
                case Token.PI_QNAME:
                case Token.TAG:
                    return ParseConstructor();
                case Token.NAMED_FUNCTION_REF:
                    return ParseNamedFunctionReference();
                default:
                    Grumble("Unexpected token " + CurrentTokenDisplay() + " at start of expression");
                    break;
            }

            return new ErrorExpression();
        }

    }
}
