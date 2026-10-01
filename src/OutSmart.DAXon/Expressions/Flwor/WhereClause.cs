////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal.Collections;
using static OutSmart.DAXon.Expressions.Flwor.Clause.ClauseName;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Expressions.Flwor
{
    /// <summary>
    /// A "where" clause in a FLWOR expression
    /// </summary>
    internal sealed class WhereClause : Clause
    {
        private readonly Operand predicateOp;
        // volatile, not a lock (round 11): this is published once and then read on a hot path, the
        // same shape UserFunction.bodyEvaluator documents. Construction is a pure function of the
        // compiled expression, so a lost race just discards an equivalent object; what needs the
        // barrier is PUBLICATION of a fully built one.
        private volatile IBooleanEvaluator predicateEvaluator;

        public override ClauseName ClauseKey => WHERE;

        public Expression Predicate
        {
            get => predicateOp.GetChildExpression(); set
            {
                predicateOp.SetChildExpression(value);
            }
        }

        public override Dictionary<string, object> TraceInfo
        {
            get
            {
                Dictionary<string, object> info = new Dictionary<string, object>(1);
                info["condition"] = Predicate.ToShortString();
                return info;
            }
        }
        public WhereClause(FLWORExpression flwor, Expression predicate)
        {
            this.predicateOp = new Operand(flwor, predicate, OperandRole.INSPECT);
        }

        public override void SetRepeated(bool repeated)
        {
            base.SetRepeated(repeated);
            if (repeated)
            {
                this.predicateOp.OperandRole = OperandRole.REPEAT_INSPECT;
            }
        }

        public override Clause Copy(FLWORExpression flwor, RebindingMap rebindings)
        {
            WhereClause w2 = new WhereClause(flwor, Predicate.Copy(rebindings));
            w2.Location = Location;
            w2.SetPackageData(GetPackageData());
            return w2;
        }

        public override void TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            base.TypeCheck(visitor, contextInfo);
        }

        public override TuplePull GetPullStream(TuplePull @base, IXPathContext context)
        {
            if (predicateEvaluator == null)
            {
                predicateEvaluator = Predicate.MakeElaborator().ElaborateForBoolean();
            }

            return new WhereClausePull(@base, predicateEvaluator);
        }

        public override void GatherVariableReferences(ExpressionVisitor visitor, IBinding binding, IList<VariableReference> references)
        {
            ExpressionTool.GatherVariableReferences(Predicate, binding, references);
        }

        public override void RefineVariableType(ExpressionVisitor visitor, IList<VariableReference> references, Expression returnExpr)
        {
            ItemType actualItemType = Predicate.GetItemType();
            foreach (VariableReference @ref in references)
            {
                @ref.RefineVariableType(actualItemType, Predicate.GetCardinality(), Predicate is Literal ? ((Literal)Predicate).GroundedValue : null, Predicate.GetSpecialProperties());
                ExpressionTool.ResetStaticProperties(returnExpr);
            }
        }

        public override TuplePush GetPushStream(TuplePush destination, Outputter output, IXPathContext context)
        {
            if (predicateEvaluator == null)
            {
                predicateEvaluator = Predicate.MakeElaborator().ElaborateForBoolean();
            }

            return new WhereClausePush(output, destination, predicateEvaluator);
        }

        public override void ProcessOperands(IOperandProcessor processor)
        {
            processor.ProcessOperand(predicateOp);
        }

        public override void AddToPathMap(PathMap pathMap, PathMap.PathMapNodeSet pathMapNodeSet)
        {
            Predicate.AddToPathMap(pathMap, pathMapNodeSet);
        }

        public override void Explain(ExpressionPresenter @out)
        {
            @out.StartElement("where");
            Predicate.Export(@out);
            @out.EndElement();
        }

        public override string ToShortString()
        {
            StringBuilder fsb = new StringBuilder(64);
            fsb.Append("where ");
            fsb.Append(Predicate.ToShortString());
            return fsb.ToString();
        }

        public override string ToString()
        {
            StringBuilder fsb = new StringBuilder(64);
            fsb.Append("where ");
            fsb.Append(Predicate.ToString());
            return fsb.ToString();
        }
    }
}