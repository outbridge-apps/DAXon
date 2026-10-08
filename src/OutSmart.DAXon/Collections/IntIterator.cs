////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;

using System.Collections.Generic;
namespace OutSmart.DAXon.Collections
{
    public interface IIntIterator : IEnumerator<int>
    {
        bool HasNext();
        int Next();
    }

    /// <summary>
    /// Base for the int-cursor iterators: bridges the .NET IEnumerator&lt;int&gt; protocol
    /// (MoveNext/Current) onto the concrete HasNext()/Next() peek-advance pair (R5.2).
    /// Implementors override HasNext()/Next() only; MoveNext/Current come from here.
    /// </summary>
    internal abstract class AbstractIntIterator : IIntIterator
    {
        public int Current { get; private set; }

        object System.Collections.IEnumerator.Current => Current;
        public abstract bool HasNext();
        public abstract int Next();

        public bool MoveNext()
        {
            if (HasNext())
            {
                Current = Next();
                return true;
            }

            return false;
        }
        public void Reset() => throw new NotSupportedException();
        public void Dispose() { }
    }
}