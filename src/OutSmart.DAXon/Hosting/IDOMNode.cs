////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

// DOMLocator Ã¢â‚¬â€ W3C DOM type referenced by Saxon validation code. Stub interface
// inside OutSmart.DAXon.Lib so it's visible without extra using-directives at the
// call sites (StandardInvalidityHandler / StandardDiagnostics both live in this ns).
namespace OutSmart.DAXon.Lib
{
    internal interface IDOMNode { string GetNodeName(); }
}
