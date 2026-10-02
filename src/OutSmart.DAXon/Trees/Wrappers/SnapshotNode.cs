////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Trees.Utilities;
using OutSmart.DAXon.Values;
using System.Collections.Generic;
using System.Threading;

namespace OutSmart.DAXon.Trees.Wrappers
{
    // Faithful port of net.sf.saxon.tree.wrapper.SnapshotNode (Saxon 12.9). New with the VirtualCopy port —
    // fn:snapshot() was previously unregistered. A node in the tree produced by snapshot(): a virtual copy
    // including all ancestors of the pivot node and all descendants (plus attributes/namespaces).
    internal sealed class SnapshotNode : VirtualCopy
    {
        protected internal NodeInfo pivot; // a node in the source tree
        private readonly PivotPath path;   // shared by every node of one snapshot

        // The string value for a node above the pivot is the string value of the pivot.
        public override UnicodeString UnicodeStringValue
        {
            get
            {
                if (original.IsSameNodeInfo(pivot) || path.IndexOf(original) >= 0)
                {
                    return pivot.UnicodeStringValue;
                }
                else
                {
                    return original.UnicodeStringValue;
                }
            }
        }

        // The child of this node assuming it is known to be above the pivot; null where this node is the
        // parent of the pivot and the pivot is an attribute/namespace node.
        private NodeInfo ChildOfAncestorNode
        {
            get
            {
                int index = path.IndexOf(original);
                if (index < 0 || index >= path.PivotIndex)
                {
                    throw new System.InvalidOperationException("pivot is not a descendant of this node");
                }

                int pivotKind = pivot.GetNodeKind();
                if ((pivotKind == OutSmart.DAXon.Types.Type.ATTRIBUTE || pivotKind == OutSmart.DAXon.Types.Type.NAMESPACE) && index + 1 == path.PivotIndex)
                {
                    return null;
                }

                VirtualCopy child = Wrap(path.At(index + 1));
                child.parent = this;
                return child;
            }
        }

        private SnapshotNode(NodeInfo @base, PivotPath path) : base(@base, path.Root)
        {
            this.pivot = path.Pivot;
            this.path = path;
        }

        public static SnapshotNode MakeSnapshot(NodeInfo original)
        {
            SnapshotNode vc = new SnapshotNode(original, new PivotPath(original));
            Configuration config = original.GetConfiguration();
            VirtualTreeInfo doc = new VirtualTreeInfo(config);
            long docNr = config.DocumentNumberAllocator.AllocateDocumentNumber();
            doc.SetDocumentNumber(docNr);
            doc.SetCopyAccumulators(true);
            vc.tree = doc;
            doc.SetRootNode(vc.Root);
            return vc;
        }

        protected override VirtualCopy Wrap(NodeInfo node)
        {
            SnapshotNode vc = new SnapshotNode(node, path);
            vc.tree = tree;
            return vc;
        }

        public override NodeInfo GetParent()
        {
            if (parent == null)
            {
                NodeInfo basep = original.GetParent();
                if (basep == null)
                {
                    return null;
                }

                parent = (VirtualCopy)Wrap(basep);
            }

            return parent;
        }

        public override void Copy(IReceiver @out, int copyOptions, ILocation locationId)
        {
            Navigator.Copy(this, @out, copyOptions, locationId);
        }

        public override IAtomicSequence Atomize()
        {
            switch (GetNodeKind())
            {
                case OutSmart.DAXon.Types.Type.ATTRIBUTE:
                case OutSmart.DAXon.Types.Type.TEXT:
                case OutSmart.DAXon.Types.Type.COMMENT:
                case OutSmart.DAXon.Types.Type.PROCESSING_INSTRUCTION:
                case OutSmart.DAXon.Types.Type.NAMESPACE:
                    return original.Atomize();
                default:
                    // At or below the pivot: the pivot itself, or a node off its path that lies under it.
                    if (original.IsSameNodeInfo(pivot) || (path.IndexOf(original) < 0 && Navigator.IsAncestorOrSelf(pivot, original)))
                    {
                        return original.Atomize();
                    }
                    else
                    {
                        // Ancestors of the pivot node have type xs:anyType. The typed value is therefore the
                        // string value as an instance of xs:untypedAtomic
                        return StringValue.MakeUntypedAtomic(pivot.UnicodeStringValue);
                    }
            }
        }

