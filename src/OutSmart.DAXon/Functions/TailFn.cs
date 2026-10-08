////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Ported from upstream net/sf/saxon/functions/TailFn.java (the class was missing from the port, so
// fn:tail was unregistered/unresolved).

using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;

namespace OutSmart.DAXon.Functions
{
    /// <summary>Implements fn:tail — all items of a sequence except the first.</summary>
    internal sealed class TailFn : SystemFunction
    {
        public override Expression MakeFunctionCall(params Expression[] arguments)
        {
            return new TailExpression(arguments[0], 2);
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            return SequenceTool.ToLazySequence(TailIterator.Make(arguments[0].Iterate(), 2));
        }
    }
}
