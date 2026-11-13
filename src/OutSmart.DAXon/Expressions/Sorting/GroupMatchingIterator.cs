////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Functions;
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
        protected readonly IPullEvaluator select;
        protected readonly IFocusIterator population;
        protected readonly Patterns.Pattern pattern;
        protected readonly IXPathContext baseContext;
        protected readonly IXPathContext runningContext;
        protected IList<IItem> currentMembers;
        protected IItem nextItem;
        protected IItem current;
        protected int position;

        protected GroupMatchingIterator(IPullEvaluator select, Patterns.Pattern pattern, IXPathContext context)
        {
            this.select = select;
            this.pattern = pattern;
            baseContext = context;
            runningContext = context.NewMinorContext();
            population = runningContext.TrackFocus(select.Iterate(context));

            // the first item in the population always starts a new group
            nextItem = population.Next();
        }

        public virtual bool HasNext => nextItem != null;
        protected abstract void Advance();

        // A fresh iterator over the same population, counted for last().
        protected abstract GroupMatchingIterator Restart();
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

        public bool SupportsGetLength()
        {
            return true;
        }

        public int GetLength()
        {
            try
            {
                return Count.SteppingCount(Restart());
            }
            catch (XPathException e)
            {
                throw new UncheckedXPathException(e);
            }
        }
    }
}
