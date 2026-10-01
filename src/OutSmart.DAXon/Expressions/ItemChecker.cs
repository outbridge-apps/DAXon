////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Expressions
{
    internal sealed class ItemChecker : UnaryExpression
    {
        private readonly ItemType requiredItemType;
        private readonly Func<RoleDiagnostic> roleSupplier;

        public override int ImplementationMethod
        {
            get
            {
                int m = ITERATE_METHOD | PROCESS_METHOD | ITEM_FEED_METHOD;
                if (!Cardinality.AllowsMany(GetCardinality()))
                {
                    m |= EVALUATE_METHOD;
                }

                return m;
            }
        }

        public override string StreamerName => "ItemChecker";

        public override IntegerValue[] IntegerBounds => BaseExpression.IntegerBounds;

        public override string ExpressionName => "treatAs";
        public ItemChecker(Expression sequence, ItemType itemType, Func<RoleDiagnostic> roleSupplier) : base(sequence)
        {
            requiredItemType = itemType;
            this.roleSupplier = roleSupplier;
        }

        public ItemType GetRequiredType()
        {
            return requiredItemType;
        }

        protected override OperandRole GetOperandRole()
        {
            return OperandRole.SAME_FOCUS_ACTION;
        }

        public override Expression Simplify()
        {
            Expression operand = BaseExpression.Simplify();
            if (requiredItemType is AnyItemType)
            {
                return operand;
            }

            BaseExpression = operand;
            return this;
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            GetOperand().TypeCheck(visitor, contextInfo);
            Expression operand = BaseExpression;
            if (operand is Block)
            {

                // Do the item-checking on each operand of the block separately (it might not be needed on all items)
                // This is particularly needed for streamability analysis of xsl:map
                Block block = (Block)operand;
                IList<Expression> checkedOperands = new List<Expression>();
                foreach (Operand o in block.Operands())
                {
                    ItemChecker checkedOp = new ItemChecker(o.GetChildExpression(), requiredItemType, roleSupplier);
                    checkedOperands.Add(checkedOp);
                }

                Block newBlock = new Block(checkedOperands.ToArray());
                ExpressionTool.CopyLocationInfo(this, newBlock);
                return newBlock.TypeCheck(visitor, contextInfo);
            }


            // When typeCheck is called a second time, we might have more information...
            TypeHierarchy th = GetConfiguration().GetTypeHierarchy();
            int card = operand.GetCardinality();
            if (card == StaticProperty.EMPTY)
            {

                //value is always empty, so no item checking needed
                return operand;
            }

            ItemType supplied = operand.GetItemType();
            Affinity relation = th.Relationship(requiredItemType, supplied);
            if (relation == Affinity.SAME_TYPE || relation == Affinity.SUBSUMES)
            {
                return operand;
            }
            else if (relation == Affinity.DISJOINT)
            {
                if (Cardinality.AllowsZero(card))
                {
                    if (!(operand is Literal))
                    {
                        RoleDiagnostic role = roleSupplier();
                        string message = role.ComposeErrorMessage(requiredItemType, operand, th);
                        visitor.StaticContext.IssueWarning("The only value that can pass type-checking is an empty sequence. " + message, DAXonErrorCode.SXWN9026, GetLocation());
                    }
                }
                else
                {
                    RoleDiagnostic role = roleSupplier();
                    string message = role.ComposeErrorMessage(requiredItemType, operand, th);
                    throw new XPathException(message).WithErrorCode(role.ErrorCode).WithLocation(this.GetLocation()).AsTypeErrorIf(role.IsTypeError());
                }
            }

            return this;
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            GetOperand().Optimize(visitor, contextInfo);
            TypeHierarchy th = visitor.GetConfiguration().GetTypeHierarchy();
            Affinity rel = th.Relationship(requiredItemType, BaseExpression.GetItemType());
            if (rel == Affinity.SAME_TYPE || rel == Affinity.SUBSUMES)
            {
                return BaseExpression;
            }

            return this;
        }

        /// <summary>
        /// Iterate over the sequence of values
        /// </summary>
        public override ISequenceIterator Iterate(IXPathContext context)
        {
            return MakeElaborator().ElaborateForPull().Iterate(context);
        }

        // TypeHierarchy and the checking closure are per-Configuration constants: build them once at
        // elaboration time so the per-evaluation cost is just the ItemCheckingIterator allocation.
        internal Action<IItem> MakeHoistedChecker()
        {
            Expression baseExpr = BaseExpression;
            TypeHierarchy th = GetConfiguration().GetTypeHierarchy();
            return (item) =>
            {
                if (!requiredItemType.Matches(item, th))
                {
                    RoleDiagnostic role = roleSupplier();
                    string message = role.ComposeErrorMessage(requiredItemType, item, th);
                    string errorCode = role.ErrorCode;
                    XPathException te = new XPathException(message, errorCode).WithFailingExpression(baseExpr).WithLocation(baseExpr.GetLocation()).AsTypeErrorIf(errorCode != "XPDY0050");
                    throw new UncheckedXPathException(te);
                }
            };
        }

        public override IItem EvaluateItem(IXPathContext context)
        {
            return MakeElaborator().ElaborateForItem().Eval(context);
        }

        internal IItem CheckItem(IItem item, TypeHierarchy th, IXPathContext context)
        {
            if (item == null)
            {
                return null;
            }

            if (requiredItemType.Matches(item, th))
            {
                return item;
            }
            else
            {
                RoleDiagnostic role = roleSupplier();
                string message = role.ComposeErrorMessage(requiredItemType, item, th);
                string errorCode = role.ErrorCode;
                if (errorCode == "XPDY0050")
                {

                    // error in "treat as" assertion
                    DynamicError(message, errorCode, context);
                }
                else
                {
                    TypeError(message, errorCode, context);
                }

                return null;
            }
        }

        public override void Process(Outputter output, IXPathContext context)
        {
            DispatchTailCall(MakeElaborator().ElaborateForPush().ProcessLeavingTail(output, context));
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            ItemChecker exp = new ItemChecker(BaseExpression.Copy(rebindings), requiredItemType, roleSupplier);
            ExpressionTool.CopyLocationInfo(this, exp);
            return exp;
        }

        /// <summary>
        /// Determine the data type of the items returned by the expression
        /// </summary>
        public override ItemType GetItemType()
        {
            ItemType operandType = BaseExpression.GetItemType();
            TypeHierarchy th = GetConfiguration().GetTypeHierarchy();
            Affinity relationship = th.Relationship(requiredItemType, operandType);
            switch (relationship)
            {
                case Affinity.OVERLAPS:
                    if (requiredItemType is NodeTest && operandType is NodeTest)
                    {
                        return new CombinedNodeTest((NodeTest)requiredItemType, Token.INTERSECT, (NodeTest)operandType);
                    }
                    else
                    {

                        // we don't know how to intersect atomic types, it doesn't actually happen
                        return requiredItemType;
                    }

                case Affinity.SUBSUMES:
                case Affinity.SAME_TYPE:

                    // shouldn't happen, but it doesn't matter
                    return operandType;
                case Affinity.SUBSUMED_BY:
                default:
                    return requiredItemType;
            }
        }

        /// <summary>
        /// Determine the data type of the items returned by the expression
        /// </summary>
        public override UType GetStaticUType(UType contextItemType)
        {
            return UType.FromTypeCode(requiredItemType.PrimitiveType);
        }

        public override bool Equals(object other)
        {
            return base.Equals(other) && requiredItemType == ((ItemChecker)other).requiredItemType;
        }

        protected override int ComputeHashCode()
        {
            return base.ComputeHashCode() ^ requiredItemType.GetHashCode();
        }

        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("treat", this);
            @out.EmitAttribute("as", AlphaCode.FromItemType(requiredItemType));
            @out.EmitAttribute("diag", roleSupplier().Save());
            BaseExpression.Export(@out);
            @out.EndElement();
        }

        public override string ToString()
        {
            string typeDesc = requiredItemType.ToString();
            return "(" + BaseExpression + ") treat as " + typeDesc;
        }

        public override string ToShortString()
        {
            return BaseExpression.ToShortString();
        }

        public override Elaborator GetElaborator()
        {
            return new ItemCheckerElaborator();
        }

        internal sealed class ItemCheckerElaborator : PullElaborator
        {
            public override IPullEvaluator ElaborateForPull()
            {
                ItemChecker exp = (ItemChecker)GetExpression();
                Expression arg = exp.BaseExpression;
                IPullEvaluator argEval = arg.MakeElaborator().ElaborateForPull();
                Action<IItem> checker = exp.MakeHoistedChecker();
                return (context) => new ItemCheckingIterator(argEval.Iterate(context), checker);
            }

            public override IItemEvaluator ElaborateForItem()
            {
                ItemChecker expr = (ItemChecker)GetExpression();
                Expression arg = expr.BaseExpression;
                IItemEvaluator argEval = arg.MakeElaborator().ElaborateForItem();
                TypeHierarchy th = expr.GetConfiguration().GetTypeHierarchy();
                return (context) => expr.CheckItem(argEval.Eval(context), th, context);
            }

            public override IPushEvaluator ElaborateForPush()
            {
                ItemChecker expr = (ItemChecker)GetExpression();
                Expression arg = expr.BaseExpression;
                int card = StaticProperty.ALLOWS_ZERO_OR_MORE;
                if (arg is CardinalityChecker)
                {
                    card = ((CardinalityChecker)arg).RequiredCardinality;
                    arg = ((CardinalityChecker)arg).BaseExpression;
                }

                if ((arg.ImplementationMethod & PROCESS_METHOD) != 0 && !(expr.requiredItemType is DocumentNodeTest))
                {
                    int finalCard = card;
                    IPushEvaluator argPush = arg.MakeElaborator().ElaborateForPush();
                    return (output, context) =>
                    {
                        TypeCheckingFilter filter = new TypeCheckingFilter(output);
                        filter.SetRequiredType(expr.requiredItemType, finalCard, expr.roleSupplier(), expr.GetLocation());
                        ITailCall tc = argPush.ProcessLeavingTail(filter, context);
                        Expression.DispatchTailCall(tc);
                        filter.FinalCheck();
                        return null;
                    };
                }
                else
                {

                    // Force pull-mode evaluation
                    IPullEvaluator argEval = arg.MakeElaborator().ElaborateForPull();
                    Action<IItem> checker = expr.MakeHoistedChecker();
                    return (output, context) =>
                    {
                        ISequenceIterator iter = new ItemCheckingIterator(argEval.Iterate(context), checker);
                        for (IItem item; (item = iter.Next()) != null;)
                        {
                            output.Append(item);
                        }

                        return null;
                    };
                }
            }
        }
    }
}