////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Expressions.Flwor;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Values;
using OutSmart.DAXon.Internal.Collections;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal;
namespace OutSmart.DAXon.Expressions
{
    internal class ForExpression : Assignation
    {
        private int actionCardinality = StaticProperty.ALLOWS_MANY;

        /// <summary>
        /// Create a "for" expression (for $x at $p in SEQUENCE return ACTION)
        /// </summary>
        public override string ExpressionName => "for";

        /// <summary>
        /// Type-check the expression
        /// </summary>
        protected virtual int RangeVariableCardinality => StaticProperty.EXACTLY_ONE;

        public override IntegerValue[] IntegerBounds => GetAction().IntegerBounds;

        public override int ImplementationMethod => ITERATE_METHOD | PROCESS_METHOD;

        public override string StreamerName => "ForExpression";
        /// <summary>
        /// Create a "for" expression (for $x at $p in SEQUENCE return ACTION)
        /// </summary>
        public ForExpression()
        {
        }

        /// <summary>
        /// Type-check the expression
        /// </summary>
        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {

            // The order of events is critical here. First we ensure that the type of the
            // sequence expression is established. This is used to establish the type of the variable,
            // which in turn is required when type-checking the action part.
            SequenceOp.TypeCheck(visitor, contextInfo);
            if (Literal.IsEmptySequence(Sequence) && !(this is OuterForExpression))
            {
                return Sequence;
            }

            if (requiredType != null)
            {

                // if declaration is null, we've already done the type checking in a previous pass
                SequenceType decl = requiredType;
                SequenceType sequenceType = SequenceType.MakeSequenceType(decl.PrimaryType, StaticProperty.ALLOWS_ZERO_OR_MORE);
                Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.VARIABLE, variableName.DisplayName, 0);
                if (visitor.StaticContext.GetXPathVersion() < 40)
                {
                    Sequence = TypeChecker.StrictTypeCheck(Sequence, sequenceType, role, visitor.StaticContext);
                }
                else
                {
                    TypeChecker tc = visitor.GetConfiguration().GetTypeChecker(false);
                    Sequence = tc.StaticTypeCheck(Sequence, sequenceType, role, visitor);
                }

                ItemType actualItemType = Sequence.GetItemType();
                RefineTypeInformation(actualItemType, RangeVariableCardinality, null, Sequence.GetSpecialProperties(), this);
            }

            if (Literal.IsEmptySequence(GetAction()))
            {
                return GetAction();
            }

