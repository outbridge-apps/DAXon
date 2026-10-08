////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Trees.Wrappers;

namespace OutSmart.DAXon.Functions
{
    // Faithful port of net.sf.saxon.functions.SnapshotFn (Saxon 12.9). The class was missing from the port,
    // so fn:snapshot() was unregistered (XPST0017).
    // XSLT 3.0 function snapshot(): a deep-copy that also includes a shallow copy of all ancestors.
    internal sealed class SnapshotFn : SystemFunction
    {

        public override string StreamerName => "SnapshotFn";
        public override int GetCardinality(Expression[] arguments)
        {
            return arguments[0].GetCardinality();
        }

        public override ISequence Call(IXPathContext context, ISequence[] arguments)
        {
            ISequence @in = arguments.Length == 0 ? (ISequence)context.GetContextItem() : arguments[0];
            ISequenceIterator iter = SnapshotSequence(@in.Iterate());
            return new LazySequence(iter);
        }

        public static ISequenceIterator SnapshotSequence(ISequenceIterator nodes)
        {
            return ItemMappingIterator.IMap(nodes, SnapshotSingle);
        }

        /// <summary>
        /// Take a snapshot of a single item
        /// </summary>
        public static IItem SnapshotSingle(IItem origin)
        {
            if (origin is NodeInfo)
            {
                if (((NodeInfo)origin).GetParent() == null)
                {
                    VirtualCopy vc = VirtualCopy.MakeVirtualCopy((NodeInfo)origin);
                    vc.GetTreeInfo().SetCopyAccumulators(true);
                    return vc;
                }
                else
                {
                    return SnapshotNode.MakeSnapshot((NodeInfo)origin);
                }
            }
            else
            {
                return origin;
            }
        }
    }
}
