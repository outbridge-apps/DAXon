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

namespace OutSmart.DAXon.Trees.Wrappers
{
    // Faithful port of net.sf.saxon.tree.wrapper.SnapshotNode (Saxon 12.9). New with the VirtualCopy port —
    // fn:snapshot() was previously unregistered. A node in the tree produced by snapshot(): a virtual copy
    // including all ancestors of the pivot node and all descendants (plus attributes/namespaces).
    internal sealed class SnapshotNode : VirtualCopy
    {
        protected internal NodeInfo pivot; // a node in the source tree

        // The string value for a node above the pivot is the string value of the pivot.
        public override UnicodeString UnicodeStringValue
        {
            get
            {
                if (Navigator.IsAncestorOrSelf(original, pivot))
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
                int pivotKind = pivot.GetNodeKind();
                SnapshotNode p = (SnapshotNode)Wrap(pivot);
                if ((pivotKind == OutSmart.DAXon.Types.Type.ATTRIBUTE || pivotKind == OutSmart.DAXon.Types.Type.NAMESPACE) && p.GetParent().IsSameNodeInfo(this))
                {
                    return null;
                }

                while (true)
                {
                    SnapshotNode q = (SnapshotNode)p.GetParent();
                    if (q == null)
                    {
                        throw new System.InvalidOperationException("pivot is not a descendant of this node");
                    }

                    if (q.IsSameNodeInfo(this))
                    {
                        return p;
                    }

                    p = q;
                }
            }
        }

        protected SnapshotNode(NodeInfo @base, NodeInfo pivot) : base(@base, pivot.Root)
        {
            this.pivot = pivot;
        }

        public static SnapshotNode MakeSnapshot(NodeInfo original)
        {
            SnapshotNode vc = new SnapshotNode(original, original);
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
            SnapshotNode vc = new SnapshotNode(node, pivot);
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
                    if (Navigator.IsAncestorOrSelf(pivot, original))
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
            if (!original.IsSameNodeInfo(pivot) && Navigator.IsAncestorOrSelf(original, pivot))
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
                    return Navigator.IsAncestorOrSelf(pivot, sourceNode) || Navigator.IsAncestorOrSelf(sourceNode, pivot);
            }
        }
    }
}
