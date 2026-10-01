////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
//import com.saxonica.ee.stream.ManualGroupIterator;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Expressions.Sorting
{
    internal sealed class GroupBreakingIterator : ILookaheadIterator, IGroupIterator
    {
        private readonly IPullEvaluator select;
        private readonly IFocusIterator population;
        private readonly IFunctionItem breakWhen;
        private readonly IXPathContext baseContext;
        private readonly IXPathContext runningContext;
        private IList<IItem> currentMembers;
        private IItem nextItem;
        private IItem current = null;
        private int position = 0;

        public bool HasNext => nextItem != null;
        public GroupBreakingIterator(IPullEvaluator select, IFunctionItem breakWhen, IXPathContext baseContext)
        {
            this.select = select;
            this.breakWhen = breakWhen;
            this.baseContext = baseContext;
            this.runningContext = baseContext.NewMinorContext();
            this.population = runningContext.TrackFocus(select.Iterate(baseContext));
            nextItem = population.Next();
        }

        private void Advance()
        {
            currentMembers = new List<IItem>(20);
            currentMembers.Add(current);
            while (true)
            {
                IItem nextCandidate = population.Next();
                if (nextCandidate == null)
                {
                    break;
                }

                BooleanValue result = (BooleanValue)breakWhen.Call(runningContext, new ISequence[] { SequenceExtent.MakeSequenceExtent(currentMembers), nextCandidate }).Head();
                try
                {
                    if (!result.GetBooleanValue())
                    {
                        currentMembers.Add(nextCandidate);
                    }
                    else
                    {
                        nextItem = nextCandidate;
                        return;
                    }
                }
                catch (InvalidCastException e)
                {
                    throw new XPathException("Grouping key values are of non-comparable types").AsTypeError().WithXPathContext(runningContext);
                }
            }

            nextItem = null;
        }

        public IAtomicSequence GetCurrentGroupingKey()
        {
            return null;
        }

        public IGroundedValue CurrentGroup()
        {
            return SequenceExtent.MakeSequenceExtent(currentMembers);
        }

        public bool SupportsHasNext()
        {
            return true;
        }

        public IItem Next()
        {
            try
            {
                if (nextItem == null)
                {
                    current = null;
                    position = -1;
                    return null;
                }

                current = nextItem;
                position++;
                Advance();
                return current;
            }
            catch (XPathException e)
            {
                throw new UncheckedXPathException(e);
            }
        }

        public void Dispose()
        {
            population.Dispose();
        }
    }
}