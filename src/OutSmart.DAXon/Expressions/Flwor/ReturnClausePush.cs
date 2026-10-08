////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Instructions;
namespace OutSmart.DAXon.Expressions.Flwor
{
    // Final destination of a FLWOR push pipeline: evaluates the return clause for each tuple.
    internal sealed class ReturnClausePush : TuplePush
    {
        private readonly IPushEvaluator returnExpr;
        public ReturnClausePush(Outputter outputter, IPushEvaluator returnExpr) : base(outputter)
        {
            this.returnExpr = returnExpr;
        }

        public override void ProcessTuple(IXPathContext context)
        {
            ITailCall tc = returnExpr(GetOutputter(), context);
            Expression.DispatchTailCall(tc);
        }
    }
}
