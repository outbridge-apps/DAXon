////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;

namespace OutSmart.DAXon.Trees.Wrappers
{
    // Faithful port of net.sf.saxon.tree.wrapper.VirtualUntypedCopy (Saxon 12.9). Was a hollow stub, so the
    // lazy (pull-mode) xsl:copy-of path with validation=strip crashed (copy-43xx family in xslt30-test).
    // A virtual copy of a node with type annotations stripped.
    internal sealed class VirtualUntypedCopy : VirtualCopy
    {

        protected VirtualUntypedCopy(NodeInfo @base, NodeInfo root) : base(@base, root)
        {
        }
        /// <summary>
        /// Public factory method: create a new untyped virtual tree as a copy of a node
        /// </summary>
        public static VirtualCopy MakeVirtualUntypedTree(NodeInfo original, NodeInfo root)
        {
            VirtualCopy vc;
            // Don't allow copies of copies of copies: define the new copy in terms of the original
            while (original is VirtualUntypedCopy && original.GetParent() == null)
            {
                original = ((VirtualUntypedCopy)original).original;
                root = ((VirtualUntypedCopy)root).original;
            }

            vc = new VirtualUntypedCopy(original, root);
            Configuration config = original.GetConfiguration();
            VirtualTreeInfo doc = new VirtualTreeInfo(config, vc);
            vc.tree = doc;
            return vc;
        }

        public override ISchemaType GetSchemaType()
        {
            switch (GetNodeKind())
            {
                case OutSmart.DAXon.Types.Type.ELEMENT:
                    return Untyped.GetInstance();
                case OutSmart.DAXon.Types.Type.ATTRIBUTE:
                    return BuiltInAtomicType.UNTYPED_ATOMIC;
                default:
                    return base.GetSchemaType();
            }
        }

        public override IAtomicSequence Atomize()
        {
            switch (GetNodeKind())
            {
                case OutSmart.DAXon.Types.Type.ELEMENT:
                case OutSmart.DAXon.Types.Type.ATTRIBUTE:
                    return StringValue.MakeUntypedAtomic(UnicodeStringValue);
                default:
                    return base.Atomize();
            }
        }

        public override void Copy(IReceiver @out, int copyOptions, ILocation locationId)
        {
            base.Copy(@out, copyOptions & ~CopyOptions.TYPE_ANNOTATIONS, locationId);
        }

        protected override VirtualCopy Wrap(NodeInfo node)
        {
            VirtualUntypedCopy vc = new VirtualUntypedCopy(node, root);
            vc.tree = tree;
            return vc;
        }

        public override bool IsNilled() => false;
    }
}
