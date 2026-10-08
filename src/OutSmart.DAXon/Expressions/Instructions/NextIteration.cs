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
using OutSmart.DAXon.Internal.Collections;
using System.Collections.Generic;
namespace OutSmart.DAXon.Expressions.Instructions
{
    /// <summary>
    /// Implements a xsl:next-iteration instruction within the body of xsl:iterate
    /// </summary>
    internal sealed class NextIteration : Instruction, TailCallLoop.ITailCallInfo
    {
        private WithParam[] actualParams;

        public WithParam[] Parameters
        {
            get => actualParams; set
            {
                this.actualParams = value;
            }
        }

        public override int InstructionNameCode => StandardNames.XSL_NEXT_ITERATION;

        public override string StreamerName => "NextIteration";
        public NextIteration()
        {
        }

        public override bool IsLiftable(bool forStreaming)
        {
            return false;
        }

        public override Expression Simplify()
        {
            WithParam.Simplify(actualParams);
            return this;
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            WithParam.TypeCheck(actualParams, visitor, contextInfo);
            return this;
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            NextIteration c2 = new NextIteration();
            ExpressionTool.CopyLocationInfo(this, c2);
            c2.actualParams = WithParam.Copy(c2, actualParams, rebindings);
            return c2;
        }

        public override IEnumerable<Operand> Operands()
        {
            IList<Operand> list = new List<Operand>();
            WithParam.GatherOperands(this, actualParams, list);
            return list;
        }

        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("nextIteration", this);
            if (actualParams != null && actualParams.Length > 0)
            {
                WithParam.ExportParameters(actualParams, @out, false);
            }

            @out.EndElement();
        }

        public override Elaborator GetElaborator()
        {
            return new NextIterationElaborator();
        }

        internal sealed class NextIterationElaborator : PushElaborator
        {
            public override IPushEvaluator ElaborateForPush()
            {
                NextIteration expr = (NextIteration)GetExpression();
                return (output, context) =>
                {
                    XPathContextMajor cm = context.MajorContext;
                    if (expr.actualParams.Length == 1)
                    {
                        cm.SetLocalVariable(expr.actualParams[0].SlotNumber, expr.actualParams[0].GetSelectValue(context));
                    }
                    else
                    {

                        // we can't overwrite any of the parameters until we've evaluated all of them: test iterate012
                        ISequence[] oldVars = cm.AllVariableValues;
                        ISequence[] newVars = ArrayTools.CopyOf(oldVars, oldVars.Length);
                        foreach (WithParam wp in expr.actualParams)
                        {
                            newVars[wp.SlotNumber] = wp.GetSelectValue(context);
                        }

                        cm.ResetAllVariableValues(newVars);
                    }

                    cm.RequestTailCall(expr, null);
                    return null;
                };
            }
        }
    }
}