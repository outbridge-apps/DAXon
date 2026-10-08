////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Values;
using System;
namespace OutSmart.DAXon.Expressions.Elaboration
{
    internal abstract class PushElaborator : Elaborator
    {
        public override IPullEvaluator ElaborateForPull()
        {
            Expression expr = GetExpression();
            if (Cardinality.AllowsMany(expr.GetCardinality()))
            {
                IPushEvaluator pushEval = ElaborateForPush();
                return (context) =>
                {
                    Controller controller = context.GetController();
                    SequenceCollector seq = controller.AllocateSequenceOutputter();
                    ITailCall tc = pushEval.ProcessLeavingTail(new ComplexContentOutputter(seq), context);
                    Expression.DispatchTailCall(tc);
                    seq.Close();
                    return seq.Iterate();
                };
            }
            else
            {
                IItemEvaluator itemEval = ElaborateForItem();
                return (context) =>
                {
                    IItem item = itemEval.Eval(context);
                    return item == null ? EmptyIterator.GetInstance() : SingletonIterator.MakeIterator(item);
                };
            }
        }

        public override ISequenceEvaluator Eagerly()
        {
            Expression expr = GetExpression();
            if (Cardinality.AllowsMany(expr.GetCardinality()))
            {
                IPushEvaluator pushEval = ElaborateForPush();
                return new EagerPushEvaluator(pushEval);
            }
            else
            {
                return new OptionalItemEvaluator(ElaborateForItem());
            }
        }

        public override IPushEvaluator ElaborateForPush()
        {

            // Must be implemented in a subclass
            throw new NotSupportedException();
        }

        public override IItemEvaluator ElaborateForItem()
        {
            IPushEvaluator pushEval = ElaborateForPush();
            return (context) =>
            {
                Controller controller = context.GetController();
                SequenceCollector seq = controller.AllocateSequenceOutputter(1);
                ITailCall tc = pushEval.ProcessLeavingTail(new ComplexContentOutputter(seq), context);
                Expression.DispatchTailCall(tc);
                seq.Close();
                return seq.FirstItem;
            };
        }

        public override IBooleanEvaluator ElaborateForBoolean()
        {
            IPullEvaluator pullEval = ElaborateForPull();
            return (context) =>
            {
                ISequenceIterator iter = pullEval.Iterate(context);
                return ExpressionTool.EffectiveBooleanValue(iter);
            };
        }

        public override IUnicodeStringEvaluator ElaborateForUnicodeString(bool zeroLengthWhenAbsent)
        {
            IItemEvaluator ie = ElaborateForItem();
            return (context) =>
            {
                IItem item = ie.Eval(context);
                return item == null ? HandleNullUnicodeString(zeroLengthWhenAbsent) : item.UnicodeStringValue;
            };
        }
    }
}