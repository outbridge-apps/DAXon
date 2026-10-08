////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;

using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Expressions.Sorting
{
    internal class ObjectToBeSorted
    {
        public IItem value;
        public AtomicValue[] sortKeyValues;
        // Single-key fast path: the lone key stored inline instead of a 1-element sortKeyValues
        // array (~one array saved per item, e.g. 900K on a 1M-row xsl:sort). Readers use
        // key0 ?? sortKeyValues[0]; the array path leaves key0 null.
        public AtomicValue key0;
        public int originalPosition;
        public ObjectToBeSorted(int numberOfSortKeys)
        {
            sortKeyValues = new AtomicValue[numberOfSortKeys];
        }

        // Single-key fast path: no sortKeyValues array (the key goes in key0).
        public ObjectToBeSorted()
        {
        }
    }
}