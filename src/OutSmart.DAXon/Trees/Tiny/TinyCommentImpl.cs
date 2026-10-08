////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Trees.Tiny
{
    sealed class TinyCommentImpl : TinyNodeImpl
    {

        public override UnicodeString UnicodeStringValue
        {
            get
            {
                int start = tree.alpha[nodeNr];
                int len = tree.beta[nodeNr];
                if (len == 0)
                {
                    return EmptyUnicodeString.GetInstance();
                }

                return tree.commentBuffer.Substring(start, start + len);
            }
        }
        public TinyCommentImpl(TinyTree tree, int nodeNr) : base(tree, nodeNr)
        {
        }

        public override IAtomicSequence Atomize()
        {
            return new StringValue(UnicodeStringValue);
        }

        public override int GetNodeKind()
        {
            return Types.Type.COMMENT;
        }

        /// <summary>
        /// Copy this node to a given outputter
        /// </summary>
        public override void Copy(IReceiver @out, int copyOptions, ILocation locationId)
        {
            @out.Comment(UnicodeStringValue, locationId, ReceiverOption.NONE);
        }
    }
}
