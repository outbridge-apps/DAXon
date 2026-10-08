////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2020 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Functions.HigherOrder
{
    /// <summary>
    /// This class implements the function fn:for-each-pair() (formerly fn:map-pairs()), which is a standard function in XQuery 3.0
    /// </summary>
    internal sealed class ForEachPairFn : SystemFunction
    {
        public override ItemType GetResultItemType(Expression[] args)
        {

            // IItem type of the result is the same as the result item type of the function
            ItemType fnType = args[2].GetItemType();
            if (fnType is SpecificFunctionType)
            {
                return ((SpecificFunctionType)fnType).ResultType.PrimaryType;
            }
            else
            {
                return AnyItemType.GetInstance();
            }
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            return SequenceTool.ToLazySequence(EvalMapPairs((IFunctionItem)arguments[2].Head(), arguments[0].Iterate(), arguments[1].Iterate(), context));
        }

        private ISequenceIterator EvalMapPairs(IFunctionItem function, ISequenceIterator seq0, ISequenceIterator seq1, IXPathContext context)
        {
            // Reused-frame invoker for the per-pair call, same contract as fold-left's use: every
            // result is materialized before the next call reuses the frame, and TryMake's
            // plain-body gate keeps closures (which could capture the reused frame) out of the
            // results. Pairs are processed and results concatenated in the same order as the
            // general path below.
            FusedArity2Caller fused = FusedArity2Caller.TryMake(function, context);
            if (fused != null)
            {
                return new FusedPairsIterator(seq0, seq1, fused);
            }

            PairedSequenceIterator pairs = new PairedSequenceIterator(seq0, seq1);
            return MappingIterator.IMap(pairs, (item) =>
            {
                Core.Controller.CheckActiveTimeoutPerStep();
                ISequence[] pair = ((ObjectValue<ISequence[]>)item).GetObject();
                return DynamicCall(function, context, pair).Iterate();
            });
        }

        private sealed class FusedPairsIterator : ISequenceIterator
        {
            private readonly ISequenceIterator seq0;
            private readonly ISequenceIterator seq1;
            private readonly FusedArity2Caller fused;
            private ISequenceIterator current;   // rest of the current call's (grounded) result

            internal FusedPairsIterator(ISequenceIterator seq0, ISequenceIterator seq1, FusedArity2Caller fused)
            {
                this.seq0 = seq0;
                this.seq1 = seq1;
                this.fused = fused;
            }

            public IItem Next()
            {
                while (true)
                {
                    if (current != null)
                    {
                        IItem rest = current.Next();
                        if (rest != null)
                        {
                            return rest;
                        }

                        current = null;
                    }

                    IItem i0 = seq0.Next();
                    if (i0 == null)
                    {
                        Dispose();
                        return null;
                    }

                    IItem i1 = seq1.Next();
                    if (i1 == null)
                    {
                        Dispose();
                        return null;
                    }

                    Core.Controller.CheckActiveTimeoutPerStep();
                    ISequence result = fused.CallTwo(i0, i1);
                    if (result is IItem one)
                    {
                        return one;   // grounded singleton — the common case, no sub-iterator
                    }

                    ISequenceIterator it = result.Iterate();
                    IItem first = it.Next();
                    if (first != null)
                    {
                        current = it;
                        return first;
                    }
                    // empty result for this pair: move on to the next pair
                }
            }

            public void Dispose()
            {
                seq0.Dispose();
                seq1.Dispose();
            }
        }

        private sealed class PairedSequenceIterator : ISequenceIterator
        {
            private readonly ISequenceIterator seq0;
            private readonly ISequenceIterator seq1;
            private readonly ISequence[] args = new ISequence[2];
            public PairedSequenceIterator(ISequenceIterator seq0, ISequenceIterator seq1)
            {
                this.seq0 = seq0;
                this.seq1 = seq1;
            }

            public ObjectValue<ISequence[]> Next()
            {
                IItem i0 = seq0.Next();
                if (i0 == null)
                {
                    Dispose();
                    return null;
                }

                IItem i1 = seq1.Next();
                if (i1 == null)
                {
                    Dispose();
                    return null;
                }

                args[0] = i0;
                args[1] = i1;
                return new ObjectValue<ISequence[]>(args);
            }

            public void Dispose()
            {
                seq0.Dispose();
                seq1.Dispose();
            }
            IItem ISequenceIterator.Next() => Next();
        }
    }
}
