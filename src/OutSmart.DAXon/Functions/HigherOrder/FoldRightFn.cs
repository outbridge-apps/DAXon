////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2020 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Functions.HigherOrder
{
    /// <summary>
    /// This class implements the function fn:fold-right(), which is a standard function in XQuery 1.1
    /// </summary>
    internal sealed class FoldRightFn : SystemFunction
    {
        public override ItemType GetResultItemType(Expression[] args)
        {

            // IItem type of the result is the same as the result item type of the function
            ItemType functionArgType = args[2].GetItemType();
            if (functionArgType is AnyFunctionType)
            {

                // will always be true once the query has been successfully type-checked
                return ((AnyFunctionType)functionArgType).ResultType.PrimaryType;
            }
            else
            {
                return AnyItemType.GetInstance();
            }
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            return EvalFoldRight((IFunctionItem)arguments[2].Head(), arguments[1].Materialize(), arguments[0].Iterate(), context);
        }

        private ISequence EvalFoldRight(IFunctionItem function, ISequence zero, ISequenceIterator @base, IXPathContext context)
        {
            FusedArity2Caller fused = FusedArity2Caller.TryMake(function, context);

            // Range-aware long lane (see FoldLeftFn.Call): iterate the range's longs directly,
            // descending, skipping the reverse iterator's per-item boxing.
            if (fused != null && @base is Expressions.AscendingRangeIterator range && range.GetStep().LongValue() == 1)
            {
                FusedArity2Caller.LongBody rangeLane = fused.TryLongLane();
                if (rangeLane != null && zero is Values.Int64Value z0
                    && ReferenceEquals(z0.GetItemType(), Types.BuiltInAtomicType.INTEGER))
                {
                    long start = range.GetMin().LongValue();
                    long limit = range.GetMax().LongValue();
                    long n = limit - start + 1;   // <= 2^31 by range construction
                    long acc = z0.LongValue();
                    long v = limit;
                    for (long i = 0; i < n; i++, v--)
                    {
                        Core.Controller.CheckActiveTimeout();
                        if (!rangeLane(v, acc, out long next))
                        {
                            ISequence zz = Values.Int64Value.MakeIntegerValue(acc);
                            for (; i < n; i++, v--)
                            {
                                zz = fused.CallTwoSeq(Values.Int64Value.MakeIntegerValue(v), zz);
                            }

                            return zz;
                        }

                        acc = next;
                    }

                    return Values.Int64Value.MakeIntegerValue(acc);
                }
            }

            ISequenceIterator reverseBase = Reverse.GetReverseIterator(@base);
            IItem item;
            if (fused != null)
            {
                // Long lane (see FoldLeftFold): fold-right's per-item call is (item, accumulator).
                FusedArity2Caller.LongBody lane = fused.TryLongLane();
                if (lane != null && zero is Values.Int64Value z
                    && ReferenceEquals(z.GetItemType(), Types.BuiltInAtomicType.INTEGER))
                {
                    long acc = z.LongValue();
                    int laneSteps = 0;
                    while ((item = reverseBase.Next()) != null)
                    {
                        if ((++laneSteps & 1023) == 0)
                        {
                            Core.Controller.CheckActiveTimeout();
                        }

                        if (item is Values.Int64Value iv
                            && ReferenceEquals(iv.GetItemType(), Types.BuiltInAtomicType.INTEGER)
                            && lane(iv.LongValue(), acc, out long next))
                        {
                            acc = next;
                            continue;
                        }

                        // Guard tripped: this item and the rest continue on the boxed path.
                        zero = Values.Int64Value.MakeIntegerValue(acc);
                        zero = fused.CallTwoSeq(item, zero);
                        while ((item = reverseBase.Next()) != null)
                        {
                            Core.Controller.CheckActiveTimeoutPerStep();
                            zero = fused.CallTwoSeq(item, zero);
                        }

                        return zero;
                    }

                    return Values.Int64Value.MakeIntegerValue(acc);
                }

                // Reused-frame invoker (same contract as fold-left); results come back materialized.
                while ((item = reverseBase.Next()) != null)
                {
                    Core.Controller.CheckActiveTimeoutPerStep();
                    zero = fused.CallTwoSeq(item, zero);
                }

                return zero;
            }

            ISequence[] args = new ISequence[2];
            while ((item = reverseBase.Next()) != null)
            {
                Core.Controller.CheckActiveTimeoutPerStep();
                args[0] = item;
                args[1] = zero.Materialize();
                try
                {
                    zero = DynamicCall(function, context, args);
                }
                catch (XPathException e)
                {
                    e.MaybeSetContext(context);
                    throw;
                }
            }

            return zero;
        }
    }
}
