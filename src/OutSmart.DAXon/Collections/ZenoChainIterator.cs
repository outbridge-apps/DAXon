////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Collections.Generic;
namespace OutSmart.DAXon.Collections.Zeno
{
    internal sealed class ZenoChainIterator<U> : IEnumerator<U>
    {
        private int majorIndex = 0;
        private int minorIndex = 0;
        private readonly List<List<U>> masterList;
        private U __cur;
        public U Current => __cur;
        object System.Collections.IEnumerator.Current => __cur;
        public ZenoChainIterator(List<List<U>> masterList)
        {
            this.masterList = masterList;
        }

        public bool HasNext()
        {
            return majorIndex < masterList.Count && minorIndex < masterList[majorIndex].Count;
        }

        public U Next()
        {
            List<U> currentSegment = masterList[majorIndex];
            U result = currentSegment[minorIndex];
            if (++minorIndex >= currentSegment.Count)
            {
                majorIndex++;
                minorIndex = 0; // Assumes no zero-length segments
            }

            return result;
        }
        void IDisposable.Dispose() { }
        bool System.Collections.IEnumerator.MoveNext() { if (HasNext()) { __cur = Next(); return true; } return false; }
        void System.Collections.IEnumerator.Reset() { }
    }
}
