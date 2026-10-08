////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Collections;
namespace OutSmart.DAXon.Regex
{
    /// <summary>
    /// End of program in a regular expression
    /// </summary>
    internal sealed class OpEndProgram : Operation
    {
        public override int MatchLength => 0;

        public override int MatchesEmptyString()
        {
            return MATCHES_ZLS_ANYWHERE;
        }

        public override IIntIterator IterateMatches(REMatcher matcher, int position)
        {

            // An anchored match is successful only if we are at the end of the string.
            // Otherwise, match has succeeded unconditionally
            if (matcher.anchoredMatch)
            {
                if (position >= matcher.search.Length())
                {
                    return new IntSingletonIterator(position);
                }
                else
                {
                    return EmptyIntIterator.GetInstance();
                }
            }
            else
            {
                matcher.SetParenEnd(0, position);
                return new IntSingletonIterator(position);
            }
        }

        public override string Display()
        {
            return "\\Z";
        }
    }
}