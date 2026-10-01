////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// Implement XPath function fn:data() with a single argument
    /// </summary>
    internal sealed class Data_1 : SystemFunction
    {
        public override Expression MakeFunctionCall(params Expression[] arguments)
        {
            return Atomizer.MakeAtomizer(arguments[0], null);
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            ISequence arg = arguments[0];
            if (arg is IItem)
            {
                return ((IItem)arg).Atomize();
            }
            else
            {
                ISequenceIterator a = Atomizer.GetAtomizingIterator(arg.Iterate(), false);
                return SequenceTool.ToLazySequence(a);
            }
        }
    }
}
