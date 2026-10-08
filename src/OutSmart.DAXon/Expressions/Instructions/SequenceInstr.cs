////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
namespace OutSmart.DAXon.Expressions.Instructions
{
    internal sealed class SequenceInstr : UnaryExpression
    {

        public override int ImplementationMethod => BaseExpression.ImplementationMethod;

        public override string ExpressionName => "sequence";

        public override string StreamerName => "SequenceInstr";
        public SequenceInstr(Expression @base) : base(@base)
        {
        }

        public override bool IsInstruction()
        {
            return true;
        }

        protected override OperandRole GetOperandRole()
        {
            return new OperandRole(0, OperandUsage.TRANSMISSION);
        }

        //    @Override
        //    public Expression simplify() throws XPathException {
        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            GetOperand().TypeCheck(visitor, contextInfo);
            return this;
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            GetOperand().Optimize(visitor, contextInfo);
            return this;
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            return new SequenceInstr(BaseExpression.Copy(rebindings));
        }

        public override void Process(Outputter output, IXPathContext context)
        {
            DispatchTailCall(MakeElaborator().ElaborateForPush().ProcessLeavingTail(output, context));
        }

        public override ISequenceIterator Iterate(IXPathContext context)
        {
            return BaseExpression.Iterate(context);
        }

        public override void Export(ExpressionPresenter @out)
        {
            BaseExpression.Export(@out);
        }

        public override Elaborator GetElaborator()
        {
            return new SequenceInstrElaborator();
        }

        internal sealed class SequenceInstrElaborator : PushElaborator
        {
            public override IPullEvaluator ElaborateForPull()
            {
                UnaryExpression expr = (UnaryExpression)GetExpression();
                IPullEvaluator basePull = expr.BaseExpression.MakeElaborator().ElaborateForPull();
                return (context) => basePull.Iterate(context);
            }

            public override IPushEvaluator ElaborateForPush()
            {
                UnaryExpression expr = (UnaryExpression)GetExpression();
                IPushEvaluator basePush = expr.BaseExpression.MakeElaborator().ElaborateForPush();
                return (output, context) => basePush.ProcessLeavingTail(output, context);
            }
        }
    }
}