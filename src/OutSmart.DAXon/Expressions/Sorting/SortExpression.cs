////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Expressions.Parsing;
namespace OutSmart.DAXon.Expressions.Sorting
{
    internal sealed class SortExpression : Expression, ISortKeyEvaluator
    {
        private readonly Operand selectOp;
        private readonly Operand sortOp;
        private IAtomicComparer[] comparators;
        private IItemEvaluator[] sortKeyEvaluators;

        public override string ExpressionName => "sort";

        public Expression BaseExpression => Select;

        public override int ImplementationMethod => ITERATE_METHOD;

        public override string StreamerName => "SortExpression";

        public Expression Select
        {
            get => selectOp.GetChildExpression(); set
            {
                selectOp.SetChildExpression(value);
            }
        }
        public SortExpression(Expression select, SortKeyDefinitionList sortKeys)
        {
            selectOp = new Operand(this, select, OperandRole.FOCUS_CONTROLLING_SELECT);
            sortOp = new Operand(this, sortKeys, OperandRole.CONSTRAINED_ATOMIC_SEQUENCE);
            AdoptChildExpression(select);
            AdoptChildExpression(sortKeys);
        }

        public override IEnumerable<Operand> Operands()
        {
            return OperandList(selectOp, sortOp);
        }

        public override PathMap.PathMapNodeSet AddToPathMap(PathMap pathMap, PathMap.PathMapNodeSet pathMapNodeSet)
        {
            PathMap.PathMapNodeSet target = Select.AddToPathMap(pathMap, pathMapNodeSet);
            foreach (SortKeyDefinition sortKeyDefinition in GetSortKeyDefinitionList())
            {
                if (sortKeyDefinition.IsSetContextForSortKey())
                {
                    sortKeyDefinition.SortKey.AddToPathMap(pathMap, target);
                }
                else
                {
                    sortKeyDefinition.SortKey.AddToPathMap(pathMap, pathMapNodeSet);
                }

                AddSortKeyDetailsToPathMap(pathMap, pathMapNodeSet, sortKeyDefinition);
            }

            return target;
        }

        public static void AddSortKeyDetailsToPathMap(PathMap pathMap, PathMap.PathMapNodeSet pathMapNodeSet, SortKeyDefinition skd)
        {
            Expression e = skd.Order;
            if (e != null)
            {
                e.AddToPathMap(pathMap, pathMapNodeSet);
            }

            e = skd.CaseOrder;
            if (e != null)
            {
                e.AddToPathMap(pathMap, pathMapNodeSet);
            }

            e = skd.DataTypeExpression;
            if (e != null)
            {
                e.AddToPathMap(pathMap, pathMapNodeSet);
            }

            e = skd.Language;
            if (e != null)
            {
                e.AddToPathMap(pathMap, pathMapNodeSet);
            }

            e = skd.CollationNameExpression;
            if (e != null)
            {
                e.AddToPathMap(pathMap, pathMapNodeSet);
            }
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            selectOp.TypeCheck(visitor, contextInfo);
            Expression select2 = Select;
            if (select2 != Select)
            {
                AdoptChildExpression(select2);
                Select = select2;
            }

            if (!Cardinality.AllowsMany(select2.GetCardinality()))
            {

                // exit now because otherwise the type checking of the sort key can cause spurious failures
                return select2;
            }

            ItemType sortedItemType = Select.GetItemType();
            bool allKeysFixed = true;
            foreach (SortKeyDefinition sortKeyDefinition in GetSortKeyDefinitionList())
            {
                if (!sortKeyDefinition.IsFixed())
                {
                    allKeysFixed = false;
                    break;
                }
            }

            if (allKeysFixed)
            {
                comparators = new IAtomicComparer[GetSortKeyDefinitionList().Count];
            }

            TypeChecker tc = visitor.GetConfiguration().GetTypeChecker(false);
            for (int i = 0; i < GetSortKeyDefinitionList().Count; i++)
            {
                SortKeyDefinition sortKeyDef = GetSortKeyDefinition(i);
                Expression sortKey = sortKeyDef.SortKey;
                if (sortKeyDef.IsSetContextForSortKey())
                {
                    ContextItemStaticInfo cit = visitor.GetConfiguration().MakeContextItemStaticInfo(sortedItemType, false);
                    sortKey = sortKey.TypeCheck(visitor, cit);
                }
                else
                {
                    sortKey = sortKey.TypeCheck(visitor, contextInfo);
                }

                if (sortKeyDef.IsBackwardsCompatible())
                {
                    sortKey = FirstItemExpression.MakeFirstItemExpression(sortKey);
                }
                else
                {
                    Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.INSTRUCTION, "xsl:sort/select", 0, "XTTE1020");
                    sortKey = tc.StaticTypeCheck(sortKey, SequenceType.OPTIONAL_ATOMIC, role, visitor); //sortKey = CardinalityChecker.makeCardinalityChecker(sortKey, StaticProperty.ALLOWS_ZERO_OR_ONE, role);
                }

                sortKeyDef.SetSortKey(sortKey, sortKeyDef.IsSetContextForSortKey());
                sortKeyDef.TypeCheck(visitor, contextInfo);
                if (sortKeyDef.IsFixed())
                {
                    IAtomicComparer comp = sortKeyDef.MakeComparator(visitor.StaticContext.MakeEarlyEvaluationContext());
                    sortKeyDef.FinalComparator = comp;
                    if (allKeysFixed)
                    {
                        comparators[i] = comp;
                    }
                }

                if (sortKeyDef.IsSetContextForSortKey() && !ExpressionTool.DependsOnFocus(sortKey))
                {
                    visitor.StaticContext.IssueWarning("Sort key will have no effect because its value does not depend on the context item", DAXonErrorCode.SXWN9033, sortKey.GetLocation());
                }
            }

