////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Trees.Iterators
{
    internal sealed class ManualIterator : IFocusIterator, ISequenceIterator, IReversibleIterator, ILastPositionFinder, IGroundedIterator, ILookaheadIterator
    {
        private IItem item;
        private int _position;
        private Func<int> lengthFinder;

        public bool HasNext => Position() != GetLength();
        public ManualIterator()
        {
            item = null;
            _position = 0;
        }

        public ManualIterator(IItem value, int position)
        {
            this.item = value;
            this._position = position;
        }

        public ManualIterator(IItem value)
        {
            this.item = value;
            this._position = 1;
            this.lengthFinder = () => 1;
        }

        public void SetContextItem(IItem value)
        {
            this.item = value;
        }

        public void SetLengthFinder(Func<int> finder)
        {
            this.lengthFinder = finder;
        }

        public void SetPosition(int position)
        {
            this._position = position;
        }

        public bool SupportsHasNext()
        {
            return true;
        }

        public IItem Next()
        {
            return null;
        }

        public IItem Current()
        {
            return item;
        }

        public int Position()
        {
            return _position;
        }

        public bool SupportsGetLength()
        {
            return true;
        }

        public int GetLength()
        {
            if (lengthFinder == null)
            {
                throw new UncheckedXPathException("Saxon streaming restriction: last() cannot be used when consuming a sequence of streamed nodes, even if the items being processed are grounded");
            }
            else
            {
                return lengthFinder();
            }
        }

        public bool IsActuallyGrounded()
        {
            return true;
        }

        public ManualIterator GetReverseIterator()
        {
            return new ManualIterator(item);
        }

        public IGroundedValue Materialize()
        {
            return item;
        }

        public IGroundedValue GetResidue()
        {
            return item;
        }
        ISequenceIterator IReversibleIterator.GetReverseIterator() => GetReverseIterator();
        public void Dispose() { }
    }
}

