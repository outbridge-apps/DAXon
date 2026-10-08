////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Trees.Iterators
{
    internal sealed class LookaheadIteratorImpl : ILookaheadIterator
    {
        private readonly ISequenceIterator @base;
        private IItem _next;

        public bool HasNext => _next != null;
        private LookaheadIteratorImpl(ISequenceIterator @base)
        {
            this.@base = @base;
            _next = @base.Next();
        }

        public static ILookaheadIterator MakeLookaheadIterator(ISequenceIterator @base)
        {
            if (@base is ILookaheadIterator && ((ILookaheadIterator)@base).SupportsHasNext())
            {
                return (ILookaheadIterator)@base;
            }
            else
            {
                return new LookaheadIteratorImpl(@base);
            }
        }

        public bool SupportsHasNext()
        {
            return true;
        }

        public IItem Next()
        {
            IItem current = _next;
            if (_next != null)
            {
                _next = @base.Next();
            }

            return current;
        }

        public void Dispose()
        {
            @base.Dispose();
        }
    }
}