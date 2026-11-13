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
    // Orders nodes by document number, then by document order. `d1 < d2 ? -1 : 1` is Java's Long.signum(d1 - d2)
    // for d1 != d2, without the overflow.
    internal sealed class GlobalOrderComparer : IComparer<NodeInfo>
    {
        private static readonly GlobalOrderComparer _instance = new GlobalOrderComparer();
        public static GlobalOrderComparer GetInstance() => _instance;
        public int Compare(NodeInfo a, NodeInfo b)
        {
            if (a == b)
                return 0;
            long d1 = a.GetTreeInfo().GetDocumentNumber();
            long d2 = b.GetTreeInfo().GetDocumentNumber();
            if (d1 == d2)
                return a.CompareOrder(b);
            return d1 < d2 ? -1 : 1;
        }
    }
}
