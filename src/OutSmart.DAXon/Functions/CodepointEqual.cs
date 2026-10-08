////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Functions
{
    internal sealed class CodepointEqual : SystemFunction, ICallable
    {
        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            StringValue op1 = (StringValue)arguments[0].Head();
            StringValue op2 = (StringValue)arguments[1].Head();
            if (op1 == null || op2 == null)
            {
                return EmptySequence.GetInstance();
            }

            return BooleanValue.Get(op1.UnicodeStringValue.Equals(op2.UnicodeStringValue));
        }
    }
}
