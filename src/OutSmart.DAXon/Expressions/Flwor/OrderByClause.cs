////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Values;
using static OutSmart.DAXon.Expressions.Flwor.Clause.ClauseName;
using System;
using System.Text;
using OutSmart.DAXon.Expressions.Parsing;
namespace OutSmart.DAXon.Expressions.Flwor
{
    /// <summary>
    /// This class represents an "order by" clause in a FLWOR expression
    /// </summary>
    internal sealed class OrderByClause : Clause
    {
        public static readonly OperandRole SORT_KEYS_ROLE = new OperandRole(OperandRole.HIGHER_ORDER | OperandRole.CONSTRAINED_CLASS, OperandUsage.NAVIGATION, SequenceType.ANY_SEQUENCE, (expr) => expr is SortKeyDefinitionList);
        Operand sortKeysOp; // Holds a SortKeyDefinitionList
        IAtomicComparer[] comparators;
        Operand tupleOp; // Holds a TupleExpression

        public override ClauseName ClauseKey => ORDER_BY;

        public SortKeyDefinitionList SortKeyDefinitions => (SortKeyDefinitionList)sortKeysOp.GetChildExpression();

        public IAtomicComparer[] AtomicComparers => comparators;
        public OrderByClause(FLWORExpression flwor, SortKeyDefinition[] sortKeys, TupleExpression tupleExpression)
        {
            this.sortKeysOp = new Operand(flwor, new SortKeyDefinitionList(sortKeys), SORT_KEYS_ROLE);
            this.tupleOp = new Operand(flwor, tupleExpression, OperandRole.FLWOR_TUPLE_CONSTRAINED);
        }

        public override bool ContainsNonInlineableVariableReference(IBinding binding)
        {
            return GetTupleExpression().IncludesBinding(binding);
        }

        public override Clause Copy(FLWORExpression flwor, RebindingMap rebindings)
        {
            SortKeyDefinitionList sortKeys = SortKeyDefinitions;
            SortKeyDefinition[] sk2 = new SortKeyDefinition[sortKeys.Count];
            for (int i = 0; i < sortKeys.Count; i++)
            {
                sk2[i] = (SortKeyDefinition)sortKeys.GetSortKeyDefinition(i).Copy(rebindings);
            }

            OrderByClause obc = new OrderByClause(flwor, sk2, (TupleExpression)GetTupleExpression().Copy(rebindings));
            obc.Location = Location;
            obc.SetPackageData(GetPackageData());
            obc.comparators = comparators;
            return obc;
        }

        public TupleExpression GetTupleExpression()
        {
            return (TupleExpression)tupleOp.GetChildExpression();
        }

        public override TuplePull GetPullStream(TuplePull @base, IXPathContext context)
        {
            return new OrderByClausePull(@base, GetTupleExpression(), this, context);
        }

        public override TuplePush GetPushStream(TuplePush destination, Outputter output, IXPathContext context)
        {
            return new OrderByClausePush(output, destination, GetTupleExpression(), this, context);
        }

        public override void ProcessOperands(IOperandProcessor processor)
        {
            processor.ProcessOperand(tupleOp);
            processor.ProcessOperand(sortKeysOp); //        for (SortKeyDefinition sortKey : sortKeys) {
        }

        public override void TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            bool allKeysFixed = true;
            SortKeyDefinitionList sortKeys = SortKeyDefinitions;
            foreach (SortKeyDefinition sk in sortKeys)
            {
                if (!sk.IsFixed())
                {
                    allKeysFixed = false;
                    break;
                }
            }

            if (allKeysFixed)
            {
                comparators = new IAtomicComparer[sortKeys.Count];
            }

            int i = 0;
            TypeChecker tc = visitor.GetConfiguration().GetTypeChecker(false);
            foreach (SortKeyDefinition skd in sortKeys)
            {
                Expression sortKey = skd.SortKey;
                int pos = i;
                Func<RoleDiagnostic> role = () =>
                {
                    RoleDiagnostic role0 = new RoleDiagnostic(RoleDiagnostic.ORDER_BY, "", pos);
                    role0.ErrorCode = "XPTY0004";
                    return role0;
                };
                sortKey = tc.StaticTypeCheck(sortKey, SequenceType.OPTIONAL_ATOMIC, role, visitor);
                skd.SetSortKey(sortKey, false);
                skd.TypeCheck(visitor, contextInfo);
                if (skd.IsFixed())
                {
                    IAtomicComparer comp = skd.MakeComparator(visitor.StaticContext.MakeEarlyEvaluationContext());
                    skd.FinalComparator = comp;
                    if (allKeysFixed)
                    {
                        comparators[i] = comp;
                    }
                }

                i++;
            }
        }

        public override void AddToPathMap(PathMap pathMap, PathMap.PathMapNodeSet pathMapNodeSet)
        {
            SortKeyDefinitionList sortKeys = SortKeyDefinitions;
            foreach (SortKeyDefinition skd in sortKeys)
            {
                Expression sortKey = skd.SortKey;
                sortKey.AddToPathMap(pathMap, pathMapNodeSet);
            }
        }

        public override void Explain(ExpressionPresenter @out)
        {
            @out.StartElement("order-by");
            foreach (SortKeyDefinition k in SortKeyDefinitions)
            {
                @out.StartSubsidiaryElement("key");
                k.SortKey.Export(@out);
                @out.EndSubsidiaryElement();
            }

            @out.EndElement();
        }

        public override string ToString()
        {
            StringBuilder fsb = new StringBuilder(64);
            fsb.Append("order by ... ");
            return fsb.ToString();
        }

        public AtomicValue EvaluateSortKey(int n, IXPathContext c)
        {
            SortKeyDefinitionList sortKeys = SortKeyDefinitions;
            return (AtomicValue)sortKeys.GetSortKeyDefinition(n).SortKeyEvaluator(c);
        }
    }
}