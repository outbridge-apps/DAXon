////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Xslt;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Transformation.Rules;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
using OutSmart.DAXon.Expressions.Instructions;
namespace OutSmart.DAXon.Expressions.Parsing
{
    public class Optimizer
    {
        protected Configuration config;
        private OptimizerOptions optimizerOptions = OptimizerOptions.FULL_EE_OPTIMIZATION;
        protected bool tracing;
        public Optimizer(Configuration config)
        {
            this.config = config;
            this.tracing = config.GetBooleanProperty(Feature<bool>.TRACE_OPTIMIZER_DECISIONS);
        }

        public virtual Configuration GetConfiguration()
        {
            return config;
        }

        public virtual void SetOptimizerOptions(OptimizerOptions options)
        {
            optimizerOptions = options;
        }

        public virtual OptimizerOptions GetOptimizerOptions()
        {
            return optimizerOptions;
        }

        public virtual bool IsOptionSet(int option)
        {
            return optimizerOptions.IsSet(option);
        }

        public virtual Expression OptimizeValueComparison(ValueComparison vc, ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            Expression lhs = vc.GetLhsExpression();
            Expression rhs = vc.GetRhsExpression();
            Expression e2 = OptimizePositionVsLast(lhs, rhs, vc.Operator);
            if (e2 != null)
            {
                Trace("Rewrote position() ~= last()", e2);
                return e2;
            }

            e2 = OptimizePositionVsLast(rhs, lhs, Token.Inverse(vc.Operator));
            if (e2 != null)
            {
                Trace("Rewrote last() ~= position()", e2);
                return e2;
            }

            return vc;
        }

        private Expression OptimizePositionVsLast(Expression lhs, Expression rhs, int @operator)
        {

            // optimise [position()=last()] etc
            if (lhs.IsCallOn(typeof(PositionAndLast.Position)) && rhs.IsCallOn(typeof(PositionAndLast.Last)))
            {
                switch (@operator)
                {
                    case Token.FEQ:
                    case Token.FGE:
                        IsLastExpression iletrue = new IsLastExpression(true);
                        ExpressionTool.CopyLocationInfo(lhs, iletrue);
                        return iletrue;
                    case Token.FNE:
                    case Token.FLT:
                        IsLastExpression ilefalse = new IsLastExpression(false);
                        ExpressionTool.CopyLocationInfo(lhs, ilefalse);
                        return ilefalse;
                    case Token.FGT:
                        return Literal.MakeLiteral(BooleanValue.FALSE, lhs);
                    case Token.FLE:
                        return Literal.MakeLiteral(BooleanValue.TRUE, lhs);
                }
            }

            return null;
        }

        public virtual Expression OptimizeGeneralComparison(ExpressionVisitor visitor, GeneralComparison gc, bool backwardsCompatible, ContextItemStaticInfo contextItemType)
        {
            return gc;
        }

        public virtual Expression OptimizeSaxonStreamFunction(ExpressionVisitor visitor, ContextItemStaticInfo cisi, Expression select)
        {
            if (select.GetItemType().IsPlainType())
            {
                return select;
            }

            return null;
        }

        public virtual Expression ConvertPathExpressionToKey(SlashExpression pathExp, ExpressionVisitor visitor)
        {
            return null;
        }

        public virtual Expression TryIndexedFilter(FilterExpression f, ExpressionVisitor visitor, bool indexFirstOperand, bool contextIsDoc)
        {
            return f;
        }

        public virtual FilterExpression ReorderPredicates(FilterExpression f, ExpressionVisitor visitor, ContextItemStaticInfo cisi)
        {
            return f;
        }

        public virtual FilterExpression ConvertToFilterExpression(SlashExpression pathExp, TypeHierarchy th)
        {
            return null;
        }

        public virtual int IsIndexableFilter(Expression filter)
        {
            return 0;
        }

        public virtual IGroundedValue MakeIndexedValue(ISequenceIterator iter)
        {
            throw new NotSupportedException("Indexing requires Saxon-EE");
        }

