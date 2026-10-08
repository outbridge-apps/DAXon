////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Functions.Registry;
using OutSmart.DAXon.Values.Arrays;
using System;
using System.Collections.Generic;
using System.Linq;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Expressions.Parsing
{
    // XPathParser part: expression grammar core — operator precedence and binary/ternary forms,
    // otherwise, the XQuery-only virtual hooks, and for/let/quantified/if.
    public partial class XPathParser
    {
        //                     EXPRESSIONS                                              //
        public virtual Expression ParseExpression()
        {
            int offset = t.currentTokenStartOffset;
            Expression exp = ParseExprSingle();
            List<Expression> list = null;
            while (t.currentToken == Token.COMMA)
            {

                // An expression containing a comma often contains many, so we accumulate all the
                // subexpressions into a list before creating the Block expression which reduces it to an array
                if (list == null)
                {
                    list = new List<Expression>(10);
                    list.Add(exp);
                }

                NextToken();
                Expression next = ParseExprSingle();
                SetLocation(next);
                list.Add(next);
            }

            if (list != null)
            {
                exp = Block.MakeBlock(list);
                SetLocation(exp, offset);
            }

            return exp;
        }

        public virtual Expression ParseExprSingle()
        {
            // Every nested sub-expression (function arguments, predicates, parenthesized/array/map
            // constructors) funnels through here, so one depth guard covers both XPath and XQuery.
            if (++expressionDepth > MAX_EXPRESSION_NESTING)
            {
                expressionDepth--;
                Grumble("Expression is too deeply nested (exceeds the limit of " + MAX_EXPRESSION_NESTING + ")", "XPST0003");
            }

            // The compile's deadline, once per sub-expression: the parser is the one compile phase every text passes.
            Controller.CheckActiveTimeout();

            // The counter above is the Java-parity ceiling; on a thread whose stack cannot hold even
            // MAX_EXPRESSION_NESTING recursion levels (a default 1 MB thread dies near ~2300), the
            // stack-adaptive probe raises the same XPST0003 before the uncatchable SOE.
            try
            {
                StackGuard.Probe();
            }
            catch (RecursionDepthError e) when (!e.Described)
            {
                expressionDepth--;

                // Described, not Grumbled: XPath is also parsed at evaluation time (xsl:evaluate,
                // a dynamic regex, fn:transform), where the engine stack sits above this parser and
                // an XPathException would have to unwind through all of it. The counter path above
                // still Grumbles - that one is a static limit, not an exhausted stack.
                throw e.Describe("Expression is too deeply nested (insufficient stack on this thread)", "XPST0003", null);
            }

            try
            {
                return ParseExprSingleImpl();
            }
            finally
            {
                expressionDepth--;
            }
        }

        private Expression ParseExprSingleImpl()
        {
            Expression e = parserExtension.ParseExtendedExprSingle(this);
            if (e != null)
            {
                return e;
            }


            // Short-circuit for a single-token expression
            int peek = t.PeekAhead();
            if (peek == Token.EOF || peek == Token.COMMA || peek == Token.RPAR || peek == Token.RSQB)
            {
                switch (t.currentToken)
                {
                    case Token.STRING_LITERAL:
                        return ParseStringLiteral(true);
                    case Token.NUMBER:
                        return ParseNumericLiteral(true);
                    case Token.HEX_INTEGER:
                        return ParseHexLiteral(true);
                    case Token.BINARY_INTEGER:
                        return ParseBinaryLiteral(true);
                    case Token.NAME:
                    case Token.PREFIX:
                    case Token.SUFFIX:
                    case Token.STAR:
                        return ParseBasicStep(true);
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
                    case Token.EOF:
                    default:
                        break;
                }
            }

            switch (t.currentToken)
            {
                case Token.EOF:
                    Grumble("Expected an expression, but reached the end of the input");
                    return null;
                case Token.FOR:
                case Token.LET:
                case Token.FOR_MEMBER:
                case Token.FOR_SLIDING:
                case Token.FOR_TUMBLING:
                    return ParseFLWORExpression();
                case Token.SOME:
                case Token.EVERY:
                    return ParseQuantifiedExpression();
                case Token.IF:
                    return ParseIfExpression();
                case Token.SWITCH:
                    return ParseSwitchExpression();
                case Token.SWITCH_CASE:
                    return ParseSwitchExpression();
                case Token.TYPESWITCH:
                    return ParseTypeswitchExpression();
                case Token.KEYWORD_CURLY:
                    if (t.currentTokenValue.Equals("try"))
                    {
                        return ParseTryCatchExpression();
                    }


                    // else drop through
                    goto default;
                default:
                    Expression e1 = ParseBinaryExpression(ParseUnaryExpression(), 4);

                    // Process ternary conditional
                    if (t.currentToken == Token.QMARK_QMARK)
                    {
                        if (!allowXPath40Syntax)
                        {
                            Grumble("Ternary conditionals (A ?? B !! C) require XPath 4.0 to be enabled (also, note this syntax will be withdrawn)");
                        }

                        return ParseTernaryExpression(e1);
                    }

                    return e1;
            }
        }

        /// <summary>
        /// Parse a ternary conditional expression
        /// </summary>
        private Expression ParseTernaryExpression(Expression condition)
        {
            NextToken();
            Expression e2 = ParseExprSingle();
            Expect(Token.BANG_BANG);
            NextToken();
            Expression e3 = ParseExprSingle();
            return Choose.MakeConditional(condition, e2, e3);
        }

        public virtual Expression ParseBinaryExpression(Expression lhs, int minPrecedence)
        {
            int chain = 0;
            while (CurrentOperatorPrecedence >= minPrecedence)
            {
                CheckIterativeDepth(++chain);
                int offset = t.currentTokenStartOffset;
                int @operator = t.currentToken;
                int prec = CurrentOperatorPrecedence;
                switch (@operator)
                {
                    case Token.INSTANCE_OF:
                    case Token.TREAT_AS:
                        NextToken();
                        Values.SequenceType seq = ParseSequenceType();
                        lhs = MakeSequenceTypeExpression(lhs, @operator, seq);
                        SetLocation(lhs, offset);
                        if (CurrentOperatorPrecedence >= prec)
                        {
                            Grumble("Left operand of '" + Token.tokens[t.currentToken] + "' needs parentheses");
                        }

                        break;
                    case Token.CAST_AS:
                    case Token.CASTABLE_AS:
                        NextToken();
                        ICastingTarget at;
                        if (allowXPath40Syntax && t.currentToken == Token.KEYWORD_LBRA && t.currentTokenValue.Equals("union"))
                        {

                            // Saxon 9.8 / XPath 4.0 proposed extension
                            at = (ICastingTarget)ParseItemType();
                        }
                        else
                        {
                            Expect(Token.NAME);
                            if (scanOnly)
                            {
                                at = BuiltInAtomicType.STRING;
                            }
                            else
                            {
                                StructuredQName sq = null;
                                try
                                {
                                    sq = qNameParser.Parse(t.currentTokenValue, env.GetDefaultElementNamespace());
                                }
                                catch (XPathException e)
                                {
                                    Grumble(e.Message, e.ErrorCodeQName);
                                }

                                Types.ItemType alias = env.ResolveTypeAlias(sq);
                                if (alias != null)
                                {
                                    if (alias is ICastingTarget)
                                    {
                                        at = (ICastingTarget)alias;
                                    }
                                    else
                                    {
                                        Grumble("The type " + t.currentTokenValue + " cannot be used as the target of a cast");
                                        at = null;
                                    }
                                }
                                else
                                {
                                    at = GetSimpleType(t.currentTokenValue);
                                }
                            }

                            NextToken();
                        }

                        if (at == BuiltInAtomicType.ANY_ATOMIC)
                        {
                            Grumble("No value is castable to xs:anyAtomicType", "XPST0080");
                        }

                        if (at == BuiltInAtomicType.NOTATION)
                        {
                            Grumble("No value is castable to xs:NOTATION", "XPST0080");
                        }

                        bool allowEmpty = t.currentToken == Token.QMARK;
                        if (allowEmpty)
                        {
                            NextToken();
                        }

                        lhs = MakeSingleTypeExpression(lhs, @operator, at, allowEmpty);
                        SetLocation(lhs, offset);
                        if (CurrentOperatorPrecedence >= prec)
                        {
                            Grumble("Left operand of '" + Token.tokens[t.currentToken] + "' needs parentheses");
                        }

                        break;
                    case Token.FAT_ARROW:
                        lhs = ParseArrowPostfix(lhs);
                        break;
                    case Token.MAPPING_ARROW:
                        CheckLanguageVersion40();
                        lhs = ParseMappingArrowPostfix(lhs);
                        break;
                    default:
                        NextToken();
                        Expression rhs = ParseUnaryExpression();
                        while (CurrentOperatorPrecedence > prec)
                        {
                            rhs = ParseBinaryExpression(rhs, CurrentOperatorPrecedence);
                        }

                        if (CurrentOperatorPrecedence == prec && !AllowMultipleOperators())
                        {
                            string tok = Token.tokens[t.currentToken];
                            string message = "Left operand of '" + Token.tokens[t.currentToken] + "' needs parentheses";
                            if (tok.Equals("<") || tok.Equals(">"))
                            {

                                // Example input: return <a>3</a><b>4</b> - bug 2659
                                message += ". Or perhaps an XQuery element constructor appears where it is not allowed";
                            }

                            Grumble(message);
                        }

                        lhs = MakeBinaryExpression(lhs, @operator, rhs);
                        SetLocation(lhs, offset);
                        break;
                }
            }

            return lhs;
        }

        private bool AllowMultipleOperators()
        {
            switch (t.currentToken)
            {
                case Token.FEQ:
                case Token.FNE:
                case Token.FLE:
                case Token.FLT:
                case Token.FGE:
                case Token.FGT:
                case Token.EQUALS:
                case Token.NE:
                case Token.LE:
                case Token.LT:
                case Token.GE:
                case Token.GT:
                case Token.IS:
                case Token.PRECEDES:
                case Token.FOLLOWS:
                case Token.TO:
                    return false;
                default:
                    return true;
            }
        }

        public static int OperatorPrecedence(int @operator)
        {
            return operatorPrecedenceTable[@operator];
        }

        private Expression MakeBinaryExpression(Expression lhs, int @operator, Expression rhs)
        {
            switch (@operator)
            {
                case Token.OR:
                    return new OrExpression(lhs, rhs);
                case Token.AND:
                    return new AndExpression(lhs, rhs);
                case Token.FEQ:
                case Token.FNE:
                case Token.FLE:
                case Token.FLT:
                case Token.FGE:
                case Token.FGT:
                    return new ValueComparison(lhs, @operator, rhs);
                case Token.EQUALS:
                case Token.NE:
                case Token.LE:
                case Token.LT:
                case Token.GE:
                case Token.GT:
                    return env.GetConfiguration().GetTypeChecker(env.IsInBackwardsCompatibleMode()).MakeGeneralComparison(lhs, @operator, rhs);
                case Token.IS:
                case Token.PRECEDES:
                case Token.FOLLOWS:
                    return new IdentityComparison(lhs, @operator, rhs);
                case Token.TO:
                    return new RangeExpression(lhs, rhs);
                case Token.CONCAT:
                    {
                        if (!allowXPath30Syntax)
                        {
                            Grumble("Concatenation operator ('||') requires XPath 3.0 to be enabled");
                        }

                        RetainedStaticContext rsc = new RetainedStaticContext(env);
                        Configuration config = env.GetConfiguration();
                        BuiltInFunctionSet lib = config.GetXPathFunctionSet(env.GetXPathVersion());
                        if (lhs.IsCallOn(typeof(Concat)))
                        {
                            Expression[] args = ((SystemFunctionCall)lhs).Arguments;
                            Expression[] newArgs = new Expression[args.Length + 1];
                            Array.Copy(args, 0, newArgs, 0, args.Length);
                            newArgs[args.Length] = rhs;
                            SystemFunction concat = lib.MakeFunction("concat", newArgs.Length);
                            concat.SetRetainedStaticContext(rsc);
                            return concat.MakeFunctionCall(newArgs);
                        }
                        else
                        {
                            SystemFunction concat = lib.MakeFunction("concat", 2);
                            concat.SetRetainedStaticContext(rsc);
                            Expression[] args = new Expression[]
                            {
                            lhs,
                            rhs
                            };
                            return concat.MakeFunctionCall(args);
                        }
                    }

                case Token.PLUS:
                case Token.MINUS:
                case Token.MULT:
                case Token.DIV:
                case Token.IDIV:
                case Token.MOD:
                    return env.GetConfiguration().GetTypeChecker(env.IsInBackwardsCompatibleMode()).MakeArithmeticExpression(lhs, @operator, rhs);
                case Token.MATH_MULT:
                    return env.GetConfiguration().GetTypeChecker(env.IsInBackwardsCompatibleMode()).MakeArithmeticExpression(lhs, Token.MULT, rhs);
                case Token.MATH_DIVIDE:
                    return env.GetConfiguration().GetTypeChecker(env.IsInBackwardsCompatibleMode()).MakeArithmeticExpression(lhs, Token.DIV, rhs);
                case Token.OTHERWISE:
                    return MakeOtherwiseExpression(lhs, rhs);
                case Token.UNION:
                case Token.INTERSECT:
                case Token.EXCEPT:
                    return new VennExpression(lhs, @operator, rhs);
                case Token.OR_ELSE:
                    {

                        // Compile ($x orElse $y) as (if ($x) then true() else boolean($y))
                        RetainedStaticContext rsc = new RetainedStaticContext(env);
                        rhs = SystemFunction.MakeCall("boolean", rsc, rhs);
                        return Choose.MakeConditional(lhs, Literal.MakeLiteral(BooleanValue.TRUE), rhs);
                    }

                case Token.AND_ALSO:
                    {

                        // Compile ($x andAlso $y) as (if ($x) then boolean($y) else false())
                        RetainedStaticContext rsc = new RetainedStaticContext(env);
                        rhs = SystemFunction.MakeCall("boolean", rsc, rhs);
                        return Choose.MakeConditional(lhs, rhs, Literal.MakeLiteral(BooleanValue.FALSE));
                    }

                default:
                    throw new ArgumentException(Token.tokens[@operator]);
            }
        }

        private Expression MakeOtherwiseExpression(Expression lhs, Expression rhs)
        {
            CheckLanguageVersion40();
            LetExpression let = new LetExpression();
            let.SetVariableQName(new StructuredQName("vv", NamespaceUri.ANONYMOUS, "n" + lhs.GetHashCode()));
            let.Sequence = lhs;
            let.SetRequiredType(Values.SequenceType.ANY_SEQUENCE);
            LocalVariableReference v1 = new LocalVariableReference(let.GetVariableQName());
            v1.SetBinding(let);
            let.AddReference(v1, false);
            LocalVariableReference v2 = new LocalVariableReference(let.GetVariableQName());
            v2.SetBinding(let);
            let.AddReference(v2, false);
            RetainedStaticContext rsc = new RetainedStaticContext(env);
            Expression[] conditions = new Expression[]
            {
                SystemFunction.MakeCall("exists", rsc, v1),
                Literal.MakeLiteral(BooleanValue.TRUE, lhs)
            };
            Expression[] actions = new Expression[]
            {
                v2,
                rhs
            };
            let.SetAction(new Choose(conditions, actions));
            return let;
        }

        private Expression MakeSequenceTypeExpression(Expression lhs, int @operator, Values.SequenceType type)
        {
            switch (@operator)
            {
                case Token.INSTANCE_OF:
                    return new InstanceOfExpression(lhs, type);
                case Token.TREAT_AS:
                    return TreatExpression.Make(lhs, type);
                default:
                    throw new ArgumentException();
            }
        }

        private Expression MakeSingleTypeExpression(Expression lhs, int @operator, ICastingTarget type, bool allowEmpty)
        {
            if (type is IAtomicType && !(type == ErrorType.GetInstance()))
            {
                switch (@operator)
                {
                    case Token.CASTABLE_AS:
                        CastableExpression castable = new CastableExpression(lhs, (IAtomicType)type, allowEmpty);
                        if (lhs is StringLiteral)
                        {
                            castable.SetOperandIsStringLiteral(true);
                        }

                        return castable;
                    case Token.CAST_AS:
                        CastExpression cast = new CastExpression(lhs, (IAtomicType)type, allowEmpty);
                        if (lhs is StringLiteral)
                        {
                            cast.SetOperandIsStringLiteral(true);
                        }

                        return cast;
                    default:
                        throw new ArgumentException();
                }
            }
            else if (allowXPath30Syntax)
            {
                switch (@operator)
                {
                    case Token.CASTABLE_AS:
                        if (type is IUnionType)
                        {
                            INamespaceResolver resolver = env.GetNamespaceResolver();
                            UnionCastableFunction ucf = new UnionCastableFunction((IUnionType)type, resolver, allowEmpty);
                            return new StaticFunctionCall(ucf, new Expression[] { lhs });
                        }
                        else if (type is IListType)
                        {
                            INamespaceResolver resolver = env.GetNamespaceResolver();
                            ListCastableFunction lcf = new ListCastableFunction((IListType)type, resolver, allowEmpty);
                            return new StaticFunctionCall(lcf, new Expression[] { lhs });
                        }

                        break;
                    case Token.CAST_AS:
                        if (type is IUnionType)
                        {
                            INamespaceResolver resolver = env.GetNamespaceResolver();
                            UnionConstructorFunction ucf = new UnionConstructorFunction((IUnionType)type, resolver, allowEmpty);
                            return new StaticFunctionCall(ucf, new Expression[] { lhs });
                        }
                        else if (type is IListType)
                        {
                            INamespaceResolver resolver = env.GetNamespaceResolver();
                            ListConstructorFunction lcf = new ListConstructorFunction((IListType)type, resolver, allowEmpty);
                            return new StaticFunctionCall(lcf, new Expression[] { lhs });
                        }

                        break;
                    default:
                        throw new ArgumentException();
                }


                //                throw new XPathException("Cannot cast to xs:anySimpleType", "XPST0080");
                //            } else {
                throw new XPathException("Cannot cast to " + type.GetType(), "XPST0051"); //            }
            }
            else
            {
                throw new XPathException("Casting to list or union types requires XPath 3.0 to be enabled", "XPST0051");
            }
        }

        protected virtual Expression ParseTypeswitchExpression()
        {
            Grumble("typeswitch is not allowed in XPath");
            return new ErrorExpression();
        }

        protected virtual Expression ParseSwitchExpression()
        {
            Grumble("switch is not allowed in XPath");
            return new ErrorExpression();
        }

        protected virtual Expression ParseValidateExpression()
        {
            Grumble("validate{} expressions are not allowed in XPath");
            return new ErrorExpression();
        }

        protected virtual Expression ParseExtensionExpression()
        {
            Grumble("extension expressions (#...#) are not allowed in XPath");
            return new ErrorExpression();
        }

        protected virtual Expression ParseTryCatchExpression()
        {
            Grumble("try/catch expressions are not allowed in XPath");
            return new ErrorExpression();
        }

        protected virtual Expression ParseFLWORExpression()
        {
            if (t.currentToken == Token.LET && !allowXPath30Syntax)
            {
                Grumble("'let' is not permitted in XPath 2.0");
            }

            if (t.currentToken == Token.FOR_SLIDING || t.currentToken == Token.FOR_TUMBLING)
            {
                Grumble("sliding/tumbling windows can only be used in XQuery");
            }

            if (t.currentToken == Token.FOR_MEMBER && !allowXPath40Syntax)
            {
                Grumble("'for member' requires XPath 4.0 to be enabled");
            }

            if (t.currentToken == Token.LET)
            {
                return ParseLetExpression();
            }
            else
            {
                return ParseForExpression();
            }
        }

        private Expression ParseForExpression()
        {
            int clauses = 0;
            int offset;
            int @operator = t.currentToken;
            Assignation first = null;
            Assignation previous = null;
            do
            {
                bool forMember = false;
                offset = t.currentTokenStartOffset;
                NextToken();
                if (IsKeyword("member") && clauses > 0)
                {
                    forMember = true;
                    NextToken();
                }

                Expect(Token.DOLLAR);
                NextToken();
                Expect(Token.NAME);
                string var = t.currentTokenValue;

                // declare the range variable
                Assignation firstClause;
                Assignation lastClause;
                if (@operator == Token.FOR)
                {
                    firstClause = lastClause = new ForExpression();
                    firstClause.SetRequiredType(Values.SequenceType.SINGLE_ITEM);
                }
                else if (@operator == Token.FOR_MEMBER)
                {

                    // "for member $m in $array" compiles to "for $temp in array:members($array) let $m := $temp?value"
                    firstClause = new ForExpression();
                    firstClause.SetRequiredType(Values.SequenceType.SINGLE_ITEM);
                    firstClause.SetVariableQName(new StructuredQName("vv", NamespaceUri.SAXON_GENERATED_VARIABLE, "fm" + firstClause.GetHashCode()));
                    DeclareRangeVariable(firstClause);
                    lastClause = new LetExpression();
                    lastClause.SetRequiredType(Values.SequenceType.ANY_SEQUENCE);
                    LocalVariableReference tempRef = new LocalVariableReference(firstClause);
                    LookupExpression lookup = new LookupExpression(tempRef, new StringLiteral("value"));
                    lastClause.Sequence = lookup;
                    firstClause.SetAction(lastClause);
                    forMember = true;
                    clauses++;
                } /*if (@operator == Token.LET)*/
                else
                {
                    firstClause = lastClause = new LetExpression();
                    firstClause.SetRequiredType(Values.SequenceType.ANY_SEQUENCE);
                }

                clauses++;
                SetLocation(firstClause, offset);
                SetLocation(lastClause, offset);
                lastClause.SetVariableQName(MakeStructuredQName(var, NamespaceUri.NULL));
                NextToken();

                // process the "in" or ":=" clause
                Expect(@operator == Token.LET ? Token.ASSIGN : Token.IN);
                NextToken();
                Expression collection = ParseExprSingle();
                if (forMember)
                {
                    collection = ArrayFunctionSet.GetInstance(40).MakeFunction("members", 1).MakeFunctionCall(collection);
                }

                firstClause.Sequence = collection;
                DeclareRangeVariable(lastClause);
                if (previous == null)
                {
                    first = firstClause;
                }
                else
                {
                    previous.SetAction(firstClause);
                }

                previous = lastClause;
            }
            while (t.currentToken == Token.COMMA || (allowXPath40Syntax && t.currentToken == @operator));

            // process the "return" expression (called the "action")
            Expect(Token.RETURN);
            NextToken();
            previous.SetAction(ParseExprSingle());

            // undeclare all the range variables
            for (int i = 0; i < clauses; i++)
            {
                UndeclareRangeVariable();
            }

            return MakeTracer(first, first.GetVariableQName());
        }

        private Expression ParseLetExpression()
        {
            int clauses = 0;
            int offset;
            Assignation first = null;
            Assignation previous = null;
            do
            {
                offset = t.currentTokenStartOffset;
                NextToken();
                Expect(Token.DOLLAR);
                NextToken();
                Expect(Token.NAME);
                string var = t.currentTokenValue;

                // declare the range variable
                Assignation firstClause;
                Assignation lastClause;
                firstClause = lastClause = new LetExpression();
                firstClause.SetRequiredType(Values.SequenceType.ANY_SEQUENCE);
                clauses++;
                SetLocation(firstClause, offset);
                SetLocation(lastClause, offset);
                lastClause.SetVariableQName(MakeStructuredQName(var, NamespaceUri.NULL));
                NextToken();

                // process the  ":=" clause
                Expect(Token.ASSIGN);
                NextToken();
                Expression collection = ParseExprSingle();
                firstClause.Sequence = collection;
                DeclareRangeVariable(lastClause);
                if (previous == null)
                {
                    first = firstClause;
                }
                else
                {
                    previous.SetAction(firstClause);
                }

                previous = lastClause;
            }
            while (t.currentToken == Token.COMMA || (allowXPath40Syntax && t.currentToken == Token.LET));

            // process the "return" expression (called the "action")
            Expect(Token.RETURN);
            NextToken();
            previous.SetAction(ParseExprSingle());

            // undeclare all the range variables
            for (int i = 0; i < clauses; i++)
            {
                UndeclareRangeVariable();
            }

            return MakeTracer(first, first.GetVariableQName());
        }

        private Expression ParseQuantifiedExpression()
        {
            int clauses = 0;
            int @operator = t.currentToken;
            QuantifiedExpression first = null;
            QuantifiedExpression previous = null;
            do
            {
                int offset = t.currentTokenStartOffset;
                NextToken();
                Expect(Token.DOLLAR);
                NextToken();
                Expect(Token.NAME);
                string var = t.currentTokenValue;
                clauses++;

                // declare the range variable
                QuantifiedExpression v = new QuantifiedExpression();
                v.SetRequiredType(Values.SequenceType.SINGLE_ITEM);
                v.Operator = @operator;
                SetLocation(v, offset);
                v.SetVariableQName(MakeStructuredQName(var, NamespaceUri.NULL));
                NextToken();
                if (t.currentToken == Token.AS && language == ParsedLanguage.XQUERY)
                {

                    // We use this path for quantified expressions in XQuery, which permit an "as" clause
                    NextToken();
                    Values.SequenceType type = ParseSequenceType();
                    if (type.GetCardinality() != StaticProperty.EXACTLY_ONE)
                    {
                        Warning("Occurrence indicator on singleton range variable has no effect", DAXonErrorCode.SXWN9039);
                        type = Values.SequenceType.MakeSequenceType(type.PrimaryType, StaticProperty.EXACTLY_ONE);
                    }

                    v.SetRequiredType(type);
                }


                // process the "in" clause
                Expect(Token.IN);
                NextToken();
                v.Sequence = ParseExprSingle();
                DeclareRangeVariable(v);
                if (previous != null)
                {
                    previous.SetAction(v);
                }
                else
                {
                    first = v;
                }

                previous = v;
            }
            while (t.currentToken == Token.COMMA);

            // process the "return/satisfies" expression (called the "action")
            Expect(Token.SATISFIES);
            NextToken();
            previous.SetAction(ParseExprSingle());

            // undeclare all the range variables
            for (int i = 0; i < clauses; i++)
            {
                UndeclareRangeVariable();
            }

            return MakeTracer(first, first.GetVariableQName());
        }

        private Expression ParseIfExpression()
        {

            // left paren already read
            int ifoffset = t.currentTokenStartOffset;
            NextToken();
            Expression condition = ParseExpression();
            Expect(Token.RPAR);
            NextToken();
            int thenoffset = t.currentTokenStartOffset;
            if (t.currentToken == Token.LCURLY)
            {
                CheckLanguageVersion40();
                return ParseBracedActions(condition);
            }

            Expect(Token.THEN);
            NextToken();
            Expression thenExp = MakeTracer(ParseExprSingle(), null);
            SetLocation(thenExp, thenoffset);
            int elseoffset = t.currentTokenStartOffset;
            Expect(Token.ELSE);
            NextToken();
            Expression elseExp = MakeTracer(ParseExprSingle(), null);
            SetLocation(elseExp, elseoffset);
            Expression ifExp = Choose.MakeConditional(condition, thenExp, elseExp);
            SetLocation(ifExp, ifoffset);
            return MakeTracer(ifExp, null);
        }

        private Expression ParseBracedActions(Expression condition)
        {
            IList<Expression> conditions = new List<Expression>();
            IList<Expression> actions = new List<Expression>();
            conditions.Add(condition);
            NextToken();
            Expression thenExp = ParseExpression();
            actions.Add(thenExp);
            Expect(Token.RCURLY);
            t.LookAhead();
            NextToken();
            while (t.currentToken == Token.ELSE)
            {
                NextToken();
                if (t.currentToken == Token.IF)
                {

                    //                nextToken();
                    //                expect(Token.LPAR);
                    NextToken();
                    condition = ParseExpression();
                    Expect(Token.RPAR);
                    NextToken();
                    Expect(Token.LCURLY);
                    NextToken();
                    thenExp = ParseExpression();
                    Expect(Token.RCURLY);
                    t.LookAhead();
                    NextToken();
                    conditions.Add(condition);
                    actions.Add(thenExp);
                }
                else
                {
                    Expect(Token.LCURLY);
                    NextToken();
                    Expression elseExp = ParseExpression();
                    Expect(Token.RCURLY);
                    t.LookAhead();
                    NextToken();
                    conditions.Add(Literal.MakeLiteral(BooleanValue.TRUE));
                    actions.Add(elseExp);
                    break;
                }
            }

            Choose result = new Choose(conditions.ToArray(), actions.ToArray());
            SetLocation(result);
            return result;
        }

    }
}
