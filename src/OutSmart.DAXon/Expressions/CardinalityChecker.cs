////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
using System.Text;
using OutSmart.DAXon.Expressions.Parsing;
namespace OutSmart.DAXon.Expressions
{
    internal sealed class CardinalityChecker : UnaryExpression
    {
        private readonly int requiredCardinality = -1;
        private readonly Func<RoleDiagnostic> roleSupplier;

        public int RequiredCardinality => requiredCardinality;

        public override int ImplementationMethod
        {
            get
            {
                int m = ITERATE_METHOD | PROCESS_METHOD | ITEM_FEED_METHOD;
                if (!Cardinality.AllowsMany(requiredCardinality))
                {
                    m |= EVALUATE_METHOD;
                }

                return m;
            }
        }

        public override IntegerValue[] IntegerBounds => BaseExpression.IntegerBounds;

        public override string ExpressionName => "CheckCardinality";

        public override string StreamerName => "CardinalityChecker";
        private CardinalityChecker(Expression sequence, int cardinality, Func<RoleDiagnostic> role) : base(sequence)
        {
            requiredCardinality = cardinality;
            this.roleSupplier = role; //computeStaticProperties();
        }

        public static Expression MakeCardinalityChecker(Expression sequence, int cardinality, Func<RoleDiagnostic> roleSupplier)
        {
            Expression result;
            if (sequence is Literal && Cardinality.Subsumes(cardinality, SequenceTool.GetCardinality(((Literal)sequence).GroundedValue)))
            {
                return sequence;
            }

            if (sequence is Atomizer && !Cardinality.AllowsMany(cardinality))
            {
                Expression @base = ((Atomizer)sequence).BaseExpression;
                result = new SingletonAtomizer(@base, roleSupplier, Cardinality.AllowsZero(cardinality));
            }
            else
            {
                result = new CardinalityChecker(sequence, cardinality, roleSupplier);
            }

            ExpressionTool.CopyLocationInfo(sequence, result);
            return result;
        }

        protected override OperandRole GetOperandRole()
        {
            return OperandRole.SAME_FOCUS_ACTION;
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            GetOperand().TypeCheck(visitor, contextInfo);
            Expression @base = BaseExpression;
            if (requiredCardinality == StaticProperty.ALLOWS_ZERO_OR_MORE || Cardinality.Subsumes(requiredCardinality, @base.GetCardinality()))
            {
                return @base;
            }

            return this;
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            GetOperand().Optimize(visitor, contextInfo);
            Expression @base = BaseExpression;
            if (requiredCardinality == StaticProperty.ALLOWS_ZERO_OR_MORE || Cardinality.Subsumes(requiredCardinality, @base.GetCardinality()))
            {
                return @base;
            }

            if ((@base.GetCardinality() & requiredCardinality) == 0)
            {
                RoleDiagnostic role = roleSupplier();
                throw new XPathException("The " + role.GetMessage() + " does not satisfy the cardinality constraints", role.ErrorCode).WithLocation(GetLocation()).AsTypeErrorIf(role.IsTypeError());
            }


            // do cardinality checking before item checking (may avoid the need for a mapping iterator)
            if (@base is ItemChecker)
            {
                ItemChecker checker = (ItemChecker)@base;
                Expression other = checker.BaseExpression;

                // change this -> checker -> other to checker -> this -> other
                BaseExpression = other;
                checker.BaseExpression = this;
                checker.ParentExpression = null;
                return checker;
            }

            return this;
        }

        public override ISequenceIterator Iterate(IXPathContext context)
        {
            ISequenceIterator @base = BaseExpression.Iterate(context);
            return CheckCardinality(@base, context);
        }

