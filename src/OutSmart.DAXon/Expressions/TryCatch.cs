////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Expressions
{
    internal sealed class TryCatch : Expression
    {
        private readonly Operand tryOp;
        private readonly IList<CatchClause> catchClauses = new List<CatchClause>();
        private bool rollbackOutput;

        /// <summary>
        /// Errors xsl:try must never intercept, whatever its catch clauses match. A stack overflow
        /// is one: there is no headroom left to run a handler. The host's wall-clock deadline
        /// (SXTO0001) and memory limit (SXLM0003) are the others, and the reason is not headroom but
        /// authority - they are limits the HOST set on the run, so a stylesheet able to catch them
        /// would turn a hard limit into a suggestion, and a catch inside the offending loop would
        /// defeat it outright. Deliberately wider than upstream, which has no such limits to protect.
        /// </summary>
        private static bool IsUncatchable(XPathException err)
        {
            return err is XPathException.StackOverflow || err.IsRunLimit();
        }

        public Expression TryExpr => tryOp.GetChildExpression();

        public override int ImplementationMethod => ITERATE_METHOD;

        public override string ExpressionName => "tryCatch";

        /// <summary>
        /// An error listener that filters out reporting of any errors that are caught be the try/catch
        /// </summary>
        public override string StreamerName => "TryCatch";
        public TryCatch(Expression tryExpr)
        {
            this.tryOp = new Operand(this, tryExpr, OperandRole.SAME_FOCUS_ACTION);
        }

        public void AddCatchExpression(IQNameTest test, Expression catchExpr)
        {
            CatchClause clause = new CatchClause();
            clause.catchOp = new Operand(this, catchExpr, OperandRole.SAME_FOCUS_ACTION);
            clause.nameTest = test;
            catchClauses.Add(clause);
        }

        public void SetRollbackOutput(bool rollback)
        {
            this.rollbackOutput = rollback;
        }

        public override bool IsInstruction()
        {
            return true;
        }

        public override bool AllowExtractingCommonSubexpressions()
        {
            return false;
        }

        /// <summary>
        /// Determine the cardinality of the function.
        /// </summary>
        protected override int ComputeCardinality()
        {
            int card = TryExpr.GetCardinality();
            foreach (CatchClause catchClause in catchClauses)
            {
                card = Cardinality.Union(card, catchClause.catchOp.GetChildExpression().GetCardinality());
            }

            return card;
        }

        public override Types.ItemType GetItemType()
        {
            Types.ItemType type = TryExpr.GetItemType();
            foreach (CatchClause catchClause in catchClauses)
            {
                type = Types.Type.GetCommonSuperType(type, catchClause.catchOp.GetChildExpression().GetItemType());
            }

            return type;
        }

        public override IEnumerable<Operand> Operands()
        {
            IList<Operand> list = new List<Operand>();
            list.Add(tryOp);
            foreach (CatchClause cc in catchClauses)
            {
                list.Add(cc.catchOp);
            }

            return list;
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            OptimizeChildren(visitor, contextInfo);
            Expression e = ParentExpression;
            while (e != null)
            {
                if (e is LetExpression && ExpressionTool.DependsOnVariable(TryExpr, new IBinding[] { (LetExpression)e }))
                {
                    ((LetExpression)e).SetNeedsEagerEvaluation(true);
                }

                e = e.ParentExpression;
            }

            return this;
        }

        public override bool Equals(object other)
        {
            return other is TryCatch && ((TryCatch)other).tryOp.GetChildExpression().IsEqual(tryOp.GetChildExpression()) && ((TryCatch)other).catchClauses.Equals(catchClauses);
        }

        protected override int ComputeHashCode()
        {
            int h = 0x636b12a0;
            for (int i = 0; i < catchClauses.Count; i++)
            {
                h ^= catchClauses[i].GetHashCode() << i;
            }

            return h + tryOp.GetChildExpression().GetHashCode();
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            TryCatch t2 = new TryCatch(tryOp.GetChildExpression().Copy(rebindings));
            foreach (CatchClause clause in catchClauses)
            {
                t2.AddCatchExpression(clause.nameTest, clause.catchOp.GetChildExpression().Copy(rebindings));
            }

            t2.SetRollbackOutput(rollbackOutput);
            ExpressionTool.CopyLocationInfo(this, t2);
            return t2;
        }

        public override IItem EvaluateItem(IXPathContext c)
        {
            return MakeElaborator().ElaborateForItem().Eval(c);
        }

        public override ISequenceIterator Iterate(IXPathContext c)
        {
            return MakeElaborator().ElaborateForPull().Iterate(c);
        }

        public override void Process(Outputter output, IXPathContext context)
        {
            DispatchTailCall(MakeElaborator().ElaborateForPush().ProcessLeavingTail(output, context));
        }

        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("try", this);
            if (rollbackOutput)
            {
                @out.EmitAttribute("flags", "r");
            }

            tryOp.GetChildExpression().Export(@out);
            foreach (CatchClause clause in catchClauses)
            {
                @out.StartElement("catch");
                @out.EmitAttribute("errors", clause.nameTest.ExportQNameTest());
                clause.catchOp.GetChildExpression().Export(@out);
                @out.EndElement();
            }

            @out.EndElement();
        }

        public override Elaborator GetElaborator()
        {
            return new TryCatchElaborator();
        }

        internal sealed class CatchClause
        {
            public Operand catchOp;
            public IQNameTest nameTest;
        }

        private sealed class TryCatchElaborator : PushElaborator
        {
            public override IPushEvaluator ElaborateForPush()
            {
                TryCatch expr = (TryCatch)GetExpression();
                IPushEvaluator tryPush = expr.TryExpr.MakeElaborator().ElaborateForPush();
                return (output, context) =>
                {
                    PipelineConfiguration pipe = output.GetPipelineConfiguration();
                    XPathContextMajor c1 = context.NewContext();
                    c1.CreateThreadManager();
                    c1.SetErrorReporter(new FilteringErrorReporter(context.GetErrorReporter(), expr.catchClauses));

                    Outputter o2;
                    if (expr.rollbackOutput)
                    {
                        o2 = new OutputterEventBuffer();
                        o2.SetPipelineConfiguration(pipe);
                    }
                    else
                    {
                        o2 = new EventMonitor(output);
                        o2.SetPipelineConfiguration(pipe);
                    }

                    object checkpoint = null;
                    if (c1.GetController().IsTracing())
                    {
                        checkpoint = c1.GetController().GetTraceListener().Checkpoint();
                    }

                    try
                    {
                        try
                        {
                            ITailCall tc = tryPush.ProcessLeavingTail(o2, c1);
                            Expression.DispatchTailCall(tc);
                            c1.WaitForChildThreads();

                            // check for xsl:break within xsl:try - test iterate-035
                            TailCallLoop.ITailCallInfo tci = c1.TailCallInfo;
                            if (tci is BreakInstr)
                            {
                                ((BreakInstr)tci).MarkContext(context);
                            }

                            if (expr.rollbackOutput)
                            {
                                ((OutputterEventBuffer)o2).Replay(output);
                            }
                        }
                        catch (UncheckedXPathException ue)
                        {
                            throw ue.GetXPathException();
                        }
                    }
                    catch (XPathException err)
                    {
                        if (err.IsGlobalError())
                        {
                            err.SetIsGlobalError(false);
                        }
                        else if (!IsUncatchable(err))
                        {
                            StructuredQName code = err.ErrorCodeQName;
                            if (code == null)
                            {
                                code = new StructuredQName("saxon", NamespaceUri.SAXON, "XXXX9999");
                            }

                            foreach (CatchClause clause in expr.catchClauses)
                            {
                                if (clause.nameTest.Matches(code))
                                {
                                    if (o2 is EventMonitor && ((EventMonitor)o2).HasBeenWrittenTo())
                                    {

                                        // rollback=no was specified, and output has been written, so we cannot recover
                                        string message = err.Message + ". The error could not be caught, because rollback-output=no was specified, and output was already written to the result tree";
                                        throw new XPathException(message, "XTDE3530").WithLocation(err.GetLocator()).WithXPathContext(context);
                                    }

                                    if (checkpoint != null)
                                    {
                                        context.GetController().GetTraceListener().Recover(checkpoint, err);
                                    }

                                    Expression caught = clause.catchOp.GetChildExpression();
                                    XPathContextMajor c2 = context.NewContext();
                                    c2.SetCurrentException(err);

                                    // check for xsl:break within xsl:catch - test iterate-036
                                    IGroundedValue v = ExpressionTool.EagerEvaluate(caught, c2);
                                    TailCallLoop.ITailCallInfo tci = c2.TailCallInfo;
                                    if (tci is BreakInstr)
                                    {
                                        ((BreakInstr)tci).MarkContext(context);
                                    }


                                    SequenceTool.Supply(v.Iterate(), (item) => output.Append(item));
                                    return null;
                                }
                            }
                        }

                        err.SetHasBeenReported(false);
                        throw;
                    }

                    return null;
                };
            }

            public override IPullEvaluator ElaborateForPull()
            {
                TryCatch expr = (TryCatch)GetExpression();
                ISequenceEvaluator tryEval = expr.TryExpr.MakeElaborator().Eagerly();
                return (context) =>
                {
                    XPathContextMajor c1 = context.NewContext();
                    c1.CreateThreadManager();
                    c1.SetErrorReporter(new FilteringErrorReporter(context.GetErrorReporter(), expr.catchClauses));
                    try
                    {
                        try
                        {

                            // Need to do eager iteration of the first argument to flush any errors out
                            ISequence v = tryEval.Evaluate(c1);
                            c1.WaitForChildThreads();

                            // check for xsl:break within xsl:try - test iterate-035
                            TailCallLoop.ITailCallInfo tci = c1.TailCallInfo;
                            if (tci is BreakInstr)
                            {
                                ((BreakInstr)tci).MarkContext(context);
                            }

                            return v.Iterate();
                        }
                        catch (UncheckedXPathException ue)
                        {
                            throw ue.GetXPathException();
                        }
                    }
                    catch (XPathException err)
                    {
                        if (err.IsGlobalError())
                        {
                            err.SetIsGlobalError(false);
                        }
                        else if (!IsUncatchable(err))
                        {
                            StructuredQName code = err.ErrorCodeQName;
                            if (code == null)
                            {
                                code = new StructuredQName("saxon", NamespaceUri.SAXON, "XXXX9999");
                            }

                            foreach (CatchClause clause in expr.catchClauses)
                            {
                                if (clause.nameTest.Matches(code))
                                {
                                    Expression caught = clause.catchOp.GetChildExpression();
                                    XPathContextMajor c2 = context.NewContext();
                                    c2.SetCurrentException(err);

                                    // check for xsl:break within xsl:catch - test iterate-036
                                    ISequence v = ExpressionTool.EagerEvaluate(caught, c2);
                                    TailCallLoop.ITailCallInfo tci = c2.TailCallInfo;
                                    if (tci is BreakInstr)
                                    {
                                        ((BreakInstr)tci).MarkContext(context);
                                    }

                                    return v.Iterate();
                                }
                            }
                        }

                        err.SetHasBeenReported(false);
                        throw;
                    }
                };
            }

            public override IItemEvaluator ElaborateForItem()
            {
                TryCatch expr = (TryCatch)GetExpression();
                ISequenceEvaluator tryEval = expr.TryExpr.MakeElaborator().Eagerly();
                return (context) =>
                {
                    IXPathContext c1 = context.NewMinorContext();
                    try
                    {
                        try
                        {
                            return tryEval.Evaluate(c1).Head();
                        }
                        catch (UncheckedXPathException e)
                        {
                            throw e.GetXPathException();
                        }
                    }
                    catch (XPathException err)
                    {
                        if (err.IsGlobalError())
                        {
                            err.SetIsGlobalError(false);
                        }
                        else if (!IsUncatchable(err))
                        {
                            StructuredQName code = err.ErrorCodeQName;
                            if (code == null)
                            {
                                code = new StructuredQName("saxon", NamespaceUri.SAXON, "XXXX9999");
                            }

                            foreach (CatchClause clause in expr.catchClauses)
                            {
                                if (clause.nameTest.Matches(code))
                                {
                                    Expression caught = clause.catchOp.GetChildExpression();
                                    XPathContextMajor c2 = context.NewContext();
                                    c2.SetCurrentException(err);
                                    return caught.EvaluateItem(c2);
                                }
                            }
                        }

                        err.SetHasBeenReported(false);
                        throw;
                    }
                };
            }
        }

        /// <summary>
        /// An error listener that filters out reporting of any errors that are caught be the try/catch
        /// </summary>
        private sealed class FilteringErrorReporter : IErrorReporter
        {
            private readonly IErrorReporter @base;
            private readonly IList<CatchClause> catchClauses;
            public FilteringErrorReporter(IErrorReporter @base, IList<CatchClause> catchClauses)
            {
                this.@base = @base;
                this.catchClauses = catchClauses;
            }

            private bool IsCaught(IXmlProcessingError err)
            {
                QName errorCode = err.GetErrorCode();
                if (errorCode == null)
                {
                    return false;
                }

                StructuredQName code = errorCode.GetStructuredQName();
                foreach (CatchClause clause in catchClauses)
                {
                    if (clause.nameTest.Matches(code))
                    {
                        return true;
                    }
                }

                return false;
            }

            public void Report(IXmlProcessingError error)
            {
                if (error.IsWarning() || !IsCaught(error))
                {
                    @base.Report(error);
                }
            }
        }
    }
}
