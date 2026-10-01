////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Patterns
{
    /// <summary>
    /// Parser for XSLT patterns. This is created by overriding selected parts of the standard ExpressionParser.
    /// </summary>
    internal sealed class PatternParser : XPathParser
    {
        int inPredicate = 0;
        public PatternParser(IStaticContext env) : base(env)
        {
        }

        public Pattern ParsePattern(string pattern, IStaticContext env)
        {
            this.env = env;
            charChecker = env.GetConfiguration().ValidCharacterChecker;
            SetLanguage(ParsedLanguage.XSLT_PATTERN, env.GetXPathVersion());
            string trimmed = pattern.Trim();
            if (trimmed.StartsWith("(:", StringComparison.Ordinal))
            {

                // Strip off any leading comments so that we can safely detect a SelectionPattern
                t = new Tokenizer();
                t.languageLevel = env.GetXPathVersion();
                t.Tokenize(trimmed, 0, -1);
                int start = t.currentTokenStartOffset;
                trimmed = trimmed.Substring(start);
            }

            allowXPath40Syntax = env.GetConfiguration().GetBooleanProperty(Feature<bool>.ALLOW_SYNTAX_EXTENSIONS) || env.GetXPathVersion() == 40;
            if (IsSelectionPattern(trimmed))
            {
                Expression e = Parse(pattern, 0, Token.EOF, env);
                if (e is Pattern)
                {
                    return (Pattern)e;
                }
                else if (e is ContextItemExpression)
                {
                    return new UniversalPattern();
                }
                else if (e is FilterExpression)
                {
                    Expression predicate = null;
                    while (e is FilterExpression)
                    {
                        Expression filter = ((FilterExpression)e).GetActionExpression();
                        e = ((FilterExpression)e).GetSelectExpression();

                        // Need to consider the possibility of a numeric predicate
                        ItemType filterType = filter.GetItemType();
                        TypeHierarchy th = env.GetConfiguration().GetTypeHierarchy();
                        Affinity rel = th.Relationship(filterType, NumericType.GetInstance());
                        if (rel != Affinity.DISJOINT)
                        {

                            // the predicate may be numeric
                            if (rel == Affinity.SAME_TYPE || rel == Affinity.SUBSUMED_BY)
                            {

                                // the predicate IS numeric: rewrite as N eq 1, since other values don't match
                                filter = new ValueComparison(filter, Token.FEQ, Literal.MakeLiteral(Int64Value.PLUS_ONE));
                            }
                            else
                            {

                                // the predicate MIGHT BE numeric: rewrite as
                                // let $P := predicate return if ($P instance of xs:numeric) then ($P eq 1) else $P
                                LetExpression let = new LetExpression();
                                StructuredQName varName = new StructuredQName("vv", NamespaceUri.SAXON_GENERATED_VARIABLE, "v" + filter.GetHashCode());
                                let.SetVariableQName(varName);
                                InstanceOfExpression condition = new InstanceOfExpression(new LocalVariableReference(let), SequenceType.SINGLE_NUMERIC);
                                LocalVariableReference @ref = new LocalVariableReference(let);
                                @ref.SetStaticType(SequenceType.ANY_SEQUENCE, null, 0);
                                ValueComparison comparison = new ValueComparison(@ref, Token.FEQ, Literal.MakeLiteral(Int64Value.PLUS_ONE));
                                Choose choice = new Choose(new Expression[] { condition, Literal.MakeLiteral(BooleanValue.TRUE) }, new Expression[] { comparison, new LocalVariableReference(let) });
                                let.Sequence = filter;
                                let.SetAction(choice);
                                let.SetRequiredType(SequenceType.ANY_SEQUENCE);
                                let.SetRetainedStaticContext(env.MakeRetainedStaticContext());
                                filter = let;
                            }
                        }

                        if (predicate == null)
                        {
                            predicate = filter;
                        }
                        else
                        {
                            predicate = new AndExpression(filter, predicate);
                        }
                    }

                    if (e is ContextItemExpression)
                    {
                        return new BooleanExpressionPattern(predicate);
                    }
                }

                Grumble("Pattern starting with '.' must be followed by a sequence of predicates");
                return null;
            }
            else if (IsTypePattern(pattern))
            {
                this.env = env;
                if (qNameParser == null)
                {
                    qNameParser = new QNameParser(env.GetNamespaceResolver());
                    if (languageVersion >= 30)
                    {
                        qNameParser = qNameParser.WithAcceptEQName(true);
                    }
                }

                language = ParsedLanguage.XSLT_PATTERN;
                t = new Tokenizer();
                t.languageLevel = env.GetXPathVersion();
                allowXPath40Syntax = t.allowSaxonExtensions = env.GetConfiguration().GetBooleanProperty(Feature<bool>.ALLOW_SYNTAX_EXTENSIONS) || t.languageLevel == 40;
                try
                {
                    t.Tokenize(pattern, 0, -1);
                }
                catch (XPathException err)
                {
                    Grumble(err.Message);
                }

                ItemType req = ParseItemType();
                Pattern result = new ItemTypePattern(req);
                while (t.currentToken == Token.LSQB)
                {
                    NextToken();
                    Expression predicate = ParsePredicate();
                    Expect(Token.RSQB);
                    NextToken();
                    result = new BasePatternWithPredicate(result, predicate);
                }

                Expect(Token.EOF);
                return result;
            }
            else
            {
                Expression exp = Parse(pattern, 0, Token.EOF, env);
                exp.SetRetainedStaticContext(env.MakeRetainedStaticContext());

                // If we have a union pattern, check that neither operand is a PredicatePattern
                if (exp is VennExpression)
                {
                    CheckNoPredicatePattern(((VennExpression)exp).GetLhsExpression());
                    CheckNoPredicatePattern(((VennExpression)exp).GetRhsExpression());
                }

                ExpressionVisitor visitor = ExpressionVisitor.Make(env);
                visitor.SetOptimizeForPatternMatching(true);
                ContextItemStaticInfo cit = visitor.GetConfiguration().MakeContextItemStaticInfo(AnyNodeTest.GetInstance(), true);
                Pattern pat;
                try
                {
                    pat = (Pattern)PatternMaker.FromExpression(exp.Simplify().TypeCheck(visitor, cit), env.GetConfiguration(), true);
                }
                catch (XPathException e)
                {
                    pat = (Pattern)PatternMaker.FromExpression(exp.Simplify(), env.GetConfiguration(), true);
                }

                pat.OriginalText = pattern;
                if (pat is UnionPattern)
                {
                    string[] parts = pattern.SplitRegex("\\|");
                    if (parts.Length == 2)
                    {
                        ((UnionPattern)pat).p1.OriginalText = parts[0];
                        ((UnionPattern)pat).p2.OriginalText = parts[1];
                    }
                }

                if (exp is FilterExpression && ((FilterExpression)exp).Base is ContextItemExpression)
                {
                    if (allowXPath40Syntax && (pattern.StartsWith("record", StringComparison.Ordinal) || pattern.StartsWith("tuple", StringComparison.Ordinal) || pattern.StartsWith("map", StringComparison.Ordinal) || pattern.StartsWith("array", StringComparison.Ordinal) || pattern.StartsWith("union", StringComparison.Ordinal)))
                    {
                    }
                    else
                    {
                        Grumble("A predicatePattern can appear only at the outermost level (parentheses not allowed)");
                    }
                }

                if (exp is FilterExpression && pat is NodeTestPattern)
                {

                    // the pattern has been simplified but needs to retain a default priority based on its syntactic form (test match-058)
                    pat.SetPriority(0.5);
                }

                return pat;
            }
        }

        private bool IsSelectionPattern(string pattern)
        {
            return pattern.StartsWith(".", StringComparison.Ordinal);
        }

        private bool IsTypePattern(string pattern)
        {
            if (pattern.MatchesRegex("^(type|record|map|array|union|atomic)\\s*\\(.+"))
            {
                CheckLanguageVersion40();
                return true;
            }

            return false;
        }

        private void CheckNoPredicatePattern(Expression exp)
        {
            if (exp is ContextItemExpression)
            {
                Grumble("A predicatePattern can appear only at the outermost level (union operator not allowed)");
            }

            if (exp is FilterExpression)
            {
                CheckNoPredicatePattern(((FilterExpression)exp).Base);
            }

            if (exp is VennExpression)
            {
                CheckNoPredicatePattern(((VennExpression)exp).GetLhsExpression());
                CheckNoPredicatePattern(((VennExpression)exp).GetRhsExpression());
            }
        }

        protected override void CustomizeTokenizer(Tokenizer t)
        {
        }

        public override Expression ParseExpression()
        {
            Tokenizer t = GetTokenizer();
            if (inPredicate > 0)
            {
                return base.ParseExpression();
            }
            else if (allowXPath40Syntax && t.currentToken == Token.KEYWORD_LBRA && (t.currentTokenValue.Equals("record") || t.currentTokenValue.Equals("type") || t.currentTokenValue.Equals("map") || t.currentTokenValue.Equals("array")))
            {

                //ItemType type = parserExtension.parseExtendedItemType(this);
                ItemType type = ParseItemType();
                Expression expr = new ItemTypePattern(type);
                expr.SetRetainedStaticContext(env.MakeRetainedStaticContext());

                //            Expression expr = new InstanceOfExpression(
                //            expr = new FilterExpression(new ContextItemExpression(), expr);
                SetLocation(expr);
                while (t.currentToken == Token.LSQB)
                {
                    expr = ParsePredicate(expr).ToPattern(env.GetConfiguration());
                }

                return expr;
            }
            else if (allowXPath40Syntax && t.currentToken == Token.KEYWORD_LBRA && (t.currentTokenValue.Equals("atomic")))
            {
                NextToken();
                Expect(Token.NAME);
                StructuredQName typeName = MakeStructuredQName(t.currentTokenValue, env.GetDefaultElementNamespace());
                NextToken();
                Expect(Token.RPAR);
                NextToken();
                ISchemaType type = env.GetConfiguration().GetSchemaType(typeName);
                if (type == null || !type.IsAtomicType())
                {
                    Grumble("Unknown atomic type " + typeName);
                }

                IAtomicType at = (IAtomicType)type;
                Expression expr = new ItemTypePattern(at);

                //            Expression expr = new InstanceOfExpression(
                //            expr = new FilterExpression(new ContextItemExpression(), expr);
                SetLocation(expr);
                while (t.currentToken == Token.LSQB)
                {
                    expr = ParsePredicate(expr);
                }

                return expr;
            }
            else
            {
                return ParseBinaryExpression(ParsePathExpression(), 10);
            }
        }

        protected override Expression ParseBasicStep(bool firstInPattern)
        {
            if (inPredicate > 0)
            {
                return base.ParseBasicStep(firstInPattern);
            }
            else
            {
                switch (t.currentToken)
                {
                    case Token.DOLLAR:
                        if (!firstInPattern)
                        {
                            Grumble("In an XSLT 3.0 pattern, a variable reference is allowed only as the first step in a path");
                            return null;
                        }
                        else
                        {
                            return base.ParseBasicStep(firstInPattern);
                        }

                    case Token.STRING_LITERAL:
                    case Token.NUMBER:
                    case Token.HEX_INTEGER:
                    case Token.BINARY_INTEGER:
                    case Token.KEYWORD_CURLY:
                    case Token.ELEMENT_QNAME:
                    case Token.ATTRIBUTE_QNAME:
                    case Token.NAMESPACE_QNAME:
                    case Token.PI_QNAME:
                    case Token.TAG:
                    case Token.NAMED_FUNCTION_REF:
                    case Token.DOTDOT:
                        Grumble("Token " + CurrentTokenDisplay() + " not allowed here in an XSLT pattern");
                        return null;
                    case Token.FUNCTION:
                        if (!firstInPattern)
                        {
                            Grumble("In an XSLT pattern, a function call is allowed only as the first step in a path");
                        }

                        return base.ParseBasicStep(firstInPattern);
                    case Token.KEYWORD_LBRA:
                        switch (t.currentTokenValue)
                        {
                            case "type":
                            case "tuple":
                            case "union":
                            case "map":
                            case "array":
                            case "atomic":
                                return parserExtension.ParseTypePattern(this);
                            default:
                                return base.ParseBasicStep(firstInPattern);
                        }

                    default:
                        return base.ParseBasicStep(firstInPattern);
                }
            }
        }

        protected override void TestPermittedAxis(int axis, string errorCode)
        {
            base.TestPermittedAxis(axis, errorCode);
            if (inPredicate == 0)
            {
                if (!AxisInfo.isSubtreeAxis[axis])
                {
                    Grumble("The " + AxisInfo.axisName[axis] + " is not allowed in a pattern");
                }
            }
        }

        protected override Expression ParsePredicate()
        {
            bool disallow = t.disallowUnionKeyword;
            t.disallowUnionKeyword = false;
            ++inPredicate;
            Expression exp = ParseExpression();
            --inPredicate;
            t.disallowUnionKeyword = disallow;
            return exp;
        }

        public override Expression ParseFunctionCall(Expression prefixArgument)
        {
            Expression fn = base.ParseFunctionCall(prefixArgument);
            if (inPredicate <= 0 && !fn.IsCallOn(typeof(SuperId)) && !fn.IsCallOn(typeof(KeyFn)) && !fn.IsCallOn(typeof(Doc)) && !fn.IsCallOn(typeof(Root_1)))
            {
                Grumble("The " + fn + " function is not allowed at the head of a pattern");
            }

            return fn;
        }

        public override Expression ParseFunctionArgument()
        {
            if (inPredicate > 0)
            {
                return base.ParseFunctionArgument();
            }
            else
            {
                switch (t.currentToken)
                {
                    case Token.DOLLAR:
                        int offset = t.currentTokenStartOffset;
                        StructuredQName variableName = ParseVariableName();
                        return ResolveVariableReference(offset, variableName);
                    case Token.STRING_LITERAL:
                        return ParseStringLiteral(true);
                    case Token.NUMBER:
                        return ParseNumericLiteral(true);
                    default:
                        Grumble("A function argument in an XSLT pattern must be a variable reference or literal");
                        return null;
                }
            }
        }

        public override Expression MakeTracer(Expression exp, StructuredQName qName)
        {

            // Suppress tracing of pattern evaluation
            return exp;
        }
    }
}