////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Collections;
using static OutSmart.DAXon.Expressions.Flwor.Clause.ClauseName;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Expressions.Parsing;
namespace OutSmart.DAXon.Expressions.Flwor
{
    /// <summary>
    /// Implements an XQuery 3.0 sliding or tumbling window clause within a FLWOR expression
    /// </summary>
    internal sealed class WindowClause : Clause
    {
        public const int WINDOW_VAR = 0;
        public const int START_ITEM = 1;
        public const int START_ITEM_POSITION = 2;
        public const int START_PREVIOUS_ITEM = 3;
        public const int START_NEXT_ITEM = 4;
        public const int END_ITEM = 5;
        public const int END_ITEM_POSITION = 6;
        public const int END_PREVIOUS_ITEM = 7;
        public const int END_NEXT_ITEM = 8;
        private bool sliding;
        private bool includeUnclosedWindows = true;
        private Operand sequenceOp;
        private Operand startConditionOp;
        private Operand endConditionOp;
        private IntHashMap<LocalVariableBinding> windowVars = new IntHashMap<LocalVariableBinding>(10);
        private ItemTypeCheckingFunction itemTypeChecker;
        private bool windowMustBeSingleton;

        public override ClauseName ClauseKey => WINDOW;

        public Expression Sequence
        {
            get => sequenceOp.GetChildExpression(); set
            {
                sequenceOp.SetChildExpression(value);
            }
        }

        public Expression StartCondition
        {
            get => startConditionOp.GetChildExpression(); set
            {
                startConditionOp.SetChildExpression(value);
            }
        }

        public Expression EndCondition
        {
            get => endConditionOp == null ? null : endConditionOp.GetChildExpression(); set
            {
                endConditionOp.SetChildExpression(value);
            }
        }

        // Tested per item of the windowed sequence: elaborated once, not on every test.
        private CachedEvaluator<IBooleanEvaluator> startEvaluator;
        private CachedEvaluator<IBooleanEvaluator> endEvaluator;

        internal IBooleanEvaluator StartConditionEvaluator => CachedEvaluator<IBooleanEvaluator>.Get(ref startEvaluator, StartCondition, e => e.ElaborateForBoolean());

        internal IBooleanEvaluator EndConditionEvaluator => CachedEvaluator<IBooleanEvaluator>.Get(ref endEvaluator, EndCondition, e => e.ElaborateForBoolean());

        public ItemTypeCheckingFunction ItemTypeChecker => itemTypeChecker;

        public override LocalVariableBinding[] RangeVariables
        {
            get
            {
                LocalVariableBinding[] vars = new LocalVariableBinding[windowVars.Count];
                int i = 0;
                foreach (LocalVariableBinding binding in windowVars.ValueSet())
                {
                    vars[i++] = binding;
                }

                return vars;
            }
        }
        public WindowClause()
        {
        }

        public void SetIsSlidingWindow(bool sliding)
        {
            this.sliding = sliding;
        }

        public bool IsSlidingWindow()
        {
            return sliding;
        }

        public bool IsTumblingWindow()
        {
            return !sliding;
        }

        public void SetIncludeUnclosedWindows(bool include)
        {
            this.includeUnclosedWindows = include;
        }

        public bool IsIncludeUnclosedWindows()
        {
            return includeUnclosedWindows;
        }

        public void InitSequence(FLWORExpression flwor, Expression sequence)
        {
            sequenceOp = new Operand(flwor, sequence, OperandRole.INSPECT);
        }

        public void InitStartCondition(FLWORExpression flwor, Expression startCondition)
        {
            startConditionOp = new Operand(flwor, startCondition, OperandRole.INSPECT);
        }

        public void InitEndCondition(FLWORExpression flwor, Expression endCondition)
        {
            endConditionOp = new Operand(flwor, endCondition, OperandRole.INSPECT);
        }

