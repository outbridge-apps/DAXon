////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Instructions;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Model;
namespace OutSmart.DAXon.Expressions
{
    public class LetExpression : Assignation
    {
        private ISequenceEvaluator evaluator = null;
        private bool needsEagerEvaluation = false;
        private bool needsLazyEvaluation = false;
        private bool _isInstruction;

        public override string ExpressionName => "let";

        public override double Cost => Sequence.Cost + GetAction().Cost;

        public override IntegerValue[] IntegerBounds => GetAction().IntegerBounds;

        public override int ImplementationMethod => GetAction().ImplementationMethod;

        public virtual ISequenceEvaluator Evaluator
        {
            get => evaluator; set
            {
                this.evaluator = value;
            }
        }
        public LetExpression()
        {
        }

        public virtual void SetInstruction(bool inst)
        {
            _isInstruction = inst;
        }

        public override bool IsInstruction()
        {
            return _isInstruction;
        }

        public virtual void SetNeedsEagerEvaluation(bool req)
        {
            if (req && needsLazyEvaluation)
            {
            }

            this.needsEagerEvaluation = req;
        }

        public virtual void SetNeedsLazyEvaluation(bool req)
        {
            if (req && needsEagerEvaluation)
            {
                this.needsEagerEvaluation = false; // Bug 2903: lazy evaluation wins
            }

            this.needsLazyEvaluation = req;
        }

        public virtual bool IsNeedsLazyEvaluation()
        {
            return needsLazyEvaluation;
        }

        public virtual bool IsNeedsEagerEvaluation()
        {
            return needsEagerEvaluation;
        }

        public override bool SupportsLazyEvaluation()
        {
            return !needsEagerEvaluation;
        }

        public override bool IsLiftable(bool forStreaming)
        {
            return base.IsLiftable(forStreaming) && !needsEagerEvaluation;
        }

