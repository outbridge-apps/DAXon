////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Trees.Tiny;
using OutSmart.DAXon.Trees.Wrappers;

namespace OutSmart.DAXon.Core
{
    // What the trees a host hands a call hold, for the call's memory limit: each tree once, by its size. Trees of other
    // models (a DOM the host wraps) are not measured, and atomic values, maps and arrays are left out.
    internal static class InputSize
    {
        // A sequence's trees are looked for among its first items. A host hands a document or a few; walking a million
        // atomic values it hands took every call 100 ms and more, with the memory limit on by default.
        private const int Scanned = 1024;

        // seen: the document numbers counted already - numbers, so the call keeps no tree reachable. A tree numbered
        // from firstOfCall on was made by the call itself, and its ledger counts it already.
        internal static long Of(ISequence value, HashSet<long> seen, long firstOfCall)
        {
            if (value is IItem single)
            {
                return OfItem(single, seen, firstOfCall);
            }

            long bytes = 0;
            using (ISequenceIterator iter = value.Iterate())
            {
                IItem item;
                for (int n = 0; n < Scanned && (item = iter.Next()) != null; n++)
                {
                    bytes += OfItem(item, seen, firstOfCall);
                }
            }

            return bytes;
        }

        private static long OfItem(IItem item, HashSet<long> seen, long firstOfCall)
        {
            if (!(item is NodeInfo node))
            {
                return 0;
            }

            while (node is IVirtualNode wrapper && wrapper.RealNode is NodeInfo real && !ReferenceEquals(real, node))
            {
                node = real;
            }

            ITreeInfo tree = node.GetTreeInfo();
            if (!(tree is TinyTree tiny))
            {
                return 0;
            }

            long number = tree.GetDocumentNumber();
            return number < firstOfCall && seen.Add(number) ? tiny.RetainedBytes() : 0;
        }
    }
}