        public ISequenceIterator CheckCardinality(ISequenceIterator @base, IXPathContext context)
        {

            // If the base iterator knows how many items there are, then check it now rather than wasting time
            if (SequenceTool.SupportsGetLength(@base))
            {
                int count = SequenceTool.GetLength(@base);
                if (count == 0 && !Cardinality.AllowsZero(requiredCardinality))
                {
                    RoleDiagnostic role = roleSupplier();
                    TypeError("An empty sequence is not allowed as the " + role.GetMessage(), role.ErrorCode, context);
                }
                else if (count == 1 && requiredCardinality == StaticProperty.EMPTY)
                {
                    RoleDiagnostic role = roleSupplier();
                    TypeError("The only value allowed for the " + role.GetMessage() + " is an empty sequence", role.ErrorCode, context);
                }
                else if (count > 1 && !Cardinality.AllowsMany(requiredCardinality))
                {
                    RoleDiagnostic role = roleSupplier();
                    TypeError("A sequence of more than one item is not allowed as the " + role.GetMessage() + DepictSequenceStart(@base, 2), role.ErrorCode, context);
                }

                return @base;
            }


            // Otherwise return an iterator that does the checking on the fly
            try
            {
                return new CardinalityCheckingIterator(@base, requiredCardinality, roleSupplier, GetLocation());
            }
            catch (XPathException e)
            {
                throw e.MaybeWithContext(context);
            }
        }

        public static string DepictSequenceStart(ISequenceIterator seq, int max)
        {
            StringBuilder sb = new StringBuilder(64);
            int count = 0;
            sb.Append(" (");
            IItem next;
            while ((next = seq.Next()) != null)
            {
                if (count++ > 0)
                {
                    sb.Append(", ");
                }

                if (count > max)
                {
                    sb.Append("...) ");
                    return sb.ToString();
                }

                sb.Append(Err.Depict(next));
            }

            sb.Append(") ");
            return sb.ToString();
        }

        public override IItem EvaluateItem(IXPathContext context)
        {
            return SingleItem(null, context);
        }

        // The singleton check with its two-item lookahead inline: no CardinalityCheckingIterator.
        // A null operand evaluator means the interpreted operand.
        private IItem SingleItem(IPullEvaluator operand, IXPathContext context)
        {
            try
            {
                ISequenceIterator iter = operand == null ? BaseExpression.Iterate(context) : operand(context);
                IItem first = iter.Next();
                if (first == null)
                {
                    if (!Cardinality.AllowsZero(requiredCardinality))
                    {
                        RoleDiagnostic role = roleSupplier();
                        TypeError("An empty sequence is not allowed as the " + role.GetMessage(), role.ErrorCode, context);
                    }

                    return null;
                }
                else
                {
                    if (requiredCardinality == StaticProperty.EMPTY)
                    {
                        RoleDiagnostic role = roleSupplier();
                        TypeError("An empty sequence is required as the " + role.GetMessage(), role.ErrorCode, context);
                        return null;
                    }

                    IItem second = iter.Next();
                    if (second != null)
                    {
                        RoleDiagnostic role = roleSupplier();
                        TypeError("A sequence of more than one item is not allowed as the " + role.GetMessage() + DepictSequenceStart(new TwoItemIterator(first, second), 2), role.ErrorCode, context);
                        return null;
                    }
                }

                return first;
            }
            catch (UncheckedXPathException e)
            {
                throw e.GetXPathException();
            }
        }

        public override void Process(Outputter output, IXPathContext context)
        {
            IPushEvaluator pusher = MakeElaborator().ElaborateForPush();
            ITailCall tc = pusher.ProcessLeavingTail(output, context);
            Expression.DispatchTailCall(tc);
        }

        public override Types.ItemType GetItemType()
        {
            return BaseExpression.GetItemType();
        }

        protected override int ComputeCardinality()
        {
            return requiredCardinality;
        }

        protected override int ComputeSpecialProperties()
        {
            return BaseExpression.GetSpecialProperties();
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            CardinalityChecker c2 = new CardinalityChecker(BaseExpression.Copy(rebindings), requiredCardinality, roleSupplier);
            ExpressionTool.CopyLocationInfo(this, c2);
            return c2;
        }

