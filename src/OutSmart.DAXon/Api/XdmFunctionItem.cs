////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;

namespace OutSmart.DAXon.Api
{
    public class XdmFunctionItem : XdmItem
    {
        // Public as it was in 1.3, for the hosts built against it; s9api has no such constructor.
        [Obsolete("A function item made this way holds no function, and any use of it fails: take one from an expression.")]
        public XdmFunctionItem() : base(null) { }
        public XdmFunctionItem(OutSmart.DAXon.Model.IItem function) : base(function) { }
        public override bool IsAtomicValue() => false;
    }
}
