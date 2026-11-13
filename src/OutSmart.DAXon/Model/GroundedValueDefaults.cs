////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2026 OutSmart
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Collections.Zeno;
using OutSmart.DAXon.Values;

namespace OutSmart.DAXon.Model
{
    /// <summary>
    /// The default methods of Java's GroundedValue and Item, which an interface cannot carry on .NET Framework: the
    /// value classes forward to these instead of each keeping its own copy.
    /// </summary>
    internal static class GroundedValueDefaults
    {
        /// <summary>The items of first, then those of each of others.</summary>
        internal static IGroundedValue Concatenate(IGroundedValue first, IGroundedValue[] others)
        {
            ZenoChain<IItem> chain = new ZenoChain<IItem>().AddAll(first.AsIterable());
            foreach (IGroundedValue v in others)
            {
                chain = chain.AddAll(v.AsIterable());
            }

            return new ZenoSequence(chain);
        }

        /// <summary>subsequence() of a single item, the sequence of one.</summary>
        internal static IGroundedValue Subsequence(IItem item, int start, int length)
        {
            return start <= 0 && (long)start + length > 0 ? (IGroundedValue)item : EmptySequence.GetInstance();
        }

        /// <summary>Whether an item of value is the node sought; an item that is no node never is.</summary>
        internal static bool ContainsNode(IGroundedValue value, NodeInfo sought)
        {
            if (value is NodeInfo node)
            {
                return sought != null && node.IsSameNodeInfo(sought);
            }

            ISequenceIterator iter = value.Iterate();
            for (IItem item; (item = iter.Next()) != null;)
            {
                if (item is NodeInfo n && n.IsSameNodeInfo(sought))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
