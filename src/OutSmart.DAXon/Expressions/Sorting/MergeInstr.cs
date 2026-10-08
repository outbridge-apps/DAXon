////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
//import com.saxonica.ee.stream.adjunct.MergeInstrAdjunct;
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Accumulators;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Patterns;
using OutSmart.DAXon.Api;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Text;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Expressions.Sorting
{
    internal sealed class MergeInstr : Instruction
    {

        private static readonly OperandRole ROW_SELECT = new OperandRole(OperandRole.USES_NEW_FOCUS | OperandRole.HIGHER_ORDER, OperandUsage.INSPECTION, Values.SequenceType.ANY_SEQUENCE);
        protected MergeSource[] mergeSources;
        private Operand actionOp;
        protected IAtomicComparer[] comparators;

        public MergeSource[] MergeSources => mergeSources;

        public override int InstructionNameCode => StandardNames.XSL_MERGE;

        public override string StreamerName => "MergeInstr";

        public MergeInstr()
        {
        }

        public MergeInstr Init(MergeSource[] mSources, Expression action)
        {
            actionOp = new Operand(this, action, OperandRole.FOCUS_CONTROLLED_ACTION);
            this.mergeSources = mSources;
            foreach (MergeSource mSource in mSources)
            {
                AdoptChildExpression(mSource.ForEachItem);
                AdoptChildExpression(mSource.ForEachSource);
                AdoptChildExpression(mSource.RowSelect);
            }

            AdoptChildExpression(action);

            return this;
        }

        public void SetAction(Expression action)
        {
            actionOp.SetChildExpression(action);
        }

        public Expression GetAction()
        {
            return actionOp.GetChildExpression();
        }

        public override void CheckPermittedContents(ISchemaType parentType, bool whole)
        {
            GetAction().CheckPermittedContents(parentType, false);
        }

        public override bool AllowExtractingCommonSubexpressions()
        {
            return false;
        }

        public override Types.ItemType GetItemType()
        {
            return GetAction().GetItemType();
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            Configuration config = visitor.GetConfiguration();
            TypeHierarchy th = config.GetTypeHierarchy();
            TypeChecker tc = config.GetTypeChecker(false);
            Types.ItemType inputType = null;
            foreach (MergeSource mergeSource in mergeSources)
            {
                ContextItemStaticInfo rowContextItemType = contextInfo;
                if (mergeSource.ForEachItem != null)
                {
                    mergeSource.forEachItemOp.TypeCheck(visitor, contextInfo);
                    rowContextItemType = config.MakeContextItemStaticInfo(mergeSource.ForEachItem.GetItemType(), false);
                }
                else if (mergeSource.ForEachSource != null)
                {
                    mergeSource.forEachStreamOp.TypeCheck(visitor, contextInfo);
                    Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.INSTRUCTION, "xsl:merge/for-each-source", 0);
                    mergeSource.SetForEachStream(tc.StaticTypeCheck(mergeSource.ForEachSource, Values.SequenceType.STRING_SEQUENCE, role, visitor));
                    rowContextItemType = config.MakeContextItemStaticInfo(NodeKindTest.DOCUMENT, false);
                }

                mergeSource.rowSelectOp.TypeCheck(visitor, rowContextItemType);
                Types.ItemType rowItemType = mergeSource.RowSelect.GetItemType();
                if (inputType == null)
                {
                    inputType = rowItemType;
                }
                else
                {
                    inputType = Types.Type.GetCommonSuperType(inputType, rowItemType, th);
                }

                ContextItemStaticInfo cit = config.MakeContextItemStaticInfo(inputType, false);
                if (mergeSource.mergeKeyDefinitions != null)
                {
                    foreach (SortKeyDefinition skd in mergeSource.mergeKeyDefinitions)
                    {
                        Expression sortKey = skd.SortKey;
                        sortKey = sortKey.TypeCheck(visitor, cit);
                        if (sortKey != null)
                        {
                            Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.INSTRUCTION, "xsl:merge-key/select", 0, "XTTE1020");
                            sortKey = CardinalityChecker.MakeCardinalityChecker(sortKey, StaticProperty.ALLOWS_ZERO_OR_ONE, role);
                            skd.SetSortKey(sortKey, true);
                        }

                        Expression exp = skd.Language.TypeCheck(visitor, config.MakeContextItemStaticInfo(inputType, false));
                        skd.Language = exp;
                        exp = skd.Order.TypeCheck(visitor, cit);
                        skd.Order = exp;
                        exp = skd.CollationNameExpression;
                        if (exp != null)
                        {
                            exp = exp.TypeCheck(visitor, cit);
                            skd.CollationNameExpression = exp;
                        }

                        exp = skd.CaseOrder.TypeCheck(visitor, cit);
                        skd.CaseOrder = exp;
                        exp = skd.DataTypeExpression;
                        if (exp != null)
                        {
                            exp = exp.TypeCheck(visitor, cit);
                            skd.DataTypeExpression = exp;
                        }
                    }
                }
            }

            actionOp.TypeCheck(visitor, config.MakeContextItemStaticInfo(inputType, false));
            if (Literal.IsEmptySequence(GetAction()))
            {
                return GetAction();
            }

            if (mergeSources.Length == 1 && Literal.IsEmptySequence(mergeSources[0].RowSelect))
            {
                return mergeSources[0].RowSelect;
            }

            FixupGroupReferences();
            return this;
        }

        public void FixupGroupReferences()
        {
            FixupGroupReferences(this, this, false);
        }

        private static void FixupGroupReferences(Expression exp, MergeInstr instr, bool isInLoop)
        {
            ExpressionTool.ProbeTreeDepth();
            if (exp == null)
            {
            }
            else if (exp.IsCallOn(typeof(CurrentMergeGroup)))
            {
                CurrentMergeGroup fn = (CurrentMergeGroup)((SystemFunctionCall)exp).TargetFunction;
                fn.SetControllingInstruction(instr, isInLoop);
            }
            else if (exp.IsCallOn(typeof(CurrentMergeKey)))
            {
                CurrentMergeKey fn = (CurrentMergeKey)((SystemFunctionCall)exp).TargetFunction;
                fn.ControllingInstruction = instr;
            }
            else if (exp is MergeInstr)
            {

                // a current-merge-group() reference to the outer xsl:merge can occur in the
                //  AVTs of a contained xsl:merge-key
                MergeInstr instr2 = (MergeInstr)exp;
                if (instr2 == instr)
                {
                    FixupGroupReferences(instr2.GetAction(), instr, false);
                }
                else
                {
                    foreach (MergeSource m in instr2.MergeSources)
                    {
                        foreach (SortKeyDefinition skd in m.mergeKeyDefinitions)
                        {
                            FixupGroupReferences(skd.Order, instr, isInLoop);
                            FixupGroupReferences(skd.CaseOrder, instr, isInLoop);
                            FixupGroupReferences(skd.DataTypeExpression, instr, isInLoop);
                            FixupGroupReferences(skd.Language, instr, isInLoop);
                            FixupGroupReferences(skd.CollationNameExpression, instr, isInLoop);
                            FixupGroupReferences(skd.Order, instr, isInLoop);
                        }

                        if (m.forEachItemOp != null)
                        {
                            FixupGroupReferences(m.ForEachItem, instr, isInLoop);
                        }

                        if (m.forEachStreamOp != null)
                        {
                            FixupGroupReferences(m.ForEachSource, instr, isInLoop);
                        }

                        if (m.rowSelectOp != null)
                        {
                            FixupGroupReferences(m.RowSelect, instr, isInLoop);
                        }
                    }
                }
            }
            else
            {
                foreach (Operand o in exp.Operands())
                {
                    FixupGroupReferences(o.GetChildExpression(), instr, isInLoop || o.IsEvaluatedRepeatedly());
                }
            }
        }

        public override bool MayCreateNewNodes()
        {
            int props = GetAction().GetSpecialProperties();
            return (props & StaticProperty.NO_NODES_NEWLY_CREATED) == 0;
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            Configuration config = visitor.GetConfiguration();
            TypeHierarchy th = config.GetTypeHierarchy();
            Types.ItemType inputType = null;
            foreach (MergeSource mergeSource in mergeSources)
            {
                ContextItemStaticInfo rowContextItemType = contextInfo;
                if (mergeSource.ForEachItem != null)
                {
                    mergeSource.forEachItemOp.Optimize(visitor, contextInfo);
                    rowContextItemType = config.MakeContextItemStaticInfo(mergeSource.ForEachItem.GetItemType(), false);
                }
                else if (mergeSource.ForEachSource != null)
                {
                    mergeSource.forEachStreamOp.Optimize(visitor, contextInfo);
                    rowContextItemType = config.MakeContextItemStaticInfo(NodeKindTest.DOCUMENT, false);
                }

                mergeSource.rowSelectOp.Optimize(visitor, rowContextItemType);
                Types.ItemType rowItemType = mergeSource.RowSelect.GetItemType();
                if (inputType == null)
                {
                    inputType = rowItemType;
                }
                else
                {
                    inputType = Types.Type.GetCommonSuperType(inputType, rowItemType, th);
                } //mergeSource.prepareForStreaming();
            }

            ContextItemStaticInfo cit = config.MakeContextItemStaticInfo(inputType, false);
            SetAction(GetAction().Optimize(visitor, cit));
            if (Literal.IsEmptySequence(GetAction()))
            {
                return GetAction();
            }

            if (mergeSources.Length == 1 && Literal.IsEmptySequence(mergeSources[0].RowSelect))
            {
                return mergeSources[0].RowSelect;
            }

            return this;
        }

        public override void PrepareForStreaming()
        {
            foreach (MergeSource mergeSource in mergeSources)
            {
                mergeSource.PrepareForStreaming();
            }
        }

        private void CheckMergeAtt(SortKeyDefinition[] sortKeyDefs)
        {
            for (int i = 1; i < sortKeyDefs.Length; i++)
            {
                if (!sortKeyDefs[0].IsEqual(sortKeyDefs[i]))
                {
                    throw new XPathException("Corresponding xsl:merge-key attributes in different xsl:merge-source elements " + "do not have the same effective values", "XTDE2210");
                }
            }
        }

        private ILastPositionFinder GetLastPositionFinder(IXPathContext context)
        {
            return new AnonymousILastPositionFinder(this, context);
        }

        public override ISequenceIterator Iterate(IXPathContext context)
        {
            try
            {
                IAtomicComparer[] comps = GetComparators(context);

                //final XPathContextMajor c1 = context.newContext();
                ISequenceIterator inputIterator = GetMergedInputIterator(context, comps);

                // Now perform the merge into a grouped sequence
                inputIterator = (ISequenceIterator)new MergeGroupingIterator(inputIterator, GetComparer(mergeSources[0].mergeKeyDefinitions, comps), GetLastPositionFinder(context));

                // and apply the merging action to each group of duplicate items within this sequence
                XPathContextMajor c3 = context.NewContext();
                c3.SetCurrentMergeGroupIterator((IGroupIterator)inputIterator);
                c3.TrackFocus(inputIterator);
                return new ContextMappingIterator((cxt) => GetAction().Iterate(cxt), c3);
            }
            catch (XPathException e)
            {
                throw e.MaybeWithLocation(GetLocation());
            }
        }

        private ISequenceIterator GetMergedInputIterator(IXPathContext context, IAtomicComparer[] comps)
        {

            // Now construct a tree of merge iterators, one for each merge sequence, for each merge source.
            ISequenceIterator inputIterator = EmptyIterator.GetInstance();
            foreach (MergeSource ms in mergeSources)
            {
                ISequenceIterator anchorsIter;
                if (ms.streamable && ms.ForEachSource != null)
                {
                }
                else if (ms.ForEachSource != null)
                {
                    ParseOptions options = context.GetConfiguration().GetParseOptions().WithSchemaValidationMode(ms.validation).WithTopLevelType(ms.schemaType).WithApplicableAccumulators(ms.accumulators);
                    ISequenceIterator uriIter = ms.ForEachSource.Iterate(context);
                    XsltController controller = (XsltController)context.GetController();
                    AccumulatorManager accumulatorManager = controller.GetAccumulatorManager();
                    anchorsIter = ItemMappingIterator.IMap(uriIter, (baseItem) =>
                    {
                        string uri = baseItem.GetStringValue();
                        NodeInfo node = DocumentFn.MakeDoc(uri, GetRetainedStaticContext().StaticBaseUriString, GetPackageData(), options, context, GetLocation(), true);
                        if (node != null)
                        {
                            accumulatorManager.SetApplicableAccumulators(node.GetTreeInfo(), ms.accumulators);
                        }

                        return node;
                    });
                    IXPathContext c2 = context.NewMinorContext();
                    IFocusIterator anchorsIterFocus = c2.TrackFocus(anchorsIter);
                    while (anchorsIterFocus.Next() != null)
                    {
                        IXPathContext c4 = c2.NewMinorContext();
                        c4.TrackFocus(ms.RowSelect.Iterate(c2));
                        MergeKeyMappingFunction addMergeKeys = new MergeKeyMappingFunction(c4, ms);
                        ContextMappingIterator contextMapKeysItr = new ContextMappingIterator(addMergeKeys.IMap, c4);
                        inputIterator = MakeMergeIterator(inputIterator, comps, ms, contextMapKeysItr);
                    }
                }
                else if (ms.ForEachItem != null)
                {
                    anchorsIter = ms.ForEachItem.Iterate(context);
                    IXPathContext c2 = context.NewMinorContext();
                    IFocusIterator anchorsIterFocus = c2.TrackFocus(anchorsIter);
                    while (anchorsIterFocus.Next() != null)
                    {
                        inputIterator = GetInputIterator(comps, inputIterator, ms, c2);
                    }
                }
                else
                {
                    inputIterator = GetInputIterator(comps, inputIterator, ms, context);
                }
            }

            return inputIterator;
        }

        private ISequenceIterator GetInputIterator(IAtomicComparer[] comps, ISequenceIterator inputIterator, MergeSource ms, IXPathContext c2)
        {
            IXPathContext c4 = c2.NewMinorContext();
            c4.TemporaryOutputState = StandardNames.XSL_MERGE_KEY;
            c4.TrackFocus(ms.RowSelect.Iterate(c2));
            MergeKeyMappingFunction addMergeKeys = new MergeKeyMappingFunction(c4, ms);
            ContextMappingIterator contextMapKeysItr = new ContextMappingIterator(addMergeKeys.IMap, c4);
            inputIterator = MakeMergeIterator(inputIterator, comps, ms, contextMapKeysItr);
            return inputIterator;
        }

        private IAtomicComparer[] GetComparators(IXPathContext context)
        {

            // First establish an array of comparators to be used for comparing items according to their
            // merge keys. Ideally this will have been done at compile time.
            IAtomicComparer[] comps = comparators;
            if (comparators == null)
            {
                SortKeyDefinition[] tempSKeys = new SortKeyDefinition[mergeSources.Length];
                for (int i = 0; i < mergeSources[0].mergeKeyDefinitions.Count; i++)
                {
                    for (int j = 0; j < mergeSources.Length; j++)
                    {
                        tempSKeys[j] = mergeSources[j].mergeKeyDefinitions.GetSortKeyDefinition(i).Fix(context);
                    }

                    CheckMergeAtt(tempSKeys);
                }

                comps = new IAtomicComparer[mergeSources[0].mergeKeyDefinitions.Count];
                for (int s = 0; s < mergeSources[0].mergeKeyDefinitions.Count; s++)
                {
                    IAtomicComparer comp = mergeSources[0].mergeKeyDefinitions.GetSortKeyDefinition(s).FinalComparator;
                    if (comp == null)
                    {
                        comp = mergeSources[0].mergeKeyDefinitions.GetSortKeyDefinition(s).MakeComparator(context);
                    }

                    comps[s] = comp;
                }
            }

            return comps;
        }

        private ISequenceIterator MakeMergeIterator(ISequenceIterator result, IAtomicComparer[] comps, MergeSource ms, ContextMappingIterator contextMapKeysItr)
        {
            if (result == null || result is EmptyIterator)
            {
                result = contextMapKeysItr;
            }
            else
            {
                result = (ISequenceIterator)new MergeIterator(result, contextMapKeysItr, GetComparer(ms.mergeKeyDefinitions, comps));
            }

            return result;
        }
        public override IEnumerable<Operand> Operands()
        {
            IList<Operand> list = new List<Operand>(6);
            list.Add(actionOp);
            if (mergeSources != null)
            {
                foreach (MergeSource ms in mergeSources)
                {
                    if (ms.forEachItemOp != null)
                    {
                        list.Add(ms.forEachItemOp);
                    }

                    if (ms.forEachStreamOp != null)
                    {
                        list.Add(ms.forEachStreamOp);
                    }

                    if (ms.rowSelectOp != null)
                    {
                        list.Add(ms.rowSelectOp);
                    }

                    list.Add(new Operand(this, ms.mergeKeyDefinitions, OperandRole.SINGLE_ATOMIC));
                }
            }

            return list;
        }

        public IComparer<ObjectValue<ItemWithMergeKeys>> GetComparer(SortKeyDefinitionList sKeys, IAtomicComparer[] comps)
        {

            return new AnonymousComparator(this, sKeys, comps);
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            MergeInstr newMerge = new MergeInstr();
            MergeSource[] c2 = new MergeSource[mergeSources.Length];
            Expression a2 = GetAction().Copy(rebindings);
            for (int c = 0; c < mergeSources.Length; c++)
            {
                c2[c] = mergeSources[c].CopyMergeSource(newMerge, rebindings);
            }

            return newMerge.Init(c2, a2);
        }

        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("merge", this);
            foreach (MergeSource mergeSource in mergeSources)
            {
                @out.StartSubsidiaryElement("mergeSrc");
                if (mergeSource.sourceName != null && !mergeSource.sourceName.StartsWith("saxon-merge-source-", StringComparison.Ordinal))
                {
                    @out.EmitAttribute("name", mergeSource.sourceName);
                }

                if (mergeSource.validation != Validation.SKIP && mergeSource.validation != Validation.BY_TYPE)
                {
                    @out.EmitAttribute("validation", Validation.Describe(mergeSource.validation));
                }

                if (mergeSource.validation == Validation.BY_TYPE)
                {
                    ISchemaType type = mergeSource.schemaType;
                    if (type != null)
                    {
                        @out.EmitAttribute("type", type.GetStructuredQName());
                    }
                }

                if (mergeSource.accumulators != null && mergeSource.accumulators.Count > 0)
                {
                    StringBuilder fsb = new StringBuilder(256);
                    foreach (Accumulator acc in mergeSource.accumulators)
                    {
                        if (fsb.Length != 0)
                        {
                            fsb.Append(' ');
                        }

                        fsb.Append(acc.AccumulatorName.EQName);
                    }

                    @out.EmitAttribute("accum", fsb.ToString());
                }

                if (mergeSource.streamable)
                {
                    @out.EmitAttribute("flags", "s");
                }

                if (mergeSource.ForEachItem != null)
                {
                    @out.SetChildRole("forEachItem");
                    mergeSource.ForEachItem.Export(@out);
                }

                if (mergeSource.ForEachSource != null)
                {
                    @out.SetChildRole("forEachStream");
                    mergeSource.ForEachSource.Export(@out);
                }

                @out.SetChildRole("selectRows");
                mergeSource.RowSelect.Export(@out);
                mergeSource.MergeKeyDefinitionSet.Export(@out);
                @out.EndSubsidiaryElement();
            }

            @out.SetChildRole("action");
            GetAction().Export(@out);
            @out.EndElement();
        }

        public override Elaborator GetElaborator()
        {
            return new MergeInstrElaborator();
        }
        internal sealed class MergeSource
        {
            private readonly MergeInstr instruction;
            public ILocation location;
            public Operand forEachItemOp = null;
            public Operand forEachStreamOp = null;
            public Operand rowSelectOp = null;
            public string sourceName = null;
            public SortKeyDefinitionList mergeKeyDefinitions = null;
            public string baseURI = null;
            public int validation;
            public ISchemaType schemaType;
            public bool streamable;
            public HashSet<Accumulator> accumulators;

            public Expression ForEachItem => forEachItemOp == null ? null : forEachItemOp.GetChildExpression();

            public Expression ForEachSource => forEachStreamOp == null ? null : forEachStreamOp.GetChildExpression();

            public Expression RowSelect
            {
                get => rowSelectOp.GetChildExpression(); set
                {
                    rowSelectOp.SetChildExpression(value);
                }
            }

            public SortKeyDefinitionList MergeKeyDefinitionSet
            {
                get => mergeKeyDefinitions; set
                {
                    mergeKeyDefinitions = value;
                }
            }
            public MergeSource(MergeInstr mi)
            {
                this.instruction = mi;
            }

            public MergeSource(MergeInstr instruction, Expression forEachItem, Expression forEachStream, Expression rSelect, string name, SortKeyDefinitionList sKeys, string baseURI)
            {
                this.instruction = instruction;
                if (forEachItem != null)
                {
                    InitForEachItem(instruction, forEachItem);
                }

                if (forEachStream != null)
                {
                    InitForEachStream(instruction, forEachStream);
                }

                if (rSelect != null)
                {
                    InitRowSelect(instruction, rSelect);
                }

                this.sourceName = name;
                this.mergeKeyDefinitions = sKeys;
                this.baseURI = baseURI;
            }

            public void InitForEachItem(MergeInstr instruction, Expression forEachItem)
            {
                forEachItemOp = new Operand(instruction, forEachItem, OperandRole.INSPECT);
            }

            public void InitForEachStream(MergeInstr instruction, Expression forEachStream)
            {
                forEachStreamOp = new Operand(instruction, forEachStream, OperandRole.INSPECT);
            }

            public void InitRowSelect(MergeInstr instruction, Expression rowSelect)
            {
                rowSelectOp = new Operand(instruction, rowSelect, ROW_SELECT);
            }

            public void SetStreamable(bool streamable)
            {
                this.streamable = streamable;
                if (streamable && instruction.GetConfiguration().GetBooleanProperty(Feature<bool>.STREAMING_FALLBACK))
                {
                    this.streamable = false;
                    Expression select = rowSelectOp.GetChildExpression();
                    rowSelectOp.SetChildExpression(SystemFunction.MakeCall("snapshot", select.GetRetainedStaticContext(), select));
                }
            }

            public MergeSource CopyMergeSource(MergeInstr newInstr, RebindingMap rebindings)
            {
                SortKeyDefinition[] newKeyDef = new SortKeyDefinition[mergeKeyDefinitions.Count];
                for (int i = 0; i < mergeKeyDefinitions.Count; i++)
                {
                    newKeyDef[i] = (SortKeyDefinition)mergeKeyDefinitions.GetSortKeyDefinition(i).Copy(rebindings);
                }

                MergeSource ms = new MergeSource(newInstr, Copy(ForEachItem, rebindings), Copy(ForEachSource, rebindings), Copy(RowSelect, rebindings), sourceName, new SortKeyDefinitionList(newKeyDef), baseURI);
                ms.validation = validation;
                ms.schemaType = schemaType;
                ms.streamable = streamable;
                ms.location = location;
                return ms;
            }

            private static Expression Copy(Expression exp, RebindingMap rebindings)
            {
                return exp == null ? null : exp.Copy(rebindings);
            }

            public void SetForEachStream(Expression forEachStream)
            {
                if (forEachStream != null)
                {
                    forEachStreamOp.SetChildExpression(forEachStream);
                }
            }

            public void PrepareForStreaming()
            {
            }
        }

        private sealed class AnonymousILastPositionFinder : ILastPositionFinder
        {

            private readonly MergeInstr parent;
            private readonly IXPathContext context;
            private int last = -1;
            public AnonymousILastPositionFinder(MergeInstr parent, IXPathContext context)
            {
                this.parent = parent;
                this.context = context;
            }
            public bool SupportsGetLength()
            {
                return true;
            }

            public int GetLength()
            {
                try
                {
                    if (last >= 0)
                    {
                        return last;
                    }
                    else
                    {
                        IAtomicComparer[] comps = parent.GetComparators(context);
                        IGroupIterator mgi = context.GetCurrentMergeGroupIterator();
                        XPathContextMajor c1 = context.NewContext();
                        c1.SetCurrentMergeGroupIterator(mgi);
                        ISequenceIterator inputIterator = parent.GetMergedInputIterator(context, comps);

                        // Now perform the merge into a grouped sequence
                        inputIterator = (ISequenceIterator)(new MergeGroupingIterator(inputIterator, parent.GetComparer(parent.mergeSources[0].mergeKeyDefinitions, comps), null));
                        return last = Count.SteppingCount(inputIterator);
                    }
                }
                catch (XPathException e)
                {
                    throw new UncheckedXPathException(e);
                }
            }
        }

        private sealed class AnonymousComparator : IComparer<ObjectValue<ItemWithMergeKeys>>

        {

            private readonly SortKeyDefinitionList sKeys;
            private readonly IAtomicComparer[] comps;
            public AnonymousComparator(MergeInstr parent, SortKeyDefinitionList sKeys, IAtomicComparer[] comps)
            {
                this.sKeys = sKeys;
                this.comps = comps;
            }
            public int Compare(ObjectValue<ItemWithMergeKeys> a, ObjectValue<ItemWithMergeKeys> b)
            {
                ItemWithMergeKeys aItem = a.GetObject();
                ItemWithMergeKeys bItem = b.GetObject();
                for (int i = 0; i < sKeys.Count; i++)
                {
                    int val;
                    try
                    {
                        val = comps[i].CompareAtomicValues(aItem.sortKeyValues[i], bItem.sortKeyValues[i]);
                    }
                    catch (NoDynamicContextException e)
                    {
                        throw new InvalidOperationException(e.Message, e);
                    }

                    if (val != 0)
                    {
                        return val;
                    }
                }

                return 0;
            }
        }

        internal sealed class MergeKeyMappingFunction
        {
            private readonly MergeSource ms;
            private readonly IXPathContext keyContext;
            private readonly ManualIterator manualIterator;
            public MergeKeyMappingFunction(IXPathContext baseContext, MergeSource ms)
            {
                this.ms = ms;
                keyContext = baseContext.NewMinorContext();
                keyContext.TemporaryOutputState = StandardNames.XSL_MERGE_KEY;

                //keyContext.setCurrentOutputUri(null);   // See bug 4160
                manualIterator = new ManualIterator();
                manualIterator.SetPosition(1);
                keyContext.SetCurrentIterator(manualIterator);
            }

            public ISequenceIterator IMap(IXPathContext context = null)
            {
                IItem currentItem = context.GetContextItem();
                manualIterator.SetContextItem(currentItem);
                ItemWithMergeKeys newItem = new ItemWithMergeKeys(currentItem, ms.mergeKeyDefinitions, ms.sourceName, keyContext);
                return SingletonIterator.MakeIterator(new ObjectValue<ItemWithMergeKeys>(newItem, typeof(ItemWithMergeKeys)));
            }
        }

        private sealed class MergeInstrElaborator : PullElaborator
        {
            public override IPullEvaluator ElaborateForPull()
            {
                MergeInstr expr = (MergeInstr)GetExpression();
                IPullEvaluator actionPull = expr.GetAction().MakeElaborator().ElaborateForPull();
                return (context) =>
                {
                    try
                    {
                        IAtomicComparer[] comps = expr.GetComparators(context);
                        ISequenceIterator inputIterator = expr.GetMergedInputIterator(context, comps);

                        // Now perform the merge into a grouped sequence
                        inputIterator = (ISequenceIterator)new MergeGroupingIterator(inputIterator, expr.GetComparer(expr.mergeSources[0].mergeKeyDefinitions, comps), expr.GetLastPositionFinder(context));

                        // and apply the merging action to each group of duplicate items within this sequence
                        XPathContextMajor c3 = context.NewContext();
                        c3.SetCurrentMergeGroupIterator((IGroupIterator)inputIterator);
                        c3.TrackFocus(inputIterator);

                        return new ContextMappingIterator((cxt) => actionPull.Iterate(cxt), c3);
                    }
                    catch (XPathException e)
                    {
                        throw e.MaybeWithLocation(expr.GetLocation());
                    }
                };
            }

            public override IPushEvaluator ElaborateForPush()
            {
                MergeInstr expr = (MergeInstr)GetExpression();
                IPullEvaluator puller = ElaborateForPull();
                return (output, context) =>
                {
                    ISequenceIterator iter = puller.Iterate(context);
                    try
                    {
                        SequenceTool.Supply(iter, (it) => output.Append(it, expr.GetLocation(), ReceiverOption.ALL_NAMESPACES));
                    }
                    catch (UncheckedXPathException err)
                    {
                        iter.Dispose();
                        throw err.GetXPathException().MaybeWithLocation(expr.GetLocation()).MaybeWithContext(context);
                    }
                    finally
                    {
                        iter.Dispose();
                    }

                    return null;
                };
            }
        }
    }
}