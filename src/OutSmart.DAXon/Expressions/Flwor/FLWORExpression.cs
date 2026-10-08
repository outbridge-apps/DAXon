////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.XQuery;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Text;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
namespace OutSmart.DAXon.Expressions.Flwor
{
    /// <summary>
    /// This class represents a FLWOR expression, evaluated using tuple streams
    /// </summary>
    public class FLWORExpression : Expression
    {

        private static readonly OperandRole SINGLE_RETURN = new OperandRole(0, OperandUsage.TRANSMISSION, SequenceType.ANY_SEQUENCE);
        private static readonly OperandRole REPEATED_RETURN = new OperandRole(OperandRole.HIGHER_ORDER, OperandUsage.TRANSMISSION, SequenceType.ANY_SEQUENCE);
        public IList<Clause> clauses;
        public Operand returnClauseOp;
        internal volatile IPushEvaluator returnPushEvaluator; // needed if generating push bytecode; ReturnPushEvaluator is the public spelling

        public virtual IList<Clause> ClauseList => clauses;

        public virtual Expression ReturnClause => returnClauseOp.GetChildExpression();

        public virtual IPushEvaluator ReturnPushEvaluator
        {
            get
            {
                lock (syncLock)
                {
                    if (returnPushEvaluator == null)
                    {
                        returnPushEvaluator = MakeElaborator().ElaborateForPush();
                    }
                }

                return returnPushEvaluator;
            }
        }

        public override int ImplementationMethod => ITERATE_METHOD | PROCESS_METHOD;

        public override string ExpressionName => "FLWOR";
        public FLWORExpression()
        {
        }

        public virtual void Init(IList<Clause> clauses, Expression returnClause)
        {
            this.clauses = clauses;
            bool looping = false;
            foreach (Clause c in clauses)
            {
                if (IsLoopingClause(c))
                {
                    looping = true;
                    break;
                }
            }

            this.returnClauseOp = new Operand(this, returnClause, looping ? REPEATED_RETURN : SINGLE_RETURN);
        }

        public static bool IsLoopingClause(Clause c)
        {
            return c.ClauseKey == Clause.ClauseName.FOR || c.ClauseKey == Clause.ClauseName.GROUP_BY || c.ClauseKey == Clause.ClauseName.WINDOW;
        }

        public override bool HasVariableBinding(IBinding binding)
        {
            foreach (Clause c in clauses)
            {
                if (ClauseHasBinding(c, binding))
                {
                    return true;
                }
            }

            return false;
        }

        private bool ClauseHasBinding(Clause c, IBinding binding)
        {
            foreach (IBinding b in c.RangeVariables)
            {
                if (b == binding)
                {
                    return true;
                }
            }

            return false;
        }

        public override bool AllowExtractingCommonSubexpressions()
        {
            return false;
        }

        public override Expression Simplify()
        {
            IOperandProcessor simplifier = (op) => op.SetChildExpression(op.GetChildExpression().Simplify());
            foreach (Clause c in clauses)
            {
                c.ProcessOperands(simplifier);
            }

            returnClauseOp.SetChildExpression(ReturnClause.Simplify());
            return this;
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            IOperandProcessor typeChecker = (op) => op.TypeCheck(visitor, contextInfo);
            for (int i = 0; i < clauses.Count; i++)
            {
                clauses[i].ProcessOperands(typeChecker);
                clauses[i].TypeCheck(visitor, contextInfo);
                LocalVariableBinding[] bindings = clauses[i].RangeVariables;
                foreach (IBinding b in bindings)
                {
                    IList<VariableReference> references = new List<VariableReference>();
                    for (int j = i; j < clauses.Count; j++)
                    {
                        clauses[j].GatherVariableReferences(visitor, b, references);
                    }

                    ExpressionTool.GatherVariableReferences(ReturnClause, b, references);
                    clauses[i].RefineVariableType(visitor, references, ReturnClause);
                }
            }

            returnClauseOp.TypeCheck(visitor, contextInfo);
            return this;
        }

