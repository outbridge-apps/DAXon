////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Core;
using OutSmart.DAXon.Expressions;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Text;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Transformation;
using OutSmart.DAXon.Trees.Iterators;
using OutSmart.DAXon.Trees.Utilities;
using OutSmart.DAXon.Values;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Functions;
using OutSmart.DAXon.Types;
using OutSmart.DAXon.Internal;
using OutSmart.DAXon.Internal.Collections;
namespace OutSmart.DAXon.Expressions.Instructions
{
    internal sealed class ForEach : Instruction, IContextSwitchingExpression
    {
        protected bool containsTailCall;
        protected Operand selectOp;
        protected Operand actionOp;
        protected Operand separatorOp;
        protected Operand threadsOp;
        protected bool _isInstruction;

        public Expression SeparatorExpression
        {
            get => separatorOp == null ? null : separatorOp.GetChildExpression(); set
            {
                separatorOp = new Operand(this, value, OperandRole.SINGLE_ATOMIC);
            }
        }

        public Expression Select
        {
            get => selectOp.GetChildExpression(); set
            {
                selectOp.SetChildExpression(value);
            }
        }

        public Expression Threads
        {
            get => threadsOp == null ? null : threadsOp.GetChildExpression(); set
            {
                if (value != null)
                {
                    if (threadsOp == null)
                    {
                        threadsOp = new Operand(this, value, OperandRole.SINGLE_ATOMIC);
                    }
                    else
                    {
                        threadsOp.SetChildExpression(value);
                    }
                }
            }
        }

        public override int InstructionNameCode => StandardNames.XSL_FOR_EACH;

        public override int ImplementationMethod => ITERATE_METHOD | PROCESS_METHOD | Expression.WATCH_METHOD | Expression.ITEM_FEED_METHOD;

        public override string ExpressionName => "forEach";

        public override string StreamerName => "ForEach";
        public ForEach(Expression select, Expression action) : this(select, action, false, null)
        {
        }

        public ForEach(Expression select, Expression action, bool containsTailCall, Expression threads)
        {
            selectOp = new Operand(this, select, OperandRole.FOCUS_CONTROLLING_SELECT);
            actionOp = new Operand(this, action, OperandRole.FOCUS_CONTROLLED_ACTION);
            if (threads != null)
            {
                threadsOp = new Operand(this, threads, OperandRole.SINGLE_ATOMIC);
            }

            this.containsTailCall = containsTailCall;
        }

        public void SetInstruction(bool inst)
        {
            _isInstruction = inst;
        }

        public override bool IsInstruction()
        {
            return _isInstruction;
        }

        public Expression GetAction()
        {
            return actionOp.GetChildExpression();
        }

        public void SetAction(Expression action)
        {
            actionOp.SetChildExpression(action);
        }

        public override IEnumerable<Operand> Operands()
        {
            return OperandSparseList(selectOp, actionOp, separatorOp, threadsOp);
        }

        public Expression GetSelectExpression()
        {
            return Select;
        }

        public Expression GetActionExpression()
        {
            return GetAction();
        }

        public override ItemType GetItemType()
        {
            return GetAction().GetItemType();
        }

        public override UType GetStaticUType(UType contextItemType)
        {
            if (IsInstruction())
            {
                return base.GetStaticUType(contextItemType);
            }
            else
            {
                return GetAction().GetStaticUType(Select.GetStaticUType(contextItemType));
            }
        }