        public virtual void OptimizeNodeSetPattern(NodeSetPattern pattern)
        {
        }

        public virtual void PrepareForStreaming(Expression exp)
        {
        }

        public virtual ISequence EvaluateStreamingArgument(Expression expr, IXPathContext context)
        {

            // non-streaming fallback implementation
            return ExpressionTool.EagerEvaluate(expr, context);
        }

        public virtual bool IsVariableReplaceableByDot(Expression exp, IBinding[] binding)
        {
            ExpressionTool.ProbeTreeDepth();

            // TODO: the fact that a variable reference appears inside a predicate (etc) shouldn't stop us
            // rewriting a where clause as a predicate. We just have to bind a new variable:
            // for $x in P where abc[n = $x/m] ==> for $x in P[let $z := . return abc[n = $z/m]
            // We could probably do this in all cases and then let $z be optimized away where appropriate
            foreach (Operand o in exp.Operands())
            {
                if (o.HasSameFocus())
                {
                    if (!IsVariableReplaceableByDot(o.GetChildExpression(), binding))
                    {
                        return false;
                    }
                }
                else if (ExpressionTool.DependsOnVariable(o.GetChildExpression(), binding))
                {
                    return false;
                }
            }

            return true;
        }

        public virtual Expression MakeConditionalDocumentSorter(DocumentSorter sorter, SlashExpression path)
        {
            return sorter;
        }

        public virtual Expression TryInlineFunctionCall(UserFunctionCall functionCall, ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {
            return functionCall;
        }

        public virtual Expression PromoteExpressionsToGlobal(Expression body, IGlobalVariableManager gvManager, ExpressionVisitor visitor)
        {
            return null;
        }

        public virtual Expression EliminateCommonSubexpressions(Expression @in)
        {
            return @in;
        }

        public virtual Expression TrySwitch(Choose choose, ExpressionVisitor visitor)
        {
            return choose;
        }

        public virtual Expression TryGeneralComparison(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType, OrExpression orExpr)
        {
            return orExpr;
        }

        public virtual IRuleTarget MakeInversion(Patterns.Pattern pattern, NamedTemplate template)
        {
            return null;
        }

        public virtual void MakeCopyOperationsExplicit(Expression parent, Operand child)
        {
        }

        public virtual void CheckStreamability(XSLTemplate sourceTemplate, TemplateRule compiledTemplate)
        {
        }

        public virtual Expression OptimizeQuantifiedExpressionForStreaming(QuantifiedExpression expr)
        {
            return expr;
        }

        public virtual Expression GenerateMultithreadedInstruction(Expression instruction)
        {
            return instruction;
        }

        public virtual Expression OptimizeNumberInstruction(NumberInstruction ni, ContextItemStaticInfo contextInfo)
        {
            return null;
        }

        public virtual void AssessFunctionStreamability(XSLFunction reporter, UserFunction compiledFunction)
        {
            throw new XPathException("Streamable stylesheet functions are not supported in Saxon-HE", "XTSE3430");
        }

        public virtual void Trace(string message, Expression exp)
        {
            if (tracing)
            {
                Logger err = GetConfiguration().Logger;
                err.Info("OPT : At line " + exp.GetLocation().GetLineNumber() + " of " + exp.GetLocation().GetSystemId());
                err.Info("OPT : " + message);
                err.Info("OPT : Expression after rewrite: " + exp);
                exp.VerifyParentPointers();
            }
        }

        public static void Trace(Configuration config, string message, Expression exp)
        {
            if (config.GetBooleanProperty(Feature<bool>.TRACE_OPTIMIZER_DECISIONS))
            {
                Logger err = config.Logger;
                err.Info("OPT : At line " + exp.GetLocation().GetLineNumber() + " of " + exp.GetLocation().GetSystemId());
                err.Info("OPT : " + message);
                err.Info("OPT : Expression after rewrite: " + exp);
                exp.VerifyParentPointers();
            }
        }
    }
}
