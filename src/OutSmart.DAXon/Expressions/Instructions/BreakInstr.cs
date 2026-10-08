////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Trees.Iterators;
using System.Collections.Generic;
using OutSmart.DAXon.Expressions.Elaboration;
namespace OutSmart.DAXon.Expressions.Instructions
{
    internal sealed class BreakInstr : Instruction, TailCallLoop.ITailCallInfo
    {

        public override int InstructionNameCode => StandardNames.XSL_BREAK;

        public override string ExpressionName => "xsl:break";
        public BreakInstr()
        {
        }

        public override IEnumerable<Operand> Operands()
        {
            return new List<Operand>();
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            BreakInstr b2 = new BreakInstr();
            ExpressionTool.CopyLocationInfo(this, b2);
            return b2;
        }

        public override bool MayCreateNewNodes()
        {

            // this is a fiction, but it prevents the instruction being moved to a global variable,
            // which would be pointless and possibly harmful
            return true;
        }

        public override bool IsLiftable(bool forStreaming)
        {
            return false;
        }

        public void MarkContext(IXPathContext context)
        {
            context.MajorContext.RequestTailCall(this, null);
        }

        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("break", this);
            @out.EndElement();
        }

        public override Elaborator GetElaborator()
        {
            return new BreakElaborator();
        }

        internal sealed class BreakElaborator : PushElaborator
        {
            public override IPushEvaluator ElaborateForPush()
            {
                BreakInstr expr = (BreakInstr)GetExpression();
                return (output, context) =>
                {
                    expr.MarkContext(context);
                    return null;
                };
            }

            public override IItemEvaluator ElaborateForItem()
            {
                BreakInstr expr = (BreakInstr)GetExpression();
                return (context) =>
                {
                    expr.MarkContext(context);
                    return null;
                };
            }

            public override IPullEvaluator ElaborateForPull()
            {
                BreakInstr expr = (BreakInstr)GetExpression();
                return (context) =>
                {
                    expr.MarkContext(context);
                    return EmptyIterator.GetInstance();
                };
            }
        }
    }
}
