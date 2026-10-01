////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using OutSmart.DAXon.Functions;

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Collections
{
    internal sealed class IntBlockSet : IntSet
    {
        private readonly int startPoint;
        private readonly int endPoint;
        private int cachedHashCode = -1;
        public IntBlockSet(int startPoint, int endPoint)
        {
            this.startPoint = startPoint;
            this.endPoint = endPoint;
        }

        public override IntSet Copy()
        {
            return this;
        }

        public override IntSet MutableCopy()
        {
            return new IntRangeSet(new int[] { startPoint }, new int[] { endPoint });
        }

        public override bool IsMutable()
        {
            return false;
        }

        public override int Size()
        {
            return endPoint - startPoint;
        }

        public override bool IsEmpty()
        {
            return Size() == 0;
        }

        public override bool Contains(int value)
        {
            return value >= startPoint && value <= endPoint;
        }

        public override bool Remove(int value)
        {
            throw new NotSupportedException("remove");
        }

        public override void Clear()
        {
            throw new NotSupportedException("clear");
        }

        public override bool Add(int value)
        {
            throw new NotSupportedException("add");
        }

        public override IIntIterator IIterator()
        {
            return MutableCopy().IIterator();
        }

        public override string ToString()
        {
            return startPoint + " - " + endPoint;
        }

        public override bool Equals(object other)
        {
            return MutableCopy().Equals(other);
        }

        public override int GetHashCode()
        {

            // Note, hashcodes are NOT the same as those used by IntHashSet and IntArraySet
            if (cachedHashCode == -1)
            {
                cachedHashCode = 0x236a89f1 ^ (startPoint + (endPoint << 3));
            }

            return cachedHashCode;
        }
    }
}
