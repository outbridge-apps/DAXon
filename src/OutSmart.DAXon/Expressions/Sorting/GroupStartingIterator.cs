////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using System.Collections.Generic;
namespace OutSmart.DAXon.Expressions.Sorting
{
    internal sealed class GroupStartingIterator : GroupMatchingIterator, ILookaheadIterator, IGroupIterator
    {
        public GroupStartingIterator(IPullEvaluator select, Patterns.Pattern startPattern, IXPathContext context) : base(select, startPattern, context)
        {
        }

        protected override GroupMatchingIterator Restart()
        {
            return new GroupStartingIterator(select, pattern, baseContext);
        }

        protected override void Advance()
        {
            currentMembers = new List<IItem>(10);
            currentMembers.Add(current);
            Core.MemoryLedger.Collected(currentMembers);
            while (true)
            {
                IItem nextCandidate = population.Next();
                if (nextCandidate == null)
                {
                    break;
                }

                if (pattern.MatchesItem(nextCandidate, runningContext))
                {
                    nextItem = nextCandidate;
                    return;
                }
                else
                {
                    currentMembers.Add(nextCandidate);
                    Core.MemoryLedger.Collected(currentMembers);
                }
            }

            nextItem = null;
        }
    }
}