            return this;
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {
            selectOp.Optimize(visitor, contextItemType);

            // optimize the sort keys
            ContextItemStaticInfo cit;
            if (GetSortKeyDefinition(0).IsSetContextForSortKey())
            {
                ItemType sortedItemType = Select.GetItemType();
                cit = visitor.GetConfiguration().MakeContextItemStaticInfo(sortedItemType, false);
            }
            else
            {
                cit = contextItemType;
            }

            foreach (SortKeyDefinition sortKeyDefinition in GetSortKeyDefinitionList())
            {
                Expression sortKey = sortKeyDefinition.SortKey;
                sortKey = sortKey.Optimize(visitor, cit);
                sortKeyDefinition.SetSortKey(sortKey, true);
            }

            if (Cardinality.AllowsMany(Select.GetCardinality()))
            {
                return this;
            }
            else
            {
                return Select;
            }
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            int len = GetSortKeyDefinitionList().Count;
            SortKeyDefinition[] sk2 = new SortKeyDefinition[len];
            for (int i = 0; i < len; i++)
            {
                sk2[i] = (SortKeyDefinition)GetSortKeyDefinition(i).Copy(rebindings);
            }

            SortExpression se2 = new SortExpression(Select.Copy(rebindings), new SortKeyDefinitionList(sk2));
            ExpressionTool.CopyLocationInfo(this, se2);
            se2.comparators = comparators;
            return se2;
        }

        protected override int ComputeCardinality()
        {
            return Select.GetCardinality();
        }

        public override ItemType GetItemType()
        {
            return Select.GetItemType();
        }

        protected override int ComputeSpecialProperties()
        {
            int props = 0;
            if (Select.HasSpecialProperty(StaticProperty.CONTEXT_DOCUMENT_NODESET))
            {
                props |= StaticProperty.CONTEXT_DOCUMENT_NODESET;
            }

            if (Select.HasSpecialProperty(StaticProperty.SINGLE_DOCUMENT_NODESET))
            {
                props |= StaticProperty.SINGLE_DOCUMENT_NODESET;
            }

            if (Select.HasSpecialProperty(StaticProperty.NO_NODES_NEWLY_CREATED))
            {
                props |= StaticProperty.NO_NODES_NEWLY_CREATED;
            }

            return props;
        }

