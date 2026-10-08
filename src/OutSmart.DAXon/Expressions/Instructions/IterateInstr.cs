////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
// Copyright (c) 2018-2023 Saxonica Limited
// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at http://mozilla.org/MPL/2.0/.
// This Source Code Form is "Incompatible With Secondary Licenses", as defined by the Mozilla Public License, v. 2.0.
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
using OutSmart.DAXon.Events;
using OutSmart.DAXon.Expressions.Elaboration;
using OutSmart.DAXon.Expressions.Parsing;
using OutSmart.DAXon.Lib;
using OutSmart.DAXon.Model;
using OutSmart.DAXon.Tracing;
using OutSmart.DAXon.Values;
using System.Collections.Generic;
using OutSmart.DAXon.Types;
namespace OutSmart.DAXon.Expressions.Instructions
{
    /// <summary>
    /// An IterateInstr is the compiled form of an xsl:iterate instruction
    /// </summary>
    internal sealed class IterateInstr : Instruction, IContextSwitchingExpression
    {
        private readonly Operand selectOp;
        private readonly Operand actionOp;
        private readonly Operand initiallyOp;
        private readonly Operand onCompletionOp;

        public LocalParamBlock InitiallyExp
        {
            get => (LocalParamBlock)initiallyOp.GetChildExpression(); set
            {
                initiallyOp.SetChildExpression(value);
            }
        }

        public Expression OnCompletion
        {
            get => onCompletionOp.GetChildExpression(); set
            {
                onCompletionOp.SetChildExpression(value);
            }
        }

        public override int InstructionNameCode => StandardNames.XSL_ITERATE;

        public override string StreamerName => "Iterate";

        public override int ImplementationMethod => PROCESS_METHOD;
        public IterateInstr(Expression select, LocalParamBlock initiallyExp, Expression action, Expression onCompletion)
        {
            if (onCompletion == null)
            {
                onCompletion = Literal.MakeEmptySequence();
            }

            selectOp = new Operand(this, select, OperandRole.FOCUS_CONTROLLING_SELECT);
            actionOp = new Operand(this, action, OperandRole.FOCUS_CONTROLLED_ACTION);
            initiallyOp = new Operand(this, initiallyExp, new OperandRole(OperandRole.CONSTRAINED_CLASS, OperandUsage.NAVIGATION, SequenceType.ANY_SEQUENCE, (expr) => expr is LocalParamBlock));
            onCompletionOp = new Operand(this, onCompletion, new OperandRole(OperandRole.USES_NEW_FOCUS, OperandUsage.TRANSMISSION));
        }

        public override IEnumerable<Operand> Operands()
        {
            return OperandList(selectOp, actionOp, initiallyOp, onCompletionOp);
        }

        public Expression GetSelectExpression()
        {
            return selectOp.GetChildExpression();
        }

        public Expression GetActionExpression()
        {
            return actionOp.GetChildExpression();
        }

        public override Expression TypeCheck(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            selectOp.TypeCheck(visitor, contextInfo);
            initiallyOp.TypeCheck(visitor, contextInfo);
            ItemType selectType = GetSelectExpression().GetItemType();
            if (selectType == ErrorType.GetInstance())
            {
                selectType = AnyItemType.GetInstance();
            }

            ContextItemStaticInfo cit = visitor.GetConfiguration().MakeContextItemStaticInfo(selectType, false);
            cit.ContextSettingExpression = GetSelectExpression();
            actionOp.TypeCheck(visitor, cit);
            onCompletionOp.TypeCheck(visitor, ContextItemStaticInfo.ABSENT);
            if (Literal.IsEmptySequence(OnCompletion))
            {
                if (Literal.IsEmptySequence(GetSelectExpression()) || Literal.IsEmptySequence(GetActionExpression()))
                {
                    return OnCompletion;
                }
            }

            return this;
        }

        public override Expression Optimize(ExpressionVisitor visitor, ContextItemStaticInfo contextInfo)
        {
            selectOp.Optimize(visitor, contextInfo);
            initiallyOp.Optimize(visitor, contextInfo);
            ContextItemStaticInfo cit2 = visitor.GetConfiguration().MakeContextItemStaticInfo(GetSelectExpression().GetItemType(), false);
            cit2.ContextSettingExpression = GetSelectExpression();
            actionOp.Optimize(visitor, cit2);
            onCompletionOp.Optimize(visitor, ContextItemStaticInfo.ABSENT);
            if (Literal.IsEmptySequence(OnCompletion))
            {
                if (Literal.IsEmptySequence(GetSelectExpression()) || Literal.IsEmptySequence(GetActionExpression()))
                {
                    return OnCompletion;
                }
            }

            return this;
        }

