////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Sorting;

using OutSmart.DAXon.Regex.CharClass;
using OutSmart.DAXon.Text;
using System.Text;
using OutSmart.DAXon.Collections;
namespace OutSmart.DAXon.Regex
{
    /// <summary>
    /// A match of a single character in the input against a set of permitted characters
    /// </summary>
    internal sealed class OpCharClass : Operation
    {
        private readonly IIntPredicateProxy predicate;

        public IIntPredicateProxy Predicate => predicate;

        public override int MatchLength => 1;
        public OpCharClass(IIntPredicateProxy predicate)
        {
            this.predicate = predicate;
        }

        public override int MatchesEmptyString()
        {
            return MATCHES_ZLS_NEVER;
        }

        public override ICharacterClass GetInitialCharacterClass(bool caseBlind)
        {
            if (predicate is ICharacterClass)
            {
                return (ICharacterClass)predicate;
            }
            else
            {
                return base.GetInitialCharacterClass(caseBlind);
            }
        }

        public override IIntIterator IterateMatches(REMatcher matcher, int position)
        {
            UnicodeString @in = matcher.search;
            if (position < @in.Length() && predicate.Test(@in.CodePointAt(position)))
            {
                return new IntSingletonIterator(position + 1);
            }
            else
            {
                return EmptyIntIterator.GetInstance();
            }
        }

        public override string Display()
        {
            if (predicate is IntSetPredicate)
            {
                IntSet s = ((IntSetPredicate)predicate).GetIntSet();
                if (s is IntSingletonSet)
                {
                    return "" + (char)((IntSingletonSet)s).Member;
                }
                else if (s is IntRangeSet)
                {
                    StringBuilder fsb = new StringBuilder(64);
                    IntRangeSet irs = (IntRangeSet)s;
                    fsb.Append('[');
                    for (int i = 0; i < irs.NumberOfRanges; i++)
                    {
                        fsb.Append((char)irs.StartPoints[1]);
                        fsb.Append('-');
                        fsb.Append((char)irs.EndPoints[1]);
                    }

                    fsb.Append('[');
                    return fsb.ToString();
                }
                else
                {
                    return "[....]";
                }
            }
            else
            {
                return "[....]";
            }
        }
    }
}