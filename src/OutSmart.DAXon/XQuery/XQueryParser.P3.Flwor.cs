////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Values.Arrays;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Internal.Net;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Expressions.Flwor;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.XQuery
{
    // XQueryParser part: FLWOR — for/let/count/trace/group-by/window clauses and order-by.
    public partial class XQueryParser
    {
        protected override Expression ParseFLWORExpression()
        {
            FLWORExpression flwor = new FLWORExpression();
            int exprOffset = t.currentTokenStartOffset;
            IList<Clause> clauseList = new List<Clause>(4);
            while (true)
            {
                int offset = t.currentTokenStartOffset;
                if (t.currentToken == Token.FOR || t.currentToken == Token.FOR_MEMBER)
                {
                    ParseForClause(flwor, clauseList);
                }
                else if (t.currentToken == Token.LET)
                {
                    ParseLetClause(flwor, clauseList);
                }
                else if (t.currentToken == Token.COUNT)
                {
                    ParseCountClause(clauseList);
                }
                else if (t.currentToken == Token.GROUP_BY)
                {
                    ParseGroupByClause(flwor, clauseList);
                }
                else if (t.currentToken == Token.FOR_TUMBLING || t.currentToken == Token.FOR_SLIDING)
                {
                    ParseWindowClause(flwor, clauseList);
                }
                else if (t.currentToken == Token.WHERE || IsKeyword("where"))
                {
                    NextToken();
                    Expression condition = ParseExprSingle();
                    WhereClause clause = new WhereClause(flwor, condition);
                    SetLocation(clause, t.currentTokenStartOffset);
                    clause.SetRepeated(ContainsLoopingClause(clauseList));
                    clauseList.Add(clause); //            } else if (t.currentToken == Token.WHILE || isKeyword("while")) {
                }
                else if (IsKeyword("trace"))
                {
                    ParseTraceClause(flwor, clauseList);
                }
                else if (IsKeyword("stable") || IsKeyword("order"))
                {

                    // we read the "stable" keyword but ignore it; Saxon ordering is always stable
                    if (IsKeyword("stable"))
                    {
                        NextToken();
                        if (!IsKeyword("order"))
                        {
                            Grumble("'stable' must be followed by 'order by'");
                        }
                    }

                    TupleExpression tupleExpression = new TupleExpression();
                    IList<LocalVariableReference> vars = new List<LocalVariableReference>();
                    foreach (Clause c in clauseList)
                    {
                        foreach (LocalVariableBinding b in c.RangeVariables)
                        {
                            vars.Add(new LocalVariableReference(b));
                        }
                    }

                    tupleExpression.SetVariables(vars);
                    IList<SortSpec> sortSpecList;
                    t.State = Tokenizer.BARE_NAME_STATE;
                    NextToken();
                    if (!IsKeyword("by"))
                    {
                        Grumble("'order' must be followed by 'by'");
                    }

                    t.State = Tokenizer.DEFAULT_STATE;
                    NextToken();
                    sortSpecList = ParseSortDefinition();
                    SortKeyDefinition[] keys = new SortKeyDefinition[sortSpecList.Count];
                    for (int i = 0; i < keys.Length; i++)
                    {
                        SortSpec spec = sortSpecList[i];
                        SortKeyDefinition key = new SortKeyDefinition();
                        key.SetSortKey(sortSpecList[i].sortKey, false);
                        string str = spec.ascending ? "ascending" : "descending";
                        key.Order = new StringLiteral(BMPString.Of(str));
                        key.EmptyLeast = spec.emptyLeast;
                        if (spec.collation != null)
                        {
                            IStringCollator comparator = env.GetConfiguration().GetCollation(spec.collation);
                            if (comparator == null)
                            {
                                Grumble("Unknown collation '" + spec.collation + '\'', "XQST0076");
                            }

                            key.Collation = comparator;
                        }

                        keys[i] = key;
                    }

                    OrderByClause clause = new OrderByClause(flwor, keys, tupleExpression);
                    clause.SetRepeated(ContainsLoopingClause(clauseList));
                    clauseList.Add(clause);
                }
                else
                {
                    break;
                }

                SetLocation(clauseList[clauseList.Count - 1], offset);
            }

            int returnOffset = t.currentTokenStartOffset;
            Expect(Token.RETURN);
            t.State = Tokenizer.DEFAULT_STATE;
            NextToken();
            Expression returnExpression = ParseExprSingle();
            returnExpression = MakeTracer(returnExpression, null);

            // undeclare all the range variables
            for (int i = clauseList.Count - 1; i >= 0; i--)
            {
                Clause clause = clauseList[i];
                for (int n = 0; n < clause.RangeVariables.Length; n++)
                {
                    UndeclareRangeVariable();
                }
            }


            flwor.Init(clauseList, returnExpression);
            SetLocation(flwor, exprOffset);
            return flwor;
        }

        protected virtual LetExpression MakeLetExpression()
        {
            if (((QueryModule)env).UserQueryContext.IsCompileWithTracing())
            {
                return new EagerLetExpression();
            }
            else
            {
                return new LetExpression();
            }
        }

        protected static bool ContainsLoopingClause(IList<Clause> clauseList)
        {
            foreach (Clause c in clauseList)
            {
                if (FLWORExpression.IsLoopingClause(c))
                {
                    return true;
                }
            }

            return false;
        }

        private void ParseForClause(FLWORExpression flwor, IList<Clause> clauseList)
        {
            bool first = true;
            bool forMember = t.currentToken == Token.FOR_MEMBER;

            // "for member $x as T in $array"
            // compiles to
            // "for $temp in array:members($array) let $x as T := $temp?value"
            do
            {
                NextToken();
                if (!first)
                {
                    if (IsKeyword("member"))
                    {
                        forMember = true;
                        NextToken();
                    }
                    else
                    {
                        forMember = false;
                    }
                }

                if (forMember && !allowXPath40Syntax)
                {
                    Grumble("The 'for member' syntax requires XQuery 4.0 to be enabled");
                }

                int offset = t.currentTokenStartOffset;
                ForClause clause = new ForClause();
                clause.SetRepeated(!first || ContainsLoopingClause(clauseList));
                if (first)
                {
                    first = false;
                }

                SetLocation(clause, offset);
                clauseList.Add(clause);
                Expect(Token.DOLLAR);
                NextToken();
                Expect(Token.NAME);
                StructuredQName explicitQName = MakeStructuredQName(t.currentTokenValue, NamespaceUri.NULL);
                StructuredQName iterationQName = explicitQName;
                if (forMember)
                {
                    iterationQName = new StructuredQName("vv", NamespaceUri.SAXON_GENERATED_VARIABLE, "fm" + clause.GetHashCode());
                }

                Values.SequenceType type = forMember ? Values.SequenceType.ANY_SEQUENCE : Values.SequenceType.SINGLE_ITEM;
                NextToken();
                bool explicitType = false;
                if (t.currentToken == Token.AS)
                {
                    explicitType = true;
                    NextToken();
                    type = ParseSequenceType();
                }

                bool allowingEmpty = false;
                if (IsKeyword("allowing"))
                {
                    if (forMember)
                    {
                        Grumble("'allowing empty' cannot appear in a 'for member' clause");
                    }

                    allowingEmpty = true;
                    clause.SetAllowingEmpty(true);
                    if (!explicitType)
                    {
                        type = forMember ? Values.SequenceType.ANY_SEQUENCE : Values.SequenceType.OPTIONAL_ITEM;
                    }

                    NextToken();
                    if (!IsKeyword("empty"))
                    {
                        Grumble("After 'allowing', expected 'empty'");
                    }

                    NextToken();
                }

                if (explicitType && !allowingEmpty && !forMember && type.GetCardinality() != StaticProperty.EXACTLY_ONE)
                {
                    Warning("Occurrence indicator on singleton range variable has no effect", DAXonErrorCode.SXWN9039);
                    type = Values.SequenceType.MakeSequenceType(type.PrimaryType, StaticProperty.EXACTLY_ONE);
                }

                LocalVariableBinding binding = new LocalVariableBinding(iterationQName, forMember ? Values.SequenceType.ANY_SEQUENCE : type);
                clause.RangeVariable = binding;
                if (IsKeyword("at"))
                {
                    NextToken();
                    Expect(Token.DOLLAR);
                    NextToken();
                    Expect(Token.NAME);
                    StructuredQName posQName = MakeStructuredQName(t.currentTokenValue, NamespaceUri.NULL);
                    if (!scanOnly && posQName.Equals(explicitQName))
                    {
                        Grumble("The two variables declared in a single 'for' clause must have different names", "XQST0089");
                    }

                    LocalVariableBinding pos = new LocalVariableBinding(posQName, Values.SequenceType.SINGLE_INTEGER);
                    clause.PositionVariable = pos;
                    NextToken();
                }

                Expect(Token.IN);
                NextToken();
                Expression collection = ParseExprSingle();
                if (forMember)
                {
                    collection = ArrayFunctionSet.GetInstance(40).MakeFunction("members", 1).MakeFunctionCall(collection);
                }

                clause.InitSequence(flwor, collection);
                DeclareRangeVariable(binding);
                if (clause.PositionVariable != null)
                {
                    DeclareRangeVariable(clause.PositionVariable);
                }

                if (allowingEmpty)
                {
                    CheckForClauseAllowingEmpty(flwor, clause);
                }

                if (forMember)
                {

                    // Generate "let $x as T := $temp?value"
                    LetClause letClause = new LetClause();
                    LocalVariableBinding letBinding = new LocalVariableBinding(explicitQName, type);
                    letClause.RangeVariable = letBinding;
                    LocalVariableReference tempRef = new LocalVariableReference(clause.RangeVariable);
                    LookupExpression lookup = new LookupExpression(tempRef, new StringLiteral("value"));
                    letClause.InitSequence(flwor, lookup);
                    DeclareRangeVariable(letBinding);
                    clauseList.Add(letClause);
                }
            }
            while (t.currentToken == Token.COMMA);
        }

        /*clause.getRangeVariable()*/
        private void CheckForClauseAllowingEmpty(FLWORExpression flwor, ForClause clause)
        {
            if (!allowXPath30Syntax)
            {
                Grumble("The 'allowing empty' option requires XQuery 3.0");
            }

            Values.SequenceType type = clause.RangeVariable.GetRequiredType();
            if (!Cardinality.AllowsZero(type.GetCardinality()))
            {
                Warning("When 'allowing empty' is specified, the occurrence indicator on the range variable type should be '?'", DAXonErrorCode.SXWN9039);
            }
        }

        /*clause.getRangeVariable()*/
        private void ParseLetClause(FLWORExpression flwor, IList<Clause> clauseList)
        {
            bool first = true;
            do
            {
                LetClause clause = new LetClause();
                SetLocation(clause, t.currentTokenStartOffset);
                clause.SetRepeated(ContainsLoopingClause(clauseList));
                if (first)
                {
                }

                clauseList.Add(clause);
                NextToken();
                if (first)
                {
                    first = false;
                }
                else
                {
                }

                Expect(Token.DOLLAR);
                NextToken();
                Expect(Token.NAME);
                string var = t.currentTokenValue;
                StructuredQName varQName = MakeStructuredQName(var, NamespaceUri.NULL);
                Values.SequenceType type = Values.SequenceType.ANY_SEQUENCE;
                NextToken();
                if (t.currentToken == Token.AS)
                {
                    NextToken();
                    type = ParseSequenceType();
                }

                LocalVariableBinding v = new LocalVariableBinding(varQName, type);
                Expect(Token.ASSIGN);
                NextToken();
                clause.InitSequence(flwor, ParseExprSingle());
                clause.RangeVariable = v;
                DeclareRangeVariable(v);
            }
            while (t.currentToken == Token.COMMA);
        }

        /*clause.getRangeVariable()*/
        private void ParseCountClause(IList<Clause> clauseList)
        {
            CountClause clause = new CountClause();
            SetLocation(clause, t.currentTokenStartOffset);
            clause.SetRepeated(ContainsLoopingClause(clauseList));
            clauseList.Add(clause);
            NextToken();
            Expect(Token.DOLLAR);
            NextToken();
            Expect(Token.NAME);
            string var = t.currentTokenValue;
            StructuredQName varQName = MakeStructuredQName(var, NamespaceUri.NULL);
            Values.SequenceType type = Values.SequenceType.ANY_SEQUENCE;
            NextToken();
            LocalVariableBinding v = new LocalVariableBinding(varQName, type);
            clause.RangeVariable = v;
            DeclareRangeVariable(v);
        }

        /*clause.getRangeVariable()*/
        private void ParseTraceClause(FLWORExpression flwor, IList<Clause> clauseList)
        {
            DiagnosticClause clause = new DiagnosticClause();
            SetLocation(clause, t.currentTokenStartOffset);
            clause.SetRepeated(ContainsLoopingClause(clauseList));
            clauseList.Add(clause);
            NextToken();
            clause.InitSequence(flwor, ParseExpression());
        }

        /*clause.getRangeVariable()*/
        private void ParseGroupByClause(FLWORExpression flwor, IList<Clause> clauseList)
        {
            GroupByClause clause = new GroupByClause(env.GetConfiguration());
            SetLocation(clause, t.currentTokenStartOffset);
            clause.SetRepeated(ContainsLoopingClause(clauseList));
            IList<StructuredQName> variableNames = new List<StructuredQName>();
            IList<string> collations = new List<string>();
            NextToken();
            while (true)
            {
                Values.SequenceType type = Values.SequenceType.ANY_SEQUENCE;
                StructuredQName varQName = ReadVariableName();
                if (t.currentToken == Token.AS)
                {
                    NextToken();
                    type = ParseSequenceType();
                    if (t.currentToken != Token.ASSIGN)
                    {
                        Grumble("In group by, if the type is declared then it must be followed by ':= value'");
                    }
                }

                if (t.currentToken == Token.ASSIGN)
                {
                    LetClause letClause = new LetClause();
                    SetLocation(clause, t.currentTokenStartOffset);
                    clauseList.Add(letClause);
                    NextToken();
                    LocalVariableBinding v = new LocalVariableBinding(varQName, type);
                    Expression value = ParseExprSingle();
                    Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.MISC, "grouping key", 0);
                    Expression atomizedValue = Atomizer.MakeAtomizer(value, role);
                    letClause.InitSequence(flwor, atomizedValue);
                    letClause.RangeVariable = v;
                    DeclareRangeVariable(v);
                }

                variableNames.Add(varQName);
                if (IsKeyword("collation"))
                {
                    NextToken();
                    Expect(Token.STRING_LITERAL);
                    collations.Add(t.currentTokenValue);
                    NextToken();
                }
                else
                {
                    collations.Add(env.GetDefaultCollationName());
                }

                if (t.currentToken == Token.COMMA)
                {
                    NextToken();
                }
                else
                {
                    break;
                }
            }


            // Each of the variable names acts both as a variable reference (for a variable in the pre-grouping stream)
            // and a variable declaration (for a variable in the post-grouping stream).
            TupleExpression groupingTupleExpr = new TupleExpression();
            TupleExpression retainedTupleExpr = new TupleExpression();
            IList<LocalVariableReference> groupingRefs = new List<LocalVariableReference>();
            IList<LocalVariableReference> retainedRefs = new List<LocalVariableReference>();
            IList<LocalVariableBinding> groupedBindings = new List<LocalVariableBinding>();
            foreach (StructuredQName q in variableNames)
            {
                bool found = LocateDeclaration(clauseList, groupingRefs, groupedBindings, q);
                if (!found)
                {
                    Grumble("The grouping variable " + q.DisplayName + " must be the name of a variable bound earlier in the FLWOR expression", "XQST0094");
                }
            }

            groupingTupleExpr.SetVariables(groupingRefs);
            clause.InitGroupingTupleExpression(flwor, groupingTupleExpr);
            IList<LocalVariableBinding> ungroupedBindings = new List<LocalVariableBinding>();
            for (int i = clauseList.Count - 1; i >= 0; i--)
            {
                foreach (LocalVariableBinding b in clauseList[i].RangeVariables)
                {
                    if (!groupedBindings.Contains(b))
                    {
                        ungroupedBindings.Add(b);
                        retainedRefs.Add(new LocalVariableReference(b));
                    }
                }
            }

            retainedTupleExpr.SetVariables(retainedRefs);
            clause.InitRetainedTupleExpression(flwor, retainedTupleExpr);
            LocalVariableBinding[] bindings = new LocalVariableBinding[groupedBindings.Count + ungroupedBindings.Count];
            int k = 0;
            foreach (LocalVariableBinding b in groupedBindings)
            {
                bindings[k] = new LocalVariableBinding(b.GetVariableQName(), b.GetRequiredType());

                k++;
            }

            foreach (LocalVariableBinding b in ungroupedBindings)
            {
                Types.ItemType itemType = b.GetRequiredType().PrimaryType;
                bindings[k] = new LocalVariableBinding(b.GetVariableQName(), Values.SequenceType.MakeSequenceType(itemType, StaticProperty.ALLOWS_ZERO_OR_MORE));

                k++;
            }

            for (int z = groupedBindings.Count; z < bindings.Length; z++)
            {
                DeclareRangeVariable(bindings[z]);
            }

            for (int z = 0; z < groupedBindings.Count; z++)
            {
                DeclareRangeVariable(bindings[z]);
            }

            clause.SetVariableBindings(bindings);
            GenericAtomicComparer[] comparers = new GenericAtomicComparer[collations.Count];
            IXPathContext context = env.MakeEarlyEvaluationContext();
            for (int i = 0; i < comparers.Length; i++)
            {
                IStringCollator coll = env.GetConfiguration().GetCollation(collations[i]);
                comparers[i] = (GenericAtomicComparer)GenericAtomicComparer.MakeAtomicComparer(BuiltInAtomicType.ANY_ATOMIC, BuiltInAtomicType.ANY_ATOMIC, coll, context);
            }

            clause.SetComparers(comparers);
            clauseList.Add(clause);
        }

        /*clause.getRangeVariable()*/
        private bool LocateDeclaration(IList<Clause> clauseList, IList<LocalVariableReference> groupingRefs, IList<LocalVariableBinding> groupedBindings, StructuredQName q)
        {
            for (int i = clauseList.Count - 1; i >= 0; i--)
            {
                foreach (LocalVariableBinding b in clauseList[i].RangeVariables)
                {
                    if (q.Equals(b.GetVariableQName()))
                    {
                        groupedBindings.Add(b);
                        groupingRefs.Add(new LocalVariableReference(b));
                        return true;
                    }
                }
            }

            return false;
        }

        /*clause.getRangeVariable()*/
        private StructuredQName ReadVariableName()
        {
            Expect(Token.DOLLAR);
            NextToken();
            Expect(Token.NAME);
            string name = t.currentTokenValue;
            NextToken();
            return MakeStructuredQName(name, NamespaceUri.NULL);
        }

        /*clause.getRangeVariable()*/
        private void ParseWindowClause(FLWORExpression flwor, IList<Clause> clauseList)
        {
            WindowClause clause = new WindowClause();
            SetLocation(clause, t.currentTokenStartOffset);
            clause.SetRepeated(ContainsLoopingClause(clauseList));
            clause.SetIsSlidingWindow(t.currentToken == Token.FOR_SLIDING);
            NextToken();
            if (!IsKeyword("window"))
            {
                Grumble("after 'sliding' or 'tumbling', expected 'window', but found " + CurrentTokenDisplay());
            }

            NextToken();
            StructuredQName windowVarName = ReadVariableName();
            Values.SequenceType windowType = Values.SequenceType.ANY_SEQUENCE;
            if (t.currentToken == Token.AS)
            {
                NextToken();
                windowType = ParseSequenceType();
            }

            LocalVariableBinding windowVar = new LocalVariableBinding(windowVarName, windowType);
            clause.SetVariableBinding(WindowClause.WINDOW_VAR, windowVar);

            // We can't assume that all the items in the input sequence belong to the item type of the windows: test case SlidingWindowExpr507
            Values.SequenceType windowItemTypeMandatory = Values.SequenceType.SINGLE_ITEM;
            Values.SequenceType windowItemTypeOptional = Values.SequenceType.OPTIONAL_ITEM;
            Expect(Token.IN);
            NextToken();
            clause.InitSequence(flwor, ParseExprSingle());
            if (IsKeyword("start"))
            {
                t.State = Tokenizer.BARE_NAME_STATE;
                NextToken();
                if (t.currentToken == Token.DOLLAR)
                {
                    LocalVariableBinding startItemVar = new LocalVariableBinding(ReadVariableName(), windowItemTypeMandatory);
                    clause.SetVariableBinding(WindowClause.START_ITEM, startItemVar);
                    DeclareRangeVariable(startItemVar);
                }

                if (IsKeyword("at"))
                {
                    NextToken();
                    LocalVariableBinding startPositionVar = new LocalVariableBinding(ReadVariableName(), Values.SequenceType.SINGLE_INTEGER);
                    clause.SetVariableBinding(WindowClause.START_ITEM_POSITION, startPositionVar);
                    DeclareRangeVariable(startPositionVar);
                }

                if (IsKeyword("previous"))
                {
                    NextToken();
                    LocalVariableBinding startPreviousItemVar = new LocalVariableBinding(ReadVariableName(), windowItemTypeOptional);
                    clause.SetVariableBinding(WindowClause.START_PREVIOUS_ITEM, startPreviousItemVar);
                    DeclareRangeVariable(startPreviousItemVar);
                }

                if (IsKeyword("next"))
                {
                    NextToken();
                    LocalVariableBinding startNextItemVar = new LocalVariableBinding(ReadVariableName(), windowItemTypeOptional);
                    clause.SetVariableBinding(WindowClause.START_NEXT_ITEM, startNextItemVar);
                    DeclareRangeVariable(startNextItemVar);
                }

                if (IsKeyword("when"))
                {
                    t.State = Tokenizer.DEFAULT_STATE;
                    NextToken();
                    clause.InitStartCondition(flwor, ParseExprSingle());
                }
                else if (allowXPath40Syntax)
                {
                    clause.InitStartCondition(flwor, Literal.MakeLiteral(BooleanValue.TRUE, flwor));
                }
                else
                {
                    Grumble("Expected 'when' condition for window start, but found " + CurrentTokenDisplay());
                }
            }
            else if (allowXPath40Syntax)
            {
                clause.InitStartCondition(flwor, Literal.MakeLiteral(BooleanValue.TRUE, flwor));
            }
            else
            {
                Grumble("in window clause, expected 'start', but found " + CurrentTokenDisplay());
            }

            if (IsKeyword("only"))
            {
                clause.SetIncludeUnclosedWindows(false);
                NextToken();
            }

            if (IsKeyword("end"))
            {
                t.State = Tokenizer.BARE_NAME_STATE;
                NextToken();
                if (t.currentToken == Token.DOLLAR)
                {
                    LocalVariableBinding endItemVar = new LocalVariableBinding(ReadVariableName(), windowItemTypeMandatory);
                    clause.SetVariableBinding(WindowClause.END_ITEM, endItemVar);
                    DeclareRangeVariable(endItemVar);
                }

                if (IsKeyword("at"))
                {
                    NextToken();
                    LocalVariableBinding endPositionVar = new LocalVariableBinding(ReadVariableName(), Values.SequenceType.SINGLE_INTEGER);
                    clause.SetVariableBinding(WindowClause.END_ITEM_POSITION, endPositionVar);
                    DeclareRangeVariable(endPositionVar);
                }

                if (IsKeyword("previous"))
                {
                    NextToken();
                    LocalVariableBinding endPreviousItemVar = new LocalVariableBinding(ReadVariableName(), windowItemTypeOptional);
                    clause.SetVariableBinding(WindowClause.END_PREVIOUS_ITEM, endPreviousItemVar);
                    DeclareRangeVariable(endPreviousItemVar);
                }

                if (IsKeyword("next"))
                {
                    NextToken();
                    LocalVariableBinding endNextItemVar = new LocalVariableBinding(ReadVariableName(), windowItemTypeOptional);
                    clause.SetVariableBinding(WindowClause.END_NEXT_ITEM, endNextItemVar);
                    DeclareRangeVariable(endNextItemVar);
                }

                if (IsKeyword("when"))
                {
                    t.State = Tokenizer.DEFAULT_STATE;
                    NextToken();
                    clause.InitEndCondition(flwor, ParseExprSingle());
                }
                else if (allowXPath40Syntax)
                {
                    clause.InitEndCondition(flwor, Literal.MakeLiteral(BooleanValue.TRUE, flwor));
                }
                else
                {
                    Grumble("Expected 'when' condition for window end, but found " + CurrentTokenDisplay());
                }
            }
            else
            {

                // no "end" condition found
                if (clause.IsSlidingWindow())
                {
                    Grumble("A sliding window requires an end condition");
                }
            }

            DeclareRangeVariable(windowVar);
            clauseList.Add(clause);
        }

        /*clause.getRangeVariable()*/
        public static Expression MakeStringJoin(Expression exp, IStaticContext env)
        {
            exp = Atomizer.MakeAtomizer(exp, null);
            Types.ItemType t = exp.GetItemType();
            if (!t.Equals(BuiltInAtomicType.STRING) && !t.Equals(BuiltInAtomicType.UNTYPED_ATOMIC))
            {
                exp = new AtomicSequenceConverter(exp, BuiltInAtomicType.STRING);
                ((AtomicSequenceConverter)exp).AllocateConverterStatically(env.GetConfiguration(), false);
            }

            if (exp.GetCardinality() == StaticProperty.EXACTLY_ONE)
            {
                return exp;
            }
            else
            {
                RetainedStaticContext rsc = new RetainedStaticContext(env);
                Expression fn = SystemFunction.MakeCall("string-join", rsc, exp, new StringLiteral(StringValue.SINGLE_SPACE));
                ExpressionTool.CopyLocationInfo(exp, fn);
                return fn;
            }
        }

        /*clause.getRangeVariable()*/
        private IList<SortSpec> ParseSortDefinition()
        {
            IList<SortSpec> sortSpecList = new List<SortSpec>(5);
            while (true)
            {
                SortSpec sortSpec = new SortSpec();
                sortSpec.sortKey = ParseExprSingle();
                sortSpec.ascending = true;
                sortSpec.emptyLeast = ((QueryModule)env).IsEmptyLeast();
                sortSpec.collation = env.GetDefaultCollationName();

                if (IsKeyword("ascending"))
                {
                    NextToken();
                }
                else if (IsKeyword("descending"))
                {
                    sortSpec.ascending = false;
                    NextToken();
                }

                if (IsKeyword("empty"))
                {
                    NextToken();
                    if (IsKeyword("greatest"))
                    {
                        sortSpec.emptyLeast = false;
                        NextToken();
                    }
                    else if (IsKeyword("least"))
                    {
                        sortSpec.emptyLeast = true;
                        NextToken();
                    }
                    else
                    {
                        Grumble("'empty' must be followed by 'greatest' or 'least'");
                    }
                }

                if (IsKeyword("collation"))
                {
                    sortSpec.collation = ReadCollationName();
                }

                sortSpecList.Add(sortSpec);
                if (t.currentToken == Token.COMMA)
                {
                    NextToken();
                }
                else
                {
                    break;
                }
            }

            return sortSpecList;
        }

        /*clause.getRangeVariable()*/
        protected virtual string ReadCollationName()
        {
            NextToken();
            Expect(Token.STRING_LITERAL);
            string collationName = UriLiteral(t.currentTokenValue);
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
            catch (URISyntaxException err)
            {
                Grumble("Collation name '" + collationName + "' is not a valid URI", "XQST0046");
                collationName = NamespaceConstant.CODEPOINT_COLLATION_URI;
            }

            NextToken();
            return collationName;
        }

    }
}