        public override ItemType GetItemType()
        {
            if (Literal.IsEmptySequence(OnCompletion))
            {
                return GetActionExpression().GetItemType();
            }
            else
            {
                TypeHierarchy th = GetConfiguration().GetTypeHierarchy();
                return Types.Type.GetCommonSuperType(GetActionExpression().GetItemType(), OnCompletion.GetItemType(), th);
            }
        }

        public override bool MayCreateNewNodes()
        {
            return (GetActionExpression().GetSpecialProperties() & OnCompletion.GetSpecialProperties() & StaticProperty.NO_NODES_NEWLY_CREATED) == 0;
        }

        public override bool HasVariableBinding(IBinding binding)
        {
            LocalParamBlock paramBlock = InitiallyExp;
            foreach (Operand o in paramBlock.Operands())
            {
                LocalParam setter = (LocalParam)o.GetChildExpression();
                if (setter == binding)
                {
                    return true;
                }
            }

            return false;
        }

        public override void CheckPermittedContents(ISchemaType parentType, bool whole)
        {
            GetActionExpression().CheckPermittedContents(parentType, false);
            OnCompletion.CheckPermittedContents(parentType, false);
        }

        public override Expression Copy(RebindingMap rebindings)
        {
            IterateInstr exp = new IterateInstr(GetSelectExpression().Copy(rebindings), (LocalParamBlock)InitiallyExp.Copy(rebindings), GetActionExpression().Copy(rebindings), OnCompletion.Copy(rebindings));
            ExpressionTool.CopyLocationInfo(this, exp);
            return exp;
        }

        public override void Export(ExpressionPresenter @out)
        {
            @out.StartElement("iterate", this);
            @out.SetChildRole("select");
            GetSelectExpression().Export(@out);
            @out.SetChildRole("params");
            InitiallyExp.Export(@out);
            if (!Literal.IsEmptySequence(OnCompletion))
            {
                @out.SetChildRole("on-completion");
                OnCompletion.Export(@out);
            }

            @out.SetChildRole("action");
            GetActionExpression().Export(@out);
            @out.EndElement();
        }

        public override Elaborator GetElaborator()
        {
            return new IterateElaborator();
        }

        internal sealed class IterateElaborator : PushElaborator
        {
            public override IPushEvaluator ElaborateForPush()
            {
                IterateInstr expr = (IterateInstr)GetExpression();
                IPullEvaluator select = expr.GetSelectExpression().MakeElaborator().ElaborateForPull();
                IPushEvaluator initial = expr.InitiallyExp.MakeElaborator().ElaborateForPush();
                IPushEvaluator action = expr.GetActionExpression().MakeElaborator().ElaborateForPush();
                IPushEvaluator completion = expr.OnCompletion.MakeElaborator().ElaborateForPush();
                return (output, context) =>
                {
                    XPathContextMajor c2 = context.NewContext();
                    c2.Origin = expr;
                    IFocusIterator iter = c2.TrackFocus(select.Iterate(context));
                    c2.SetCurrentTemplateRule(null);
                    PipelineConfiguration pipe = output.GetPipelineConfiguration();
                    pipe.XPathContext = c2;
                    bool tracing = context.GetController().IsTracing();
                    ITraceListener listener = tracing ? context.GetController().GetTraceListener() : null;
                    ITailCall tc = initial.ProcessLeavingTail(output, context);
                    Expression.DispatchTailCall(tc);
                    while (true)
                    {
                        IItem item = iter.Next();
                        if (item != null)
                        {
                            context.GetController().CheckTimeoutPerStep();
                            if (tracing)
                            {
                                listener.StartCurrentItem(item);
                            }

                            tc = action.ProcessLeavingTail(output, c2);
                            Expression.DispatchTailCall(tc);
                            if (tracing)
                            {
                                listener.EndCurrentItem(item);
                            }

                            TailCallLoop.ITailCallInfo comp = c2.TailCallInfo;
                            if (comp == null)
                            {
                            }
                            else if (comp is BreakInstr)
                            {

                                // indicates a xsl:break instruction was encountered: break the loop
                                iter.Dispose();
                                return null;
                            }
                            else
                            {
                            }
                        }
                        else
                        {

                            // Execute on-completion instruction
                            XPathContextMinor c3 = context.NewMinorContext();
                            c3.SetCurrentIterator(null);
                            tc = completion.ProcessLeavingTail(output, c3);
                            Expression.DispatchTailCall(tc);
                            break;
                        }
                    }

                    pipe.XPathContext = context;
                    return null;
                };
            }
        }
    }
}
