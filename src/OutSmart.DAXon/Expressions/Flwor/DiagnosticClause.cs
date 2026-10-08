////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Types;
using static OutSmart.DAXon.Expressions.Flwor.Clause.ClauseName;
using System.Collections.Generic;
using System.Text;
using OutSmart.DAXon.Expressions.Parsing;
namespace OutSmart.DAXon.Expressions.Flwor
{
    /// <summary>
    /// A "let" clause in a FLWOR expression
    /// </summary>
    internal sealed class DiagnosticClause : Clause
    {
        private Operand sequenceOp;
        public override ClauseName ClauseKey => DIAG;

        //    }
        public Expression Sequence => sequenceOp.GetChildExpression();

        public override LocalVariableBinding[] RangeVariables => new LocalVariableBinding[]
            {
            };

        public override Clause Copy(FLWORExpression flwor, RebindingMap rebindings)
        {
            DiagnosticClause diag2 = new DiagnosticClause();
            diag2.Location = Location;
            diag2.SetPackageData(GetPackageData());
            diag2.InitSequence(flwor, Sequence.Copy(rebindings));
            return diag2;
        }

        public void InitSequence(FLWORExpression flwor, Expression sequence)
        {
            sequenceOp = new Operand(flwor, sequence, IsRepeated() ? OperandRole.REPEAT_NAVIGATE : OperandRole.NAVIGATE);
        }

        public override TuplePull GetPullStream(TuplePull @base, IXPathContext context)
        {
            return new DiagnosticClausePull();
        }

        public override TuplePush GetPushStream(TuplePush destination, Outputter output, IXPathContext context)
        {
            return new DiagnosticClausePush();
        }

        public override void ProcessOperands(IOperandProcessor processor)
        {
            processor.ProcessOperand(sequenceOp);
        }

        //    }
        public override void TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
        }

        //    }
        //
        public override void GatherVariableReferences(ExpressionVisitor visitor, IBinding binding, IList<VariableReference> references)
        {
            ExpressionTool.GatherVariableReferences(Sequence, binding, references);
        }

        //    }
        //
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

        //    }
        //
        public override void AddToPathMap(PathMap pathMap, PathMap.PathMapNodeSet pathMapNodeSet)
        {
            Sequence.AddToPathMap(pathMap, pathMapNodeSet);
        }

        //    }
        //
        public override void Explain(ExpressionPresenter @out)
        {
            @out.StartElement("trace");
            Sequence.Export(@out);
            @out.EndElement();
        }

        //    }
        //
        public override string ToShortString()
        {
            StringBuilder fsb = new StringBuilder(64);
            fsb.Append("trace ");
            fsb.Append(Sequence.ToShortString());
            return fsb.ToString();
        }

        //    }
        //
        public override string ToString()
        {
            StringBuilder fsb = new StringBuilder(64);
            fsb.Append("trace ");
            fsb.Append(Sequence.ToString());
            return fsb.ToString();
        }
    }
}
