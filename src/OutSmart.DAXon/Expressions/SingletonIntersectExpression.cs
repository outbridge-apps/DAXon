////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Ported from upstream net/sf/saxon/expr/SingletonIntersectExpression.java (replaces the Phase 4.8c stub).

using System;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;

namespace OutSmart.DAXon.Expressions
{
    /// <summary>
    /// This expression is equivalent to (A intersect B) in the case where A has cardinality
    /// zero-or-one. This is handled as a special case because the standard sort-merge algorithm
    /// involves an unnecessary sort on B.
    /// </summary>
    internal sealed class SingletonIntersectExpression : VennExpression
    {

        public override string ExpressionName => "singleton-intersect";

        public override int ImplementationMethod => ITERATE_METHOD;
        public SingletonIntersectExpression(Expression p1, int op, Expression p2) : base(p1, op, p2)
        {
        }

        public override Expression Simplify()
        {
            return this;
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {
            return this;
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            SingletonIntersectExpression exp = new SingletonIntersectExpression(GetLhsExpression().Copy(rebindings), @operator, GetRhsExpression().Copy(rebindings));
            ExpressionTool.CopyLocationInfo(this, exp);
            return exp;
        }

        /// <summary>
        /// Iterate over the value of the expression: the singleton LHS node if it occurs in the RHS
        /// node-set, otherwise the empty sequence.
        /// </summary>
        public override ISequenceIterator Iterate(IXPathContext c)
        {
            NodeInfo m = (NodeInfo)GetLhsExpression().EvaluateItem(c);
            if (m == null)
            {
                return EmptyIterator.GetInstance();
            }

            ISequenceIterator iter = GetRhsExpression().Iterate(c);
            NodeInfo n;
            while ((n = (NodeInfo)iter.Next()) != null)
            {
                if (n.Equals(m))
                {
                    return SingletonIterator.MakeIterator(m);
                }
            }

            return EmptyIterator.GetInstance();
        }

        public override bool EffectiveBooleanValue(IXPathContext c)
        {
            NodeInfo m = (NodeInfo)GetLhsExpression().EvaluateItem(c);
            return m != null && ContainsNode(GetRhsExpression().Iterate(c), m);
        }

        /// <summary>
        /// Ask whether the sequence supplied in the first argument contains the node supplied in the
        /// second. The iterator is closed if the node is found.
        /// </summary>
        public static bool ContainsNode(ISequenceIterator iter, NodeInfo m)
        {
            NodeInfo n;
            while ((n = (NodeInfo)iter.Next()) != null)
            {
                if (n.Equals(m))
                {
                    return true;
                }
            }

            return false;
        }

        protected override string DisplayOperator()
        {
            return "among";
        }

        protected override string Tag()
        {
            return "among";
        }

        public override Elaborator GetElaborator()
        {
            return new SingletonIntersectElaborator();
        }

        /// <summary>Elaborator for a singleton-intersect expression (A among B).</summary>
        internal sealed class SingletonIntersectElaborator : ItemElaborator
        {
            public override IItemEvaluator ElaborateForItem()
            {
                SingletonIntersectExpression exp = (SingletonIntersectExpression)GetExpression();
                IItemEvaluator lhs = exp.GetLhsExpression().MakeElaborator().ElaborateForItem();
                IPullEvaluator rhs = exp.GetRhsExpression().MakeElaborator().ElaborateForPull();
                return (context) =>
                {
                    NodeInfo node = (NodeInfo)lhs.Eval(context);
                    if (node == null)
                    {
                        return null;
                    }

                    ISequenceIterator nodeSet = rhs.Iterate(context);
                    return ContainsNode(nodeSet, node) ? node : null;
                };
            }
        }
    }
}
