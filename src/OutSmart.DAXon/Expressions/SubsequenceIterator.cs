////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using System;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Expressions
{
    /// <summary>
    /// A SubsequenceIterator selects a subsequence of a sequence
    /// </summary>
    internal sealed class SubsequenceIterator : ISequenceIterator, ILastPositionFinder, ILookaheadIterator
    {
        private readonly ISequenceIterator @base;
        private int basePosition = 0;
        private readonly int min;
        private readonly int max;
        private IItem nextItem = null;

        /// <summary>
        /// Test whether there are any more items available in the sequence
        /// </summary>
        public bool HasNext => nextItem != null;
        private SubsequenceIterator(ISequenceIterator @base, int min, int max)
        {
            this.@base = @base;
            this.min = min;
            if (min < 1)
            {
                min = 1;
            }

            this.max = max;
            if (max < min)
            {
                nextItem = null;
                return;
            }

            int i = 1;
            while (i++ <= min)
            {
                nextItem = @base.Next();
                basePosition++;
                if (nextItem == null)
                {
                    break;
                }
            }
        }

        public static ISequenceIterator Make(ISequenceIterator @base, int min, int max)
        {
            if (@base is ArrayIterator)
            {
                return ((ArrayIterator)@base).MakeSliceIterator(min, max);
            }
            else if (max == int.MaxValue)
            {
                return TailIterator.Make(@base, min);
            }
            else if (@base is IGroundedIterator && ((IGroundedIterator)@base).IsActuallyGrounded() && min > 4)
            {
                try
                {
                    IGroundedValue value = SequenceTool.ToGroundedValue(@base);
                    value = value.Subsequence(min - 1, max - min + 1);
                    return value.Iterate();
                }
                catch (UncheckedXPathException e)
                {
                    throw e.GetXPathException();
                }
            }
            else
            {
                return new SubsequenceIterator(@base, min, max);
            }
        }

        public bool SupportsHasNext()
        {
            return true;
        }

        public IItem Next()
        {
            if (nextItem == null)
            {
                return null;
            }

            IItem current = nextItem;
            if (basePosition < max)
            {
                nextItem = @base.Next();
                basePosition++;
            }
            else
            {
                nextItem = null;
                @base.Dispose();
            }

            return current;
        }

        public void Dispose()
        {
            @base.Dispose();
        }

        public bool SupportsGetLength()
        {
            return SequenceTool.SupportsGetLength(@base);
        }

        /// <summary>
        /// Get the last position (that @is, the number of items in the sequence).
        /// </summary>
        public int GetLength()
        {
            int lastBase = SequenceTool.GetLength(@base);
            int z = Math.Min(lastBase, max);
            return Math.Max(z - min + 1, 0);
        }
    }
}
