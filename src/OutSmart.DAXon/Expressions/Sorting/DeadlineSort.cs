////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;

namespace OutSmart.DAXon.Expressions.Sorting
{
    /// <summary>
    /// A List.Sort cannot be stopped from outside, and over a million nodes it runs for seconds: past a few thousand
    /// nodes one comparison in 1024 looks at the run's deadline. The sort wraps what a comparison throws in an
    /// InvalidOperationException, so the time limit's own error is taken out again.
    /// </summary>
    internal static class DeadlineSort
    {
        // Below this a sort takes about a millisecond: not worth the wrapper.
        private const int Watched = 4096;

        internal static void Sort(List<NodeInfo> nodes, IComparer<NodeInfo> comparer)
        {
            if (nodes.Count < Watched)
            {
                nodes.Sort(comparer);
                return;
            }

            try
            {
                nodes.Sort(new Watching(comparer));
            }
            catch (InvalidOperationException e) when (e.InnerException is XPathException x && x.IsTimeLimit())
            {
                throw x;
            }
        }

        // Not generic: a comparer shared over reference types pays a dictionary lookup on every call.
        private sealed class Watching : IComparer<NodeInfo>
        {
            private readonly IComparer<NodeInfo> inner;
            private int comparisons;

            internal Watching(IComparer<NodeInfo> inner)
            {
                this.inner = inner;
            }

            public int Compare(NodeInfo a, NodeInfo b)
            {
                if ((++comparisons & 1023) == 0)
                {
                    Core.Controller.CheckActiveTimeout();
                }

                return inner.Compare(a, b);
            }
        }
    }
}