        public override void ResetLocalStaticProperties()
        {
            base.ResetLocalStaticProperties();
            references = new List<VariableReference>(); // bug 3233
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {

            // The order of events is critical here. First we ensure that the type of the
            // sequence expression is established. This is used to establish the type of the variable,
            // which in turn is required when type-checking the action part.
            SequenceOp.TypeCheck(visitor, contextInfo);
            Func<RoleDiagnostic> role = () => new RoleDiagnostic(RoleDiagnostic.VARIABLE, GetVariableQName().DisplayName, 0);
            if (visitor.StaticContext.GetXPathVersion() == 40)
            {
                TypeChecker tc = visitor.GetConfiguration().GetTypeChecker(false);
                Sequence = tc.StaticTypeCheck(Sequence, requiredType, role, visitor);
            }
            else
            {
                Sequence = TypeChecker.StrictTypeCheck(Sequence, requiredType, role, visitor.StaticContext);
            }

            ItemType actualItemType = Sequence.GetItemType();
            RefineTypeInformation(actualItemType, Sequence.GetCardinality(), Sequence is Literal ? ((Literal)Sequence).GroundedValue : null, Sequence.GetSpecialProperties(), this);
            ActionOp.TypeCheck(visitor, contextInfo);
            return this;
        }

        public override bool ImplementsStaticTypeCheck()
        {
            return true;
        }

        public override Expression StaticTypeCheck(SequenceType req, bool backwardsCompatible, Func<RoleDiagnostic> roleSupplier, ExpressionVisitor visitor)
        {
            TypeChecker tc = visitor.GetConfiguration().GetTypeChecker(backwardsCompatible);
            SetAction(tc.StaticTypeCheck(GetAction(), req, roleSupplier, visitor));
            return this;
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextItemType)
        {
            Optimizer opt = visitor.ObtainOptimizer();

            // if this is a construct of the form "let $j := EXP return $j" replace it with EXP
            // Remarkably, people do write this, and it can also be produced by previous rewrites
            // Note that type checks will already have been added to the sequence expression
            if (GetAction() is VariableReference && ((VariableReference)GetAction()).GetBinding() == this && !ExpressionTool.ChangesXsltContext(Sequence))
            {
                SequenceOp.Optimize(visitor, contextItemType);
                opt.Trace("Eliminated trivial variable " + VariableName, Sequence);
                return Sequence;
            }

            if (Sequence is Literal && opt.IsOptionSet(OptimizerOptions.INLINE_VARIABLES))
            {

                // inline the variable: replace all references by the constant value
                // This relies on the fact that optimizing the action part will cause any references to be inlined
                opt.Trace("Inlined constant variable " + VariableName, Sequence);
                ReplaceVariable(Sequence);
                return GetAction().Optimize(visitor, contextItemType);
            }


            // if this is an XSLT construct of the form <xsl:variable>text</xsl:variable>, try to replace
            // it by <xsl:variable select="..."/>. This can be done if all the references to the variable use
            // its value as a string (rather than, say, as a node or as a boolean)
            if (Sequence is DocumentInstr && ((DocumentInstr)Sequence).IsTextOnly())
            {

                // Ensure the list of references is accurate
                VerifyReferences();

                // Check whether all uses of the variable are atomized or stringified
                if (AllReferencesAreFlattened())
                {
                    Expression stringValueExpression = ((DocumentInstr)Sequence).StringValueExpression;
                    stringValueExpression = stringValueExpression.TypeCheck(visitor, contextItemType);
                    Sequence = stringValueExpression;
                    requiredType = SequenceType.SINGLE_UNTYPED_ATOMIC;
                    AdoptChildExpression(Sequence);
                    RefineTypeInformation(requiredType.PrimaryType, requiredType.GetCardinality(), null, 0, this);
                }
            }


            // If the initializing expression has (potential) side-effects, prevent optimizations such as elimination
            // of unused variables
            if (Sequence.HasSpecialProperty(StaticProperty.HAS_SIDE_EFFECTS))
            {
                needsEagerEvaluation = true;
            }


            // Removal of redundant variables, and inlining of variables that are only used once, depends on accurate
            // knowledge of all references to the variable. The problem is that obtaining this knowledge can be expensive:
            // see bug 2707. On the other hand, failing to do these optimizations is not fatal. So the general approach
            // is that we limit the time spent discovering the information, and we don't do the optimization unless
            // it is safe.
            // Typically on entry to optimize(), the typeCheck() method has already been called, and this has set up
            // a list of references. First we examine this list of references and remove any that are "dead", that @is
            // they no longer have this LetExpression as an ancestor in the expression tree. This function also checks
            // whether any of these references are known to be in a loop, and returns true if so.
            hasLoopingReference |= RemoveDeadReferences();
            if (!needsEagerEvaluation)
            {

                // If there are less than two references, and none @is in a loop, then there is potential
                // for optimization. But we now need to be absolutely sure that we have an accurate list
                // of references.
                bool considerRemoval = ((references != null && references.Count < 2) || Sequence is VariableReference) && !indexedVariable && !hasLoopingReference && !needsEagerEvaluation;
                if (considerRemoval)
                {
                    VerifyReferences();

                    // At this point the list of references is either accurate, or null
                    considerRemoval = references != null;
                }

                if (considerRemoval && references.Count == 0)
                {

                    // variable is not used - no need to evaluate it
                    ActionOp.Optimize(visitor, contextItemType);
                    opt.Trace("Eliminated unused variable " + VariableName, GetAction());
                    return GetAction();
                }


                // Don't inline context-dependent variables in a streamable template. See strmode011.
                // The reason for this is that a variable <xsl:variable><xsl:copy-of select="."/></xsl:variable>
                // can be evaluated in streaming mode, but an arbitrary expression using copy() inline can't (e.g.
                // if it appears in a path expression or as an operand of an arithmetic expression)
                if (considerRemoval && references.Count == 1 && ExpressionTool.DependsOnFocus(Sequence))
                {
                    if (visitor.IsOptimizeForStreaming())
                    {
                        considerRemoval = false;
                    }


                    // Disallow inlining if the focus of the variable reference differs from the containing binding.
                    Expression child = references[0];
                    Expression parent = child.ParentExpression;
                    while (parent != null && parent != this)
                    {
                        Operand operand = ExpressionTool.FindOperand(parent, child);
                        if (!operand.HasSameFocus())
                        {
                            considerRemoval = false;
                            break;
                        }

                        child = parent;
                        parent = child.ParentExpression;
                    }
                }

                if (considerRemoval && references.Count == 1)
                {
                    if (ExpressionTool.ChangesXsltContext(Sequence))
                    {

                        // Don't inline variables whose initializer might contain a call to xsl:result-document
                        considerRemoval = false;
                    }
                    else if ((Sequence.Dependencies & StaticProperty.DEPENDS_ON_CURRENT_GROUP) != 0)
                    {

                        // Don't inline variables that depend on current-group() or current-grouping-key()
                        considerRemoval = false;
                    }
                    else if (references[0].IsInLoop())
                    {

                        // Don't inline variables where the variable reference is evaluated repeatedly
                        considerRemoval = false;
                    }
                }

                if (considerRemoval && (references.Count == 1 || Sequence is Literal || Sequence is VariableReference) && opt.IsOptionSet(OptimizerOptions.INLINE_VARIABLES))
                {
                    InlineReferences();
                    opt.Trace("Inlined references to $" + VariableName, GetAction());
                    references = null;
                    return GetAction().Optimize(visitor, contextItemType);
                }
            }

            int tries = 0;
            while (tries++ < 5)
            {
                Expression seq0 = Sequence;
                SequenceOp.Optimize(visitor, contextItemType);
                if (Sequence is Literal && !indexedVariable && opt.IsOptionSet(OptimizerOptions.INLINE_VARIABLES))
                {
                    return Optimize(visitor, contextItemType);
                }

                if (seq0 == Sequence)
                {
                    break;
                }
            }

            tries = 0;
            while (tries++ < 5)
            {
                Expression act0 = GetAction();
                ActionOp.Optimize(visitor, contextItemType);
                if (act0 == GetAction())
                {
                    break;
                }

                if (!indexedVariable && !needsEagerEvaluation)
                {
                    VerifyReferences();
                    if (references != null && references.Count < 2)
                    {
                        if (references.Count == 0)
                        {

                            // We may have removed references to the variable; try again at eliminating this expression.
                            hasLoopingReference = false;
                            return Optimize(visitor, contextItemType);
                        }
                        else
                        {

                            // there is one remaining reference; try again at inlining if it's not in a loop
                            if (!references[0].IsInLoop())
                            {
                                return Optimize(visitor, contextItemType);
                            }
                        }
                    }
                }
            }


            // Don't use lazy evaluation for a variable that is referenced inside the "try" part of a contained try catch (XSLT3 test try-031)
            return this;
        }

        public virtual void SetEvaluator()
        {
            if (IsIndexedVariable())
            {
                IPullEvaluator pullEval = Sequence.MakeElaborator().ElaborateForPull();
                Evaluator = new IndexedVariableEvaluator(pullEval);
            }
            else if (needsEagerEvaluation || !Sequence.SupportsLazyEvaluation())
            {
                Evaluator = Sequence.MakeElaborator().Eagerly();
            }
            else if (needsLazyEvaluation)
            {
                Evaluator = Sequence.MakeElaborator().Lazily(NominalReferenceCount > 1, needsLazyEvaluation);
            }
            else if (evaluator == null)
            {
                Evaluator = new LearningEvaluator(Sequence, Sequence.MakeElaborator().Lazily(NominalReferenceCount > 1, false));
            }
        }

        private void InlineReferences()
        {

            // Note that the list of references might include references that are no longer reachable on the tree.
            // We therefore take no action if (a) the parent of the reference is null, or (b) the reference @is
            // not found among the children of its parent.
            foreach (VariableReference @ref in references)
            {
                Expression parent = @ref.ParentExpression;
                if (parent != null)
                {
                    Operand o = ExpressionTool.FindOperand(parent, @ref);
                    if (o != null)
                    {
                        o.SetChildExpression(Sequence.Copy(new RebindingMap()));
                    }

                    ExpressionTool.ResetStaticProperties(parent);
                }
            }
        }

        private bool AllReferencesAreFlattened()
        {
            if (references != null)
            {
                foreach (VariableReference @ref in references)
                {
                    if (!@ref.IsFlattened())
                    {
                        return false;
                    }
                }

                return true;
            }

            return false;
        }

        public override bool IsVacuousExpression()
        {
            return GetAction().IsVacuousExpression();
        }

        public override void CheckPermittedContents(ISchemaType parentType, bool whole)
        {
            GetAction().CheckPermittedContents(parentType, whole);
        }

        public override void GatherProperties(Action<string, object> consumer)
        {
            consumer("name",GetVariableQName());
        }

        /// <summary>
        /// Iterate over the result of the expression to return a sequence of items
        /// </summary>
        public override ISequenceIterator Iterate(IXPathContext context)
        {

            // minimize stack consumption by evaluating nested LET expressions iteratively
            LetExpression let = this;
            while (true)
            {
                ISequence val = let.Eval(context);
                context.SetLocalVariable(let.LocalSlotNumber, val);
                if (let.GetAction() is LetExpression)
                {
                    let = (LetExpression)let.GetAction();
                }
                else
                {
                    break;
                }
            }

            return let.GetAction().Iterate(context);
        }

        /// <summary>
        /// Iterate over the result of the expression to return a sequence of items
        /// </summary>
        public virtual ISequence Eval(IXPathContext context)
        {
            if (evaluator == null)
            {
                if (needsEagerEvaluation)
                {
                    Evaluator = Sequence.MakeElaborator().Eagerly();
                }
                else
                {
                    Evaluator = new LearningEvaluator(Sequence, Sequence.MakeElaborator().Lazily(NominalReferenceCount > 1, false));
                }
            }

            try
            {
                int savedOutputState = context.TemporaryOutputState;
                context.TemporaryOutputState = StandardNames.XSL_VARIABLE;
                ISequence result = evaluator.Evaluate(context);
                context.TemporaryOutputState = savedOutputState;
                return result;
            }
            catch (InvalidCastException)
            {
                int savedOutputState = context.TemporaryOutputState;
                context.TemporaryOutputState = StandardNames.XSL_VARIABLE;
                ISequence result = ExpressionTool.EagerEvaluate(Sequence, context);
                context.TemporaryOutputState = savedOutputState;
                return result;
            }
        }

        public override IItem EvaluateItem(IXPathContext context)
        {
            return MakeElaborator().ElaborateForItem().Eval(context);
        }

        public override bool EffectiveBooleanValue(IXPathContext context)
        {

            // minimize stack consumption by evaluating nested LET expressions iteratively
            LetExpression let = this;
            while (true)
            {
                ISequence val = let.Eval(context);
                context.SetLocalVariable(let.LocalSlotNumber, val);
                if (let.GetAction() is LetExpression)
                {
                    let = (LetExpression)let.GetAction();
                }
                else
                {
                    break;
                }
            }

            return let.GetAction().EffectiveBooleanValue(context);
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
            if (IsInstruction())
            {
                return UType.ANY;
            }

            return GetAction().GetStaticUType(contextItemType);
        }

        /// <summary>
        /// Determine the static cardinality of the expression
        /// </summary>
        protected override int ComputeCardinality()
        {
            return GetAction().GetCardinality();
        }

        /// <summary>
        /// Determine the static cardinality of the expression
        /// </summary>
        protected override int ComputeSpecialProperties()
        {
            int props = GetAction().GetSpecialProperties();
            int seqProps = Sequence.GetSpecialProperties();
            if ((seqProps & StaticProperty.NO_NODES_NEWLY_CREATED) == 0)
            {
                props &= ~StaticProperty.NO_NODES_NEWLY_CREATED;
            }

            return props;
        }

        public override int MarkTailFunctionCalls(StructuredQName qName, int arity)
        {
            return ExpressionTool.MarkTailFunctionCalls(GetAction(), qName, arity);
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            LetExpression let = new LetExpression();
            rebindings.Put(this, let);
            let.indexedVariable = indexedVariable;
            let.hasLoopingReference = hasLoopingReference;
            let.SetNeedsEagerEvaluation(needsEagerEvaluation);
            let.SetNeedsLazyEvaluation(needsLazyEvaluation);
            let.SetVariableQName(variableName);
            let.SetRequiredType(requiredType);
            let.Sequence = Sequence.Copy(rebindings);
            let.SetInstruction(IsInstruction());
            ExpressionTool.CopyLocationInfo(this, let);
            Expression newAction = GetAction().Copy(rebindings);
            let.SetAction(newAction);
            return let;
        }

        public override string ToString()
        {
            return "let $" + VariableEQName + " := " + Sequence + " return " + ExpressionTool.Parenthesize(GetAction());
        }

        public override string ToShortString()
        {
            return "let $" + VariableName + " := ...";
        }

        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("let", this);
            @out.EmitAttribute("var", variableName);
            if (GetRequiredType() != SequenceType.ANY_SEQUENCE)
            {
                @out.EmitAttribute("as", GetRequiredType().ToAlphaCode());
            }

            if (IsIndexedVariable())
            {
                @out.EmitAttribute("indexable", "true");
            }

            @out.EmitAttribute("slot", LocalSlotNumber + "");
            if (needsEagerEvaluation || needsLazyEvaluation)
            {
                string flags = (needsEagerEvaluation ? "e" : "") + (needsLazyEvaluation ? "l" : "");
                @out.EmitAttribute("flags", flags);
            }

            Sequence.Export(@out);
            GetAction().Export(@out);
            @out.EndElement();
        }

