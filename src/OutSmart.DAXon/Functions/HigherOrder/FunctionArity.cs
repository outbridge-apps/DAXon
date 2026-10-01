////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2020 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Functions.HigherOrder
{
    /// <summary>
    /// This class implements the function function-arity(), which is a standard function in XPath 3.0
    /// </summary>
    internal sealed class FunctionArity : SystemFunction
    {
        public override IntegerValue[] IntegerBounds => new IntegerValue[]
            {
                Int64Value.ZERO,
                Int64Value.MakeIntegerValue(65535)
            };

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            IFunctionItem f = (IFunctionItem)arguments[0].Head();
            return Int64Value.MakeIntegerValue(f.GetArity());
        }
    }
}
