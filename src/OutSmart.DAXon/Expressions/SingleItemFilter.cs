////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Parsing;

using OutSmart.DAXon.Values;
namespace OutSmart.DAXon.Expressions
{
    /// <summary>
    /// A SingleItemFilter is an expression that selects zero or one items from a supplied sequence
    /// </summary>
    internal abstract class SingleItemFilter : UnaryExpression
    {
        public SingleItemFilter(Expression @base) : base(@base)
        {
        }

        protected override OperandRole GetOperandRole()
        {
            return OperandRole.SAME_FOCUS_ACTION;
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            GetOperand().Optimize(visitor, contextInfo);
            Expression @base = BaseExpression;
            if (!Cardinality.AllowsMany(@base.GetCardinality()))
            {
                return @base;
            }

            // Java: return super.optimize(...) — the transpiler mangled `super` into the local `@base`,
            // so this returned the operand and DROPPED the filter wrapper (E[1] optimized to E). Call the
            // base-class Optimize (keeps `this`) instead.
            return base.Optimize(visitor, contextInfo);
        }

        protected override int ComputeCardinality()
        {
            return StaticProperty.ALLOWS_ZERO_OR_ONE;
        }
    }
}