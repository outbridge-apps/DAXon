////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
namespace OutSmart.DAXon.Expressions
{
    // Faithful port of net.sf.saxon.expr.CurrentItemExpression (Saxon 12.9). Was a hollow stub whose
    // GetItemType threw, so any use of current() crashed at type-check.
    // Generated when compiling the current() function in XSLT: differs from ContextItemExpression "."
    // only in the error code returned when there is no context item.
    internal sealed class CurrentItemExpression : ContextItemExpression
    {
        public CurrentItemExpression()
        {
            SetErrorCodeForUndefinedContext("XTDE1360", false);
        }
    }
}
