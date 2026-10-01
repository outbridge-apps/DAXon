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
    // XQueryParser part: typeswitch, switch, validate, and extension (pragma) expression forms.
    public partial class XQueryParser
    {
        protected override Expression ParseTypeswitchExpression()
        {

            // On entry, the "(" has already been read
            int offset = t.currentTokenStartOffset;
            NextToken();
            Expression operand = ParseExpression();
            IList<IList<Values.SequenceType>> types = new List<IList<Values.SequenceType>>(10);
            IList<Expression> actions = new List<Expression>(10);
            Expect(Token.RPAR);
            NextToken();

            // The code generated takes the form:
            //    let $zzz := operand return
            //    else default-action
            //
            // If a variable is declared in a case clause or default clause,
            // then "action-n" takes the form
            //    let $v as type := $zzz return action-n
            // we were generating "let $v as type := $zzz return action-n" but this gives a compile time error if
            // there's a case clause that specifies an impossible type.
            LetExpression outerLet = MakeLetExpression();
            outerLet.SetRequiredType(Values.SequenceType.ANY_SEQUENCE);
            outerLet.SetVariableQName(new StructuredQName("zz", NamespaceUri.SAXON, "zz_typeswitchVar"));
            outerLet.Sequence = operand;
            bool braced = false;
            if (t.currentToken == Token.LCURLY)
            {
                CheckLanguageVersion40();
                braced = true;
                NextToken();
            }

            while (t.currentToken == Token.CASE || IsKeyword("case"))
            {
                IList<Values.SequenceType> typeList;
                Expression action;
                NextToken();
                if (t.currentToken == Token.DOLLAR)
                {
                    NextToken();
                    Expect(Token.NAME);
                    string var = t.currentTokenValue;
                    StructuredQName varQName = MakeStructuredQName(var, NamespaceUri.NULL);
                    NextToken();
                    Expect(Token.AS);
                    NextToken();
                    typeList = ParseSequenceTypeList();
                    action = MakeTracer(ParseTypeswitchReturnClause(varQName, outerLet), varQName);
                    if (action is TraceExpression)
                    {
                        ((TraceExpression)action).SetProperty("type", typeList[0].ToString());
                    }
                }
                else
                {
                    typeList = ParseSequenceTypeList();
                    action = MakeTracer(ParseExprSingle(), null);
                    if (action is TraceExpression)
                    {
                        ((TraceExpression)action).SetProperty("type", typeList[0].ToString());
                    }
                }

                types.Add(typeList);
                actions.Add(action);
            }

            if (types.Count == 0)
            {
                Grumble("At least one case clause is required in a typeswitch");
            }

            Expect(Token.DEFAULT);
            int defaultOffset = t.currentTokenStartOffset;
            NextToken();
            Expression defaultAction;
            if (t.currentToken == Token.DOLLAR)
            {
                NextToken();
                Expect(Token.NAME);
                string var = t.currentTokenValue;
                StructuredQName varQName = MakeStructuredQName(var, NamespaceUri.NULL);
                NextToken();
                Expect(Token.RETURN);
                NextToken();
                defaultAction = MakeTracer(ParseTypeswitchReturnClause(varQName, outerLet), varQName);
            }
            else
            {
                t.TreatCurrentAsOperator();
                Expect(Token.RETURN);
                NextToken();
                defaultAction = MakeTracer(ParseExprSingle(), null);
            }

            Expression lastAction = defaultAction;

            // Note, the ragged "choose" later gets flattened into a single-level choose, saving stack space
            for (int i = types.Count - 1; i >= 0; i--)
            {
                LocalVariableReference var = new LocalVariableReference(outerLet);
                SetLocation(var);
                Expression ioe = new InstanceOfExpression(var, types[i][0]);
                for (int j = 1; j < types[i].Count; j++)
                {
                    ioe = new OrExpression(ioe, new InstanceOfExpression(var.Copy(new RebindingMap()), types[i][j]));
                }

                SetLocation(ioe);
                Expression ife = Choose.MakeConditional(ioe, actions[i], lastAction);
                SetLocation(ife);
                lastAction = ife;
            }

            outerLet.SetAction(lastAction);
            if (braced)
            {
                Expect(Token.RCURLY);
                t.LookAhead();
                NextToken();
            }

            return MakeTracer(outerLet, null);
        }

        /*clause.getRangeVariable()*/
        //
        private IList<Values.SequenceType> ParseSequenceTypeList()
        {
            IList<Values.SequenceType> typeList = new List<Values.SequenceType>();
            while (true)
            {
                Values.SequenceType type = ParseSequenceType();
                typeList.Add(type);
                t.TreatCurrentAsOperator();
                if (t.currentToken == Token.UNION)
                {
                    NextToken();
                }
                else
                {
                    break;
                }
            }

            Expect(Token.RETURN);
            NextToken();
            return typeList;
        }

        /*clause.getRangeVariable()*/
        private Expression ParseTypeswitchReturnClause(StructuredQName varQName, LetExpression outerLet)
        {
            Expression action;

            LetExpression innerLet = MakeLetExpression();
            innerLet.SetRequiredType(Values.SequenceType.ANY_SEQUENCE);
            innerLet.SetVariableQName(varQName);
            innerLet.Sequence = new LocalVariableReference(outerLet);
            DeclareRangeVariable(innerLet);
            action = ParseExprSingle();
            UndeclareRangeVariable();
            innerLet.SetAction(action);
            return innerLet; //        if (Literal.isEmptySequence(action)) {
        }

        /*clause.getRangeVariable()*/
        //
        protected override Expression ParseSwitchExpression()
        {
            Expression operand;
            bool braced = false;
            if (t.currentToken == Token.SWITCH)
            {

                // On entry, the "(" has already been read
                NextToken();
                operand = ParseExpression();
                Expect(Token.RPAR);
                NextToken();
            }
            else if (t.currentToken == Token.KEYWORD_CURLY)
            {
                CheckLanguageVersion40();
                operand = Literal.MakeLiteral(BooleanValue.TRUE);
                braced = true;
                NextToken();
                if (IsKeyword("case"))
                {
                    t.currentToken = Token.CASE;
                }
            }
            else if (t.currentToken == Token.SWITCH_CASE)
            {
                CheckLanguageVersion40();
                operand = Literal.MakeLiteral(BooleanValue.TRUE);
                t.currentToken = Token.CASE;
            }
            else
            {
                throw new InvalidOperationException();
            }

            if (t.currentToken == Token.LCURLY)
            {
                CheckLanguageVersion40();
                braced = true;
                NextToken();
                if (IsKeyword("case"))
                {
                    t.currentToken = Token.CASE;
                }
            }

            IList<Expression> conditions = new List<Expression>(10);
            IList<Expression> actions = new List<Expression>(10);

            // The code generated takes the form:
            //    let $zzz := zero-or-one(atomize(operand)) return
            //    choose
            //      when ($zzz eq t1) then action1
            //      when ($zzz eq t2) then action2
            //      when (true) default-action
            //
            // We rely on the optimizer to convert this to a SwitchExpression in the case where all the case clauses
            // are literal constants.
            LetExpression outerLet = MakeLetExpression();
            outerLet.SetRequiredType(Values.SequenceType.OPTIONAL_ATOMIC);
            outerLet.SetVariableQName(new StructuredQName("zz", NamespaceUri.SAXON, "zz_switchVar"));
            outerLet.Sequence = Atomizer.MakeAtomizer(operand, null);
            do
            {
                IList<Expression> caseExpressions = new List<Expression>(4);
                Expect(Token.CASE);
                do
                {
                    NextToken();
                    Expression c = ParseExprSingle();
                    caseExpressions.Add(c);
                }
                while (t.currentToken == Token.CASE);
                Expect(Token.RETURN);
                NextToken();
                Expression action = ParseExprSingle();
                for (int i = 0; i < caseExpressions.Count; i++)
                {
                    SwitchCaseComparison vc = new SwitchCaseComparison(new LocalVariableReference(outerLet), Token.FEQ, caseExpressions[i], allowXPath40Syntax);
                    if (i == 0)
                    {
                        conditions.Add(vc);
                        actions.Add(action);
                    }
                    else
                    {
                        OrExpression orExpr = new OrExpression(conditions.RemoveAtAndGet(conditions.Count - 1), vc);
                        conditions.Add(orExpr);
                    } //actions.add((i==0 ? action : action.copy()));
                }
            }
            while (t.currentToken == Token.CASE);
            Expect(Token.DEFAULT);
            NextToken();
            Expect(Token.RETURN);
            NextToken();
            Expression defaultExpr = ParseExprSingle();
            conditions.Add(Literal.MakeLiteral(BooleanValue.TRUE));
            actions.Add(defaultExpr);
            Choose choice = new Choose(conditions.ToArray(), actions.ToArray());
            outerLet.SetAction(choice);
            if (braced)
            {
                Expect(Token.RCURLY);
                t.LookAhead();
                NextToken();
            }

            return MakeTracer(outerLet, null);
        }

        /*clause.getRangeVariable()*/
        //
        //
        protected override Expression ParseValidateExpression()
        {
            int offset = t.currentTokenStartOffset;
            int mode = Validation.STRICT;
            bool foundCurly = false;
            ISchemaType requiredType = null;
            // A validate expression in a non-schema-aware processor is XQST0075 (not XQST0009, which is
            // specific to schema import) — per XQuery 3.1 §2.2.5 / err:XQST0075.
            EnsureSchemaAware("validate expression", "XQST0075");
            switch (t.currentToken)
            {
                case Token.VALIDATE_STRICT:
                    mode = Validation.STRICT;
                    NextToken();
                    break;
                case Token.VALIDATE_LAX:
                    mode = Validation.LAX;
                    NextToken();
                    break;
                case Token.VALIDATE_TYPE:

                    //                if (XQUERY10.equals(queryVersion)) {
                    //                    grumble("validate-as-type requires XQuery 3.0");
                    //                }
                    mode = Validation.BY_TYPE;
                    NextToken();
                    Expect(Token.KEYWORD_CURLY);
                    if (!NameChecker.IsQName(StringTool.CodePoints(t.currentTokenValue)))
                    {
                        Grumble("Schema type name expected after 'validate type");
                    }

                    requiredType = env.GetConfiguration().GetSchemaType(MakeStructuredQName(t.currentTokenValue, env.GetDefaultElementNamespace()));
                    if (requiredType == null)
                    {
                        Grumble("Unknown schema type " + t.currentTokenValue, "XQST0104");
                    }

                    foundCurly = true;
                    break;
                case Token.KEYWORD_CURLY:
                    if (t.currentTokenValue.Equals("validate"))
                    {
                        mode = Validation.STRICT;
                    }
                    else
                    {
                        throw new InvalidOperationException("shouldn't be parsing a validate expression");
                    }

                    foundCurly = true;
                    break;
            }

            if (!foundCurly)
            {
                Expect(Token.LCURLY);
            }

            NextToken();
            Expression exp = ParseExpression();
            if (exp is ParentNodeConstructor)
            {
                ((ParentNodeConstructor)exp).SetValidationAction(mode, mode == Validation.BY_TYPE ? requiredType : null);
            }
            else
            {

                // the expression must return a single element or document node. The type-
                // checking machinery can't handle a union type, so we just check that it's
                // a node for now. Because we are reusing XSLT copy-of code, we need
                // an ad-hoc check that the node is of the right kind.
                exp = new CopyOf(exp, true, mode, requiredType, true);
                SetLocation(exp);
                ((CopyOf)exp).SetRequireDocumentOrElement(true);
            }

            Expect(Token.RCURLY);
            t.LookAhead(); // always done manually after an RCURLY
            NextToken();
            return MakeTracer(exp, null);
        }

        /*clause.getRangeVariable()*/
        //
        //
        protected override Expression ParseExtensionExpression()
        {
            ISchemaType requiredType = null;
            string trimmed = Whitespace.Trim(t.currentTokenValue);
            int c = 0;
            int len = trimmed.Length;
            while (c < len && " \t\r\n".IndexOf(trimmed[c]) < 0)
            {
                c++;
            }

            string qname = trimmed.Substring(0, c);
            string pragmaContents = "";
            while (c < len && " \t\r\n".IndexOf(trimmed[c]) >= 0)
            {
                c++;
            }

            if (c < len)
            {
                pragmaContents = trimmed.Substring(c) /*Java substring(begin,END==length()) -> C# to-end overload*/;
            }

            bool validateType = false;
            StructuredQName pragmaName = MakeStructuredQName(qname, NamespaceUri.NULL);
            NamespaceUri uri = pragmaName.GetNamespaceUri();
            string localName = pragmaName.GetLocalPart();
            if (uri.Equals(NamespaceUri.SAXON))
            {
                if (localName == "validate-type")
                {
                    if (!env.GetConfiguration().IsLicensedFeature(Configuration.LicenseFeature.ENTERPRISE_XQUERY))
                    {
                        Warning("Ignoring saxon:validate-type. To use this feature " + "you need the Saxon-EE processor from https://www.saxonica.com/", DAXonErrorCode.SXWN9042);
                    }
                    else
                    {
                        string typeName = Whitespace.Trim(pragmaContents);
                        if (!NameChecker.IsQName(StringTool.CodePoints(typeName)))
                        {
                            Grumble("Schema type name expected in saxon:validate-type pragma: found " + Err.Wrap(typeName));
                        }

                        requiredType = env.GetConfiguration().GetSchemaType(MakeStructuredQName(typeName, env.GetDefaultElementNamespace()));
                        if (requiredType == null)
                        {
                            Grumble("Unknown schema type " + typeName);
                        }

                        validateType = true;
                    }
                }
                else
                {
                    Warning("Ignored pragma " + qname + " (unrecognized Saxon pragma)", DAXonErrorCode.SXWN9042);
                }
            }

            NextToken();
            Expression expr;
            if (t.currentToken == Token.PRAGMA)
            {
                expr = ParseExtensionExpression();
            }
            else
            {
                Expect(Token.LCURLY);
                NextToken();
                if (t.currentToken == Token.RCURLY)
                {
                    t.LookAhead(); // always done manually after an RCURLY
                    NextToken();
                    Grumble("Unrecognized pragma, with no fallback expression", "XQST0079");
                }

                expr = ParseExpression();
                Expect(Token.RCURLY);
                t.LookAhead(); // always done manually after an RCURLY
                NextToken();
            }

            if (validateType)
            {
                if (expr is ParentNodeConstructor)
                {
                    ((ParentNodeConstructor)expr).SetValidationAction(Validation.BY_TYPE, requiredType);
                    return expr;
                }
                else if (expr is AttributeCreator)
                {
                    if (!(requiredType is ISimpleType))
                    {
                        Grumble("The type used for validating an attribute must be a simple type");
                    }


                    ((AttributeCreator)expr).SetSchemaType((ISimpleType)requiredType);
                    ((AttributeCreator)expr).SetValidationAction(Validation.BY_TYPE);
                    return expr;
                }
                else
                {
                    CopyOf copy = new CopyOf(expr, true, Validation.BY_TYPE, requiredType, true);
                    copy.SetLocation(MakeLocation());
                    return copy;
                }
            }
            else
            {
                return expr;
            }
        }

    }
}
