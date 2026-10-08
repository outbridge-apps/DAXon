////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace OutSmart.DAXon.Api
{
    /// <summary>
    /// Enumeration class defining the seven kinds of node defined in the XDM model
    /// </summary>
    public enum XdmNodeKind
    {
        // DOCUMENT(Types.DOCUMENT)
        DOCUMENT,
        // ELEMENT(Types.ELEMENT)
        ELEMENT,
        // ATTRIBUTE(Types.ATTRIBUTE)
        ATTRIBUTE,
        // TEXT(Types.TEXT)
        TEXT,
        // COMMENT(Types.COMMENT)
        COMMENT,
        // PROCESSING_INSTRUCTION(Types.PROCESSING_INSTRUCTION)
        PROCESSING_INSTRUCTION,
        // NAMESPACE(Types.NAMESPACE)
        NAMESPACE

        // --------------------
        // private final int number;
        // XdmNodeKind(int number) {
        //     this.number = number;
        // }
        // protected int getNumber() {
    }
}