////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;

namespace OutSmart.DAXon.Expressions.Elaboration
{
    // Faithful to upstream LiteralEvaluator: store the grounded ctor value; Evaluate => value.
    internal sealed class LiteralEvaluator : ISequenceEvaluator
    {
        private readonly IGroundedValue value;
        public LiteralEvaluator(object value) { this.value = value as IGroundedValue; }
        public ISequence Evaluate(IXPathContext context) => value;
    }
}
