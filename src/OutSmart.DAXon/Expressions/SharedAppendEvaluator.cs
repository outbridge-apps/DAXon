////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Model;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Collections.Zeno;

namespace OutSmart.DAXon.Expressions
{
    // The transpiled upstream SharedAppendEvaluator assigns lambdas to the Block.IChainAction *interface* —
    // invalid C#. Faithful port with named IChainAction classes (EagerAction/PullAction) capturing the
    // per-child evaluator instead of lambdas.
    internal sealed class SharedAppendEvaluator : ISequenceEvaluator
    {
        private readonly Block.IChainAction[] actions;
        public SharedAppendEvaluator(object expr)
        {
            var block = (Block)expr;
            actions = new Block.IChainAction[block.Count];
            for (int i = 0; i < block.Count; i++)
            {
                var child = block.GetOperanda()[i].GetChildExpression();
                if (child is VariableReference)
                {
                    actions[i] = new EagerAction(child.MakeElaborator().Eagerly());
                }
                else
                {
                    actions[i] = new PullAction(child.MakeElaborator().ElaborateForPull());
                }
            }
        }
        public ISequence Evaluate(IXPathContext context)
        {
            var chain = new ZenoSequence();
            foreach (var action in actions)
            {
                chain = action.Perform(chain, context);
            }
            return chain;
        }
        private sealed class EagerAction : Block.IChainAction
        {
            private readonly ISequenceEvaluator eval;
            public EagerAction(ISequenceEvaluator eval) { this.eval = eval; }
            public ZenoSequence Perform(ZenoSequence @in, IXPathContext context)
                => @in.AppendSequence(eval.Evaluate(context).Materialize());
        }
        private sealed class PullAction : Block.IChainAction
        {
            private readonly IPullEvaluator pull;
            public PullAction(IPullEvaluator pull) { this.pull = pull; }
            public ZenoSequence Perform(ZenoSequence @in, IXPathContext context)
            {
                var chain = @in;
                var iter = pull == null ? null : pull(context);
                if (iter != null)
                {
                    for (IItem item; (item = iter.Next()) != null;)
                    {
                        chain = chain.Append(item);
                    }
                }
                return chain;
            }
        }
    }
}
