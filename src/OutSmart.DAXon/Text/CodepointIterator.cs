////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Trees.Iterators;

using OutSmart.DAXon.Values;
using OutSmart.DAXon.Collections;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Text
{
    /// <summary>
    /// IIterator over a string to produce a sequence of single character strings
    /// </summary>
    internal sealed class CodepointIterator : IAtomicIterator
    {
        readonly IIntIterator codepoints;
        private int steps;
        public CodepointIterator(IIntIterator codepoints)
        {
            this.codepoints = codepoints;
        }

        // The raw codepoint stream, for consumers (fn:sum) that can fold the ints directly
        // instead of boxing an Int64Value per character. Valid only before the first Next().
        internal IIntIterator RawCodepoints => codepoints;

        // A long string gives millions of items, and count() or [last()] pull them all without a look at the limits.
        public AtomicValue Next()
        {
            if ((++steps & 1023) == 0)
            {
                OutSmart.DAXon.Core.Controller.CheckActiveTimeoutNow();
            }

            return codepoints.MoveNext() ? new Int64Value(codepoints.Current) : null;
        }
        IItem ISequenceIterator.Next() => Next();
        public void Dispose() { }
    }
}

