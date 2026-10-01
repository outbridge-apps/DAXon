////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Model;

namespace OutSmart.DAXon.Expressions
{
    // Upstream: a LetExpression marker used when compiling with tracing, so variable values are
    // materialized eagerly and visible to the trace listener. Was a stub whose implicit conversion
    // to LetExpression THREW - compiling any XQuery FLWOR let with tracing enabled crashed.
    internal sealed class EagerLetExpression : LetExpression
    {
    }
}
