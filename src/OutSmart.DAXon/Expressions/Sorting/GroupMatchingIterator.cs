////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
//import com.saxonica.ee.stream.ManualGroupIterator;

using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Values;
using System.Collections.Generic;
namespace OutSmart.DAXon.Expressions.Sorting
{
    /// <summary>
    /// A GroupMatchingIterator contains code shared between GroupStartingIterator and GroupEndingIterator
    /// </summary>
    internal abstract class GroupMatchingIterator : ILookaheadIterator, ILastPositionFinder, IGroupIterator
    {
        protected IPullEvaluator select;
        protected IFocusIterator population;
        protected Patterns.Pattern pattern;
        protected IXPathContext baseContext;
        protected IXPathContext runningContext;
        protected IList<IItem> currentMembers;
        protected IItem nextItem;
        protected IItem current = null;
        protected int position = 0;

        public virtual bool HasNext => nextItem != null;
        protected abstract void Advance();
        public virtual IAtomicSequence GetCurrentGroupingKey()
        {
            return null;
        }

        public virtual IGroundedValue CurrentGroup()
        {
            return SequenceExtent.MakeSequenceExtent(currentMembers);
        }

        public virtual bool SupportsHasNext()
        {
            return true;
        }

        public virtual IItem Next()
        {
            try
            {
                if (nextItem != null)
                {
                    current = nextItem;
                    position++;
                    Advance();
                    return current;
                }
                else
                {
                    current = null;
                    position = -1;
                    return null;
                }
            }
            catch (XPathException e)
            {
                throw new UncheckedXPathException(e);
            }
        }

        public virtual void Dispose()
        {
            population.Dispose();
        }
        public abstract bool SupportsGetLength();
        public abstract int GetLength();
    }
}
