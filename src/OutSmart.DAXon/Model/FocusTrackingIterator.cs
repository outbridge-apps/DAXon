////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Trees.Wrappers;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Model
{
    public class FocusTrackingIterator : IFocusIterator, ILookaheadIterator, IGroundedIterator, ILastPositionFinder
    {
        private ISequenceIterator @base;
        private IItem curr;
        private int pos = 0;
        private int last = -1;
        private SiblingMemory siblingMemory;

        public virtual ISequenceIterator UnderlyingIterator => @base;

        public virtual bool HasNext => ((ILookaheadIterator)@base).HasNext;

        public FocusTrackingIterator() { }
        public FocusTrackingIterator(ISequenceIterator @base)
        {
            this.@base = @base;
        }
        public static FocusTrackingIterator Track(ISequenceIterator @base)
        {
            return new FocusTrackingIterator(@base);
        }

        public virtual IItem Next()
        {
            curr = @base.Next();
            if (curr == null)
            {
                last = pos;
                pos = -1;
            }
            else
            {
                pos++;
            }

            return curr;
        }

        public virtual IItem Current()
        {
            return curr;
        }

        public virtual int Position()
        {
            return pos;
        }

        public virtual int GetLength()
        {
            if (last == -1)
            {
                if (SequenceTool.SupportsGetLength(@base))
                {
                    last = SequenceTool.GetLength(@base);
                }

                if (last == -1)
                {
                    IGroundedValue residue = SequenceExtent.MakeResidue(@base);
                    last = pos + residue.GetLength();
                    @base = residue.Iterate();
                }
            }

            return last;
        }

        public virtual bool SupportsGetLength()
        {
            // Cheap-length probe: answer true only when the base can. The unconditional `true`
            // sent every prober (SortedIterator.BuildArray array sizing, HasLength) into
            // GetLength's residue branch, materializing the whole remaining population into an
            // intermediate extent just to learn its size. GetLength itself keeps that fallback
            // for callers that need an answer regardless (fn:last() via GetLast).
            return SequenceTool.SupportsGetLength(@base);
        }

        public virtual bool SupportsHasNext()
        {
            return @base is ILookaheadIterator && ((ILookaheadIterator)@base).SupportsHasNext();
        }

        public virtual IGroundedValue Materialize()
        {
            return SequenceTool.ToGroundedValue(@base);
        }

        public virtual IGroundedValue GetResidue()
        {
            return SequenceExtent.From(this);
        }

        public virtual void Dispose()
        {
            @base.Dispose();
        }

        public virtual bool IsActuallyGrounded()
        {
            return (@base is IGroundedIterator && ((IGroundedIterator)@base).IsActuallyGrounded());
        }

        /// <summary>
        /// Cached data to support optimization of the getSiblingPosition() method
        /// </summary>
        public virtual int GetSiblingPosition(NodeInfo node, NodeTest nodeTest, int max)
        {
            if (node is ISiblingCountingNode && nodeTest is AnyNodeTest)
            {
                return ((ISiblingCountingNode)node).GetSiblingPosition();
            }

            if (siblingMemory == null)
            {
                siblingMemory = new SiblingMemory();
            }
            else if (siblingMemory.mostRecentNodeTest.Equals(nodeTest) && node.Equals(siblingMemory.mostRecentNode))
            {
                return siblingMemory.mostRecentPosition;
            }

            SiblingMemory s = siblingMemory;
            IAxisIterator prev = node.IterateAxis(AxisInfo.PRECEDING_SIBLING, nodeTest);
            NodeInfo prior;
            int count = 1;
            while ((prior = prev.Next()) != null)
            {
                if (prior.Equals(s.mostRecentNode) && nodeTest.Equals(s.mostRecentNodeTest))
                {
                    int result = count + s.mostRecentPosition;
                    s.mostRecentNode = node;
                    s.mostRecentPosition = result;
                    return result;
                }

                if (++count > max)
                {
                    return count;
                }
            }

            s.mostRecentNode = node;
            s.mostRecentPosition = count;
            s.mostRecentNodeTest = nodeTest;
            return count;
        }

        /// <summary>
        /// Cached data to support optimization of the getSiblingPosition() method
        /// </summary>
        private sealed class SiblingMemory
        {
            public NodeTest mostRecentNodeTest = null;
            public NodeInfo mostRecentNode = null;
            public int mostRecentPosition = -1;
        }
    }
}