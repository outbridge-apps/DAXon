////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Trees.Tiny;
using OutSmart.DAXon.Types;
using System;
using System.Collections.Generic;

namespace OutSmart.DAXon.Trees.Wrappers
{
    // Faithful port of net.sf.saxon.tree.wrapper.SpaceStrippedDocument (Saxon 12.9). Was a hollow stub whose
    // Wrap threw, so ANY stylesheet with xsl:strip-space crashed in Controller.PrepareInputTree.
    // A view of a real document in which selected whitespace text nodes are treated as stripped.
    internal sealed class SpaceStrippedDocument : GenericTreeInfo
    {
        private readonly ISpaceStrippingRule strippingRule;
        private readonly bool preservesSpace;
        private readonly bool _containsAssertions;
        private readonly ITreeInfo underlyingTree;

        public ISpaceStrippingRule StrippingRule => strippingRule;

        public override IEnumerator<string> UnparsedEntityNames => underlyingTree.UnparsedEntityNames;

        public SpaceStrippedDocument(ITreeInfo doc, ISpaceStrippingRule strippingRule) : base(doc.GetConfiguration())
        {
            SetRootNode(Wrap(doc.GetRootNode()));
            this.strippingRule = strippingRule;
            this.underlyingTree = doc;
            preservesSpace = FindPreserveSpace(doc);
            _containsAssertions = FindAssertions(doc);
        }

        public SpaceStrippedNode Wrap(NodeInfo node)
        {
            return SpaceStrippedNode.MakeWrapper(node, this, null);
        }

        public override bool IsTyped() => underlyingTree.IsTyped();

        public override NodeInfo SelectID(string id, bool getParent)
        {
            NodeInfo n = underlyingTree.SelectID(id, false);
            if (n == null)
            {
                return null;
            }
            else
            {
                return Wrap(n);
            }
        }

        public override Durability GetDurability() => underlyingTree.GetDurability();

        public override String[] GetUnparsedEntity(string name) => underlyingTree.GetUnparsedEntity(name);

        // Scan the document in advance for xml:space="preserve" (cheaper than checking every whitespace text node)
        private static bool FindPreserveSpace(ITreeInfo doc)
        {
            if (doc is TinyTree)
            {
                // Optimisation - see bug 2929. Makes a vast difference especially if there are few attributes in the tree
                return ((TinyTree)doc).HasXmlSpacePreserveAttribute();
            }
            else
            {
                IAxisIterator iter = doc.GetRootNode().IterateAxis(AxisInfo.DESCENDANT, NodeKindTest.ELEMENT);
                NodeInfo node;
                while ((node = iter.Next()) != null)
                {
                    string val = node.GetAttributeValue(NamespaceUri.XML, "space");
                    if (val == "preserve")
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        // Whether the wrapped document contains any nodes annotated with complex types that define assertions
        private static bool FindAssertions(ITreeInfo doc)
        {
            if (doc.IsTyped())
            {
                IAxisIterator iter = doc.GetRootNode().IterateAxis(AxisInfo.DESCENDANT, NodeKindTest.ELEMENT);
                while (true)
                {
                    NodeInfo node = iter.Next();
                    if (node == null)
                    {
                        return false;
                    }

                    ISchemaType type = node.GetSchemaType();
                    if (type.IsComplexType() && ((IComplexType)type).HasAssertions())
                    {
                        return true;
                    }
                }
            }
            else
            {
                return false;
            }
        }

        public bool ContainsPreserveSpace() => preservesSpace;

        public bool ContainsAssertions() => _containsAssertions;

        // Memo of the strip/preserve rule verdict by parent-element fingerprint: the rule lookup
        // allocates a NameOfNode per whitespace text and the verdict is name-deterministic. Same
        // direct-mapped immutable-slot pattern as SimpleMode's dispatch memo - safe under any
        // sharing, bounded by construction.
        private const int VerdictSlots = 64;
        private VerdictSlot[] verdicts;

        private sealed class VerdictSlot
        {
            internal readonly int Fp;
            internal readonly bool Preserved;

            internal VerdictSlot(int fp, bool preserved)
            {
                Fp = fp;
                Preserved = preserved;
            }
        }

        internal bool PreservedByRule(NodeInfo actualParent)
        {
            int fp = actualParent.HasFingerprint() ? actualParent.Fingerprint : -1;
            VerdictSlot[] slots = null;
            if (fp >= 0)
            {
                slots = verdicts ?? (verdicts = new VerdictSlot[VerdictSlots]);
                VerdictSlot slot = slots[fp & (VerdictSlots - 1)];
                if (slot != null && slot.Fp == fp)
                {
                    return slot.Preserved;
                }
            }

            bool preserved;
            try
            {
                preserved = strippingRule.IsSpacePreserving(NameOfNode.MakeName(actualParent), null) == Stripper.ALWAYS_PRESERVE;
            }
            catch (XPathException)
            {
                // Ambiguity between strip-space and preserve-space. Take the recovery action.
                preserved = true;
            }

            if (slots != null)
            {
                slots[fp & (VerdictSlots - 1)] = new VerdictSlot(fp, preserved);
            }

            return preserved;
        }
    }
}
