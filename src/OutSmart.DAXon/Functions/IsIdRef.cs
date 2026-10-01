////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Values;

namespace OutSmart.DAXon.Functions
{
    // IsIdRef extends ExtensionFunctionDefinition in Saxon (KeyManager passes new IsIdRef()
    // to IntegratedFunctionLibrary.makeFunctionCall); reflect the real base so the upcast is valid.
    // The internal saxon:is-idref predicate; registered for the built-in idref key on every compile
    // (KeyManager.RegisterIdrefKey).
    internal sealed class IsIdRef : ExtensionFunctionDefinition
    {
        private static readonly StructuredQName _qName =
            new StructuredQName("", NamespaceUri.SAXON, "is-idref");
        public override StructuredQName FunctionQName => _qName;
        public override int MinimumNumberOfArguments => 0;
        public override int MaximumNumberOfArguments => 0;
        public override SequenceType[] ArgumentTypes => new SequenceType[] { };
        public IsIdRef() { }
        public override SequenceType GetResultType(SequenceType[] suppliedArgumentTypes) => SequenceType.SINGLE_BOOLEAN;
        public override ExtensionFunctionCall MakeCallExpression() => new IsIdRefCall();
        private sealed class IsIdRefCall : ExtensionFunctionCall
        {
            public override ISequence Call(IXPathContext context, ISequence[] arguments)
            {
                var ci = context.GetContextItem();
                return BooleanValue.Get(ci is NodeInfo && ((NodeInfo)ci).IsIdref());
            }
            public override bool EffectiveBooleanValue(IXPathContext context, ISequence[] arguments)
            {
                var ci = context.GetContextItem();
                return ci is NodeInfo && ((NodeInfo)ci).IsIdref();
            }
        }
    }
}
