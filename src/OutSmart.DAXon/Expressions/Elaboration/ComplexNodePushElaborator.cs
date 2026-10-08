////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using System;
namespace OutSmart.DAXon.Expressions.Elaboration
{
    internal abstract class ComplexNodePushElaborator : FallbackElaborator
    {
        public override IPullEvaluator ElaborateForPull()
        {
            IPushEvaluator pushEval = this.ElaborateForPush();
            return (context) =>
            {
                Controller controller = context.GetController();
                if (controller == null)
                {
                    throw new NoDynamicContextException("No controller available");
                }

                SequenceCollector seq = controller.AllocateSequenceOutputter(1);
                ITailCall tc = pushEval.ProcessLeavingTail(new ComplexContentOutputter(seq), context);
                Expression.DispatchTailCall(tc);
                seq.Close();
                ISequenceIterator result = seq.Iterate();
                seq.Reset();
                return result;
            };
        }

        public override IItemEvaluator ElaborateForItem()
        {
            IPushEvaluator pushEval = this.ElaborateForPush();
            return (context) =>
            {
                Controller controller = context.GetController();
                if (controller == null)
                {
                    throw new NoDynamicContextException("No controller available");
                }

                SequenceCollector seq = controller.AllocateSequenceOutputter(1);
                ITailCall tc = pushEval.ProcessLeavingTail(new ComplexContentOutputter(seq), context);
                Expression.DispatchTailCall(tc);
                seq.Close();
                IItem result = seq.FirstItem;
                seq.Reset();
                return result;
            };
        }

        public override IPushEvaluator ElaborateForPush()
        {

            // Must be implemented in a subclass
            throw new NotSupportedException();
        }
    }
}