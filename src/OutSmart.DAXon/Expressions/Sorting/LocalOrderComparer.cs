////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System.Collections.Generic;
using OutSmart.DAXon.Model;

namespace OutSmart.DAXon.Expressions.Sorting
{
    // COLLATION_KEY_NaN is IAtomicMatchKey: callers assign it from GetMapKey()/AsMapKey().
    // Implements IComparer<NodeInfo> for DocumentSorter's `(IComparer<NodeInfo>)GetInstance()` cast;
    // Compare delegates to NodeInfo.CompareOrder (faithful to upstream LocalOrderComparer).
    internal sealed class LocalOrderComparer : IComparer<NodeInfo>
    {
        private static readonly LocalOrderComparer _instance = new LocalOrderComparer();
        public LocalOrderComparer() { }
        public static LocalOrderComparer GetInstance() => _instance;
        public int Compare(NodeInfo a, NodeInfo b) => a.CompareOrder(b);
    }
}
