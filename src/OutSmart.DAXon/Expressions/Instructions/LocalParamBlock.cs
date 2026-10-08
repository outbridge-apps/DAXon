////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Types;
using System.Collections.Generic;
using System.Linq;
using OutSmart.DAXon.Expressions.Elaboration;
namespace OutSmart.DAXon.Expressions.Instructions
{
    /// <summary>
    /// Represents the set of xsl:param elements at the start of an xsl:iterate instruction
    /// </summary>
    internal sealed class LocalParamBlock : Instruction
    {
        readonly Operand[] operanda;

        public override string ExpressionName => "params";

        public int NumberOfParams => operanda.Length;

        public override int ImplementationMethod => PROCESS_METHOD;
        public LocalParamBlock(LocalParam[] @params)
        {
            operanda = new Operand[@params.Length];
            for (int i = 0; i < @params.Length; i++)
            {
                operanda[i] = new Operand(this, @params[i], OperandRole.NAVIGATE);
            }
        }

        public override IEnumerable<Operand> Operands()
        {
            return operanda.ToList();
        }

        protected override int ComputeSpecialProperties()
        {
            return 0;
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            LocalParam[] lps2 = new LocalParam[NumberOfParams];
            int i = 0;
            foreach (Operand o in Operands())
            {
                LocalParam oldLps = (LocalParam)o.GetChildExpression();
                LocalParam newLps = (LocalParam)(oldLps.Copy(rebindings));
                rebindings.Put(oldLps, newLps);
                lps2[i++] = newLps;
            }

            return new LocalParamBlock(lps2);
        }

        public override ItemType GetItemType()
        {
            return ErrorType.GetInstance();
        }

        public override int GetCardinality()
        {
            return StaticProperty.EMPTY;
        }

        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("params", this);
            foreach (Operand o in Operands())
            {
                o.GetChildExpression().Export(@out);
            }

            @out.EndElement();
        }

        public override Elaborator GetElaborator()
        {
            return new LocalParamBlockElaborator();
        }

        internal sealed class LocalParamBlockElaborator : PushElaborator
        {
            public override IPushEvaluator ElaborateForPush()
            {
                LocalParamBlock expr = (LocalParamBlock)GetExpression();
                ISequenceEvaluator[] paramEval = new ISequenceEvaluator[expr.operanda.Length];
                for (int i = 0; i < expr.operanda.Length; i++)
                {
                    paramEval[i] = expr.operanda[i].GetChildExpression().MakeElaborator().Eagerly();
                }

                return (@out, context) =>
                {
                    foreach (ISequenceEvaluator eagerEvaluator in paramEval)
                    {
                        eagerEvaluator.Evaluate(context);
                    }

                    return null;
                };
            }
        }
    }
}