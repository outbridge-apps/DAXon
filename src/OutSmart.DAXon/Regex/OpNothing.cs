////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Collections;
namespace OutSmart.DAXon.Regex
{
    /// <summary>
    /// Match empty string within a regular expression
    /// </summary>
    internal sealed class OpNothing : Operation
    {

        public override int MatchLength => 0;
        public override IIntIterator IterateMatches(REMatcher matcher, int position)
        {
            return new IntSingletonIterator(position);
        }

        public override int MatchesEmptyString()
        {
            return MATCHES_ZLS_ANYWHERE;
        }

        public override string Display()
        {
            return "()";
        }
    }
}