        public override Elaborator GetElaborator()
        {
            return new LetExprElaborator();
        }

        internal sealed class LetExprElaborator : PullElaborator
        {
            private ISequenceEvaluator MakeSequenceEvaluator(LetExpression let)
            {
                if (let.evaluator != null)
                {
                    return let.evaluator;
                }

                let.SetEvaluator();
                return let.evaluator;
            }

            private IItemEvaluator SetAllVariables(LetExpression start, IList<Expression> finalAction)
            {
                IList<LetExpression> setters = new List<LetExpression>();
                setters.Add(start);
                Expression next = start.GetAction();
                while (next is LetExpression)
                {
                    setters.Add((LetExpression)next);
                    next = ((LetExpression)next).GetAction();
                }

                finalAction.Add(next);
                switch (setters.Count)
                {
                    case 1:
                        {
                            LetExpression let = setters[0];
                            ISequenceEvaluator evaluator = MakeSequenceEvaluator(let);
                            int slot = setters[0].slotNumber;
                            return (context) =>
                            {
                                context.SetLocalVariable(slot, evaluator.Evaluate(context));
                                return null;
                            };
                        }

                    case 2:
                        {
                            ISequenceEvaluator evaluator0 = MakeSequenceEvaluator(setters[0]);
                            int slot0 = setters[0].slotNumber;
                            ISequenceEvaluator evaluator1 = MakeSequenceEvaluator(setters[1]);
                            int slot1 = setters[1].slotNumber;
                            return (context) =>
                            {
                                context.SetLocalVariable(slot0, evaluator0.Evaluate(context));
                                context.SetLocalVariable(slot1, evaluator1.Evaluate(context));
                                return null;
                            };
                        }

                    case 3:
                        {
                            ISequenceEvaluator evaluator0 = MakeSequenceEvaluator(setters[0]);
                            int slot0 = setters[0].slotNumber;
                            ISequenceEvaluator evaluator1 = MakeSequenceEvaluator(setters[1]);
                            int slot1 = setters[1].slotNumber;
                            ISequenceEvaluator evaluator2 = MakeSequenceEvaluator(setters[2]);
                            int slot2 = setters[2].slotNumber;
                            return (context) =>
                            {
                                context.SetLocalVariable(slot0, evaluator0.Evaluate(context));
                                context.SetLocalVariable(slot1, evaluator1.Evaluate(context));
                                context.SetLocalVariable(slot2, evaluator2.Evaluate(context));
                                return null;
                            };
                        }

                    case 4:
                        {
                            ISequenceEvaluator evaluator0 = MakeSequenceEvaluator(setters[0]);
                            int slot0 = setters[0].slotNumber;
                            ISequenceEvaluator evaluator1 = MakeSequenceEvaluator(setters[1]);
                            int slot1 = setters[1].slotNumber;
                            ISequenceEvaluator evaluator2 = MakeSequenceEvaluator(setters[2]);
                            int slot2 = setters[2].slotNumber;
                            ISequenceEvaluator evaluator3 = MakeSequenceEvaluator(setters[3]);
                            int slot3 = setters[3].slotNumber;
                            return (context) =>
                            {
                                context.SetLocalVariable(slot0, evaluator0.Evaluate(context));
                                context.SetLocalVariable(slot1, evaluator1.Evaluate(context));
                                context.SetLocalVariable(slot2, evaluator2.Evaluate(context));
                                context.SetLocalVariable(slot3, evaluator3.Evaluate(context));
                                return null;
                            };
                        }

                    default:
                        {
                            ISequenceEvaluator[] evaluators = new ISequenceEvaluator[setters.Count];
                            int[] slots = new int[setters.Count];
                            for (int i = 0; i < setters.Count; i++)
                            {
                                evaluators[i] = MakeSequenceEvaluator(setters[i]);
                                slots[i] = setters[i].slotNumber;
                            }

                            return (context) =>
                            {
                                for (int i = 0; i < slots.Length; i++)
                                {
                                    context.SetLocalVariable(slots[i], evaluators[i].Evaluate(context));
                                }

                                return null;
                            };
                        }
                }
            }

