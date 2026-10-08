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
using OutSmart.DAXon.Types;
using System.Collections.Generic;

namespace OutSmart.DAXon.Expressions.Instructions
{
    /// <summary>
    /// This class implements an xsl:fork expression.
    /// </summary>
    internal sealed class Fork : Instruction
    {
        internal Operand[] operanda;

        public override int InstructionNameCode => StandardNames.XSL_FORK;

        public int Size => operanda.Length;

        public override string StreamerName => "Fork";

        public Fork(Operand[] prongs)
        {
            operanda = new Operand[prongs.Length];
            for (int i = 0; i < prongs.Length; i++)
            {
                operanda[i] = new Operand(this, prongs[i].GetChildExpression(), OperandRole.SAME_FOCUS_ACTION);
            }
        }

        public Fork(Expression[] prongs)
        {
            operanda = new Operand[prongs.Length];
            for (int i = 0; i < prongs.Length; i++)
            {
                operanda[i] = new Operand(this, prongs[i], OperandRole.SAME_FOCUS_ACTION);
            }
        }

        public override IEnumerable<Operand> Operands()
        {
            return operanda;
        }

        public Expression GetProng(int i)
        {
            return operanda[i].GetChildExpression();
        }

        public override ItemType GetItemType()
        {
            if (Size == 0)
            {
                return ErrorType.GetInstance();
            }

            ItemType t1 = null;
            foreach (Operand o in Operands())
            {
                ItemType t2 = o.GetChildExpression().GetItemType();
                t1 = t1 == null ? t2 : OutSmart.DAXon.Types.Type.GetCommonSuperType(t1, t2);
                if (t1 is AnyItemType)
                {
                    return t1; // no point going any further
                }
            }

            return t1;
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            Expression[] e2 = new Expression[Size];
            int i = 0;
            foreach (Operand o in Operands())
            {
                e2[i++] = o.GetChildExpression().Copy(rebindings);
            }

            Fork f2 = new Fork(e2);
            ExpressionTool.CopyLocationInfo(this, f2);
            return f2;
        }

        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("fork", this);
            foreach (Operand o in Operands())
            {
                o.GetChildExpression().Export(@out);
            }

            @out.EndElement();
        }

        public override Elaborator GetElaborator()
        {
            return new ForkElaborator();
        }

        private sealed class ForkElaborator : PushElaborator
        {
            public override IPushEvaluator ElaborateForPush()
            {
                Fork expr = (Fork)GetExpression();
                IPushEvaluator[] prongs = new IPushEvaluator[expr.Size];
                for (int i = 0; i < prongs.Length; i++)
                {
                    prongs[i] = expr.GetProng(i).MakeElaborator().ElaborateForPush();
                }

                return (output, context) =>
                {
                    // non-streamed evaluation
                    foreach (IPushEvaluator prong in prongs)
                    {
                        Expression.DispatchTailCall(prong(output, context));
                    }

                    return null;
                };
            }
        }
    }
}