        public override bool Equals(object other)
        {
            return base.Equals(other) && requiredCardinality == ((CardinalityChecker)other).requiredCardinality;
        }

        protected override int ComputeHashCode()
        {
            return base.ComputeHashCode() ^ requiredCardinality;
        }

        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("check", this);
            string occ = Cardinality.GetOccurrenceIndicator(requiredCardinality);
            if (occ.Equals(""))
            {
                occ = "1";
            }

            @out.EmitAttribute("card", occ);
            @out.EmitAttribute("diag", roleSupplier().Save());
            BaseExpression.Export(@out);
            @out.EndElement();
        }

        public override string ToString()
        {
            Expression operand = BaseExpression;
            switch (requiredCardinality)
            {
                case StaticProperty.ALLOWS_ONE:
                    return "exactly-one(" + operand + ")";
                case StaticProperty.ALLOWS_ZERO_OR_ONE:
                    return "zero-or-one(" + operand + ")";
                case StaticProperty.ALLOWS_ONE_OR_MORE:
                    return "one-or-more(" + operand + ")";
                case StaticProperty.EMPTY:
                    return "must-be-empty(" + operand + ")";
                default:
                    return "check(" + operand + ")";
            }
        }

        public override string ToShortString()
        {
            return BaseExpression.ToShortString();
        }

        public override void SetLocation(ILocation id)
        {
            base.SetLocation(id);
        }

        public override Elaborator GetElaborator()
        {
            return new CardinalityCheckerElaborator();
        }

        internal sealed class CardinalityCheckerElaborator : PullElaborator
        {
            public override IPullEvaluator ElaborateForPull()
            {
                CardinalityChecker expr = (CardinalityChecker)GetExpression();
                Expression arg = expr.BaseExpression;
                IPullEvaluator argEval = arg.MakeElaborator().ElaborateForPull();
                return (context) => expr.CheckCardinality(argEval.Iterate(context), context);
            }

            // Item consumers (fused scalar-fn args): the two-item lookahead inline, skipping the
            // CardinalityCheckingIterator of the pull path. The operand is elaborated here, once:
            // EvaluateItem's interpreted Iterate re-elaborated it on every call.
            public override IItemEvaluator ElaborateForItem()
            {
                CardinalityChecker expr = (CardinalityChecker)GetExpression();
                IPullEvaluator argEval = expr.BaseExpression.MakeElaborator().ElaborateForPull();
                return (context) => expr.SingleItem(argEval, context);
            }

            public override IPushEvaluator ElaborateForPush()
            {
                CardinalityChecker expr = (CardinalityChecker)GetExpression();
                Expression next = expr.BaseExpression;
                Types.ItemType type = Types.Type.ITEM_TYPE;
                if (next is ItemChecker)
                {
                    type = ((ItemChecker)next).GetRequiredType();
                    next = ((ItemChecker)next).BaseExpression;
                }

                if ((next.ImplementationMethod & PROCESS_METHOD) != 0 && !(type is DocumentNodeTest))
                {
                    Types.ItemType finalType = type;
                    IPushEvaluator pushEval = next.MakeElaborator().ElaborateForPush();
                    return (output, context) =>
                    {
                        TypeCheckingFilter filter = new TypeCheckingFilter(output);
                        filter.SetRequiredType(finalType, expr.requiredCardinality, expr.roleSupplier(), expr.GetLocation());
                        ITailCall tc = pushEval.ProcessLeavingTail(filter, context);
                        Expression.DispatchTailCall(tc);
                        try
                        {
                            filter.FinalCheck();
                        }
                        catch (XPathException e)
                        {
                            throw e.MaybeWithLocation(expr.GetLocation());
                        }

                        return null;
                    };
                }
                else
                {

                    // Force pull-mode evaluation
                    IPullEvaluator argEval = next.MakeElaborator().ElaborateForPull();
                    return (output, context) =>
                    {
                        ISequenceIterator iter = expr.CheckCardinality(argEval.Iterate(context), context);
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