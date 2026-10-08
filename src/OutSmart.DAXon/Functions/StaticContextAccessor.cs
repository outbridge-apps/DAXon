////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Functions
{
    internal abstract class StaticContextAccessor : SystemFunction
    {
        public abstract AtomicValue Evaluate(RetainedStaticContext rsc);
        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            return Evaluate(GetRetainedStaticContext());
        }

        public override Expression MakeFunctionCall(params Expression[] arguments)
        {
            return Literal.MakeLiteral(Evaluate(GetRetainedStaticContext()));
        }

        /// <summary>
        /// Implement the XPath function default-collation()
        /// </summary>
        internal sealed class DefaultCollation : StaticContextAccessor
        {
            public override AtomicValue Evaluate(RetainedStaticContext rsc)
            {
                return new StringValue(rsc.DefaultCollationName);
            }
        }
    }
}
