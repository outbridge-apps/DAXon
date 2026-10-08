////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace OutSmart.DAXon.Collections
{
    /// <summary>
    /// Interface defining a map from integers to integers
    /// </summary>
    internal interface IIntToIntMap
    {
        int DefaultValue { get; set; }
        /// <summary>
        /// Clear the map.
        /// </summary>
        void Clear();
        bool Contains(int key);
        int Get(int key);
        int Size();
        bool Remove(int key);
        void Put(int key, int value);
        IIntIterator KeyIterator();
    }
}