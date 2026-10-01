////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;

namespace OutSmart.DAXon.Trees.Iterators
{
    /// <summary>
    /// An iterator that prepends a given item to the items returned by another iterator.
    /// </summary>
    internal sealed class PrependSequenceIterator : ISequenceIterator
    {
        internal IItem start;
        internal readonly ISequenceIterator @base;

        public PrependSequenceIterator(IItem start, ISequenceIterator @base)
        {
            this.start = start;
            this.@base = @base;
        }

        public IItem Next()
        {
            if (start != null)
            {
                IItem temp = start;
                start = null;
                return temp;
            }
            else
            {
                return @base.Next();
            }
        }

        public void Dispose()
        {
            @base.Dispose();
        }
    }
}
