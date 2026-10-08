////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
namespace OutSmart.DAXon.Trees.Utilities
{
    public class IndexedStack<T> : IEnumerable<T>
    {
        private readonly List<T> items;

        // Shadow LINQ Count extension; expose lowercase Java-style alias
        public int Count => items.Count;

        // PHASE7_INDEXER_IS
        public T this[int i] { get { return Get(i); } set { Set(i, value); } }
        public IndexedStack()
        {
            items = new List<T>(20);
        }

        public IndexedStack(int size)
        {
            items = new List<T>(size);
        }

        public virtual int Size()
        {
            return items.Count;
        }

        public virtual bool IsEmpty()
        {
            return items.Count == 0;
        }

        public virtual void IPush(T item)
        {
            items.Add(item);
        }

        public virtual T Peek()
        {
            if (items.Count == 0)
            {
                throw new InvalidOperationException("Stack is empty");
            }
            else
            {
                return items[items.Count - 1];
            }
        }

        public virtual T Pop()
        {
            if (items.Count == 0)
            {
                throw new InvalidOperationException("Stack is empty");
            }
            else
            {
                return items.RemoveAtAndGet(items.Count - 1);
            }
        }
        public virtual T Get(int i)
        {
            return items[i];
        }

        public virtual void Set(int i, T value)
        {
            items[i] = value;
        }

        public virtual bool Contains(T value)
        {
            return items.Contains(value);
        }

        public virtual int IndexOf(T value)
        {
            return items.IndexOf(value);
        }

        public virtual IEnumerator<T> GetEnumerator()
        {
            return items.GetEnumerator();
        }
        // (r1-injected NIE GetEnumerator removed - the renamed real GetEnumerator above implements IEnumerable<T>)
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}