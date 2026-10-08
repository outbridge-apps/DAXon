////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Regex
{
    /// <summary>
    /// A ATokenIterator is an iterator over the strings that result from tokenizing a string using a regular expression
    /// </summary>
    internal sealed class ATokenIterator : IAtomicIterator
    {
        private readonly UnicodeString input;
        private readonly REMatcher matcher;
        private StringValue current;
        private int prevEnd;
        public ATokenIterator(UnicodeString input, REMatcher matcher)
        {
            this.input = input;
            this.matcher = matcher;
            prevEnd = 0;
        }

        public StringValue Next()
        {
            if (prevEnd < 0)
            {
                current = null;
                return null;
            }

            bool matched;
            try
            {
                matched = matcher.Match(input, prevEnd);
            }
            catch (RecursionDepthError e) when (!e.Described)
            {
                // Same as ARegexIterator: described, not wrapped.
                throw ARegularExpression.DescribeOverflow(e);
            }

            if (matched)
            {
                int start = matcher.GetParenStart(0);
                current = new StringValue(input.Substring(prevEnd, start));
                prevEnd = matcher.GetParenEnd(0);
            }
            else
            {
                current = new StringValue(input.Substring(prevEnd));
                prevEnd = -1;
            }

            return CurrentStringValue();
        }

        private StringValue CurrentStringValue()
        {
            return current;
        }
        AtomicValue IAtomicIterator.Next() => Next();
        IItem ISequenceIterator.Next() => Next();
        public void Dispose() { }
    }
}

