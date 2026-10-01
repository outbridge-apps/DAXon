////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;using OutSmart.DAXon.Functions;

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Collections
{
    /// <summary>
    /// An immutable integer set containing every integer
    /// </summary>
    internal sealed class IntUniversalSet : IntSet
    {
        private static readonly IntUniversalSet THE_INSTANCE = new IntUniversalSet();

        private IntUniversalSet()
        {
        }
        public static IntUniversalSet GetInstance()
        {
            return THE_INSTANCE;
        }

        public override IntSet Copy()
        {
            return this;
        }

        public override IntSet MutableCopy()
        {
            return new IntComplementSet(new IntHashSet());
        }

        public override bool IsMutable()
        {
            return false;
        }

        public override void Clear()
        {
            throw new NotSupportedException("IntUniversalSet is immutable");
        }

        public override int Size()
        {
            return int.MaxValue;
        }

        public override bool IsEmpty()
        {
            return false;
        }

        public override bool Contains(int value)
        {
            return true;
        }

        public override bool Remove(int value)
        {
            throw new NotSupportedException("IntUniversalSet is immutable");
        }

        public override bool Add(int value)
        {
            throw new NotSupportedException("IntUniversalSet is immutable");
        }

        public override IIntIterator IIterator()
        {
            throw new NotSupportedException("Cannot enumerate an infinite set");
        }

        public override IntSet Union(IntSet other)
        {
            return this;
        }

        public override IntSet Intersect(IntSet other)
        {
            return other.Copy();
        }

        public override IntSet Except(IntSet other)
        {
            if (other is IntUniversalSet)
            {
                return IntEmptySet.GetInstance();
            }
            else
            {
                return new IntComplementSet(other.Copy());
            }
        }

        public override bool ContainsAll(IntSet other)
        {
            return true;
        }
    }
}