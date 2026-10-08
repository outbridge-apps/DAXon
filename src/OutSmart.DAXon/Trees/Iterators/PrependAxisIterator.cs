////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;
using OutSmart.DAXon.Model;

namespace OutSmart.DAXon.Trees.Iterators
{
    // Faithful port of upstream PrependSequenceIterator.next() (yield the prepended `start` once, then delegate)
    // + PrependAxisIterator.next() (narrow to NodeInfo). A distinct PrependSequenceIterator lives in
    // Expressions.Instructions (used by ForEach) and GeneralComparison10 imports both namespaces — sharing the
    // name would be CS0104, so this type stays separate. Explicit ISequenceIterator.Next() redirects to the
    // axis-slot `new NodeInfo Next()` (covariant-redirect idiom, mirrors DocumentOrderIterator).
    internal sealed class PrependAxisIterator : IAxisIterator
    {
        private IItem start;
        private readonly ISequenceIterator @base;
        public PrependAxisIterator(NodeInfo start, ISequenceIterator @base)
        {
            this.start = start;
            this.@base = @base;
        }
        public NodeInfo Next()
        {
            if (start != null)
            {
                IItem temp = start;
                start = null;
                return (NodeInfo)temp;
            }
            return (NodeInfo)@base.Next();
        }
        IItem ISequenceIterator.Next() => Next();
        void ISequenceIterator.Dispose() { @base.Dispose(); }
        void IDisposable.Dispose() { @base.Dispose(); }
    }
}