            ActionOp.TypeCheck(visitor, contextInfo);
            actionCardinality = GetAction().GetCardinality();
            return this;
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {
            Configuration config = visitor.GetConfiguration();
            Optimizer opt = visitor.ObtainOptimizer();
            bool debug = config.GetBooleanProperty(Feature<bool>.TRACE_OPTIMIZER_DECISIONS);

            // Try to promote any WHERE clause appearing immediately within the FOR expression
            if (Choose.IsSingleBranchChoice(GetAction()))
            {
                ActionOp.Optimize(visitor, contextItemType);
            }

            Expression p = PromoteWhereClause();
            if (p != null)
            {
                if (debug)
                {
                    opt.Trace("Promoted where clause in for $" + VariableName, p);
                }

                return p.Optimize(visitor, contextItemType);
            }

            Expression seq0 = Sequence;
            SequenceOp.Optimize(visitor, contextItemType);
            if (seq0 != Sequence)
            {

                // if it changed, re-optimize
                return Optimize(visitor, contextItemType);
            }

            if (Literal.IsEmptySequence(Sequence) && !(this is OuterForExpression))
            {
                return Sequence;
            }

            Expression act0 = GetAction();
            ActionOp.Optimize(visitor, contextItemType);
            if (act0 != GetAction())
            {

                // it's now worth re-attempting the "where" clause optimizations
                return Optimize(visitor, contextItemType);
            }

            if (Literal.IsEmptySequence(GetAction()))
            {
                return GetAction();
            }


            // Simplify an expression of the form "for $b in a/b/c return $b/d".
            // (XQuery users seem to write these a lot!)
            if (Sequence is SlashExpression && GetAction() is SlashExpression)
            {
                SlashExpression path2 = (SlashExpression)GetAction();
                Expression start2 = path2.GetSelectExpression();
                Expression step2 = path2.GetActionExpression();
                if (start2 is VariableReference && ((VariableReference)start2).GetBinding() == this && ExpressionTool.GetReferenceCount(GetAction(), this, false) == 1 && ((step2.Dependencies & (StaticProperty.DEPENDS_ON_POSITION | StaticProperty.DEPENDS_ON_LAST)) == 0))
                {
                    Expression newPath = new SlashExpression(Sequence, path2.GetActionExpression());
                    ExpressionTool.CopyLocationInfo(this, newPath);
                    newPath = newPath.Simplify().TypeCheck(visitor, contextItemType);
                    if (newPath is SlashExpression)
                    {

                        // if not, it has been wrapped in a DocumentSorter or Reverser, which makes it ineligible.
                        // see test qxmp299, where this condition isn't satisfied
                        if (debug)
                        {
                            opt.Trace("Collapsed return clause of for $" + VariableName + " into path expression", newPath);
                        }

                        return newPath.Optimize(visitor, contextItemType);
                    }
                }
            }


            // Simplify an expression of the form "for $x in EXPR return $x". These sometimes
            // arise as a result of previous optimization steps.
            if (GetAction() is VariableReference && ((VariableReference)GetAction()).GetBinding() == this)
            {
                if (debug)
                {
                    opt.Trace("Collapsed redundant for expression $" + VariableName, Sequence);
                }

                return Sequence;
            }


            // If the cardinality of the sequence is exactly one, rewrite as a LET expression
            if (Sequence.GetCardinality() == StaticProperty.EXACTLY_ONE)
            {
                LetExpression let = new LetExpression();
                let.SetVariableQName(variableName);
                let.SetRequiredType(SequenceType.MakeSequenceType(Sequence.GetItemType(), StaticProperty.EXACTLY_ONE));
                let.Sequence = Sequence;
                let.SetAction(GetAction());
                let.SetSlotNumber(slotNumber);
                let.SetRetainedStaticContextLocally(GetRetainedStaticContext());
                ExpressionTool.RebindVariableReferences(GetAction(), this, let);
                return let.TypeCheck(visitor, contextItemType).Optimize(visitor, contextItemType);
            }

            return this;
        }

        public override Expression Unordered(bool retainAllNodes, bool forStreaming)
        {
            Sequence = Sequence.Unordered(retainAllNodes, forStreaming);
            SetAction(GetAction().Unordered(retainAllNodes, forStreaming));
            return this;
        }

        private Expression PromoteWhereClause()
        {
            if (Choose.IsSingleBranchChoice(GetAction()))
            {
                Expression condition = ((Choose)GetAction()).GetCondition(0);
                IBinding[] bindingList = new IBinding[]
                {
                    this
                };
                IList<Expression> list = new List<Expression>(5);
                Expression promotedCondition = null;
                BooleanExpression.ListAndComponents(condition, list);
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    Expression term = list[i];
                    if (!ExpressionTool.DependsOnVariable(term, bindingList))
                    {
                        if (promotedCondition == null)
                        {
                            promotedCondition = term;
                        }
                        else
                        {
                            promotedCondition = new AndExpression(term, promotedCondition);
                        }

                        list.RemoveAt(i);
                    }
                }

                if (promotedCondition != null)
                {
                    if (list.Count == 0)
                    {

                        // the whole if() condition has been promoted
                        Expression oldThen = ((Choose)GetAction()).GetAction(0);
                        SetAction(oldThen);
                        return Choose.MakeConditional(promotedCondition, this);
                    }
                    else
                    {

                        // one or more terms of the if() condition have been promoted
                        Expression retainedCondition = list[0];
                        for (int i = 1; i < list.Count; i++)
                        {
                            retainedCondition = new AndExpression(retainedCondition, list[i]);
                        }

                        ((Choose)GetAction()).SetCondition(0, retainedCondition);
                        Expression newIf = Choose.MakeConditional(promotedCondition, this, Literal.MakeEmptySequence());
                        ExpressionTool.CopyLocationInfo(this, newIf);
                        return newIf;
                    }
                }
            }

