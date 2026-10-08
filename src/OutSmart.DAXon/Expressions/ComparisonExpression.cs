////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Lib;
namespace OutSmart.DAXon.Expressions
{
    /// <summary>
    /// Interface implemented by expressions that perform a comparison
    /// </summary>
    public interface IComparisonExpression
    {
        IAtomicComparer GetAtomicComparer();
        IStringCollator StringCollator { get; }
        int SingletonOperator { get; }
        Operand Lhs { get; }
        Operand Rhs { get; }
        Expression GetLhsExpression();
        Expression GetRhsExpression();
        bool ConvertsUntypedToOther();
    }
}