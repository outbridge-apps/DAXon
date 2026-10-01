////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Expressions.Sorting
{
    internal sealed class GroupEndingIterator : GroupMatchingIterator, IGroupIterator, ILookaheadIterator
    {
        public GroupEndingIterator(IPullEvaluator select, Patterns.Pattern endPattern, IXPathContext context)
        {
            this.select = select;
            this.pattern = endPattern;
            baseContext = context;
            runningContext = context.NewMinorContext();
            this.population = runningContext.TrackFocus(select.Iterate(context));

            // the first item in the population always starts a new group
            nextItem = population.Next();
        }

        public override bool SupportsGetLength()
        {
            return true;
        }

        public override int GetLength()
        {
            try
            {
                GroupEndingIterator another = new GroupEndingIterator(select, pattern, baseContext);
                return Count.SteppingCount(another);
            }
            catch (XPathException e)
            {
                throw new UncheckedXPathException(e);
            }
        }

        protected override void Advance()
        {
            currentMembers = new List<IItem>(20);
            currentMembers.Add(current);
            nextItem = current;
            while (nextItem != null)
            {
                if (pattern.MatchesItem(nextItem, runningContext))
                {
                    nextItem = population.Next();
                    if (nextItem != null)
                    {
                        break;
                    }
                }
                else
                {
                    nextItem = population.Next();
                    if (nextItem != null)
                    {
                        currentMembers.Add(nextItem);
                    }
                }
            }
        }
    }
}