            public override ISequenceEvaluator Eagerly()
            {
                LetExpression expr = (LetExpression)GetExpression();
                if (expr.needsLazyEvaluation)
                {
                    return Lazily(true, true);
                }

                ISequenceEvaluator selectEval = expr.Sequence.MakeElaborator().Eagerly();
                ISequenceEvaluator actionEval = expr.GetAction().MakeElaborator().Eagerly();
                int slot = expr.LocalSlotNumber;
                return new EagerLocalVariableEvaluator(slot, selectEval, actionEval);
            }

            public override IPullEvaluator ElaborateForPull()
            {
                LetExpression expr = (LetExpression)GetExpression();
                IList<Expression> finalAction = new List<Expression>(1);
                IItemEvaluator setter = SetAllVariables(expr, finalAction);
                IPullEvaluator actionPull = finalAction[0].MakeElaborator().ElaborateForPull();
                return (context) =>
                {
                    int savedOutputState = context.TemporaryOutputState;
                    context.TemporaryOutputState = StandardNames.XSL_VARIABLE;
                    setter.Eval(context);
                    context.TemporaryOutputState = savedOutputState;
                    return actionPull.Iterate(context);
                };
            }

            public override IPushEvaluator ElaborateForPush()
            {
                LetExpression expr = (LetExpression)GetExpression();
                IList<Expression> finalAction = new List<Expression>(1);
                IItemEvaluator setter = SetAllVariables(expr, finalAction);
                IPushEvaluator actionPush = finalAction[0].MakeElaborator().ElaborateForPush();
                return (@out, context) =>
                {
                    int savedOutputState = context.TemporaryOutputState;
                    context.TemporaryOutputState = StandardNames.XSL_VARIABLE;
                    setter.Eval(context);
                    context.TemporaryOutputState = savedOutputState;
                    return actionPush.ProcessLeavingTail(@out, context);
                };
            }

