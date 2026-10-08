////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Parsing;
namespace OutSmart.DAXon.Expressions
{
    internal sealed class GeneralComparison20 : GeneralComparison
    {

        protected override GeneralComparison InverseComparison
        {
            get
            {
                GeneralComparison20 gc = new GeneralComparison20(GetRhsExpression(), Token.Inverse(@operator), GetLhsExpression());
                gc.SetRetainedStaticContext(GetRetainedStaticContext());
                return gc;
            }
        }
        public GeneralComparison20(Expression p0, int op, Expression p1) : base(p0, op, p1)
        {
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            GeneralComparison20 gc = new GeneralComparison20(GetLhsExpression().Copy(rebindings), @operator, GetRhsExpression().Copy(rebindings));
            ExpressionTool.CopyLocationInfo(this, gc);
            gc.SetRetainedStaticContext(GetRetainedStaticContext());
            gc.comparer = comparer;
            gc.singletonOperator = singletonOperator;
            gc.runtimeCheckNeeded = runtimeCheckNeeded;
            gc.comparisonCardinality = comparisonCardinality;
            return gc;
        }
    }
}