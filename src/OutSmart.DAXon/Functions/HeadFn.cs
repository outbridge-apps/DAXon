////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Ported from upstream net/sf/saxon/functions/HeadFn.java (the class was missing from the port, so
// fn:head was unregistered/unresolved).

using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;

namespace OutSmart.DAXon.Functions
{
    /// <summary>Implements fn:head — the first item of a sequence (or the empty sequence).</summary>
    internal sealed class HeadFn : SystemFunction
    {
        // As upstream: the first item and no more is evaluated, so head((1, 1 idiv 0)) is 1, as (1, 1 idiv 0)[1] is.
        public override Expression MakeFunctionCall(Expression[] arguments)
        {
            return FirstItemExpression.MakeFirstItemExpression(arguments[0]);
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            IItem head = arguments[0].Head();
            return SequenceTool.ItemOrEmpty(head);
        }
    }
}
