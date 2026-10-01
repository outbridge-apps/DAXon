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
    /// This class implements the function fn:fold-left(), which is a standard function in XPath 3.0
    /// </summary>
    internal sealed class FoldLeftFn : FoldingFunction
    {
        public override IFold GetFold(IXPathContext context, params ISequence[] arguments)
        {
            ISequence arg0 = arguments[0];
            return new FoldLeftFold(context, arg0.Materialize(), (IFunctionItem)arguments[1].Head());
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            // Range-aware long lane: the base values are the range's own longs, so the base
            // iterator's per-item boxing and the IFold ceremony drop out entirely. Step is
            // always 1 for `to`-ranges; anything else keeps the generic path.
            ISequenceIterator it = arguments[0].Iterate();
            if (it is Expressions.AscendingRangeIterator range && range.GetStep().LongValue() == 1)
            {
                IFunctionItem fn = (IFunctionItem)arguments[2].Head();
                arguments[2] = (ISequence)fn;   // keep the fallback single-read
                FusedArity2Caller fused = FusedArity2Caller.TryMake(fn, context);
                FusedArity2Caller.LongBody lane = fused == null ? null : fused.TryLongLane();
                if (lane != null)
                {
                    IGroundedValue zero = arguments[1].Materialize();
                    arguments[1] = zero;
                    if (zero is Values.Int64Value z && ReferenceEquals(z.GetItemType(), Types.BuiltInAtomicType.INTEGER))
                    {
                        long start = range.GetMin().LongValue();
                        long limit = range.GetMax().LongValue();
                        long n = limit - start + 1;   // <= 2^31 by range construction, so no overflow
                        long acc = z.LongValue();
                        long v = start;
                        for (long i = 0; i < n; i++, v++)
                        {
                            Core.Controller.CheckActiveTimeout();
                            if (!lane(acc, v, out long next))
                            {
                                // Guard tripped: this value and the rest replay on the boxed path.
                                ISequence data = Values.Int64Value.MakeIntegerValue(acc);
                                for (; i < n; i++, v++)
                                {
                                    data = fused.CallTwo(data, Values.Int64Value.MakeIntegerValue(v));
                                }

                                return data;
                            }

                            acc = next;
                        }

                        return Values.Int64Value.MakeIntegerValue(acc);
                    }
                }
            }

            return RunFold(GetFold(context, TailArguments(arguments)), it);
        }

        public override ItemType GetResultItemType(Expression[] args)
        {

            // IItem type of the result is the same as the result item type of the argument function
            ItemType functionArgType = args[2].GetItemType();
            if (functionArgType is AnyFunctionType)
            {

                // will always be true once the query has been successfully type-checked
                return ((AnyFunctionType)args[2].GetItemType()).ResultType.PrimaryType;
            }
            else
            {
                return AnyItemType.GetInstance();
            }
        }

        internal sealed class FoldLeftFold : IFold
        {
            private readonly IXPathContext context;
            private readonly IFunctionItem function;
            private readonly FusedArity2Caller fused;
            private readonly FusedArity2Caller.LongBody lane;
            private long acc;
            private bool laneActive;
            private ISequence data;
            private int counter;
            public FoldLeftFold(IXPathContext context, IGroundedValue zero, IFunctionItem function)
            {
                this.context = context;
                this.function = function;
                this.fused = FusedArity2Caller.TryMake(function, context);
                this.data = zero;
                this.counter = 0;
                // Long lane: plain xs:integer only — a subtype-labelled zero/item must stay boxed
                // (an identity body returns the labelled instance itself on the boxed path).
                if (fused != null && zero is Values.Int64Value z
                    && ReferenceEquals(z.GetItemType(), Types.BuiltInAtomicType.INTEGER))
                {
                    lane = fused.TryLongLane();
                    if (lane != null)
                    {
                        acc = z.LongValue();
                        laneActive = true;
                    }
                }
            }

            public void ProcessItem(IItem item)
            {
                if (laneActive)
                {
                    if (item is Values.Int64Value iv
                        && ReferenceEquals(iv.GetItemType(), Types.BuiltInAtomicType.INTEGER)
                        && lane(acc, iv.LongValue(), out long next))
                    {
                        acc = next;
                        return;
                    }

                    // Guard tripped or non-plain-integer item: rejoin the boxed path from the
                    // current accumulator; nothing was consumed, this item replays boxed.
                    laneActive = false;
                    data = Values.Int64Value.MakeIntegerValue(acc);
                }

                if (fused != null)
                {
                    // Reused-frame invoker; results come back materialized, so no memo wrapping.
                    data = fused.CallTwo(data, item);
                    return;
                }

                ISequence[] args = new ISequence[2];
                args[0] = data;
                args[1] = item;

                // The result can be returned as a LazySequence. Since we are passing it to a user-defined
                // function which can read it repeatedly, we need at the very least to wrap it in a MemoSequence.
                // But wrapping MemoSequences too deeply can cause a StackOverflow when the unwrapping finally
                // takes place; so to avoid this, we periodically ground the value as a real in-memory concrete
                // sequence. We don't want to do this every time because it involves allocating memory.
                ISequence result = DynamicCall(function, context, args);
                if (counter++ % 32 == 0)
                {
                    data = result.Materialize();
                }
                else
                {
                    data = result;
                }
            }

            public bool IsFinished()
            {
                return false;
            }

            public ISequence Result()
            {
                return laneActive ? Values.Int64Value.MakeIntegerValue(acc) : data;
            }
        }
    }
}