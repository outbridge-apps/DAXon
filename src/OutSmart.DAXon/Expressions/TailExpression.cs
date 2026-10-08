////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
namespace OutSmart.DAXon.Expressions
{
    internal sealed class TailExpression : UnaryExpression
    {
        int start; // 1-based offset of first item from base expression

        public override int ImplementationMethod => ITERATE_METHOD;

        /* | StaticProperty.ALLOWS_ONE */
        public int Start => start;

        /* | StaticProperty.ALLOWS_ONE */
        public override string StreamerName => "TailExpression";

        /* | StaticProperty.ALLOWS_ONE */
        public override string ExpressionName => "tail";
        public TailExpression(Expression @base, int start) : base(@base)
        {
            this.start = start;
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            try
            {
                GetOperand().Optimize(visitor, contextInfo);
                if (BaseExpression is Literal)
                {
                    IGroundedValue value = SequenceTool.ToGroundedValue(Iterate(visitor.StaticContext.MakeEarlyEvaluationContext()));
                    return Literal.MakeLiteral(value, this);
                }

                return this;
            }
            catch (UncheckedXPathException e)
            {
                throw e.GetXPathException();
            }
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            TailExpression exp = new TailExpression(BaseExpression.Copy(rebindings), start);
            ExpressionTool.CopyLocationInfo(this, exp);
            return exp;
        }

        public override ItemType GetItemType()
        {
            return BaseExpression.GetItemType();
        }

        protected override int ComputeCardinality()
        {

            // bug 6313
            return BaseExpression.GetCardinality() | StaticProperty.ALLOWS_ZERO;
        }

        /* | StaticProperty.ALLOWS_ONE */
        protected override OperandRole GetOperandRole()
        {
            return OperandRole.SAME_FOCUS_ACTION;
        }

        /* | StaticProperty.ALLOWS_ONE */
        public override bool Equals(object other)
        {
            return other is TailExpression && BaseExpression.IsEqual(((TailExpression)other).BaseExpression) && start == ((TailExpression)other).start;
        }

        /* | StaticProperty.ALLOWS_ONE */
        protected override int ComputeHashCode()
        {
            return base.ComputeHashCode() ^ start;
        }

        /* | StaticProperty.ALLOWS_ONE */
        public override ISequenceIterator Iterate(IXPathContext context)
        {
            ISequenceIterator baseIter = BaseExpression.Iterate(context);
            return TailIterator.Make(baseIter, start);
        }

        /* | StaticProperty.ALLOWS_ONE */
        public override void Export(ExpressionPresenter destination)
        {
            destination.StartElement("tail", this);
            destination.EmitAttribute("start", start + "");
            BaseExpression.Export(destination);
            destination.EndElement();
        }

        /* | StaticProperty.ALLOWS_ONE */
        public override string ToString()
        {
            if (start == 2)
            {
                return "tail(" + BaseExpression + ")";
            }
            else
            {
                return ExpressionTool.Parenthesize(BaseExpression) + "[position() ge " + start + "]";
            }
        }

        /* | StaticProperty.ALLOWS_ONE */
        public override string ToShortString()
        {
            if (start == 2)
            {
                return "tail(" + BaseExpression.ToShortString() + ")";
            }
            else
            {
                return BaseExpression.ToShortString() + "[position() ge " + start + "]";
            }
        }

        /* | StaticProperty.ALLOWS_ONE */
        public override Elaborator GetElaborator()
        {
            return new TailExprElaborator();
        }

        /* | StaticProperty.ALLOWS_ONE */
        /// <summary>
        /// Elaborator for a tail expression
        /// </summary>
        internal sealed class TailExprElaborator : PullElaborator
        {
            public override IPullEvaluator ElaborateForPull()
            {
                TailExpression expr = (TailExpression)GetExpression();
                IPullEvaluator baseEval = expr.BaseExpression.MakeElaborator().ElaborateForPull();
                int start = expr.Start;
                return (context) =>
                {
                    ISequenceIterator baseIter = baseEval.Iterate(context);
                    return TailIterator.Make(baseIter, start);
                };
            }
        }
    }
}