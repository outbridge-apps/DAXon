////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Instructions;
namespace OutSmart.DAXon.Expressions.Elaboration
{

    internal static class PushEvaluatorExtensions
    {
        public static ITailCall ProcessLeavingTail(this IPushEvaluator e, Outputter @out, IXPathContext context) => e == null ? default : e(@out, context);
    }
}