            return null;
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            ForExpression forExp = new ForExpression();
            ExpressionTool.CopyLocationInfo(this, forExp);
            forExp.SetRequiredType(requiredType);
            forExp.SetVariableQName(variableName);
            forExp.Sequence = Sequence.Copy(rebindings);
            rebindings.Put(this, forExp);
            Expression newAction = GetAction().Copy(rebindings);
            forExp.SetAction(newAction);
            forExp.variableName = variableName;
            forExp.slotNumber = slotNumber;
            return forExp;
        }

        public override int MarkTailFunctionCalls(StructuredQName qName, int arity)
        {
            if (!Cardinality.AllowsMany(Sequence.GetCardinality()))
            {
                return ExpressionTool.MarkTailFunctionCalls(GetAction(), qName, arity);
            }
            else
            {
                return UserFunctionCall.NOT_TAIL_CALL;
            }
        }

        public override bool IsVacuousExpression()
        {
            return GetAction().IsVacuousExpression();
        }

        public override void CheckPermittedContents(ISchemaType parentType, bool whole)
        {
            GetAction().CheckPermittedContents(parentType, false);
        }

        public override ISequenceIterator Iterate(IXPathContext context)
        {

            // First create an iteration of the base sequence.
            // Then create a MappingIterator which applies a mapping function to each
            // item in the base sequence. The mapping function is essentially the "return"
            // expression, wrapped in a MappingAction object that is responsible also for
            // setting the range variable at each step.
            ISequenceIterator @base = Sequence.Iterate(context);
            MappingAction map = new MappingAction(context, LocalSlotNumber, GetAction());
            switch (actionCardinality)
            {
                case StaticProperty.EXACTLY_ONE:
                    return new ItemMappingIterator(@base, map, true);
                case StaticProperty.ALLOWS_ZERO_OR_ONE:
                    return new ItemMappingIterator(@base, map, false);
                default:
                    return new MappingIterator(@base, map);
            }
        }

        public override void Process(Outputter output, IXPathContext context)
        {
            DispatchTailCall(MakeElaborator().ElaborateForPush().ProcessLeavingTail(output, context));
        }

        public override ItemType GetItemType()
        {
            return GetAction().GetItemType();
        }

        public override UType GetStaticUType(UType contextItemType)
        {
            return GetAction().GetStaticUType(contextItemType);
        }

        protected override int ComputeCardinality()
        {
            int c1 = Sequence.GetCardinality();
            int c2 = GetAction().GetCardinality();
            return Cardinality.Multiply(c1, c2);
        }

        public override string ToString()
        {
            return "for $" + VariableEQName + AllowingEmptyString() + " in " + (Sequence == null ? "(...)" : Sequence.ToString()) + " return " + (GetAction() == null ? "(...)" : ExpressionTool.Parenthesize(GetAction()));
        }

        public override string ToShortString()
        {
            return "for $" + GetVariableQName().DisplayName + AllowingEmptyString() + " in " + (Sequence == null ? "(...)" : Sequence.ToShortString()) + " return " + (GetAction() == null ? "(...)" : GetAction().ToShortString());
        }

        protected virtual string AllowingEmptyString()
        {
            return "";
        }

        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("for", this);
            ExplainSpecializedAttributes(@out);
            @out.EmitAttribute("var", GetVariableQName());
            ItemType varType = Sequence.GetItemType();
            if (varType != AnyItemType.GetInstance())
            {
                @out.EmitAttribute("as", AlphaCode.FromItemType(varType));
            }

