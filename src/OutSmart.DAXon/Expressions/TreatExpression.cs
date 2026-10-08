////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

using System;

namespace OutSmart.DAXon.Expressions
{

    // Treat Expression: implements "treat as data-type ( expression )". Factory only.
    // Ported from upstream net/sf/saxon/expr/TreatExpression.java: a CardinalityChecker
    // wrapping an ItemChecker, both carrying a RoleDiagnostic with error code XPDY0050,
    // so the operand's type is checked at runtime.
    internal abstract class TreatExpression
    {

        public static Expression Make(Expression sequence, OutSmart.DAXon.Values.SequenceType type)
        {
            return Make(sequence, type, "XPDY0050");
        }

        public static Expression Make(Expression sequence, OutSmart.DAXon.Values.SequenceType type, string errorCode)
        {
            Func<OutSmart.DAXon.Expressions.Parsing.RoleDiagnostic> role =
                () => new OutSmart.DAXon.Expressions.Parsing.RoleDiagnostic(
                    OutSmart.DAXon.Expressions.Parsing.RoleDiagnostic.TYPE_OP, "treat as", 0, errorCode);
            Expression e = CardinalityChecker.MakeCardinalityChecker(sequence, type.GetCardinality(), role);
            return new ItemChecker(e, type.PrimaryType, role);
        }
    }
}
