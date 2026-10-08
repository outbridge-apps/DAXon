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
    /// An immutable integer set containing all int values except those in an excluded set
    /// </summary>
    internal sealed class IntComplementSet : IntSet
    {
        private readonly IntSet exclusions;

        public IntSet Exclusions => exclusions;
        public IntComplementSet(IntSet exclusions)
        {
            this.exclusions = exclusions.Copy();
        }

        public override IntSet Copy()
        {
            return new IntComplementSet(exclusions.Copy());
        }

        public override IntSet MutableCopy()
        {
            return Copy();
        }

        public override void Clear()
        {
            throw new NotSupportedException("IntComplementSet cannot be emptied");
        }

        public override int Size()
        {
            return int.MaxValue - exclusions.Count;
        }

        public override bool IsEmpty()
        {
            return Size() == 0;
        }

        public override bool Contains(int value)
        {
            return !exclusions.Contains(value);
        }

        public override bool Remove(int value)
        {
            bool b = Contains(value);
            if (b)
            {
                exclusions.Add(value);
            }

            return b;
        }

        public override bool Add(int value)
        {
            bool b = Contains(value);
            if (!b)
            {
                exclusions.Remove(value);
            }

            return b;
        }

        public override IIntIterator IIterator()
        {
            throw new NotSupportedException("Cannot enumerate an infinite set");
        }

        public override IntSet Union(IntSet other)
        {
            return new IntComplementSet(exclusions.Except(other));
        }

        public override IntSet Intersect(IntSet other)
        {
            if (other.IsEmpty())
            {
                return IntEmptySet.GetInstance();
            }
            else if (other == IntUniversalSet.GetInstance())
            {
                return Copy();
            }
            else if (other is IntComplementSet)
            {
                return new IntComplementSet(exclusions.Union(((IntComplementSet)other).exclusions));
            }
            else
            {
                return other.Intersect(this);
            }
        }

        public override IntSet Except(IntSet other)
        {
            return new IntComplementSet(exclusions.Union(other));
        }

        public override bool ContainsAll(IntSet other)
        {
            if (other is IntComplementSet)
            {
                return ((IntComplementSet)other).exclusions.ContainsAll(exclusions);
            }
            else if (other is IntUniversalSet)
            {
                return (!exclusions.IsEmpty());
            }
            else
            {
                IIntIterator ii = other.IIterator();
                while (ii.MoveNext())
                {
                    if (exclusions.Contains(ii.Current))
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }
}