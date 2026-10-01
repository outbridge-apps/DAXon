////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Trees.Iterators;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Expressions
{
    internal sealed class ItemCheckingIterator : ISequenceIterator, ILookaheadIterator, ILastPositionFinder
    {
        private readonly ISequenceIterator @base;
        private readonly Action<IItem> action;

        public bool HasNext => ((ILookaheadIterator)@base).HasNext;
        public ItemCheckingIterator(ISequenceIterator @base, Action<IItem> action)
        {
            this.@base = @base;
            this.action = action;
        }

        public bool SupportsHasNext()
        {
            return @base is ILookaheadIterator && ((ILookaheadIterator)@base).SupportsHasNext();
        }

        public IItem Next()
        {
            IItem nextSource = @base.Next();
            if (nextSource == null)
            {
                return null;
            }


            // Call the supplied checking function
            action(nextSource);
            return nextSource;
        }

        public void Dispose()
        {
            @base.Dispose();
        }

        public bool SupportsGetLength()
        {
            return SequenceTool.SupportsGetLength(@base);
        }

        public int GetLength()
        {
            return SequenceTool.GetLength(@base);
        }
    }
}