////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Values;

namespace OutSmart.DAXon.Expressions
{
    /// <summary>
    /// An OuterForExpression implements a "for $x allowing empty in ..." clause in XQuery. It behaves like a
    /// ForExpression except that when the input sequence is empty the return clause is still evaluated once,
    /// with the range variable bound to the empty sequence (the XQuery left-outer-join construct).
    /// Ported from upstream Saxon (was a hollow excluded stub whose implicit ForExpression conversion threw,
    /// so FLWORExpression.RewriteForOrLet crashed as soon as it rewrote an `allowing empty` for-clause).
    /// </summary>
    internal sealed class OuterForExpression : ForExpression
    {
        // The range variable may be bound to an empty sequence.
        protected override int RangeVariableCardinality => StaticProperty.ALLOWS_ZERO_OR_ONE;

        public override string ExpressionName => "outerFor";

        public override Expression Copy(RebindingMap rebindings)
        {
            OuterForExpression forExp = new OuterForExpression();
            ExpressionTool.CopyLocationInfo(this, forExp);
            forExp.SetRequiredType(requiredType);
            forExp.SetVariableQName(variableName);
            forExp.Sequence = Sequence.Copy(rebindings);
            rebindings.Put(this, forExp);
            Expression newAction = GetAction().Copy(rebindings);
            forExp.SetAction(newAction);
            forExp.variableName = variableName;
            forExp.slotNumber = slotNumber;
            return forExp;
        }

        protected override string AllowingEmptyString()
        {
            return "allowing empty ";
        }

        public override Elaborator GetElaborator()
        {
            return new OuterForExprElaborator();
        }

        internal sealed class OuterForExprElaborator : PullElaborator
        {
            public override IPullEvaluator ElaborateForPull()
            {
                OuterForExpression expr = (OuterForExpression)GetExpression();
                IPullEvaluator selectEval = expr.Sequence.MakeElaborator().ElaborateForPull();
                IPullEvaluator actionEval = expr.GetAction().MakeElaborator().ElaborateForPull();
                int slot = expr.LocalSlotNumber;
                return (context) =>
                {
                    ISequenceIterator @base = selectEval.Iterate(context);
                    IItem first = @base.Next();
                    if (first == null)
                    {
                        // Empty input: bind the variable to () and evaluate the return clause once.
                        context.SetLocalVariable(slot, EmptySequence.GetInstance());
                        return actionEval.Iterate(context);
                    }

                    ISequenceIterator prepended = new PrependSequenceIterator(first, @base);
                    return new MappingIterator(prepended, SequenceMapper.Of((item) =>
                    {
                        context.SetLocalVariable(slot, item);
                        return actionEval.Iterate(context);
                    }));
                };
            }

            public override IPushEvaluator ElaborateForPush()
            {
                OuterForExpression expr = (OuterForExpression)GetExpression();
                IPullEvaluator selectEval = expr.Sequence.MakeElaborator().ElaborateForPull();
                IPushEvaluator actionEval = expr.GetAction().MakeElaborator().ElaborateForPush();
                int slot = expr.LocalSlotNumber;
                return (@out, context) =>
                {
                    ISequenceIterator @base = selectEval.Iterate(context);
                    IItem first = @base.Next();
                    if (first == null)
                    {
                        context.SetLocalVariable(slot, EmptySequence.GetInstance());
                        Expression.DispatchTailCall(actionEval.ProcessLeavingTail(@out, context));
                        return null;
                    }

                    ISequenceIterator prepended = new PrependSequenceIterator(first, @base);
                    for (IItem item; (item = prepended.Next()) != null;)
                    {
                        context.SetLocalVariable(slot, item);
                        Expression.DispatchTailCall(actionEval.ProcessLeavingTail(@out, context));
                    }

                    return null;
                };
            }
        }
    }
}