        public void SetVariableBinding(int role, LocalVariableBinding binding)
        {
            foreach (LocalVariableBinding b in windowVars.ValueSet())
            {
                if (b.GetVariableQName().Equals(binding.GetVariableQName()))
                {
                    throw new XPathException("Two variables in a window clause cannot have the same name (" + binding.GetVariableQName().DisplayName + ")", "XQST0103");
                }
            }

            windowVars.Put(role, binding);
        }

        public LocalVariableBinding GetVariableBinding(int role)
        {
            return windowVars[role];
        }

        public override void TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            SequenceType requiredType = GetVariableBinding(WindowClause.WINDOW_VAR).GetRequiredType();
            ItemType required = requiredType.PrimaryType;
            ItemType supplied = Sequence.GetItemType();
            Configuration config = visitor.GetConfiguration();
            TypeHierarchy th = config.GetTypeHierarchy();
            Affinity rel = th.Relationship(required, supplied);
            switch (rel)
            {
                case Affinity.SAME_TYPE:
                case Affinity.SUBSUMES:

                    // no action
                    break;
                case Affinity.OVERLAPS:
                case Affinity.SUBSUMED_BY:
                    Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.VARIABLE, GetVariableBinding(WindowClause.WINDOW_VAR).GetVariableQName().DisplayName, 0);
                    itemTypeChecker = new ItemTypeCheckingFunction(required, role, Location, config);
                    break;
                case Affinity.DISJOINT:
                    string message = "The items in the window will always be instances of " + supplied + ", never of the required type " + required;
                    throw new XPathException(message, "XPTY0004", Location);
            }