        public override bool IsId() => original.IsId();
        public override bool IsIdref() => original.IsIdref();
        public override bool IsNilled() => original.IsNilled();
        public override string GetPublicId() => original != null ? original.GetPublicId() : null;

        public override IAxisIterator IterateAxis(int axisNumber, INodePredicate nodeTest)
        {
            if (!original.IsSameNodeInfo(pivot) && path.IndexOf(original) >= 0)
            {
                // We're on a node above the pivot node
                switch (axisNumber)
                {
                    case AxisInfo.CHILD:
                        // return only the child that is included in the snapshot, that is, the one
                        // that is an ancestor-or-self of the pivot node
                        return Navigator.FilteredSingleton(ChildOfAncestorNode, nodeTest);
                    case AxisInfo.DESCENDANT:
                    case AxisInfo.DESCENDANT_OR_SELF:
                        // Use the child axis recursively, for efficiency
                        IAxisIterator iter = new Navigator.DescendantEnumeration(this, axisNumber == AxisInfo.DESCENDANT_OR_SELF, true);
                        if (!(nodeTest is AnyNodeTest))
                        {
                            iter = new Navigator.AxisFilter(iter, nodeTest);
                        }

                        return iter;
                    case AxisInfo.PRECEDING_SIBLING:
                    case AxisInfo.FOLLOWING_SIBLING:
                    case AxisInfo.PRECEDING:
                    case AxisInfo.FOLLOWING:
                        return EmptyIterator.OfNodes();
                    default:
                        return base.IterateAxis(axisNumber, nodeTest);
                }
            }
            else
            {
                return base.IterateAxis(axisNumber, nodeTest);
            }
        }

        protected internal override bool IsIncludedInCopy(NodeInfo sourceNode)
        {
            switch (sourceNode.GetNodeKind())
            {
                case OutSmart.DAXon.Types.Type.ATTRIBUTE:
                case OutSmart.DAXon.Types.Type.NAMESPACE:
                    return IsIncludedInCopy(sourceNode.GetParent());
                default:
                    return path.IndexOf(sourceNode) >= 0 || Navigator.IsAncestorOrSelf(pivot, sourceNode);
            }
        }

        // The pivot's ancestors-or-self in the source tree, root first. Upstream answers "is this node
        // above the pivot" and "which child leads to it" by walking up from the pivot for every node, so
        // copying or descending a deep snapshot was quadratic (4000 levels 2.8 s).
        private sealed class PivotPath
        {
            internal readonly NodeInfo Pivot;
            internal readonly NodeInfo Root;
            private Chain chain;   // built on first use, published whole

            internal PivotPath(NodeInfo pivot)
            {
                Pivot = pivot;
                Root = pivot.Root;
            }

            internal int PivotIndex => Built().Nodes.Length - 1;

            internal NodeInfo At(int index) => Built().Nodes[index];

            // The node's position on the path (0 is the root), or -1 if it is not an ancestor-or-self of the pivot.
            internal int IndexOf(NodeInfo node)
            {
                return Built().Index.TryGetValue(node, out int index) ? index : -1;
            }

            private Chain Built()
            {
                Chain c = Volatile.Read(ref chain);
                if (c == null)
                {
                    c = new Chain(Pivot);
                    Volatile.Write(ref chain, c);
                }

                return c;
            }

            private sealed class Chain
            {
                internal readonly NodeInfo[] Nodes;
                internal readonly Dictionary<NodeInfo, int> Index;

                internal Chain(NodeInfo pivot)
                {
                    var up = new List<NodeInfo>();
                    for (NodeInfo n = pivot; n != null; n = n.GetParent())
                    {
                        up.Add(n);
                    }

                    up.Reverse();
                    Nodes = up.ToArray();
                    Index = new Dictionary<NodeInfo, int>(Nodes.Length);
                    for (int i = 0; i < Nodes.Length; i++)
                    {
                        Index[Nodes[i]] = i;
                    }
                }
            }
        }
    }
}
