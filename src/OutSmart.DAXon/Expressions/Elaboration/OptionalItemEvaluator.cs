////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Expressions.Elaboration
{
    internal sealed class OptionalItemEvaluator : ISequenceEvaluator
    {
        readonly IItemEvaluator evaluator;
        public OptionalItemEvaluator(IItemEvaluator eval)
        {
            this.evaluator = eval;
        }

        public ISequence Evaluate(IXPathContext context)
        {
            try
            {
                IItem result = evaluator.Eval(context);
                return result == null ? EmptySequence.GetInstance() : result;
            }
            catch (UncheckedXPathException e)
            {
                throw e.GetXPathException();
            }
        }
    }
}