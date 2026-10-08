////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Trees.Tiny
{
    /// <summary>
    /// A node in the XML parse tree representing a text node with compressed whitespace content
    /// </summary>
    internal sealed class WhitespaceTextImpl : TinyNodeImpl
    {

        public override UnicodeString UnicodeStringValue
        {
            get
            {
                long value = ((long)tree.alpha[nodeNr] << 32) | ((long)tree.beta[nodeNr] & 0xffffffff);
                return new CompressedWhitespace(value);
            }
        }
        public WhitespaceTextImpl(TinyTree tree, int nodeNr) : base(tree, nodeNr)
        {
        }

        public static UnicodeString GetStringValue(TinyTree tree, int nodeNr)
        {
            long value = ((long)tree.alpha[nodeNr] << 32) | ((long)tree.beta[nodeNr] & 0xffffffff);
            return new CompressedWhitespace(value);
        }

        public static void AppendStringValue(TinyTree tree, int nodeNr, UnicodeBuilder buffer)
        {
            long value = ((long)tree.alpha[nodeNr] << 32) | ((long)tree.beta[nodeNr] & 0xffffffff);
            buffer.Append(CompressedWhitespace.Uncompress(value));
        }

        public override OutSmart.DAXon.Model.IAtomicSequence Atomize()
        {
            return StringValue.MakeUntypedAtomic(UnicodeStringValue);
        }

        public override int GetNodeKind()
        {
            return Types.Type.TEXT;
        }

        /// <summary>
        /// Copy this node to a given outputter
        /// </summary>
        public override void Copy(IReceiver @out, int copyOptions, ILocation locationId)
        {
            @out.Characters(UnicodeStringValue, locationId, ReceiverOption.NONE);
        }
    }
}