        public override bool ImplementsStaticTypeCheck()
        {
            foreach (Clause c in clauses)
            {
                switch (c.ClauseKey)
                {
                    case Clause.ClauseName.LET:
                    case Clause.ClauseName.WHERE:
                        continue;
                    default:
                        return false;
                }
            }

            return true;
        }

        public override Expression StaticTypeCheck(SequenceType req, bool backwardsCompatible, Func<RoleDiagnostic> roleSupplier, ExpressionVisitor visitor)
        {

            // only called if implementsStaticTypeCheck() returns true
            TypeChecker tc = visitor.GetConfiguration().GetTypeChecker(backwardsCompatible);
            returnClauseOp.SetChildExpression(tc.StaticTypeCheck(ReturnClause, req, roleSupplier, visitor));
            return this;
        }

        public override ItemType GetItemType()
        {
            return ReturnClause.GetItemType();
        }

        protected override int ComputeCardinality()
        {

            // Assume that simple cases, like a FLWOR whose clauses are all "let" clauses, will have been converted into something else.
            return StaticProperty.ALLOWS_ZERO_OR_MORE;
        }

        public override int ComputeDependencies()
        {
            return base.ComputeDependencies() | StaticProperty.DEPENDS_ON_OWN_RANGE_VARIABLES;
        }

        public override IEnumerable<Operand> Operands()
        {
            IList<Operand> list = new List<Operand>(5);
            try
            {
                foreach (Clause c in clauses)
                {

                    c.ProcessOperands((op) => list.Add(op));
                }
            }
            catch (XPathException e)
            {
                throw new InvalidOperationException(e.Message, e);
            }

            list.Add(returnClauseOp);
            return list;
        }
        public override void CheckForUpdatingSubexpressions()
        {
            IOperandProcessor processor = (op) =>
            {
                op.GetChildExpression().CheckForUpdatingSubexpressions();
                if (op.GetChildExpression().IsUpdatingExpression())
                {
                    throw new XPathException("An updating expression cannot be used in a clause of a FLWOR expression", "XUST0001");
                }
            };
            foreach (Clause c in clauses)
            {
                c.ProcessOperands(processor);
            }

            ReturnClause.CheckForUpdatingSubexpressions();
        }

        public override bool IsUpdatingExpression()
        {
            return ReturnClause.IsUpdatingExpression();
        }

        public override PathMap.PathMapNodeSet AddToPathMap(PathMap pathMap, PathMap.PathMapNodeSet pathMapNodeSet)
        {
            foreach (Clause c in clauses)
            {
                c.AddToPathMap(pathMap, pathMapNodeSet);
            }

            return ReturnClause.AddToPathMap(pathMap, pathMapNodeSet);
        }

        public virtual void InjectCode(ICodeInjector injector)
        {
            if (injector != null)
            {
                // Upstream guards against double-injection by looking for existing TraceClauses;
                // this port's injector traces clause OPERANDS in place and returns no extra
                // clause (the TraceClause wrapper is not ported), so there is nothing to detect.
                IList<Clause> expandedList = new List<Clause>(clauses.Count * 2);
                expandedList.Add(clauses[0]);
                for (int i = 1; i < clauses.Count; i++)
                {
                    Clause extraClause = injector.InjectClause(this, clauses[i - 1]);
                    if (extraClause != null)
                    {
                        expandedList.Add(extraClause);
                    }

                    expandedList.Add(clauses[i]);
                }

                Clause extra = injector.InjectClause(this, clauses[clauses.Count - 1]);
                if (extra != null)
                {
                    expandedList.Add(extra);
                }

                clauses = expandedList;
                returnClauseOp.SetChildExpression(ExpressionTool.InjectCode(returnClauseOp.GetChildExpression(), injector));
            }
        }

        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("FLWOR", this);
            foreach (Clause c in clauses)
            {
                c.Explain(@out);
            }

            @out.StartSubsidiaryElement("return");
            ReturnClause.Export(@out);
            @out.EndSubsidiaryElement();
            @out.EndElement();
        }

