////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace OutSmart.DAXon.Transformation
{
    // Faithful port of net/sf/saxon/trans/QuitParsingException.java (Saxon 12.9). Was a stub extending
    // raw Exception with an NIE implicit-conversion operator — upstream it IS an XPathException
    // (code SXQP0001), thrown by a Receiver to abandon parsing early (fn:stream-available probe,
    // streamed early-exit); catch sites (ReceivingContentHandler, XsltController) treat it specially.
    internal sealed class QuitParsingException : XPathException
    {
        private readonly bool notifiedByConsumer;

        public QuitParsingException(bool notifiedByConsumer)
            : base("The input file has not been read to completion", "SXQP0001")
        {
            this.notifiedByConsumer = notifiedByConsumer;
        }
    }
}
