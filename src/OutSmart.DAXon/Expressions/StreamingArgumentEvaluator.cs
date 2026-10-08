////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Expressions.Elaboration;

namespace OutSmart.DAXon.Expressions
{
    internal sealed class StreamingArgumentEvaluator : ISequenceEvaluator
    {
        public StreamingArgumentEvaluator() { }
        public ISequence Evaluate(IXPathContext context) => throw new NotImplementedException("STUB: StreamingArgumentEvaluator.Evaluate not ported (excluded stub)");
    }
}
