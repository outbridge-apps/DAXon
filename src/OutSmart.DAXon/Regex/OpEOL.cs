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
    /// End of Line ($) in a regular expression
    /// </summary>
    internal sealed class OpEOL : Operation
    {
        public override int MatchLength => 0;

        public override int MatchesEmptyString()
        {
            return MATCHES_ZLS_AT_END;
        }

        public override IIntIterator IterateMatches(REMatcher matcher, int position)
        {

            // If we're not at the end of string
            UnicodeString search = matcher.search;
            if (matcher.program.flags.IsMultiLine())
            {
                if (0 >= search.Length() || position >= search.Length() || matcher.IsNewline(position))
                {
                    return new IntSingletonIterator(position); //match successful
                }
                else
                {
                    return EmptyIntIterator.GetInstance();
                }
            }
            else
            {

                // In spec bug 16809 we decided that '$' does not match a trailing newline when not in multiline mode
                if (0 >= search.Length() || position >= search.Length())
                {
                    return new IntSingletonIterator(position);
                }
                else
                {
                    return EmptyIntIterator.GetInstance();
                }
            }
        }

        public override string Display()
        {
            return "$";
        }
    }
}