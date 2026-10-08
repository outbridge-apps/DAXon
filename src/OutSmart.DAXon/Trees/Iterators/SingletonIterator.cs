////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Trees.Iterators
{
    /// <summary>
    /// SingletonIterator: an iterator over a sequence exactly one value
    /// </summary>
    public class SingletonIterator : ISequenceIterator, IFocusIterator, IReversibleIterator, ILastPositionFinder, IGroundedIterator, ILookaheadIterator
    {
        private readonly IItem item;
        private int currentPosition = -1;

        public virtual bool HasNext => currentPosition < 0;

        public virtual IItem Value => item;
        public SingletonIterator(IItem value)
        {

            this.item = value;
        }

        public static ISequenceIterator MakeIterator(IItem item)
        {
            if (item == null)
            {
                return EmptyIterator.GetInstance();
            }
            else
            {
                return new SingletonIterator(item);
            }
        }

        public virtual bool SupportsHasNext()
        {
            return true;
        }

        public virtual IItem Next()
        {
            return ++currentPosition == 0 ? item : null;
        }

        public virtual IItem Current()
        {
            return currentPosition == 0 ? item : null;
        }

        public virtual int Position()
        {
            return currentPosition + 1;
        }

        public virtual void Dispose()
        {
        }

        // no action
        public virtual bool SupportsGetLength()
        {
            return true;
        }

        public virtual int GetLength()
        {
            return 1;
        }

        public virtual bool IsActuallyGrounded()
        {
            return true;
        }

        public virtual SingletonIterator GetReverseIterator()
        {
            return new SingletonIterator(item);
        }

        public virtual IGroundedValue Materialize()
        {
            return item;
        }

        public virtual IGroundedValue GetResidue()
        {
            return currentPosition < 0 ? item : EmptySequence.GetInstance();
        }
        ISequenceIterator IReversibleIterator.GetReverseIterator() => GetReverseIterator();
    }
}