        public override Expression Copy(RebindingMap rebindings)
        {

            IList<Clause> newClauses = new List<Clause>();
            FLWORExpression f2 = new FLWORExpression();
            foreach (Clause c in clauses)
            {
                Clause c2 = c.Copy(f2, rebindings);
                c2.Location = c.Location;
                c2.SetRepeated(c.IsRepeated());
                LocalVariableBinding[] oldBindings = c.RangeVariables;
                LocalVariableBinding[] newBindings = c2.RangeVariables;
                for (int i = 0; i < oldBindings.Length; i++)
                {
                    rebindings.Put(oldBindings[i], newBindings[i]);
                }

                newClauses.Add(c2);
            }

            f2.Init(newClauses, ReturnClause.Copy(rebindings));
            ExpressionTool.CopyLocationInfo(this, f2);
            return f2;
        }

        public override Expression Unordered(bool retainAllNodes, bool forStreaming)
        {
            foreach (Clause c in clauses)
            {
                if (c is ForClause && ((ForClause)c).PositionVariable == null)
                {
                    ((ForClause)c).Sequence = ((ForClause)c).Sequence.Unordered(retainAllNodes, forStreaming);
                }
            }

            returnClauseOp.SetChildExpression(ReturnClause.Unordered(retainAllNodes, forStreaming));
            return this;
        }

