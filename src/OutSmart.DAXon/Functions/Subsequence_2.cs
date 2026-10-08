////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Values;
using System;
using OutSmart.DAXon.Expressions;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// Implements the XPath 2.0 subsequence() function with two arguments
    /// </summary>
    internal sealed class Subsequence_2 : SystemFunction, ICallable
    {

        // fall through  (for example, in 1.0 mode start can be a StringValue ...)
        public override string StreamerName => "Subsequence";
        public override int GetSpecialProperties(Expression[] arguments)
        {
            return arguments[0].GetSpecialProperties();
        }

        public override int GetCardinality(Expression[] arguments)
        {
            return arguments[0].GetCardinality() | StaticProperty.ALLOWS_ZERO_OR_ONE;
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            return SequenceTool.ToLazySequence(SubSequence(arguments[0].Iterate(), (NumericValue)arguments[1].Head()));
        }

        public static ISequenceIterator SubSequence(ISequenceIterator seq, NumericValue startVal)
        {
            long lstart;
            if (startVal is Int64Value)
            {
                lstart = startVal.LongValue();
                if (lstart <= 1)
                {
                    return seq;
                }
            }
            else if (startVal.IsNaN())
            {
                return EmptyIterator.GetInstance();
            }
            else
            {
                startVal = startVal.Round(0);
                if (startVal.CompareTo(Int64Value.PLUS_ONE) <= 0)
                {
                    return seq;
                }
                else if (startVal.CompareTo(Int64Value.MAX_LONG) > 0)
                {
                    return EmptyIterator.GetInstance();
                }
                else
                {
                    lstart = startVal.LongValue();
                }
            }

            if (lstart > int.MaxValue)
            {

                // we don't allow sequences longer than an this
                return EmptyIterator.GetInstance();
            }

            return TailIterator.Make(seq, (int)lstart);
        }

        public override Expression MakeFunctionCall(Expression[] arguments)
        {

            // Handle the case where the second argument is known statically
            try
            {
                if (Literal.IsAtomic(arguments[1]) && !(arguments[0] is ErrorExpression))
                {
                    NumericValue start = (NumericValue)((Literal)arguments[1]).GroundedValue;
                    start = start.Round(0);
                    long intStart = start.LongValue();
                    if (intStart > int.MaxValue)
                    {

                        // Handle this case dynamically. Test case cbcl-subsequence-012
                        return base.MakeFunctionCall(arguments);
                    }

                    if (intStart <= 0)
                    {
                        return arguments[0];
                    }

                    return new TailExpression(arguments[0], (int)intStart);
                }
            }
            catch (Exception)
            {
            }

            return base.MakeFunctionCall(arguments);
        }
    }
}
