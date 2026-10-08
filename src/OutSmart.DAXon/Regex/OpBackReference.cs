////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Sorting;

using OutSmart.DAXon.Text;
using OutSmart.DAXon.Collections;
namespace OutSmart.DAXon.Regex
{
    /// <summary>
    /// A back-reference in a regular expression
    /// </summary>
    internal sealed class OpBackReference : Operation
    {
        int groupNr;
        public OpBackReference(int groupNr)
        {
            this.groupNr = groupNr;
        }

        public override int MatchesEmptyString()
        {
            return 0; // no information available
        }

        public override IIntIterator IterateMatches(REMatcher matcher, int position)
        {

            // Get the start and end of the backref
            int s = matcher.startBackref[groupNr];
            int e = matcher.endBackref[groupNr];

            // We don't know the backref yet
            if (s == -1 || e == -1)
            {
                return EmptyIntIterator.GetInstance();
            }


            // The backref is empty size
            if (s == e)
            {
                return new IntSingletonIterator(position);
            }


            // Get the length of the backref
            int l = e - s;

            // If there's not enough input left, give up.
            UnicodeString search = matcher.search;
            if (position + l - 1 >= search.Length())
            {
                return EmptyIntIterator.GetInstance();
            }


            // Case fold the backref?
            if (matcher.program.flags.IsCaseIndependent())
            {

                // Compare backref to input
                for (int i = 0; i < l; i++)
                {
                    if (!matcher.EqualCaseBlind(search.CodePointAt(position + i), search.CodePointAt(s + i)))
                    {
                        return EmptyIntIterator.GetInstance();
                    }
                }
            }
            else
            {

                // Compare backref to input
                for (int i = 0; i < l; i++)
                {
                    if (search.CodePointAt(position + i) != search.CodePointAt(s + i))
                    {
                        return EmptyIntIterator.GetInstance();
                    }
                }
            }

            return new IntSingletonIterator(position + l);
        }

        public override string Display()
        {
            return "\\" + groupNr;
        }
    }
}