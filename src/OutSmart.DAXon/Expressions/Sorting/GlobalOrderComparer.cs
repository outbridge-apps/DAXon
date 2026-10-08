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
    // I5 B4a (2026-06-12): implements System.Collections.Generic.IComparer<NodeInfo> (was the now-retired
    // OutSmart.DAXon.Internal.Collections.Comparator<NodeInfo>) so DocumentSorter's stage-0-renamed
    // `(IComparer<NodeInfo>)GlobalOrderComparer.GetInstance()` cast (DocumentSorter.cs:42/54) no longer throws.
    // Faithful port of net.sf.saxon.expr.sort.GlobalOrderComparer (the real file is <Compile Remove>'d at
    // csproj:545 because it calls OutSmart.DAXon.Internal.Long.Signum, which OutSmart.DAXon.Internal lacks): orders by document number
    // first, then intra-document order. `d1 < d2 ? -1 : 1` == Long.signum(d1-d2) here (d1!=d2) and is overflow-safe.
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
