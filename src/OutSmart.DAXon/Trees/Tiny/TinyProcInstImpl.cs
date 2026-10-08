////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Trees.Utilities;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Trees.Tiny
{
    /// <summary>
    /// TinyProcInstImpl is a node in the TinyTree representing a processing instruction
    /// </summary>
    sealed class TinyProcInstImpl : TinyNodeImpl
    {

        public override UnicodeString UnicodeStringValue
        {
            get
            {
                int start = tree.alpha[nodeNr];
                int len = tree.beta[nodeNr];
                if (len == 0)
                {
                    return EmptyUnicodeString.GetInstance(); // need to special-case this for the Microsoft JVM
                }

                return tree.commentBuffer.Substring(start, start + len);
            }
        }
        public TinyProcInstImpl(TinyTree tree, int nodeNr) : base(tree, nodeNr)
        {
        }

        public override IAtomicSequence Atomize()
        {
            return new StringValue(UnicodeStringValue);
        }

        public override int GetNodeKind()
        {
            return Types.Type.PROCESSING_INSTRUCTION;
        }

        /// <summary>
        /// Get the base URI of this processing instruction node.
        /// </summary>
        public override string GetBaseURI()
        {
            return Navigator.GetBaseURI(this);
        }

        public override void Copy(IReceiver @out, int copyOptions, ILocation locationId)
        {
            @out.ProcessingInstruction(DisplayName, UnicodeStringValue, locationId, ReceiverOption.NONE);
        }
    }
}
