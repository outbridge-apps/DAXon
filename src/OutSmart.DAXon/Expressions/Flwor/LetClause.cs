////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Types;
using static OutSmart.DAXon.Expressions.Flwor.Clause.ClauseName;
using System;
using System.Collections.Generic;
using System.Text;
using OutSmart.DAXon.Expressions.Parsing;
namespace OutSmart.DAXon.Expressions.Flwor
{
    /// <summary>
    /// A "let" clause in a FLWOR expression
    /// </summary>
    internal sealed class LetClause : Clause
    {
        private LocalVariableBinding rangeVariable;
        private Operand sequenceOp;
        private ISequenceEvaluator variableEvaluator;
        public override ClauseName ClauseKey => LET;

        public Expression Sequence
        {
            get => sequenceOp.GetChildExpression(); set
            {
                sequenceOp.SetChildExpression(value);
            }
        }

        public LocalVariableBinding RangeVariable
        {
            get => rangeVariable; set
            {
                this.rangeVariable = value;
            }
        }

        public override LocalVariableBinding[] RangeVariables => new LocalVariableBinding[]
            {
                rangeVariable
            };

        public ISequenceEvaluator GetEvaluator()
        {
            if (variableEvaluator == null)
            {
                variableEvaluator = new LearningEvaluator(Sequence, Sequence.MakeElaborator().Lazily(true, false));
            }

            return variableEvaluator;
        }

        public override Clause Copy(FLWORExpression flwor, RebindingMap rebindings)
        {
            LetClause let2 = new LetClause();
            let2.Location = Location;
            let2.SetPackageData(GetPackageData());
            let2.rangeVariable = rangeVariable.Copy();
            let2.InitSequence(flwor, Sequence.Copy(rebindings));
            return let2;
        }

        public void InitSequence(FLWORExpression flwor, Expression sequence)
        {
            sequenceOp = new Operand(flwor, sequence, IsRepeated() ? OperandRole.REPEAT_NAVIGATE : OperandRole.NAVIGATE);
        }

        public void EvaluateRangeVariable(IXPathContext context)
        {
            if (variableEvaluator == null)
            {
                GetEvaluator();
            }

            ISequence val = variableEvaluator.Evaluate(context);
            context.SetLocalVariable(RangeVariable.LocalSlotNumber, val);
        }

        public override TuplePull GetPullStream(TuplePull @base, IXPathContext context)
        {
            return new LetClausePull(@base, this);
        }

        public override TuplePush GetPushStream(TuplePush destination, Outputter output, IXPathContext context)
        {
            return new LetClausePush(output, destination, this);
        }

        public override void ProcessOperands(IOperandProcessor processor)
        {
            processor.ProcessOperand(sequenceOp);
        }

        public override void TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.VARIABLE, rangeVariable.GetVariableQName().DisplayName, 0);
            if (visitor.StaticContext.GetXPathVersion() < 40)
            {
                Sequence = TypeChecker.StrictTypeCheck(Sequence, rangeVariable.GetRequiredType(), role, visitor.StaticContext);
            }
            else
            {
                TypeChecker tc = visitor.GetConfiguration().GetTypeChecker(false);
                Sequence = tc.StaticTypeCheck(Sequence, rangeVariable.GetRequiredType(), role, visitor);
            }
        }

        public override void GatherVariableReferences(ExpressionVisitor visitor, IBinding binding, IList<VariableReference> references)
        {
            ExpressionTool.GatherVariableReferences(Sequence, binding, references);
        }

        public override void RefineVariableType(ExpressionVisitor visitor, IList<VariableReference> references, Expression returnExpr)
        {
            Expression seq = Sequence;
            ItemType actualItemType = seq.GetItemType();
            foreach (VariableReference @ref in references)
            {
                @ref.RefineVariableType(actualItemType, Sequence.GetCardinality(), seq is Literal ? ((Literal)seq).GroundedValue : null, seq.GetSpecialProperties());
                ExpressionTool.ResetStaticProperties(returnExpr);
            }
        }

        public override void AddToPathMap(PathMap pathMap, PathMap.PathMapNodeSet pathMapNodeSet)
        {
            PathMap.PathMapNodeSet varPath = Sequence.AddToPathMap(pathMap, pathMapNodeSet);
            pathMap.RegisterPathForVariable(rangeVariable, varPath);
        }

        public override void Explain(ExpressionPresenter @out)
        {
            @out.StartElement("let");
            @out.EmitAttribute("var", RangeVariable.GetVariableQName());
            @out.EmitAttribute("slot", RangeVariable.LocalSlotNumber + "");
            Sequence.Export(@out);
            @out.EndElement();
        }

        public override string ToShortString()
        {
            StringBuilder fsb = new StringBuilder(64);
            fsb.Append("let $");
            fsb.Append(rangeVariable.GetVariableQName().DisplayName);
            fsb.Append(" := ");
            fsb.Append(Sequence.ToShortString());
            return fsb.ToString();
        }

        public override string ToString()
        {
            StringBuilder fsb = new StringBuilder(64);
            fsb.Append("let $");
            fsb.Append(rangeVariable.GetVariableQName().DisplayName);
            fsb.Append(" := ");
            fsb.Append(Sequence.ToString());
            return fsb.ToString();
        }
    }
}
