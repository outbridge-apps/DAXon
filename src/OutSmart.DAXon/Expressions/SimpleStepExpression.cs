////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Elaboration;using OutSmart.DAXon.Functions;

using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Expressions
{
    internal sealed class SimpleStepExpression : SlashExpression
    {

        private static readonly OperandRole STEP_ROLE = new OperandRole(OperandRole.USES_NEW_FOCUS | OperandRole.HIGHER_ORDER, OperandUsage.TRANSMISSION, SequenceType.ANY_SEQUENCE);

        public override int ImplementationMethod => ITERATE_METHOD;

        public override string ExpressionName => "simpleStep";
        public SimpleStepExpression(Expression start, Expression step) : base(start, step)
        {
            if (!(step is AxisExpression))
            {
                throw new ArgumentException();
            }
        }

        public AxisExpression GetAxisExpression()
        {
            return (AxisExpression)GetStep();
        }
        protected override OperandRole GetOperandRole(int arg)
        {
            return arg == 0 ? OperandRole.FOCUS_CONTROLLING_SELECT : STEP_ROLE;
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            Lhs.TypeCheck(visitor, contextInfo);
            ItemType selectType = Start.GetItemType();
            if (selectType == ErrorType.GetInstance())
            {
                return Literal.MakeEmptySequence();
            }

            ContextItemStaticInfo cit = visitor.GetConfiguration().MakeContextItemStaticInfo(selectType, false);
            cit.ContextSettingExpression = Start;
            Rhs.TypeCheck(visitor, cit);
            if (!(GetStep() is AxisExpression))
            {
                if (Literal.IsEmptySequence(GetStep()))
                {
                    return GetStep();
                }

                SlashExpression se = new SlashExpression(Start, GetStep());
                ExpressionTool.CopyLocationInfo(this, se);
                return se;
            }

            if (Start is ContextItemExpression && AxisInfo.isForwards[((AxisExpression)GetStep()).Axis])
            {
                return GetStep();
            }

            return this;
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {
            return this;
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            Expression lhs = Start.Copy(rebindings);
            Expression rhs = GetStep().Copy(rebindings);
            if (!(rhs is AxisExpression))
            {
                SlashExpression se = new SlashExpression(Start, GetStep());
                ExpressionTool.CopyLocationInfo(this, se);
                return se;
            }

            SimpleStepExpression exp = new SimpleStepExpression(lhs, rhs);
            ExpressionTool.CopyLocationInfo(this, exp);
            return exp;
        }

        public override ISequenceIterator Iterate(IXPathContext context)
        {
            return MakeElaborator().ElaborateForPull().Iterate(context); //        NodeInfo origin;
            //        try {
            //            origin = (NodeInfo) getStart().evaluateItem(context);
            //        } catch (XPathException e) {
            //                throw new XPathException("The context item for axis step "
            //                    + toShortString() + " is absent", "XPDY0002", getLocation());
            //            } else {
            //                throw e;
        }

        public override Elaborator GetElaborator()
        {
            return new SimpleStepExprElaborator();
        }

        /// <summary>
        /// Elaborator for a simple step expression, that is X/axis.Y where X evaluates to a singleton
        /// </summary>
        internal sealed class SimpleStepExprElaborator : PullElaborator
        {
            public override IPullEvaluator ElaborateForPull()
            {

                // TODO: do we need to catch exceptions as in the iterate() method?
                SimpleStepExpression expr = (SimpleStepExpression)GetExpression();
                IItemEvaluator select = expr.GetSelectExpression().MakeElaborator().ElaborateForItem();
                AxisExpression step = (AxisExpression)expr.GetStep();
                bool nullable = Cardinality.AllowsZero(expr.GetSelectExpression().GetCardinality());
                if (nullable)
                {
                    return (context) =>
                    {
                        NodeInfo origin = (NodeInfo)select.Eval(context);
                        if (origin == null)
                        {
                            return EmptyIterator.GetInstance();
                        }

                        return step.Iterate(origin);
                    };
                }
                else
                {
                    return (context) =>
                    {
                        NodeInfo start = (NodeInfo)select.Eval(context);
                        return step.Iterate(start);
                    };
                }
            }
        }
    }
}