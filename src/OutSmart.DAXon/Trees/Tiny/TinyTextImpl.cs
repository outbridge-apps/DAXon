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
using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Trees.Tiny
{
    /// <summary>
    /// A node in the XML parse tree representing character content
    /// </summary>
    internal sealed class TinyTextImpl : TinyNodeImpl
    {

        public override UnicodeString UnicodeStringValue => GetStringValue(tree, nodeNr);
        public TinyTextImpl(TinyTree tree, int nodeNr) : base(tree, nodeNr)
        {
        }

        public static UnicodeString GetStringValue(TinyTree tree, int nodeNr)
        {

            //        return tree.textChunks[tree.alpha[nodeNr]];
            int start = tree.alpha[nodeNr];
            int len = tree.beta[nodeNr];

            return tree.textBuffer.Substring(start, start + len);
        }

        // In-place whiteness for whitespace stripping (no Substring per node).
        internal static bool IsWhitespaceOnly(TinyTree tree, int nodeNr)
        {
            int start = tree.alpha[nodeNr];
            return tree.textBuffer.IsAllWhite(start, start + tree.beta[nodeNr]);
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

        /// <summary>
        /// Copy this node to a given outputter
        /// </summary>
        public override IAtomicSequence Atomize()
        {
            return StringValue.MakeUntypedAtomic(UnicodeStringValue);
        }
    }
}