        public override bool MayCreateNewNodes()
        {
            int props = GetAction().GetSpecialProperties();
            return (props & StaticProperty.NO_NODES_NEWLY_CREATED) == 0;
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            selectOp.TypeCheck(visitor, contextInfo);
            ItemType selectType = Select.GetItemType();
            if (selectType == ErrorType.GetInstance())
            {
                return Literal.MakeEmptySequence();
            }

            ContextItemStaticInfo cit = visitor.GetConfiguration().MakeContextItemStaticInfo(Select.GetItemType(), false);
            cit.ContextSettingExpression = Select;
            actionOp.TypeCheck(visitor, cit);
            if (!Cardinality.AllowsMany(Select.GetCardinality()))
            {
                actionOp.OperandRole = actionOp.OperandRole.ModifyProperty(OperandRole.SINGLETON, true);
            }

            return this;
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            selectOp.Optimize(visitor, contextInfo);
            ContextItemStaticInfo cit = visitor.GetConfiguration().MakeContextItemStaticInfo(Select.GetItemType(), false);
            cit.ContextSettingExpression = Select;
            actionOp.Optimize(visitor, cit);
            if (!visitor.IsOptimizeForStreaming())
            {

                // Don't eliminate a void for-each if streaming, because it can consume the stream: see test accumulator-015
                if (Literal.IsEmptySequence(Select))
                {
                    return Select;
                }

                if (Literal.IsEmptySequence(GetAction()))
                {
                    return GetAction();
                }
            }

            if (Select.GetCardinality() == StaticProperty.EXACTLY_ONE && GetAction() is AxisExpression)
            {
                return new SimpleStepExpression(Select, GetAction());
            }


            // Rewrite (1 to $N) ! (. + $M) as ($N to $N + $M)
            if (Select is RangeExpression && IsSimpleArithmeticShift(GetAction()))
            {
                ArithmeticExpression arith = (ArithmeticExpression)GetAction();
                RangeExpression range = (RangeExpression)Select;
                return new RangeExpression(new ArithmeticExpression(range.StartExpression.Copy(new RebindingMap()), arith.Operator, arith.GetRhsExpression().Copy(new RebindingMap())), new ArithmeticExpression(range.EndExpression.Copy(new RebindingMap()), arith.Operator, arith.GetRhsExpression().Copy(new RebindingMap()))).TypeCheck(visitor, contextInfo).Optimize(visitor, contextInfo);
            }


            // Rewrite (1 to 1000) ! (. + $N) as ($N to 1000 + $N); only for step 1: a range counting down
            // (a materialized reverse()) would become start + $N to end + $N, which is empty
            if (Select is Literal && ((Literal)Select).GroundedValue is IntegerRange && ((IntegerRange)((Literal)Select).GroundedValue).GetStep() == 1 && IsSimpleArithmeticShift(GetAction()) && SeparatorExpression == null)
            {
                ArithmeticExpression arith = (ArithmeticExpression)GetAction();
                IntegerRange range = (IntegerRange)((Literal)Select).GroundedValue;
                Expression shift = arith.GetRhsExpression();
                Expression newStart = new ArithmeticExpression(Literal.MakeLiteral(new Int64Value(range.start), this), arith.Operator, shift.Copy(new RebindingMap()));
                Expression newEnd = new ArithmeticExpression(Literal.MakeLiteral(new Int64Value(range.end), this), arith.Operator, shift.Copy(new RebindingMap()));
                return new RangeExpression(newStart, newEnd).TypeCheck(visitor, contextInfo).Optimize(visitor, contextInfo);
            }

            if (threadsOp != null && !Literal.IsEmptySequence(Threads) && !containsTailCall)
            {
                return visitor.ObtainOptimizer().GenerateMultithreadedInstruction(this);
            }

            return this;
        }

        private bool IsSimpleArithmeticShift(Expression exp)
        {
            if (!(exp is ArithmeticExpression))
            {
                return false;
            }

            ArithmeticExpression arith = (ArithmeticExpression)exp;
            if (!(arith.GetLhsExpression() is ContextItemExpression))
            {
                return false;
            }

            if (!(arith.Operator == Token.PLUS || arith.Operator == Token.MINUS))
            {
                return false;
            }

            if (!(arith.GetRhsExpression() is Literal || arith.GetRhsExpression() is VariableReference))
            {
                return false;
            }

            return true;
        }

        public override Expression Unordered(bool retainAllNodes, bool forStreaming)
        {
            Select = Select.Unordered(retainAllNodes, forStreaming);
            SetAction(GetAction().Unordered(retainAllNodes, forStreaming));
            return this;
        }

        public override PathMap.PathMapNodeSet AddToPathMap(PathMap pathMap, PathMap.PathMapNodeSet pathMapNodeSet)
        {
            PathMap.PathMapNodeSet target = Select.AddToPathMap(pathMap, pathMapNodeSet);
            return GetAction().AddToPathMap(pathMap, target);
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            ForEach f2 = new ForEach(Select.Copy(rebindings), GetAction().Copy(rebindings), containsTailCall, Threads);
            if (separatorOp != null)
            {
                f2.SeparatorExpression = SeparatorExpression.Copy(rebindings);
            }

            ExpressionTool.CopyLocationInfo(this, f2);
            f2.SetInstruction(IsInstruction());
            return f2;
        }

