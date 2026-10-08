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
    // ConstantFunction stub.
    // Runtime: functional impl ported from the excluded ConstantFunction.cs (csproj Compile Remove). The real
    // ConstantFunction stores an IGroundedValue and Call returns it; fn:true()/fn:false() (XPath20FunctionSet:
    // 210/109) are ConstantFunction.True/.False whose Call returns the constant BooleanValue. The (object value)
    // ctor + New() are retained for the existing callers (ContextItemAccessorFunction, CurrentGroup,
    // CurrentGroupingKey, CurrentOutputUri, PositionAndLast) -- previously no-ops; Call now returns the stored
    // value when it is an IGroundedValue (all those callers pass GroundedValues).
    internal class ConstantFunction : SystemFunction
    {
        private readonly IGroundedValue _value;
        public ConstantFunction(object value) { _value = value as IGroundedValue; }
        public override ISequence Call(IXPathContext context, ISequence[] arguments) => _value;
        internal sealed class True : ConstantFunction { public True() : base(BooleanValue.TRUE) { } }
        internal sealed class False : ConstantFunction { public False() : base(BooleanValue.FALSE) { } }
    }
}