            windowMustBeSingleton = !Cardinality.AllowsMany(requiredType.GetCardinality());
            if (requiredType.GetCardinality() == StaticProperty.ALLOWS_ZERO)
            {
                string message = "The value of the window variable can never be an empty sequence";
                throw new XPathException(message, "XPTY0004", Location);
            }
        }

        protected internal void CheckWindowContents(Window w)
        {
            if (windowMustBeSingleton && w.contents.Count > 1)
            {
                throw new XPathException("Required type of window allows only a single item; window has length " + w.contents.Count, "XPTY0004", Location);
            }

            ItemTypeCheckingFunction checker = ItemTypeChecker;
            if (checker != null)
            {
                ISequenceIterator check = new ItemMappingIterator(new ListIterator.Of<IItem>(w.contents), checker);
                SequenceTool.Supply(check, (it) =>
                {
                }); // a convenient way to consume the iterator and thus perform the checking
            }
        }

        public override Clause Copy(FLWORExpression flwor, RebindingMap rebindings)
        {
            WindowClause wc = new WindowClause();
            wc.Location = Location;
            wc.SetPackageData(GetPackageData());
            wc.sliding = sliding;
            wc.includeUnclosedWindows = includeUnclosedWindows;
            wc.InitSequence(flwor, Sequence.Copy(rebindings));
            wc.InitStartCondition(flwor, StartCondition.Copy(rebindings));
            if (EndCondition != null)
            {
                wc.InitEndCondition(flwor, EndCondition.Copy(rebindings));
            }

            wc.windowVars = windowVars;
            return wc;
        }

        public override TuplePull GetPullStream(TuplePull @base, IXPathContext context)
        {
            return new WindowClausePull(@base, this, context);
        }

        public override TuplePush GetPushStream(TuplePush destination, Outputter output, IXPathContext context)
        {
            return new WindowClausePush(output, destination, this);
        }

        public override void ProcessOperands(IOperandProcessor processor)
        {
            processor.ProcessOperand(sequenceOp);
            processor.ProcessOperand(startConditionOp);
            if (endConditionOp != null)
            {
                processor.ProcessOperand(endConditionOp);
            }
        }

        public override void AddToPathMap(PathMap pathMap, PathMap.PathMapNodeSet pathMapNodeSet)
        {
            throw new NotSupportedException("Cannot use document projection with windowing");
        }

        public override void Explain(ExpressionPresenter @out)
        {
            @out.StartElement(IsSlidingWindow() ? "slidingWindow" : "tumblingWindow");
            @out.StartSubsidiaryElement("select");
            Sequence.Export(@out);
            @out.EndSubsidiaryElement();
            @out.StartSubsidiaryElement("start");
            StartCondition.Export(@out);
            @out.EndSubsidiaryElement();
            if (endConditionOp != null)
            {
                @out.StartSubsidiaryElement("end");
                EndCondition.Export(@out);
                @out.EndSubsidiaryElement();
            }

            @out.EndElement();
        }

        protected internal bool MatchesStart(IItem previous, IItem current, IItem next, int position, IXPathContext context)
        {
            WindowClause clause = this;
            LocalVariableBinding binding;
            binding = clause.GetVariableBinding(WindowClause.START_ITEM);
            if (binding != null)
            {
                context.SetLocalVariable(binding.LocalSlotNumber, current);
            }

            binding = clause.GetVariableBinding(WindowClause.START_ITEM_POSITION);
            if (binding != null)
            {
                context.SetLocalVariable(binding.LocalSlotNumber, new Int64Value(position));
            }

            binding = clause.GetVariableBinding(WindowClause.START_NEXT_ITEM);
            if (binding != null)
            {
                context.SetLocalVariable(binding.LocalSlotNumber, MakeValue(next));
            }

            binding = clause.GetVariableBinding(WindowClause.START_PREVIOUS_ITEM);
            if (binding != null)
            {
                context.SetLocalVariable(binding.LocalSlotNumber, MakeValue(previous));
            }

            return clause.StartConditionEvaluator(context);
        }

        protected internal bool MatchesEnd(Window window, IItem previous, IItem current, IItem next, int position, IXPathContext context)
        {
            WindowClause clause = this;
            LocalVariableBinding binding;
            binding = clause.GetVariableBinding(WindowClause.START_ITEM);
            if (binding != null)
            {
                context.SetLocalVariable(binding.LocalSlotNumber, window.startItem);
            }

            binding = clause.GetVariableBinding(WindowClause.START_ITEM_POSITION);
            if (binding != null)
            {
                context.SetLocalVariable(binding.LocalSlotNumber, new Int64Value(window.startPosition));
            }

            binding = clause.GetVariableBinding(WindowClause.START_NEXT_ITEM);
            if (binding != null)
            {
                context.SetLocalVariable(binding.LocalSlotNumber, MakeValue(window.startNextItem));
            }

            binding = clause.GetVariableBinding(WindowClause.START_PREVIOUS_ITEM);
            if (binding != null)
            {
                context.SetLocalVariable(binding.LocalSlotNumber, MakeValue(window.startPreviousItem));
            }

            binding = clause.GetVariableBinding(WindowClause.END_ITEM);
            if (binding != null)
            {
                context.SetLocalVariable(binding.LocalSlotNumber, current);
            }

            binding = clause.GetVariableBinding(WindowClause.END_ITEM_POSITION);
            if (binding != null)
            {
                context.SetLocalVariable(binding.LocalSlotNumber, new Int64Value(position));
            }

            binding = clause.GetVariableBinding(WindowClause.END_NEXT_ITEM);
            if (binding != null)
            {
                context.SetLocalVariable(binding.LocalSlotNumber, MakeValue(next));
            }

            binding = clause.GetVariableBinding(WindowClause.END_PREVIOUS_ITEM);
            if (binding != null)
            {
                context.SetLocalVariable(binding.LocalSlotNumber, MakeValue(previous));
            }

            return clause.EndConditionEvaluator(context);
        }

        protected internal static ISequence MakeValue(IItem item)
        {
            if (item == null)
            {
                return EmptySequence.GetInstance();
            }
            else
            {
                return item;
            }
        }

        protected internal sealed class Window
        {
            public IItem startItem;
            public int startPosition;
            public IItem startPreviousItem;
            public IItem startNextItem;
            public IItem endItem;
            public int endPosition;
            public IItem endPreviousItem;
            public IItem endNextItem;
            public IList<IItem> contents;
            public bool despatched;
            public bool IsFinished()
            {
                return endPosition > 0;
            }

            public bool IsDespatched()
            {
                return despatched;
            }
        }
    }
}