        protected override int ComputeSpecialProperties()
        {
            int p = base.ComputeSpecialProperties();
            if (Select.GetCardinality() == StaticProperty.EXACTLY_ONE)
            {
                p |= GetAction().GetSpecialProperties();
            }
            else
            {
                p |= GetAction().GetSpecialProperties() & StaticProperty.ALL_NODES_UNTYPED;
            }

            return p;
        }

        public override bool AlwaysCreatesNewNodes()
        {
            return (GetAction() is Instruction) && ((Instruction)GetAction()).AlwaysCreatesNewNodes();
        }

        public override bool IsUpdatingExpression()
        {
            return GetAction().IsUpdatingExpression();
        }

        public override void CheckForUpdatingSubexpressions()
        {
            if (Select.IsUpdatingExpression())
            {
                throw new XPathException("Updating expression appears in a context where it is not permitted", "XUST0001").WithLocation(Select.GetLocation());
            }
        }

        public override void CheckPermittedContents(ISchemaType parentType, bool whole)
        {
            GetAction().CheckPermittedContents(parentType, false);
        }

        //
        //
        //    }
        protected NodeInfo MakeSeparator(IXPathContext context)
        {
            NodeInfo separator;
            UnicodeString sepValue = separatorOp.GetChildExpression().EvaluateAsString(context);
            Orphan orphan = new Orphan(context.GetConfiguration());
            orphan.SetNodeKind(Types.Type.TEXT);
            orphan.SetStringValue(sepValue);
            separator = orphan;
            return separator;
        }

        public override ISequenceIterator Iterate(IXPathContext context)
        {
            XPathContextMinor c2 = context.NewMinorContext();
            c2.TrackFocus(Select.Iterate(context));
            if (separatorOp == null)
            {
                return new ContextMappingIterator((c3) => GetAction().Iterate(c3), c2);
            }
            else
            {
                NodeInfo separator = MakeSeparator(context);
                IContextMappingFunction mapper = (cxt) =>
                {
                    if (cxt.GetCurrentIterator().Position() == 1)
                    {
                        return GetAction().Iterate(cxt);
                    }
                    else
                    {
                        return (ISequenceIterator)new PrependSequenceIterator(separator, GetAction().Iterate(cxt));
                    }
                };
                return new ContextMappingIterator(mapper, c2);
            }
        }

        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("forEach", this);
            if (containsTailCall)
            {
                @out.EmitAttribute("flags", "t");
            }

            Select.Export(@out);
            GetAction().Export(@out);
            if (separatorOp != null)
            {
                @out.SetChildRole("separator");
                separatorOp.GetChildExpression().Export(@out);
            }

            ExplainThreads(@out);
            @out.EndElement();
        }

        protected void ExplainThreads(ExpressionPresenter @out)
        {
        }

        //
        //
        //    }
        // no action in this class: implemented in subclass
        public override string ToString()
        {
            return ExpressionTool.Parenthesize(Select) + " ! " + ExpressionTool.Parenthesize(GetAction());
        }

        public override string ToShortString()
        {
            return Select.ToShortString() + "!" + GetAction().ToShortString();
        }

        public override Elaborator GetElaborator()
        {
            return new ForEachElaborator();
        }

        internal sealed class ForEachElaborator : PullElaborator
        {
            private NodeInfo MakeSeparator(IUnicodeStringEvaluator evaluator, IXPathContext context)
            {
                UnicodeString sepValue = evaluator.Eval(context);
                Orphan orphan = new Orphan(context.GetConfiguration());
                orphan.SetNodeKind(Types.Type.TEXT);
                orphan.SetStringValue(sepValue);
                return orphan;
            }