            public override IItemEvaluator ElaborateForItem()
            {
                LetExpression expr = (LetExpression)GetExpression();
                IList<Expression> finalAction = new List<Expression>(1);
                IItemEvaluator setter = SetAllVariables(expr, finalAction);
                IItemEvaluator actionEval = finalAction[0].MakeElaborator().ElaborateForItem();
                return (context) =>
                {
                    int savedOutputState = context.TemporaryOutputState;
                    context.TemporaryOutputState = StandardNames.XSL_VARIABLE;
                    setter.Eval(context);
                    context.TemporaryOutputState = savedOutputState;
                    return actionEval.Eval(context);
                };
            }

            public override IUpdateEvaluator ElaborateForUpdate()
            {
                LetExpression expr = (LetExpression)GetExpression();
                IList<Expression> finalAction = new List<Expression>(1);
                IItemEvaluator setter = SetAllVariables(expr, finalAction);
                IUpdateEvaluator actionEval = finalAction[0].MakeElaborator().ElaborateForUpdate();
                return (context, pul) =>
                {
                    int savedOutputState = context.TemporaryOutputState;
                    context.TemporaryOutputState = StandardNames.XSL_VARIABLE;
                    setter.Eval(context);
                    context.TemporaryOutputState = savedOutputState;
                    actionEval.RegisterUpdates(context, pul);
                };
            }
        }

        private sealed class EagerLocalVariableEvaluator : ISequenceEvaluator
        {
            private readonly int slot;
            private readonly ISequenceEvaluator selectEval;
            private readonly ISequenceEvaluator actionEval;
            public EagerLocalVariableEvaluator(int slot, ISequenceEvaluator selectEval, ISequenceEvaluator actionEval)
            {
                this.slot = slot;
                this.selectEval = selectEval;
                this.actionEval = actionEval;
            }

            public ISequence Evaluate(IXPathContext context)
            {
                int savedOutputState = context.TemporaryOutputState;
                context.TemporaryOutputState = StandardNames.XSL_VARIABLE;
                ISequence value = selectEval.Evaluate(context);
                context.SetLocalVariable(slot, value);
                context.TemporaryOutputState = savedOutputState;
                return actionEval.Evaluate(context);
            }
        }
    }
}
