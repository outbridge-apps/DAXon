////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
namespace OutSmart.DAXon.Collections
{
    /// <summary>
    /// An immutable integer set containing a single integer
    /// </summary>
    internal sealed class IntSingletonSet : IntSet
    {
        private readonly int value;

        public int Member => value;
        public IntSingletonSet(int value)
        {
            this.value = value;
        }

        public override void Clear()
        {
            throw new NotSupportedException("IntSingletonSet is immutable");
        }

        public override IntSet Copy()
        {
            return this;
        }

        public override IntSet MutableCopy()
        {
            IntHashSet intHashSet = new IntHashSet();
            intHashSet.Add(value);
            return intHashSet;
        }

        public override bool IsMutable()
        {
            return false;
        }

        public override int Size()
        {
            return 1;
        }

        public override bool IsEmpty()
        {
            return false;
        }

        public override bool Contains(int value)
        {
            return this.value == value;
        }

        public override bool Remove(int value)
        {
            throw new NotSupportedException("IntSingletonSet is immutable");
        }

        public override bool Add(int value)
        {
            throw new NotSupportedException("IntSingletonSet is immutable");
        }

        public override IIntIterator IIterator()
        {
            return new IntSingletonIterator(value);
        }

        public override IntSet Union(IntSet other)
        {
            IntSet n = other.MutableCopy();
            n.Add(value);
            return n;
        }

        public override IntSet Intersect(IntSet other)
        {
            if (other.Contains(value))
            {
                return this;
            }
            else
            {
                return IntEmptySet.GetInstance();
            }
        }

        public override IntSet Except(IntSet other)
        {
            if (other.Contains(value))
            {
                return IntEmptySet.GetInstance();
            }
            else
            {
                return this;
            }
        }

        public override bool ContainsAll(IntSet other)
        {
            if (other.Count > 1)
            {
                return false;
            }

            IIntIterator ii = other.IIterator();
            while (ii.MoveNext())
            {
                if (value != ii.Current)
                {
                    return false;
                }
            }

            return true;
        }
    }
}