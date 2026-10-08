////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
namespace OutSmart.DAXon.Collections
{
    /// <summary>
    /// A set of integers represented as int values
    /// </summary>
    public abstract class IntSet
    {
        // PHASE7_INTSET_COUNT: shadow LINQ-style extension
        public int Count => Size();
        public abstract IntSet Copy();
        public abstract IntSet MutableCopy();
        public virtual bool IsMutable()
        {
            return true;
        }

        /// <summary>
        /// Clear the contents of the IntSet (making it an empty set)
        /// </summary>
        public abstract void Clear();
        public abstract int Size();
        public abstract bool IsEmpty();
        public abstract bool Contains(int value);
        public abstract bool Remove(int value);
        public abstract bool Add(int value);
        public abstract IIntIterator IIterator();
        public virtual bool ContainsAll(IntSet other)
        {
            if (other == IntUniversalSet.GetInstance() || (other is IntComplementSet))
            {
                return false;
            }

            IIntIterator it = other.IIterator();
            while (it.MoveNext())
            {
                if (!Contains(it.Current))
                {
                    return false;
                }
            }

            return true;
        }

        public virtual IntSet Union(IntSet other)
        {
            if (other == IntUniversalSet.GetInstance())
            {
                return other;
            }

            if (this.IsEmpty())
            {
                return other.Copy();
            }

            if (other.IsEmpty())
            {
                return this.Copy();
            }

            if (other is IntComplementSet)
            {
                return other.Union(this);
            }

            IntHashSet n = new IntHashSet(this.Count + other.Count);
            IIntIterator it = IIterator();
            while (it.MoveNext())
            {
                n.Add(it.Current);
            }

            it = other.IIterator();
            while (it.MoveNext())
            {
                n.Add(it.Current);
            }

            return n;
        }

        public virtual IntSet Intersect(IntSet other)
        {
            if (this.IsEmpty() || other.IsEmpty())
            {
                return IntEmptySet.GetInstance();
            }

            IntHashSet n = new IntHashSet(Size());
            IIntIterator it = IIterator();
            while (it.MoveNext())
            {
                int v = it.Current;
                if (other.Contains(v))
                {
                    n.Add(v);
                }
            }

            return n;
        }

        public virtual IntSet Except(IntSet other)
        {
            IntHashSet n = new IntHashSet(Size());
            IIntIterator it = IIterator();
            while (it.MoveNext())
            {
                int v = it.Current;
                if (!other.Contains(v))
                {
                    n.Add(v);
                }
            }

            return n;
        }
    }
}