            public override IPullEvaluator ElaborateForPull()
            {
                ForEach forEach = (ForEach)GetExpression();
                IPullEvaluator select = forEach.GetSelectExpression().MakeElaborator().ElaborateForPull();
                if (forEach.SeparatorExpression == null)
                {
                    // A statically ≤1-item action maps via its item evaluator directly — no
                    // SingletonIterator allocated per focus item just to be drained once.
                    if (!Cardinality.AllowsMany(forEach.GetActionExpression().GetCardinality()))
                    {
                        IItemEvaluator actionItem = forEach.GetActionExpression().MakeElaborator().ElaborateForItem();
                        return (context) =>
                        {
                            XPathContextMinor c2 = context.NewMinorContext();
                            c2.TrackFocus(select.Iterate(context));
                            return new SingletonContextMappingIterator(actionItem, c2);
                        };
                    }

                    IPullEvaluator action = forEach.GetActionExpression().MakeElaborator().ElaborateForPull();
                    IContextMappingFunction mapper = (cxt) => action.Iterate(cxt);
                    return (context) =>
                    {
                        XPathContextMinor c2 = context.NewMinorContext();
                        c2.TrackFocus(select.Iterate(context));
                        return new ContextMappingIterator(mapper, c2);
                    };
                }
                else
                {
                    IPullEvaluator action = forEach.GetActionExpression().MakeElaborator().ElaborateForPull();
                    IUnicodeStringEvaluator sepEval = forEach.SeparatorExpression.MakeElaborator().ElaborateForUnicodeString(true);
                    return (context) =>
                    {
                        NodeInfo separator = MakeSeparator(sepEval, context);
                        IContextMappingFunction mapper = (cxt) =>
                        {
                            if (cxt.GetCurrentIterator().Position() == 1)
                            {
                                return action.Iterate(cxt);
                            }
                            else
                            {
                                return (ISequenceIterator)new PrependSequenceIterator(separator, action.Iterate(cxt));
                            }
                        };
                        XPathContextMinor c2 = context.NewMinorContext();
                        c2.TrackFocus(select.Iterate(context));
                        return new ContextMappingIterator(mapper, c2);
                    };
                }
            }

            public override IPushEvaluator ElaborateForPush()
            {
                ForEach forEach = (ForEach)GetExpression();
                IPullEvaluator select = forEach.GetSelectExpression().MakeElaborator().ElaborateForPull();
                IPushEvaluator action = forEach.GetActionExpression().MakeElaborator().ElaborateForPush();
                if (forEach.SeparatorExpression == null)
                {
                    if (forEach.containsTailCall)
                    {

                        // This path is used when a for-each has a singleton select and contains a call-template
                        // or apply-templates that is classified as a tail call. The iterator deliberately doesn't
                        // advance beyond the first (and only) item. See bug 5845.
                        return (@out, context) =>
                        {
                            XPathContextMinor c2 = context.NewMinorContext();
                            IFocusIterator iter = c2.TrackFocus(select.Iterate(context));
                            ITailCall tc = null;
                            if (iter.Next() != null)
                            {
                                tc = action.ProcessLeavingTail(@out, c2);
                            }

                            return tc;
                        };
                    }
                    else
                    {
                        return (@out, context) =>
                        {
                            Controller controller = context.GetController();
                            XPathContextMinor c2 = context.NewMinorContext();
                            IFocusIterator iter = c2.TrackFocus(select.Iterate(context));
                            ITailCall tc = null;
                            while (iter.Next() != null)
                            {
                                controller.CheckTimeoutPerStep();
                                DispatchTailCall(tc);
                                tc = action.ProcessLeavingTail(@out, c2);
                            }

                            return tc;
                        };
                    }
                }
                else
                {
                    IUnicodeStringEvaluator sepEval = forEach.SeparatorExpression.MakeElaborator().ElaborateForUnicodeString(true);
                    return (@out, context) =>
                    {
                        Controller controller = context.GetController();
                        NodeInfo separator = MakeSeparator(sepEval, context);
                        XPathContextMinor c2 = context.NewMinorContext();
                        IFocusIterator iter = c2.TrackFocus(select.Iterate(context));
                        ITailCall tc = null;
                        if (iter.Next() != null)
                        {
                            DispatchTailCall(tc);
                            tc = action.ProcessLeavingTail(@out, c2);
                        }

                        while (iter.Next() != null)
                        {
                            controller.CheckTimeoutPerStep();
                            @out.Append(separator);
                            DispatchTailCall(tc);
                            tc = action.ProcessLeavingTail(@out, c2);
                        }

                        return tc;
                    };
                }
            }

            public override IUpdateEvaluator ElaborateForUpdate()
            {
                ForEach forEach = (ForEach)GetExpression();
                IPullEvaluator select = forEach.GetSelectExpression().MakeElaborator().ElaborateForPull();
                IUpdateEvaluator action = forEach.GetActionExpression().MakeElaborator().ElaborateForUpdate();
                return (context, pul) =>
                {
                    XPathContextMinor c2 = context.NewMinorContext();
                    c2.TrackFocus(select.Iterate(context));
                    ISequenceIterator iter = c2.GetCurrentIterator();
                    while (iter.Next() != null)
                    {
                        action.RegisterUpdates(c2, pul);
                    }
                };
            }
        }
    }
}