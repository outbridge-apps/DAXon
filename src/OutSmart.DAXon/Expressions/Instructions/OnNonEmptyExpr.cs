////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Expressions.Instructions
{
    /// <summary>
    /// A compiled xsl:on-non-empty instruction.
    /// </summary>
    internal sealed class OnNonEmptyExpr : UnaryExpression
    {

        public override int IntrinsicDependencies => StaticProperty.HAS_SIDE_EFFECTS;

        public override int ImplementationMethod => BaseExpression.ImplementationMethod;

        public override string ExpressionName => "onNonEmpty";

        public override string StreamerName => "OnNonEmpty";
        public OnNonEmptyExpr(Expression @base) : base(@base)
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

        public override Expression Copy(RebindingMap rebindings)
        {
            return new OnNonEmptyExpr(BaseExpression.Copy(rebindings));
        }

        public override bool AllowExtractingCommonSubexpressions()
        {
            return false;
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            GetOperand().TypeCheck(visitor, contextInfo);
            return this;
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            GetOperand().Optimize(visitor, contextInfo);
            if (visitor.IsOptimizeForStreaming())
            {
                visitor.ObtainOptimizer().MakeCopyOperationsExplicit(this, GetOperand());
            }

            return this;
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
            @out.StartElement("onNonEmpty", this);
            BaseExpression.Export(@out);
            @out.EndElement();
        }

        public override Elaborator GetElaborator()
        {
            return new SequenceInstr.SequenceInstrElaborator();
        }
    }
}