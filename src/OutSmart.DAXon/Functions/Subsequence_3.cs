////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Expressions;
namespace OutSmart.DAXon.Functions
{
    /// <summary>
    /// Implements the XPath 2.0 subsequence() function with three arguments
    /// </summary>
    internal sealed class Subsequence_3 : SystemFunction, ICallable
    {

        public override string StreamerName => "Subsequence";
        public override int GetSpecialProperties(Expression[] arguments)
        {
            return arguments[0].GetSpecialProperties();
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            return SequenceTool.ToLazySequence(SubSequence(arguments[0].Iterate(), (NumericValue)arguments[1].Head(), (NumericValue)arguments[2].Head(), context));
        }

        public static ISequenceIterator SubSequence(ISequenceIterator seq, NumericValue startVal, NumericValue lengthVal, IXPathContext context)
        {
            if (lengthVal == null)
            {
                return Subsequence_2.SubSequence(seq, startVal);
            }

            if (startVal is Int64Value && lengthVal is Int64Value)
            {

                // Fast path where the second and third arguments evaluate to integers
                long lstart = startVal.LongValue();
                if (lstart > int.MaxValue)
                {
                    return EmptyIterator.GetInstance();
                }

                long llength = lengthVal.LongValue();
                if (llength > int.MaxValue)
                {
                    llength = int.MaxValue;
                }

                if (llength < 1)
                {
                    return EmptyIterator.GetInstance();
                }

                long lend = lstart + llength - 1;
                if (lend < 1)
                {
                    return EmptyIterator.GetInstance();
                }

                int start = lstart < 1 ? 1 : (int)lstart;
                return SubsequenceIterator.Make(seq, start, (int)lend);
            }
            else
            {
                if (startVal.IsNaN())
                {
                    return EmptyIterator.GetInstance();
                }

                if (startVal.CompareTo(Int64Value.MAX_LONG) > 0)
                {
                    return EmptyIterator.GetInstance();
                }

                startVal = startVal.Round(0);
                if (lengthVal.IsNaN())
                {
                    return EmptyIterator.GetInstance();
                }

                lengthVal = lengthVal.Round(0);
                if (lengthVal.CompareTo(Int64Value.ZERO) <= 0)
                {
                    return EmptyIterator.GetInstance();
                }

                NumericValue rend = (NumericValue)ArithmeticExpression.Compute(startVal, Calculator.PLUS, lengthVal, context);
                if (rend.IsNaN())
                {

                    // Can happen when start = -INF, length = +INF
                    return EmptyIterator.GetInstance();
                }

                rend = (NumericValue)ArithmeticExpression.Compute(rend, Calculator.MINUS, Int64Value.PLUS_ONE, context);
                if (rend.CompareTo(Int64Value.ZERO) <= 0)
                {
                    return EmptyIterator.GetInstance();
                }

                long lstart;
                if (startVal.CompareTo(Int64Value.PLUS_ONE) <= 0)
                {
                    lstart = 1;
                }
                else
                {
                    lstart = startVal.LongValue();
                }

                if (lstart > int.MaxValue)
                {
                    return EmptyIterator.GetInstance();
                }

                long lend;
                if (rend.CompareTo(Int64Value.MAX_LONG) >= 0)
                {
                    lend = int.MaxValue;
                }
                else
                {
                    lend = rend.LongValue();
                }

                return SubsequenceIterator.Make(seq, (int)lstart, (int)lend);
            }
        }
    }
}
