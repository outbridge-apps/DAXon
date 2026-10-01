////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Sorting;using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Regex
{
    /// <summary>
    /// Beginning of Line (^) in a regular expression
    /// </summary>
    internal sealed class OpBOL : Operation
    {
        public override int MatchLength => 0;

        public override int MatchesEmptyString()
        {
            return MATCHES_ZLS_AT_START;
        }

        public override IIntIterator IterateMatches(REMatcher matcher, int position)
        {

            // Fail if we're not at the start of the string
            if (position != 0)
            {

                // If we're multiline matching, we could still be at the start of a line
                if (matcher.program.flags.IsMultiLine())
                {

                    // Continue if at the start of a line
                    if (matcher.IsNewline(position - 1) && !(position >= matcher.search.Length()))
                    {
                        return new IntSingletonIterator(position);
                    }
                }

                return EmptyIntIterator.GetInstance();
            }

            return new IntSingletonIterator(position);
        }

        public override string Display()
        {
            return "^";
        }
    }
}