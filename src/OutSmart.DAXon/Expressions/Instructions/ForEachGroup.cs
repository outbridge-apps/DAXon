////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Net;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Expressions.Sorting;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Expressions.Instructions
{
    internal sealed class ForEachGroup : Instruction, ISortKeyEvaluator, IContextSwitchingExpression
    {
        public const int GROUP_BY = 0;
        public const int GROUP_ADJACENT = 1;
        public const int GROUP_STARTING = 2;
        public const int GROUP_ENDING = 3;
        public const int GROUP_SPLIT_WHEN = 4;
        private readonly byte algorithm;
        private IStringCollator collator; // collation used for the grouping comparisons
        private IAtomicComparer[] sortComparators; // comparators used for sorting the groups
        private IItemEvaluator[] sortKeyEvaluators;
        private bool composite;
        private bool inFork;
        private readonly Operand selectOp;
        private readonly Operand actionOp;
        private readonly Operand keyOp;
        private Operand collationOp;
        private readonly Operand sortKeysOp;

        public override int InstructionNameCode => StandardNames.XSL_FOR_EACH_GROUP;

        public byte Algorithm => algorithm;

        public Expression GroupingKey => keyOp.GetChildExpression();

        public SortKeyDefinitionList SortKeyDefinitions => sortKeysOp == null ? null : (SortKeyDefinitionList)sortKeysOp.GetChildExpression();

        public override string StreamerName => "ForEachGroup";

        public Expression CollationNameExpression
        {
            get => collationOp == null ? null : collationOp.GetChildExpression(); set
            {
                if (collationOp == null)
                {
                    collationOp = new Operand(this, value, OperandRole.SINGLE_ATOMIC);
                }
                else
                {
                    collationOp.SetChildExpression(value);
                }
            }
        }
        public ForEachGroup(Expression select, Expression action, byte algorithm, Expression key, IStringCollator collator, Expression collationNameExpression, SortKeyDefinitionList sortKeys)
        {
            selectOp = new Operand(this, select, OperandRole.FOCUS_CONTROLLING_SELECT);
            actionOp = new Operand(this, action, OperandRole.FOCUS_CONTROLLED_ACTION);
            OperandRole keyRole = (algorithm == GROUP_ENDING || algorithm == GROUP_STARTING) ? OperandRole.PATTERN : OperandRole.NEW_FOCUS_ATOMIC;
            keyOp = new Operand(this, key, keyRole);
            if (collationNameExpression != null)
            {
                collationOp = new Operand(this, collationNameExpression, OperandRole.SINGLE_ATOMIC);
            }

            if (sortKeys != null)
            {
                sortKeysOp = new Operand(this, sortKeys, OperandRole.CONSTRAINED_SINGLE_ATOMIC);
            }

            this.algorithm = algorithm;
            this.collator = collator;
            foreach (Operand o in Operands())
            {
                AdoptChildExpression(o.GetChildExpression());
            }
        }

        public override IEnumerable<Operand> Operands()
        {
            return OperandSparseList(selectOp, actionOp, keyOp, collationOp, sortKeysOp);
        }

        public Expression GetSelectExpression()
        {
            return selectOp.GetChildExpression();
        }

        public Expression GetActionExpression()
        {
            return actionOp.GetChildExpression();
        }

        public bool IsComposite()
        {
            return composite;
        }

        public void SetComposite(bool composite)
        {
            this.composite = composite;
        }

        public bool IsInFork()
        {
            return inFork;
        }

        public void SetIsInFork(bool inFork)
        {
            this.inFork = inFork;
        }

        public override bool AllowExtractingCommonSubexpressions()
        {
            return false;
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            selectOp.TypeCheck(visitor, contextInfo);
            if (collationOp != null)
            {
                collationOp.TypeCheck(visitor, contextInfo);
            }

            ItemType selectedItemType = GetSelectExpression().GetItemType();
            if (selectedItemType == ErrorType.GetInstance())
            {
                return Literal.MakeEmptySequence();
            }

            FixupGroupReferences(this, this, selectedItemType, false);
            ContextItemStaticInfo cit = visitor.GetConfiguration().MakeContextItemStaticInfo(selectedItemType, false);
            cit.ContextSettingExpression = GetSelectExpression();
            actionOp.TypeCheck(visitor, cit);
            keyOp.TypeCheck(visitor, cit);
            if (Literal.IsEmptySequence(GetSelectExpression()))
            {
                return GetSelectExpression();
            }

            if (Literal.IsEmptySequence(GetActionExpression()))
            {
                return GetActionExpression();
            }

            if (SortKeyDefinitions != null)
            {
                bool allFixed = true;
                foreach (SortKeyDefinition sk in SortKeyDefinitions)
                {
                    Expression sortKey = sk.SortKey;
                    sortKey = sortKey.TypeCheck(visitor, cit);
                    if (sk.IsBackwardsCompatible())
                    {
                        sortKey = FirstItemExpression.MakeFirstItemExpression(sortKey);
                    }
                    else
                    {
                        Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.INSTRUCTION, "xsl:sort/select", 0, "XTTE1020");
                        sortKey = CardinalityChecker.MakeCardinalityChecker(sortKey, StaticProperty.ALLOWS_ZERO_OR_ONE, role);
                    }

                    sk.SetSortKey(sortKey, true);
                    sk.TypeCheck(visitor, contextInfo);
                    if (sk.IsFixed())
                    {
                        IAtomicComparer comp = sk.MakeComparator(visitor.StaticContext.MakeEarlyEvaluationContext());
                        sk.FinalComparator = comp;
                    }
                    else
                    {
                        allFixed = false;
                    }
                }

                if (allFixed)
                {
                    sortComparators = new IAtomicComparer[SortKeyDefinitions.Count];
                    for (int i = 0; i < SortKeyDefinitions.Count; i++)
                    {
                        sortComparators[i] = SortKeyDefinitions.GetSortKeyDefinition(i).FinalComparator;
                    }
                }
            }

            return this;
        }

        private static void FixupGroupReferences(Expression exp, ForEachGroup feg, ItemType selectedItemType, bool isInLoop)
        {
            ExpressionTool.ProbeTreeDepth();
            if (exp == null)
            {
            }
            else if (exp is CurrentGroupCall)
            {
                ((CurrentGroupCall)exp).SetControllingInstruction(feg, selectedItemType, isInLoop);
            }
            else if (exp is ForEachGroup)
            {

                // a current-group() reference to the outer for-each-group can occur in the select expression
                // or in the AVTs of a contained xsl:sort
                ForEachGroup feg2 = (ForEachGroup)exp;
                if (feg2 == feg)
                {
                    FixupGroupReferences(feg2.GetActionExpression(), feg, selectedItemType, false);
                }
                else
                {
                    FixupGroupReferences(feg2.GetSelectExpression(), feg, selectedItemType, isInLoop);
                    FixupGroupReferences(feg2.GroupingKey, feg, selectedItemType, isInLoop);
                    if (feg2.SortKeyDefinitions != null)
                    {
                        foreach (SortKeyDefinition skd in feg2.SortKeyDefinitions)
                        {
                            FixupGroupReferences(skd.Order, feg, selectedItemType, isInLoop);
                            FixupGroupReferences(skd.CaseOrder, feg, selectedItemType, isInLoop);
                            FixupGroupReferences(skd.DataTypeExpression, feg, selectedItemType, isInLoop);
                            FixupGroupReferences(skd.Language, feg, selectedItemType, isInLoop);
                            FixupGroupReferences(skd.CollationNameExpression, feg, selectedItemType, isInLoop);
                            FixupGroupReferences(skd.Order, feg, selectedItemType, isInLoop);
                        }
                    }
                }
            }
            else
            {
                foreach (Operand o in exp.Operands())
                {
                    FixupGroupReferences(o.GetChildExpression(), feg, selectedItemType, isInLoop || o.IsHigherOrder());
                }
            }
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {
            selectOp.Optimize(visitor, contextItemType);
            ItemType selectedItemType = GetSelectExpression().GetItemType();
            ContextItemStaticInfo sit = visitor.GetConfiguration().MakeContextItemStaticInfo(selectedItemType, false);
            sit.ContextSettingExpression = GetSelectExpression();
            actionOp.Optimize(visitor, sit);
            keyOp.Optimize(visitor, sit);
            if (Literal.IsEmptySequence(GetSelectExpression()))
            {
                return GetSelectExpression();
            }

            if (Literal.IsEmptySequence(GetActionExpression()))
            {
                return GetActionExpression();
            }


            // Optimize the sort key definitions
            if (SortKeyDefinitions != null)
            {
                foreach (SortKeyDefinition skd in SortKeyDefinitions)
                {
                    Expression sortKey = skd.SortKey;
                    sortKey = sortKey.Optimize(visitor, sit);
                    skd.SetSortKey(sortKey, true);
                }
            }

            if (collationOp != null)
            {
                collationOp.Optimize(visitor, contextItemType);
            }

            if (collator == null && (CollationNameExpression is StringLiteral))
            {
                string collation = ((StringLiteral)CollationNameExpression).Stringify();
                URI collationURI;
                try
                {
                    collationURI = new URI(collation);
                    if (!collationURI.IsAbsolute())
                    {
                        collationURI = StaticBaseURI.Resolve(collationURI);
                        string collationNameString = collationURI.ToString();
                        CollationNameExpression = new StringLiteral(collationNameString);
                        collator = visitor.GetConfiguration().GetCollation(collationNameString);
                        if (collator == null)
                        {
                            throw new XPathException("Unknown collation " + Err.Wrap(collationURI.ToString(), Err.URI)).WithErrorCode("XTDE1110").WithLocation(GetLocation());
                        }
                    }
                }
                catch (URISyntaxException)
                {
                    throw new XPathException("Collation name '" + CollationNameExpression + "' is not a valid URI").WithErrorCode("XTDE1110").WithLocation(GetLocation());
                }
            }

            return this;
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            SortKeyDefinition[] newKeyDef = null;
            if (SortKeyDefinitions != null)
            {
                newKeyDef = new SortKeyDefinition[SortKeyDefinitions.Count];
                for (int i = 0; i < SortKeyDefinitions.Count; i++)
                {
                    newKeyDef[i] = (SortKeyDefinition)SortKeyDefinitions.GetSortKeyDefinition(i).Copy(rebindings);
                }
            }

            ForEachGroup feg = new ForEachGroup(GetSelectExpression().Copy(rebindings), GetActionExpression().Copy(rebindings), algorithm, GroupingKey.Copy(rebindings), collator, CollationNameExpression.Copy(rebindings), newKeyDef == null ? null : new SortKeyDefinitionList(newKeyDef));
            ExpressionTool.CopyLocationInfo(this, feg);
            feg.SetComposite(IsComposite());
            FixupGroupReferences(feg, feg, GetSelectExpression().GetItemType(), false);
            return feg;
        }

        public override ItemType GetItemType()
        {
            return GetActionExpression().GetItemType();
        }

        public override int ComputeDependencies()
        {

            // Some of the dependencies in the "action" part and in the grouping and sort keys aren't relevant,
            // because they don't depend on values set outside the for-each-group expression
            int dependencies = 0;
            dependencies |= GetSelectExpression().Dependencies;
            dependencies |= GroupingKey.Dependencies & ~StaticProperty.DEPENDS_ON_FOCUS;
            dependencies |= GetActionExpression().Dependencies & ~(StaticProperty.DEPENDS_ON_FOCUS | StaticProperty.DEPENDS_ON_CURRENT_GROUP);
            if (SortKeyDefinitions != null)
            {
                foreach (SortKeyDefinition skd in SortKeyDefinitions)
                {
                    dependencies |= skd.SortKey.Dependencies & ~StaticProperty.DEPENDS_ON_FOCUS;
                    Expression e = skd.CaseOrder;
                    if (e != null && !(e is Literal))
                    {
                        dependencies |= e.Dependencies;
                    }

                    e = skd.DataTypeExpression;
                    if (e != null && !(e is Literal))
                    {
                        dependencies |= e.Dependencies;
                    }

                    e = skd.Order;
                    if (e != null && !(e is Literal))
                    {
                        dependencies |= e.Dependencies;
                    }

                    e = skd.CollationNameExpression;
                    if (e != null && !(e is Literal))
                    {
                        dependencies |= e.Dependencies;
                    }

                    e = skd.Stable;
                    if (e != null && !(e is Literal))
                    {
                        dependencies |= e.Dependencies;
                    }

                    e = skd.Language;
                    if (e != null && !(e is Literal))
                    {
                        dependencies |= e.Dependencies;
                    }
                }
            }

            if (CollationNameExpression != null)
            {
                dependencies |= CollationNameExpression.Dependencies;
            }

            return dependencies;
        }

        protected override int ComputeSpecialProperties()
        {
            int p = base.ComputeSpecialProperties();
            p |= GetActionExpression().GetSpecialProperties() & StaticProperty.ALL_NODES_UNTYPED;
            return p;
        }

        public override bool MayCreateNewNodes()
        {
            int props = GetActionExpression().GetSpecialProperties();
            return (props & StaticProperty.NO_NODES_NEWLY_CREATED) == 0;
        }

        public override PathMap.PathMapNodeSet AddToPathMap(PathMap pathMap, PathMap.PathMapNodeSet pathMapNodeSet)
        {
            PathMap.PathMapNodeSet target = GetSelectExpression().AddToPathMap(pathMap, pathMapNodeSet);
            if (CollationNameExpression != null)
            {
                CollationNameExpression.AddToPathMap(pathMap, pathMapNodeSet);
            }

            if (SortKeyDefinitions != null)
            {
                foreach (SortKeyDefinition skd in SortKeyDefinitions)
                {
                    skd.SortKey.AddToPathMap(pathMap, target);
                    SortExpression.AddSortKeyDetailsToPathMap(pathMap, pathMapNodeSet, skd);
                }
            }

            return GetActionExpression().AddToPathMap(pathMap, target);
        }

        public override void CheckPermittedContents(ISchemaType parentType, bool whole)
        {
            GetActionExpression().CheckPermittedContents(parentType, false);
        }

        private IStringCollator GetCollator(IXPathContext context)
        {
            if (CollationNameExpression != null)
            {
                StringValue collationValue = (StringValue)CollationNameExpression.EvaluateItem(context);
                string cname = collationValue.GetStringValue();
                try
                {
                    return context.GetConfiguration().GetCollation(cname, StaticBaseURIString, "FOCH0002");
                }
                catch (XPathException e)
                {
                    throw e.WithLocation(GetLocation());
                }
            }
            else
            {

                // Fallback - this shouldn't happen
                return CodepointCollator.GetInstance();
            }
        }

        private void MakeSortKeyEvaluators()
        {
            lock (syncLock)
            {
                if (sortKeyEvaluators == null && SortKeyDefinitions != null)
                {
                    sortKeyEvaluators = new IItemEvaluator[SortKeyDefinitions.Count];
                    for (int s = 0; s < SortKeyDefinitions.Count; s++)
                    {
                        sortKeyEvaluators[s] = SortKeyDefinitions.GetSortKeyDefinition(s).SortKey.MakeElaborator().ElaborateForItem();
                    }
                }
            }
        }

        public override ISequenceIterator Iterate(IXPathContext context)
        {
            return MakeElaborator().ElaborateForPull().Iterate(context);
        }

        public AtomicValue EvaluateSortKey(int n, IXPathContext c)
        {
            return (AtomicValue)sortKeyEvaluators[n].Eval(c);
        }

        public SortKeyDefinitionList GetSortKeyDefinitionList()
        {
            if (sortKeysOp == null)
            {
                return null;
            }

            return (SortKeyDefinitionList)sortKeysOp.GetChildExpression();
        }

        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("forEachGroup", this);
            @out.EmitAttribute("algorithm", GetAlgorithmName(algorithm));
            string flags = "";
            if (composite)
            {
                flags = "c";
            }

            if (IsInFork())
            {
                flags += "k";
            }

            if (!(flags.Length == 0))
            {
                @out.EmitAttribute("flags", flags);
            }

            @out.SetChildRole("select");
            GetSelectExpression().Export(@out);
            @out.SetChildRole(algorithm == GROUP_BY || algorithm == GROUP_ADJACENT || algorithm == GROUP_SPLIT_WHEN ? "key" : "match");
            GroupingKey.Export(@out);
            if (SortKeyDefinitions != null)
            {
                @out.SetChildRole("sort");
                GetSortKeyDefinitionList().Export(@out);
            }

            if (CollationNameExpression != null)
            {
                @out.SetChildRole("collation");
                CollationNameExpression.Export(@out);
            }

            @out.SetChildRole("content");
            GetActionExpression().Export(@out);
            @out.EndElement();
        }

        private static string GetAlgorithmName(byte algorithm)
        {
            switch (algorithm)
            {
                case GROUP_BY:
                    return "by";
                case GROUP_ADJACENT:
                    return "adjacent";
                case GROUP_STARTING:
                    return "starting";
                case GROUP_ENDING:
                    return "ending";
                case GROUP_SPLIT_WHEN:
                    return "split";
                default:
                    return "** unknown algorithm **";
            }
        }

        public override Elaborator GetElaborator()
        {
            return new ForEachGroupElaborator();
        }

        internal sealed class ForEachGroupElaborator : PushElaborator
        {
            private IPullEvaluator GroupIteratorProvider
            {
                get
                {

                    // get an iterator over the groups in "order of first appearance"
                    ForEachGroup expr = (ForEachGroup)GetExpression();
                    Expression select = expr.GetSelectExpression();
                    IPullEvaluator selectPull = select.MakeElaborator().ElaborateForPull();
                    int algorithm = expr.Algorithm;
                    switch (algorithm)
                    {
                        case GROUP_BY:
                            {
                                // Static probe of the fused tiny group-by shape: select is
                                // start/child::element-test and the key is a fused child atomizer.
                                // The runtime gate (single untyped Tiny parent, codepoint collation)
                                // picks the node-number builder; everything else stays generic.
                                int rowFp = -2;
                                int keyFp = -1;
                                IPullEvaluator startPull = null;
                                if (!expr.composite && FusedChildAtomizer.MatchAtomizer(expr.GroupingKey, out keyFp)
                                    && select is SlashExpression slash
                                    && slash.GetStep() is AxisExpression ax && ax.Axis == AxisInfo.CHILD)
                                {
                                    if (ax.GetNodeTest() is NameTest nt && nt.PrimitiveType == Types.Type.ELEMENT)
                                    {
                                        rowFp = nt.Fingerprint;
                                    }
                                    else if (ax.GetNodeTest() is NodeKindTest nkt && nkt.PrimitiveType == Types.Type.ELEMENT)
                                    {
                                        rowFp = -1;
                                    }

                                    if (rowFp != -2)
                                    {
                                        startPull = slash.Start.MakeElaborator().ElaborateForPull();
                                    }
                                }

                                return (context) =>
                                {
                                    IStringCollator coll = expr.collator;
                                    if (coll == null)
                                    {

                                        // The collation is determined at run-time
                                        coll = expr.GetCollator(context);
                                    }

                                    if (startPull != null && coll is CodepointCollator)
                                    {
                                        ISequenceIterator start = startPull.Iterate(context);
                                        IItem p1 = start.Next();
                                        if (p1 != null && start.Next() == null
                                            && p1 is Trees.Tiny.TinyParentNodeImpl tp && tp.tree.TypeArray == null)
                                        {
                                            return new GroupByIterator(tp, rowFp, keyFp, expr.GroupingKey, context.NewMinorContext(), coll);
                                        }
                                    }

                                    IXPathContext c2 = context.NewMinorContext();
                                    IFocusIterator population = c2.TrackFocus(selectPull.Iterate(context));
                                    return new GroupByIterator(population, expr.GroupingKey, c2, coll, expr.composite);
                                };
                            }

                        case GROUP_ADJACENT:
                            {
                                return (context) =>
                                {
                                    IStringCollator coll = expr.collator;
                                    if (coll == null)
                                    {

                                        // The collation is determined at run-time
                                        coll = expr.GetCollator(context);
                                    }

                                    return new GroupAdjacentIterator(selectPull, expr.GroupingKey, context, coll, expr.composite);
                                };
                            }

                        case GROUP_STARTING:
                            return (context) => new GroupStartingIterator(selectPull, (Patterns.Pattern)expr.GroupingKey, context);
                        case GROUP_ENDING:
                            return (context) => new GroupEndingIterator(selectPull, (Patterns.Pattern)expr.GroupingKey, context);
                        case GROUP_SPLIT_WHEN:
                            return (context) =>
                            {
                                IFunctionItem breakWhen = (IFunctionItem)expr.GroupingKey.EvaluateItem(context);
                                return new GroupBreakingIterator(selectPull, breakWhen, context);
                            };
                        default:
                            throw new InvalidOperationException("Unknown grouping algorithm");
                    }
                }
            }

            private IPullEvaluator SortedGroupIteratorProvider
            {
                get
                {

                    // now iterate over the leading nodes of the groups
                    ForEachGroup expr = (ForEachGroup)GetExpression();
                    if (expr.SortKeyDefinitions != null)
                    {
                        if (expr.sortComparators == null)
                        {

                            // Sort criteria vary dynamically: bug 6472
                            return (context) =>
                            {
                                IXPathContext xpc = context.NewMinorContext();
                                IAtomicComparer[] comps = new IAtomicComparer[expr.SortKeyDefinitions.Count];
                                for (int s = 0; s < expr.SortKeyDefinitions.Count; s++)
                                {
                                    comps[s] = expr.SortKeyDefinitions.GetSortKeyDefinition(s).MakeComparator(xpc);
                                }

                                IPullEvaluator grouper = GroupIteratorProvider;
                                return new SortedGroupIterator(xpc, (IGroupIterator)grouper.Iterate(xpc), expr, comps);
                            };
                        }
                        else
                        {
                            IAtomicComparer[] comps = expr.sortComparators;
                            IPullEvaluator grouper = GroupIteratorProvider;
                            return (context) =>
                            {
                                IXPathContext xpc = context.NewMinorContext();
                                return new SortedGroupIterator(xpc, (IGroupIterator)grouper.Iterate(xpc), expr, comps);
                            };
                        }
                    }
                    else
                    {
                        return GroupIteratorProvider;
                    }
                }
            }

            public override IPushEvaluator ElaborateForPush()
            {
                ForEachGroup expr = (ForEachGroup)GetExpression();
                expr.MakeSortKeyEvaluators();
                IPullEvaluator grouper = SortedGroupIteratorProvider;
                IPushEvaluator action = expr.GetActionExpression().MakeElaborator().ElaborateForPush();
                return (output, context) =>
                {
                    Controller controller = context.GetController();
                    PipelineConfiguration pipe = output.GetPipelineConfiguration();
                    IGroupIterator groupIterator = (IGroupIterator)grouper.Iterate(context);
                    XPathContextMajor c2 = context.NewContext();
                    c2.Origin = expr;
                    IFocusIterator focusIterator = c2.TrackFocus(groupIterator);
                    c2.SetCurrentGroupIterator(groupIterator);
                    c2.SetCurrentTemplateRule(null);
                    pipe.XPathContext = c2;
                    if (controller.IsTracing())
                    {
                        ITraceListener listener = controller.GetTraceListener();
                        IItem item;
                        while ((item = focusIterator.Next()) != null)
                        {
                            context.GetController().CheckTimeoutPerStep();
                            listener.StartCurrentItem(item);
                            ITailCall tc = action.ProcessLeavingTail(output, c2);
                            Expression.DispatchTailCall(tc);
                            listener.EndCurrentItem(item);
                        }
                    }
                    else
                    {
                        while (focusIterator.Next() != null)
                        {
                            context.GetController().CheckTimeoutPerStep();
                            ITailCall tc = action.ProcessLeavingTail(output, c2);
                            Expression.DispatchTailCall(tc);
                        }
                    }

                    pipe.XPathContext = context;
                    return null;
                };
            }

            public override IPullEvaluator ElaborateForPull()
            {
                ForEachGroup expr = (ForEachGroup)GetExpression();
                expr.MakeSortKeyEvaluators();
                IPullEvaluator grouper = SortedGroupIteratorProvider;
                IPullEvaluator action = expr.GetActionExpression().MakeElaborator().ElaborateForPull();
                return (context) =>
                {
                    IGroupIterator master = (IGroupIterator)grouper.Iterate(context);
                    XPathContextMajor c2 = context.NewContext();
                    c2.Origin = expr;
                    c2.TrackFocus(master);
                    c2.SetCurrentGroupIterator(master);
                    c2.SetCurrentTemplateRule(null);
                    return new ContextMappingIterator((cxt) => action.Iterate(cxt), c2);
                };
            }
        }
    }
}
