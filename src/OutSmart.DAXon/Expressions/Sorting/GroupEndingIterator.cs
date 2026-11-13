////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Expressions.Sorting
{
    internal sealed class GroupEndingIterator : GroupMatchingIterator, IGroupIterator, ILookaheadIterator
    {
        public GroupEndingIterator(IPullEvaluator select, Patterns.Pattern endPattern, IXPathContext context) : base(select, endPattern, context)
        {
        }

        protected override GroupMatchingIterator Restart()
        {
            return new GroupEndingIterator(select, pattern, baseContext);
        }

        protected override void Advance()
        {
            currentMembers = new List<IItem>(20);
            currentMembers.Add(current);
            Core.MemoryLedger.Collected(currentMembers);
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
                        Core.MemoryLedger.Collected(currentMembers);
                    }
                }
            }
        }
    }
}
