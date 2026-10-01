////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using System.Collections.Generic;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;

namespace OutSmart.DAXon.Serialization
{
    // XHTML 1.x serializer: like the XML emitter, but a recognised XHTML empty element is closed with " />"
    // (space before the slash) while other empty elements get an explicit end tag. Was a hollow stub extending
    // the bare Emitter with an empty Append, so method=xhtml output hit SequenceReceiver.StartDocument stubs.
    internal sealed class XHTML1Emitter : XMLEmitter
    {
        internal static readonly HashSet<string> emptyTags1 = new HashSet<string>
        {
            "area", "base", "basefont", "br", "col", "embed", "frame", "hr", "img", "input", "isindex", "link", "meta", "param"
        };

        private bool IsRecognizedHtmlElement(INodeName name)
        {
            return name.HasURI(NamespaceUri.XHTML);
        }

        protected override void WriteEmptyElementTagCloser(string displayName, INodeName name)
        {
            if (IsRecognizedHtmlElement(name) && emptyTags1.Contains(name.GetLocalPart()))
            {
                writer.WriteAscii(StringConstants.EMPTY_TAG_END_XHTML);
            }
            else
            {
                writer.WriteAscii(StringConstants.EMPTY_TAG_MIDDLE);
                writer.Write(displayName);
                writer.WriteCodePoint('>');
            }
        }
    }
}
