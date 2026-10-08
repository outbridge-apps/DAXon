////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Trees.Iterators
{
    internal sealed class UntypedAtomizingIterator : ISequenceIterator, ILastPositionFinder, ILookaheadIterator
    {
        private readonly ISequenceIterator @base;

        public bool HasNext => ((ILookaheadIterator)@base).HasNext;
        public UntypedAtomizingIterator(ISequenceIterator @base)
        {
            this.@base = @base;
        }

        public AtomicValue Next()
        {
            try
            {
                IItem nextSource = @base.Next();
                if (nextSource == null)
                {
                    return null;
                }
                else
                {
                    return (AtomicValue)nextSource.Atomize();
                }
            }
            catch (XPathException e)
            {
                throw new UncheckedXPathException(e);
            }
        }

        public void Dispose()
        {
            @base.Dispose();
        }

        public bool SupportsGetLength()
        {
            return SequenceTool.SupportsGetLength(@base);
        }

        public int GetLength()
        {
            return SequenceTool.GetLength(@base);
        }

        public bool SupportsHasNext()
        {
            return @base is ILookaheadIterator && ((ILookaheadIterator)@base).SupportsHasNext();
        }
        IItem ISequenceIterator.Next() => Next();
    }
}