        public override int GetEvaluationMethod()
        {
            return Expression.PROCESS_METHOD;
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {
            Optimizer opt = visitor.ObtainOptimizer();
            OptimizerOptions options = opt.GetOptimizerOptions();

            // Optimize all the subexpressions
            foreach (Clause c in clauses)
            {
                c.ProcessOperands((op) => op.Optimize(visitor, contextItemType));
                c.Optimize(visitor, contextItemType);
            }


            // Optimize the return expression
            returnClauseOp.SetChildExpression(ReturnClause.Optimize(visitor, contextItemType));

            // For a very simple "for" or "let" expression, convert it to a ForExpression or LetExpression now
            if (clauses.Count == 1)
            {
                Clause c = clauses[0];
                if (c is LetClause || (c is ForClause && ((ForClause)c).PositionVariable == null))
                {
                    return RewriteForOrLet(visitor, contextItemType);
                }
            }


            // If any 'let' clause declares a variable that is used only once, then inline it. If the variable
            // is not used at all, then eliminate it
            if (options.IsSet(OptimizerOptions.INLINE_VARIABLES))
            {
                bool tryAgain;
                bool changed = false;
                do
                {
                    tryAgain = false;
                    foreach (Clause c in clauses)
                    {
                        if (c.ClauseKey == Clause.ClauseName.LET && !(((LetClause)c).Sequence is TraceExpression))
                        {
                            LetClause lc = (LetClause)c;
                            if (!ExpressionTool.DependsOnVariable(this, new IBinding[] { lc.RangeVariable }))
                            {
                                clauses.Remove(c);
                                opt.Trace("Removed unused variable " + lc.RangeVariable.GetVariableQName().DisplayName, this);
                                tryAgain = true;
                                break;
                            }

                            bool suppressInlining = false;
                            foreach (Clause c2 in clauses)
                            {
                                if (c2.ContainsNonInlineableVariableReference(lc.RangeVariable))
                                {
                                    suppressInlining = true;
                                    break;
                                }
                            }

                            if (!suppressInlining)
                            {
                                bool oneRef = lc.RangeVariable.NominalReferenceCount == 1;
                                bool simpleSeq = lc.Sequence is VariableReference || lc.Sequence is Literal;
                                if (oneRef || simpleSeq)
                                {
                                    ExpressionTool.ReplaceVariableReferences(this, lc.RangeVariable, lc.Sequence, true);
                                    clauses.Remove(c);
                                    opt.Trace("Inlined variable " + lc.RangeVariable.GetVariableQName().DisplayName, this);
                                    if (clauses.Count == 0)
                                    {
                                        return ReturnClause;
                                    }

                                    tryAgain = true;
                                    break;
                                }
                            }
                        }
                    }

                    changed |= tryAgain;
                }
                while (tryAgain);

                // If changed, remove any redundant trace clauses
                if (changed)
                {
                    for (int i = clauses.Count - 1; i >= 1; i--)
                    {
                        if (clauses[i].ClauseKey == Clause.ClauseName.TRACE && clauses[i - 1].ClauseKey == Clause.ClauseName.TRACE)
                        {
                            clauses.RemoveAt(i);
                        }
                    }
                }
            }


            // If any 'where' clause depends on the context item, remove this dependency, because it makes
            // it easier to rearrange where clauses as predicates
            bool depends = false;
            foreach (Clause w in clauses)
            {
                if (w is WhereClause && w.IsRepeated() && ExpressionTool.DependsOnFocus(((WhereClause)w).Predicate))
                {
                    depends = true;
                    break;
                }
            }

            if (depends && contextItemType != null)
            {
                Expression expr1 = ExpressionTool.TryToFactorOutDot(this, contextItemType.GetItemType());
                if (expr1 == null || expr1 == this)
                {

                    //no optimisation possible
                    return this;
                }

                ResetLocalStaticProperties();
                return expr1.Optimize(visitor, contextItemType);
            }


            // Now convert any terms within WHERE clauses where possible into predicates on the appropriate
            // expression bound to a variable on a for clause. This enables the resulting filter expression
            // to be handled using indexing (in Saxon-EE), and it also reduces the number of items that need
            // to be tested against the predicate
            Expression expr2 = RewriteWhereClause(visitor, contextItemType);
            if (expr2 != null && expr2 != this)
            {
                return expr2.Optimize(visitor, contextItemType);
            }


            // If the FLWOR expression consists entirely of FOR and LET clauses, convert it to a ForExpression
            // or LetExpression. This is largely to take advantage of existing optimizations implemented for those
            // expressions.
            bool allForOrLetExpr = true;
            foreach (Clause c in clauses)
            {
                if (c is ForClause)
                {
                    if (((ForClause)c).PositionVariable != null)
                    {
                        allForOrLetExpr = false;
                        break;
                    }
                }
                else if (!(c is LetClause))
                {
                    allForOrLetExpr = false;
                    break;
                }
            }

            if (allForOrLetExpr)
            {
                return RewriteForOrLet(visitor, contextItemType);
            }

            return this;
        }

        private Expression RewriteWhereClause(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {
            WhereClause whereClause;
            int whereIndex = 0;
            IList<WhereClauseStruct> whereList = new List<WhereClauseStruct>();
            foreach (Clause c in clauses)
            {
                if (c is WhereClause)
                {
                    WhereClauseStruct wStruct = new WhereClauseStruct();
                    wStruct.whereClause = (WhereClause)c;

                    //keep track of whereclause from the end of the list of clauses.
                    //We are always attempting to rewrite whereclauses from left to right,
                    // therefore index will always be in snyc
                    wStruct.whereIndex = clauses.Count - whereIndex;
                    whereList.Add(wStruct);
                }

                whereIndex++;
            }

            if (whereList.Count == 0)
            {
                return null;
            }

            while (whereList.Count > 0)
            {
                whereClause = whereList[0].whereClause;
                whereIndex = whereList[0].whereIndex;
                Expression condition = whereClause.Predicate;
                IList<Expression> list = new List<Expression>(5);
                BooleanExpression.ListAndComponents(condition, list);
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    Expression term = list[i];
                    for (int c = clauses.Count - whereIndex - 1; c >= 0; c--)
                    {
                        Clause clause = clauses[c];
                        IBinding[] bindingList = clause.RangeVariables;

                        // Find the first clause prior to the where clause that declares variables on which the
                        // term of the where clause depends
                        if (ExpressionTool.DependsOnVariable(term, bindingList) || clause.ClauseKey == Clause.ClauseName.COUNT)
                        {

                            // remove this term from the where clause
                            Expression removedExpr = list.RemoveAtAndGet(i);
                            if (list.Count == 0)
                            {

                                // the where clause has no terms left, so remove the clause
                                clauses.RemoveAt(clauses.Count - whereIndex);
                            }
                            else
                            {

                                // change the predicate of the where clause to use only those terms that remain
                                whereClause.Predicate = MakeAndCondition(list);
                            }

                            if ((clause is ForClause) && !((ForClause)clause).IsAllowingEmpty())
                            {

                                // if the clause is a "for" clause, try to add the term as a predicate
                                bool added = ((ForClause)clause).AddPredicate(this, visitor, contextItemType, term);

                                //If we cannot add the WhereClause term as a predicate then put it back into the list of clauses
                                if (!added)
                                {
                                    WhereClause newWhere = new WhereClause(this, removedExpr);
                                    newWhere.Location = clause.Location;
                                    clauses.Insert(c + 1,newWhere);
                                }
                            }
                            else
                            {

                                // the clause is not a "for" clause, so just move the "where" to this place in the list of clauses
                                WhereClause newWhere = new WhereClause(this, term);
                                newWhere.Location = clause.Location;
                                clauses.Insert(c + 1,newWhere);
                            }


                            // we found a variable on which the term depends so we can't move it any further
                            break;
                        }
                    }

                    if (list.Count - 1 == i)
                    {
                        list.RemoveAt(i);
                        if (list.Count == 0)
                        {
                            clauses.RemoveAt(clauses.Count - whereIndex);
                        }
                        else
                        {
                            whereClause.Predicate = MakeAndCondition(list);
                        }

                        WhereClause newWhere = new WhereClause(this, term);
                        newWhere.Location = condition.GetLocation();
                        clauses.Insert(0,newWhere);
                    }
                }

                whereList.RemoveAt(0);
            }

            return this;
        }

        private Expression MakeAndCondition(IList<Expression> list)
        {
            if (list.Count == 1)
            {
                return list[0];
            }
            else
            {
                return new AndExpression(list[0], MakeAndCondition(list.GetRange(1, (list.Count) - (1))));
            }
        }

        private Expression RewriteForOrLet(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {
            Expression action = ReturnClause;
            for (int i = clauses.Count - 1; i >= 0; i--)
            {
                if (clauses[i] is ForClause)
                {
                    ForClause forClause = (ForClause)clauses[i];
                    ForExpression forExpr;
                    if (forClause.IsAllowingEmpty())
                    {
                        forExpr = new OuterForExpression();
                    }
                    else
                    {
                        forExpr = new ForExpression();
                    }

                    forExpr.SetLocation(forClause.Location);
                    forExpr.SetRetainedStaticContext(GetRetainedStaticContext());

                    forExpr.SetAction(action);
                    forExpr.Sequence = forClause.Sequence;
                    forExpr.SetVariableQName(forClause.RangeVariable.GetVariableQName());
                    forExpr.SetRequiredType(forClause.RangeVariable.GetRequiredType());
                    ExpressionTool.RebindVariableReferences(action, forClause.RangeVariable, forExpr);
                    action = forExpr; //                if (injector != null) {
                    //                }
                }
                else
                {
                    LetClause letClause = (LetClause)clauses[i];
                    LetExpression letExpr = new LetExpression();
                    letExpr.SetLocation(letClause.Location);
                    letExpr.SetRetainedStaticContext(GetRetainedStaticContext());

                    letExpr.SetAction(action);
                    letExpr.Sequence = letClause.Sequence;
                    letExpr.SetVariableQName(letClause.RangeVariable.GetVariableQName());
                    letExpr.SetRequiredType(letClause.RangeVariable.GetRequiredType());
                    if (letClause.RangeVariable.IsIndexedVariable())
                    {
                        letExpr.SetIndexedVariable();
                    }


                    ExpressionTool.RebindVariableReferences(action, letClause.RangeVariable, letExpr);
                    action = letExpr; //                if (injector != null) {
                    //                }
                }
            }

            action = action.TypeCheck(visitor, contextItemType);
            action = action.Optimize(visitor, contextItemType);
            return action;
        }

        public override ISequenceIterator Iterate(IXPathContext context)
        {
            return MakeElaborator().ElaborateForPull().Iterate(context);
        }

        public override void Process(Outputter output, IXPathContext context)
        {
            ITailCall tc = MakeElaborator().ElaborateForPush().ProcessLeavingTail(output, context);
            Expression.DispatchTailCall(tc);
        }

        public override string ToShortString()
        {
            StringBuilder sb = new StringBuilder(64);
            sb.Append(clauses[0].ToShortString());
            sb.Append(" ... return ");
            sb.Append(ReturnClause.ToShortString());
            return sb.ToString();
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder(64);
            foreach (Clause c in clauses)
            {
                sb.Append(c.ToString());
                sb.Append(' ');
            }

            sb.Append(" return ");
            sb.Append(ReturnClause.ToString());
            return sb.ToString();
        }

        public virtual bool HasLoopingVariableReference(IBinding binding)
        {

            // Determine the clause that binds the variable (if any)
            int bindingClause = -1;
            for (int i = 0; i < clauses.Count; i++)
            {
                if (ClauseHasBinding(clauses[i], binding))
                {
                    bindingClause = i;
                    break;
                }
            }

            bool boundOutside = bindingClause < 0;
            if (boundOutside)
            {
                bindingClause = 0;
            }


            // Determine the last clause that contains a reference to the variable.
            // (If any reference to the variable is a looping reference, then the last one will be)
            int lastReferencingClause = clauses.Count; // indicates the return clause
            if (!ExpressionTool.DependsOnVariable(ReturnClause, new IBinding[] { binding }))
            {

                // artifice to get a response value from the generic processExpression() method
                IList<bool> response = new List<bool>();
                IOperandProcessor checker = (op) =>
                {
                    if (response.Count == 0 && ExpressionTool.DependsOnVariable(op.GetChildExpression(), new IBinding[] { binding }))
                    {
                        response.Add(true);
                    }
                };
                for (int i = clauses.Count - 1; i >= 0; i--)
                {
                    try
                    {
                        clauses[i].ProcessOperands(checker);
                        if (response.Count > 0)
                        {
                            lastReferencingClause = i;
                            break;
                        }
                    }
                    catch (XPathException)
                    {
                    }
                }
            }


            // If any clause between the binding clause and the last referencing clause is a looping clause,
            // then the variable is used within a loop
            for (int i = lastReferencingClause - 1; i >= bindingClause; i--)
            {
                if (IsLoopingClause(clauses[i]))
                {
                    return true;
                }
            }


            // otherwise there is no loop caused by the clauses of the FLWOR expression itself.
            return false;
        }

        public override Elaborator GetElaborator()
        {
            return new FLWORElaborator();
        }

        private sealed class WhereClauseStruct
        {
            public int whereIndex;
            public WhereClause whereClause;
        }

        private sealed class FLWORElaborator : PullElaborator
        {
            public override IPullEvaluator ElaborateForPull()
            {
                FLWORExpression expr = (FLWORExpression)GetExpression();
                IPullEvaluator returnPull = expr.ReturnClause.MakeElaborator().ElaborateForPull();
                return (context) =>
                {
                    TuplePull stream = new SingularityPull();
                    foreach (Clause c in expr.clauses)
                    {
                        stream = c.GetPullStream(stream, context);
                    }

                    return new ReturnClauseIterator(stream, returnPull, context);
                };
            }

            public override IPushEvaluator ElaborateForPush()
            {
                FLWORExpression expr = (FLWORExpression)GetExpression();
                expr.returnPushEvaluator = expr.ReturnClause.MakeElaborator().ElaborateForPush();
                return (output, context) =>
                {
                    TuplePush destination = new ReturnClausePush(output, expr.returnPushEvaluator);
                    for (int i = expr.clauses.Count - 1; i >= 0; i--)
                    {
                        Clause c = expr.clauses[i];
                        destination = c.GetPushStream(destination, output, context);
                    }

                    destination.ProcessTuple(context);
                    destination.Dispose();
                    return null;
                };
            }

            public override IUpdateEvaluator ElaborateForUpdate()
            {
                FLWORExpression expr = (FLWORExpression)GetExpression();
                IUpdateEvaluator returnAction = expr.ReturnClause.MakeElaborator().ElaborateForUpdate();
                return (context, pul) =>
                {
                    TuplePull stream = new SingularityPull();
                    foreach (Clause c in expr.clauses)
                    {
                        stream = c.GetPullStream(stream, context);
                    }

                    while (stream.NextTuple(context))
                    {
                        returnAction.RegisterUpdates(context, pul);
                    }
                };
            }
        }
    }
}