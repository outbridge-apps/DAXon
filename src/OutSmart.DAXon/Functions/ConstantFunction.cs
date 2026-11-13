////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Model;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Values;

namespace OutSmart.DAXon.Functions
{
    // A function whose call returns a fixed value: fn:true(), fn:false() and the context accessors' early values.
    internal class ConstantFunction : SystemFunction
    {
        private readonly IGroundedValue _value;
        public ConstantFunction(object value) { _value = value as IGroundedValue; }
        public override ISequence Call(IXPathContext context, ISequence[] arguments) => _value;
        // A call is its value, as in Saxon: true() compiles to a literal, so nothing treats it as work (loop lifting).
        public override Expression MakeFunctionCall(params Expression[] arguments) => Literal.MakeLiteral(_value);
        internal sealed class True : ConstantFunction { public True() : base(BooleanValue.TRUE) { } }
        internal sealed class False : ConstantFunction { public False() : base(BooleanValue.FALSE) { } }
    }
}
