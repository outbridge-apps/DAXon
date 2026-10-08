////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// This class implements the XPath 2.0 fn:compare() function
    /// </summary>
    internal sealed class Compare : CollatingFunctionFixed
    {
        private static Int64Value CompareFn(StringValue s1, StringValue s2, IAtomicComparer comparer)
        {
            if (s1 == null || s2 == null)
            {
                return null;
            }

            int result = comparer.CompareAtomicValues(s1, s2);
            if (result < 0)
            {
                return Int64Value.MINUS_ONE;
            }
            else if (result > 0)
            {
                return Int64Value.PLUS_ONE;
            }
            else
            {
                return Int64Value.ZERO;
            }
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            StringValue arg0 = (StringValue)arguments[0].Head();
            StringValue arg1 = (StringValue)arguments[1].Head();
            GenericAtomicComparer comparer = new GenericAtomicComparer(StringCollator, context);
            return SequenceTool.ItemOrEmpty(CompareFn(arg0, arg1, comparer));
        }
    }
}