        public override ISequenceIterator Iterate(IXPathContext context)
        {
            ISequenceIterator iter = Select.Iterate(context);
            if (iter is EmptyIterator)
            {
                return iter;
            }

            return IterateSorted(iter, context);
        }

        // True when the single sort key is the context item's atomized value (`xsl:sort select="."`,
        // with or without the standard coercion layers). In that shape sorting the atomized VALUES
        // yields the same order as sorting the nodes: each node's key equals its converted value,
        // and equal keys keep input order either way (the stability tie-break).
        internal bool HasSingleSelfAtomizedKey()
        {
            if (GetSortKeyDefinitionList().Count != 1)
            {
                return false;
            }

            Expression k = GetSortKeyDefinition(0).SortKey;
            while (true)
            {
                if (k is AtomicSequenceConverter asc)
                {
                    k = asc.BaseExpression;
                }
                else if (k is Atomizer at)
                {
                    k = at.BaseExpression;
                }
                else if (k is CardinalityChecker cc)
                {
                    k = cc.BaseExpression;
                }
                else
                {
                    break;
                }
            }

            return k is ContextItemExpression;
        }

        public ISequenceIterator IterateSorted(ISequenceIterator iter, IXPathContext context)
        {
            IAtomicComparer[] comps = comparators;
            if (comparators == null)
            {
                int len = GetSortKeyDefinitionList().Count;
                comps = new IAtomicComparer[len];
                for (int s = 0; s < len; s++)
                {
                    IAtomicComparer comp = GetSortKeyDefinition(s).FinalComparator;
                    if (comp == null)
                    {
                        comp = GetSortKeyDefinition(s).MakeComparator(context);
                    }

                    comps[s] = comp;
                }
            }

            MakeSortKeyEvaluators();
            iter = new SortedIterator(context, iter, this, comps, GetSortKeyDefinition(0).IsSetContextForSortKey());
            ((SortedIterator)iter).SetHostLanguage(GetPackageData().GetHostLanguage());
            return iter;
        }

        public void MakeSortKeyEvaluators()
        {
            lock (syncLock)
            {
                if (sortKeyEvaluators == null)
                {
                    int len = GetSortKeyDefinitionList().Count;
                    sortKeyEvaluators = new IItemEvaluator[len];
                    for (int s = 0; s < len; s++)
                    {
                        sortKeyEvaluators[s] = GetSortKeyDefinition(s).SortKey.MakeElaborator().ElaborateForItem();
                    }
                }
            }
        }

        public AtomicValue EvaluateSortKey(int n, IXPathContext c)
        {
            return (AtomicValue)sortKeyEvaluators[n].Eval(c);
        }

        public override string ToShortString()
        {
            return "sort(" + BaseExpression.ToShortString() + ")";
        }

        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("sort", this);
            @out.SetChildRole("select");
            Select.Export(@out);
            GetSortKeyDefinitionList().Export(@out);
            @out.EndElement();
        }

        public SortKeyDefinitionList GetSortKeyDefinitionList()
        {
            return (SortKeyDefinitionList)sortOp.GetChildExpression();
        }

        public SortKeyDefinition GetSortKeyDefinition(int i)
        {
            return GetSortKeyDefinitionList().GetSortKeyDefinition(i);
        }

        public override Elaborator GetElaborator()
        {
            return new SortExprElaborator();
        }

        /// <summary>
        /// Elaborator for a sort expression - sorts nodes into order based on a user-supplied sort key
        /// </summary>
        internal sealed class SortExprElaborator : PullElaborator
        {
            public override IPullEvaluator ElaborateForPull()
            {

                // TODO: elaborate the sort key expression, and other expressions in the sort key definition
                SortExpression expr = (SortExpression)GetExpression();
                IPullEvaluator baseEval = expr.BaseExpression.MakeElaborator().ElaborateForPull();
                return (context) => expr.IterateSorted(baseEval.Iterate(context), context);
            }
        }
    }
}