////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

namespace OutSmart.DAXon.Expressions.Elaboration
{
    /// <summary>
    /// Interface implemented by expressions that return a boolean result.
    /// </summary>
    // IBooleanEvaluator interface -> delegate for lambda assignability.
    public delegate bool IBooleanEvaluator(IXPathContext context);
}
