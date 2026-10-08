////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
//import com.saxonica.ee.stream.ManualGroupIterator;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Expressions.Sorting
{
    internal sealed class GroupAdjacentIterator : IGroupIterator, ILastPositionFinder, ILookaheadIterator
    {
        private readonly IPullEvaluator select;
        private readonly IFocusIterator population;
        private readonly Expression keyExpression;
        private readonly IItemEvaluator keyItem;   // non-composite + statically single key: no per-item iterator
        private readonly IPullEvaluator keyEval;   // built once — Expression.Iterate re-elaborates per call
        private readonly IStringCollator collator;
        private readonly IXPathContext baseContext;
        private readonly IXPathContext runningContext;
        private readonly int implicitTimezone;
        private CompositeAtomicKey currentComparisonKey;
        private IAtomicSequence currentKey;
        private IList<IItem> currentMembers;
        private CompositeAtomicKey nextComparisonKey;
        private IList<AtomicValue> nextKey;
        // Non-composite groups compare exactly one match key per item; holding it directly skips
        // the two per-item lists, the CompositeAtomicKey wrapper and its SequenceEqual enumerators.
        private IAtomicMatchKey currentMatchKey;
        private IAtomicMatchKey nextMatchKey;
        private AtomicValue nextSingleKey;
        private IItem nextItem;
        private IItem current;
        private readonly bool composite;

        public bool HasNext => nextItem != null;
        public GroupAdjacentIterator(IPullEvaluator select, Expression keyExpression, IXPathContext baseContext, IStringCollator collator, bool composite)
        {
            this.select = select;
            this.keyExpression = keyExpression;
            Elaborator keyElab = keyExpression.MakeElaborator();
            this.keyItem = !composite && !Cardinality.AllowsMany(keyExpression.GetCardinality()) ? keyElab.ElaborateForItem() : null;
            this.keyEval = keyItem == null ? keyElab.ElaborateForPull() : null;
            this.baseContext = baseContext;
            this.runningContext = baseContext.NewMinorContext();
            this.population = runningContext.TrackFocus(select.Iterate(baseContext));
            this.collator = collator;
            this.composite = composite;
            this.implicitTimezone = baseContext.GetImplicitTimezone();
            nextItem = population.Next();
            if (nextItem != null)
            {
                if (composite)
                {
                    nextKey = GetKey(runningContext);
                    nextComparisonKey = GetComparisonKey(nextKey, baseContext);
                }
                else
                {
                    nextSingleKey = GetSingleKey(runningContext);
                    nextMatchKey = MatchKeyOf(nextSingleKey);
                }
            }
        }

        public bool SupportsGetLength()
        {
            return true;
        }

        public int GetLength()
        {
            try
            {
                GroupAdjacentIterator another = new GroupAdjacentIterator(select, keyExpression, baseContext, collator, composite);
                return Count.SteppingCount(another);
            }
            catch (XPathException e)
            {
                throw new UncheckedXPathException(e);
            }
        }

        // Non-composite key: exactly one atomic value, enforced with the same XTTE1100 text
        // (the error path re-counts so "found N" matches the list-based message).
        private AtomicValue GetSingleKey(IXPathContext context)
        {
            if (keyItem != null)
            {
                AtomicValue val = (AtomicValue)keyItem.Eval(context);
                if (val == null)
                {
                    throw new XPathException(
                        "A grouping key value (group-adjacent) must be a single atomic value unless composite='yes'; found 0",
                        "XTTE1100");
                }

                return val;
            }

            ISequenceIterator iter = keyEval.Iterate(context);
            AtomicValue first = (AtomicValue)iter.Next();
            if (first != null && iter.Next() == null)
            {
                return first;
            }

            int found = first == null ? 0 : 2;
            if (found == 2)
            {
                while (iter.Next() != null)
                {
                    found++;
                }
            }

            throw new XPathException(
                "A grouping key value (group-adjacent) must be a single atomic value unless composite='yes'; found " + found,
                "XTTE1100");
        }

        private IAtomicMatchKey MatchKeyOf(AtomicValue key)
        {
            return key.IsNaN() ? DistinctValues.NaN_MATCH_KEY : key.GetXPathMatchKey(collator, implicitTimezone);
        }

        // Composite path only — non-composite groups go through GetSingleKey.
        private IList<AtomicValue> GetKey(IXPathContext context)
        {
            IList<AtomicValue> key = new List<AtomicValue>();
            ISequenceIterator iter = keyEval.Iterate(context);
            while (true)
            {
                AtomicValue val = (AtomicValue)iter.Next();
                if (val == null)
                {
                    break;
                }

                key.Add(val);
            }

            return key;
        }

        private CompositeAtomicKey GetComparisonKey(IList<AtomicValue> key, IXPathContext keyContext)
        {
            IList<IAtomicMatchKey> ckey = new List<IAtomicMatchKey>(key.Count);
            foreach (AtomicValue aKey in key)
            {
                IAtomicMatchKey comparisonKey;
                if (aKey.IsNaN())
                {
                    comparisonKey = DistinctValues.NaN_MATCH_KEY;
                }
                else
                {
                    comparisonKey = aKey.GetXPathMatchKey(collator, keyContext.GetImplicitTimezone());
                }

                ckey.Add(comparisonKey);
            }

            return new CompositeAtomicKey(ckey);
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

                if (composite)
                {
                    IList<AtomicValue> newKey = GetKey(runningContext);
                    CompositeAtomicKey newComparisonKey = GetComparisonKey(newKey, baseContext);
                    try
                    {
                        if (newComparisonKey.Equals(currentComparisonKey))
                        {
                            currentMembers.Add(nextCandidate);
                        }
                        else
                        {
                            nextItem = nextCandidate;
                            nextComparisonKey = newComparisonKey;
                            nextKey = newKey;
                            return;
                        }
                    }
                    catch (InvalidCastException)
                    {
                        throw new XPathException("Grouping key values are of non-comparable types").AsTypeError().WithXPathContext(runningContext);
                    }
                }
                else
                {
                    AtomicValue newSingleKey = GetSingleKey(runningContext);
                    IAtomicMatchKey newMatchKey = MatchKeyOf(newSingleKey);
                    try
                    {
                        // Equals(a, b) mirrors SequenceEqual's default comparer on the old
                        // one-element key lists (null-safe, then a.Equals(b)).
                        if (Equals(newMatchKey, currentMatchKey))
                        {
                            currentMembers.Add(nextCandidate);
                        }
                        else
                        {
                            nextItem = nextCandidate;
                            nextMatchKey = newMatchKey;
                            nextSingleKey = newSingleKey;
                            return;
                        }
                    }
                    catch (InvalidCastException)
                    {
                        throw new XPathException("Grouping key values are of non-comparable types").AsTypeError().WithXPathContext(runningContext);
                    }
                }
            }

            nextItem = null;
            nextKey = null;
            nextSingleKey = null;
            nextMatchKey = null;
        }

        public IAtomicSequence GetCurrentGroupingKey()
        {
            return currentKey;
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
                    return null;
                }

                current = nextItem;
                if (composite)
                {
                    if (nextKey.Count == 1)
                    {
                        currentKey = nextKey[0];
                    }
                    else
                    {
                        currentKey = new AtomicArray(nextKey);
                    }

                    currentComparisonKey = nextComparisonKey;
                }
                else
                {
                    currentKey = nextSingleKey;
                    currentMatchKey = nextMatchKey;
                }

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