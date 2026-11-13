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
    /// A List.Sort cannot be stopped from outside, and over a million items it runs for seconds: one comparison in 1024
    /// looks at the run's deadline (for nodes, past a few thousand). The sort wraps what a comparison throws in an
    /// InvalidOperationException, which ComparerFailure takes out again.
    /// </summary>
    internal static class DeadlineSort
    {
        // Below this a sort takes about a millisecond: not worth the wrapper.
        private const int Watched = 4096;

        internal static void Sort(List<NodeInfo> nodes, IComparer<NodeInfo> comparer)
        {
            if (InOrder(nodes, comparer))
            {
                return;
            }

            if (nodes.Count < Watched)
            {
                nodes.Sort(comparer);
                return;
            }

            XPathException failure;
            try
            {
                nodes.Sort(new Watching(comparer));
                return;
            }
            catch (InvalidOperationException e)
            {
                failure = ComparerFailure.Reported(e);
            }

            throw failure;
        }

        /// <summary>
        /// List.Sort by compare, which looks at the run's deadline every 1024 comparisons: nothing else stops it, and a
        /// million string keys make twenty million. What compare throws is reported as ComparerFailure says. List.Sort
        /// is not stable: compare breaks ties itself, by original position.
        /// </summary>
        internal static void Sort<T>(List<T> list, Comparison<T> compare, bool xslSort = false)
        {
            int comparisons = 0;
            XPathException failure;
            try
            {
                list.Sort((a, b) =>
                {
                    if ((++comparisons & 1023) == 0)
                    {
                        Core.Controller.CheckActiveTimeoutNow();
                    }

                    return compare(a, b);
                });
                return;
            }
            catch (InvalidOperationException e)
            {
                failure = ComparerFailure.Reported(e, xslSort);
            }
            catch (InvalidCastException e)
            {
                failure = ComparerFailure.Reported(e, xslSort);
            }

            throw failure;
        }

        // Nodes already in document order - a group, a variable, a step from ordered parents - are the common input, and
        // List.Sort spends n log n comparisons on them where Java's TimSort spends n: one pass finds out.
        private static bool InOrder(List<NodeInfo> nodes, IComparer<NodeInfo> comparer)
        {
            for (int i = 1; i < nodes.Count; i++)
            {
                Core.Controller.CheckActiveTimeoutEvery64K(i);
                if (comparer.Compare(nodes[i - 1], nodes[i]) > 0)
                {
                    return false;
                }
            }

            return true;
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
                // The clock itself: the stride the loop that gathered the nodes grew would put the first look millions
                // of comparisons away.
                if ((++comparisons & 1023) == 0)
                {
                    Core.Controller.CheckActiveTimeoutNow();
                }

                return inner.Compare(a, b);
            }
        }
    }
}
