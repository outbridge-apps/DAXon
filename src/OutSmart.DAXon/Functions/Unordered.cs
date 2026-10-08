////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// XPath 2.0 unordered() function
    /// </summary>
    internal sealed class Unordered : SystemFunction
    {
        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            return arguments[0];
        }

        public override Expression MakeFunctionCall(Expression[] arguments)
        {
            return arguments[0];
        }
    }
}
