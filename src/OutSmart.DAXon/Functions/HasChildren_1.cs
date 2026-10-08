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
    /// <summary>
    /// This class implements the function fn:has-children($node), which is a standard function in XPath 3.0
    /// </summary>
    internal sealed class HasChildren_1 : SystemFunction
    {
        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            NodeInfo arg = (NodeInfo)arguments[0].Head();
            if (arg == null)
            {
                return BooleanValue.FALSE;
            }

            return BooleanValue.Get(arg.HasChildNodes());
        }
    }
}
