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
        // NB: no makeFunctionCall override — the upstream one rewrites to FirstItemExpression, which is
        // only a hollow stub in this port (MakeFirstItemExpression returns its operand unchanged, so
        // head(1 to 5) would yield the whole sequence). The runtime Call path below is correct.
        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            IItem head = arguments[0].Head();
            return SequenceTool.ItemOrEmpty(head);
        }
    }
}
