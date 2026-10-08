////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Internal.Collections;
using System;
namespace OutSmart.DAXon.Trees.Tiny
{
    /// <summary>
    /// Statistics on the size of TinyTree instances, kept so that the system can learn how much space to allocate to new trees
    /// </summary>
    public class Statistics
    {
        private readonly object syncLock = new object();
        // We maintain statistics, recording how large the trees created under this Java VM
        // turned out to be. These figures are then used when allocating space for new trees, on the assumption
        // that there is likely to be some uniformity. The statistics are initialized to an arbitrary value
        // so that they can be used every time including the first time.
        // We keep data for the last 10 trees created, noting the space actually used for various arrays.
        // To decide the space allocation for the next tree, we examine these last 10 entries. The values
        // are combined by a bitwise "or" of these values, yielding a value that is typically a bit higher
        // than their maximum. So we're generally allocating more space than we need, but not by too much.
        // The algorithm works best when all the trees have similar sizes.
        private int treesCreated = 0;
        private int nextSlot = 0;
        private readonly int[] last10Nodes = new int[10];
        private readonly int[] last10Attributes = new int[10];
        private readonly int[] last10Namespaces = new int[10];
        private readonly int[] last10Characters = new int[10];

        // Nodes and attributes per 64 KB of input by the input's size (its bit count), from the latest tree of
        // that size whose input length was known; nodes 0 = none yet. Documents of one size tend to be of one
        // kind, and a density learned from another kind mis-sizes a tree.
        private readonly int[] nodesPer64KB = new int[64];
        private readonly int[] attributesPer64KB = new int[64];

        public virtual int AverageNodes => GetUpperBound(last10Nodes);

        public virtual int AverageAttributes => GetUpperBound(last10Attributes);

        public virtual int AverageNamespaces => GetUpperBound(last10Namespaces);

        public virtual int AverageCharacters => GetUpperBound(last10Characters);
        public Statistics() : this(4000, 100, 20, 4000)
        {
        }

        public Statistics(int nodes, int atts, int namespaces, int chars)
        {
            ArrayTools.Fill(last10Nodes, nodes);
            ArrayTools.Fill(last10Attributes, atts);
            ArrayTools.Fill(last10Namespaces, namespaces);
            ArrayTools.Fill(last10Characters, chars);
        }

        private int GetUpperBound(int[] last10)
        {
            int bits = 0;
            for (int i = 0; i < 10; i++)
            {
                bits |= last10[i];
            }

            return bits;
        }

        // Array sizes for a tree whose input is length bytes (or chars) long, at the density of the latest tree
        // of its size (else of a neighbouring power of two) plus 10%, and never above one node per 12 bytes or
        // attribute per 16, so the input's own size bounds them. -1 when no tree of about that size was seen.
        internal void SizesFromInput(long length, out int nodes, out int attributes)
        {
            nodes = -1;
            attributes = -1;
            int b = Bucket(length);
            int k = nodesPer64KB[b] > 0 ? b : b < 63 && nodesPer64KB[b + 1] > 0 ? b + 1 : b > 0 && nodesPer64KB[b - 1] > 0 ? b - 1 : -1;
            if (k >= 0)
            {
                nodes = (int)Math.Min(Math.Min(length * nodesPer64KB[k] / 65536 * 11 / 10, length / 12) + 64, int.MaxValue / 2);
                attributes = (int)Math.Min(Math.Min(length * attributesPer64KB[k] / 65536 * 11 / 10, length / 16) + 16, int.MaxValue / 2);
            }
        }

        private static int Bucket(long length)
        {
            int bits = 0;
            while (length > 0)
            {
                length >>= 1;
                bits++;
            }

            return bits;
        }

        public virtual void UpdateStatistics(int numberOfNodes, int numberOfAttributes, int numberOfNamespaces, LargeTextBuffer textBuffer)
        {
            UpdateStatistics(numberOfNodes, numberOfAttributes, numberOfNamespaces, textBuffer, -1);
        }

        internal void UpdateStatistics(int numberOfNodes, int numberOfAttributes, int numberOfNamespaces, LargeTextBuffer textBuffer, long inputLength)
        {
            if (inputLength >= 256)
            {
                // ints, so a concurrent reader sees whole values; a stale pair only mis-sizes one tree
                int b = Bucket(inputLength);
                attributesPer64KB[b] = (int)Math.Min(numberOfAttributes * 65536L / inputLength + (numberOfAttributes > 0 ? 1 : 0), int.MaxValue);
                nodesPer64KB[b] = (int)Math.Min(numberOfNodes * 65536L / inputLength + 1, int.MaxValue);
            }

            lock (syncLock)
            {
                // Upstream stopped here after a million trees, freezing whatever the last ten were for good
                // on a long-lived Processor. The ring keeps turning; only the count saturates.
                int n = nextSlot;
                nextSlot = n == 9 ? 0 : n + 1;
                last10Nodes[n] = numberOfNodes;
                last10Attributes[n] = numberOfAttributes;
                last10Namespaces[n] = numberOfNamespaces;
                last10Characters[n] = Math.Max(textBuffer.Length(), 65536);
                if (treesCreated < int.MaxValue)
                {
                    treesCreated++;
                }
            }
        }

        public override string ToString()
        {
            return treesCreated + "(" + AverageNodes + "," + AverageAttributes + "," + AverageNamespaces + "," + AverageCharacters + ")";
        }
    }
}