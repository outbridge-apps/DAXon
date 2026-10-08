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
namespace OutSmart.DAXon.Expressions.Elaboration
{
    /// <summary>
    /// A ISequenceEvaluator that evaluates an expression eagerly, in push mode.
    /// </summary>
    internal sealed class EagerPushEvaluator : ISequenceEvaluator
    {
        readonly IPushEvaluator pusher;
        public EagerPushEvaluator(IPushEvaluator select)
        {
            this.pusher = select;
        }

        public ISequence Evaluate(IXPathContext context)
        {
            try
            {
                Controller controller = context.GetController();
                SequenceCollector seq = controller.AllocateSequenceOutputter();
                ComplexContentOutputter @out = new ComplexContentOutputter(seq);
                @out.Open();
                ITailCall tail = pusher.ProcessLeavingTail(@out, context);
                Expression.DispatchTailCall(tail);
                @out.Close();
                return seq.Sequence;
            }
            catch (UncheckedXPathException e)
            {
                throw e.GetXPathException();
            }
        }
    }
}