            @out.EmitAttribute("slot", "" + LocalSlotNumber);
            @out.SetChildRole("in");
            Sequence.Export(@out);
            @out.SetChildRole("return");
            GetAction().Export(@out);
            @out.EndElement();
        }

        protected virtual void ExplainSpecializedAttributes(ExpressionPresenter @out)
        {
        }

        public override Elaborator GetElaborator()
        {
            return new ForExprElaborator();
        }

        internal sealed class MappingAction : IMappingFunction, IItemMappingFunction
        {
            protected IXPathContext context;
            private readonly int slotNumber;
            private readonly Expression action;
            public MappingAction(IXPathContext context, int slotNumber, Expression action)
            {
                this.context = context;
                this.slotNumber = slotNumber;
                this.action = action;
            }

            public ISequenceIterator IMap(IItem item)
            {
                context.SetLocalVariable(slotNumber, item);
                return action.Iterate(context);
            }

            public IItem MapItem(IItem item)
            {
                context.SetLocalVariable(slotNumber, item);
                return action.EvaluateItem(context);
            }
        }

        internal sealed class ForExprElaborator : PullElaborator
        {
            public override IPullEvaluator ElaborateForPull()
            {
                ForExpression expr = (ForExpression)GetExpression();
                IPullEvaluator selectEval = expr.Sequence.MakeElaborator().ElaborateForPull();
                int actionCardinality = expr.GetAction().GetCardinality();
                int slot = expr.LocalSlotNumber;
                if (Cardinality.AllowsMany(actionCardinality))
                {
                    IPullEvaluator actionEval = expr.GetAction().MakeElaborator().ElaborateForPull();
                    return (context) =>
                    {
                        ISequenceIterator @base = selectEval.Iterate(context);
                        return new MappingIterator(@base, SequenceMapper.Of((item) =>
                        {
                            context.SetLocalVariable(slot, item);
                            return actionEval.Iterate(context);
                        }), context.GetController());
                    };
                }
                else
                {
                    IItemEvaluator actionEval = expr.GetAction().MakeElaborator().ElaborateForItem();
                    // Parity with Iterate(): an EXACTLY_ONE body makes the mapping one-to-one, so
                    // LastPositionFinder consumers (fn:count) read the base length without mapping.
                    bool oneToOne = actionCardinality == StaticProperty.EXACTLY_ONE;
                    return (context) =>
                    {
                        ISequenceIterator @base = selectEval.Iterate(context);
                        return new ItemMappingIterator(@base, new DelegateItemMappingFunction((item) =>
                        {
                            context.SetLocalVariable(slot, item);
                            return actionEval.Eval(context);
                        }), oneToOne, context.GetController());
                    };
                }
            }

            public override IPushEvaluator ElaborateForPush()
            {
                ForExpression expr = (ForExpression)GetExpression();
                IPullEvaluator selectEval = expr.Sequence.MakeElaborator().ElaborateForPull();
                IPushEvaluator actionEval = expr.GetAction().MakeElaborator().ElaborateForPush();
                int slot = expr.LocalSlotNumber;
                return (@out, context) =>
                {
                    Controller controller = context.GetController();
                    ISequenceIterator @base = selectEval.Iterate(context);
                    for (IItem item; (item = @base.Next()) != null;)
                    {
                        controller.CheckTimeoutPerStep();
                        context.SetLocalVariable(slot, item);
                        ITailCall tc = actionEval.ProcessLeavingTail(@out, context);
                        DispatchTailCall(tc);
                    }

                    return null;
                };
            }

            public override IUpdateEvaluator ElaborateForUpdate()
            {
                ForExpression expr = (ForExpression)GetExpression();
                IPullEvaluator selectEval = expr.Sequence.MakeElaborator().ElaborateForPull();
                IUpdateEvaluator actionEval = expr.GetAction().MakeElaborator().ElaborateForUpdate();
                int slot = expr.LocalSlotNumber;
                return (context, pul) =>
                {
                    try
                    {
                        SequenceTool.Supply(selectEval.Iterate(context), (item) =>
                        {
                            context.SetLocalVariable(slot, item);
                            actionEval.RegisterUpdates(context, pul);
                        });
                    }
                    catch (UncheckedXPathException e)
                    {
                        throw e.GetXPathException();
                    }
                };
            }
        }
    }
}
